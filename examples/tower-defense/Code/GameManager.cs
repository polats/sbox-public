using Sandbox;
using Sandbox.Navigation;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace Local.TowerDefense;

/// <summary>
/// Drives the tower-defense game: bakes the navmesh, runs the state
/// machine (Menu / Preparing / WaveActive / WaveComplete / Victory / GameOver),
/// owns the persistent state (gold, wave, bestwave, base hp, towers),
/// handles player input (click-to-place towers), and persists everything to
/// FileSystem.Data on every transition / hp / gold change.
/// </summary>
public sealed class GameManager : Component
{
	public const int MaxWaves = 5;
	public const int TowerCost = 50;
	public const int MaxTowers = 5;
	public const int StartingGold = 100;

	[Property] public bool AutoPlay { get; set; } = false;
	[Property] public Vector3 SpawnPoint { get; set; } = new Vector3( -600f, 0f, 0f );
	[Property] public Vector3 BasePoint { get; set; } = new Vector3( 600f, 0f, 0f );

	// Public game state — exposed for HUD reads.
	public int Gold { get; private set; } = StartingGold;
	public int Wave { get; set; } = 0;
	public int BestWave { get; set; } = 0;
	public string HudHint { get; set; } = "";

	public BaseState CurrentState { get; private set; }
	public string StateName => CurrentState?.Name ?? "(none)";

	public BaseObject Base { get; private set; }

	public int AliveCreepCount => Scene.GetAllComponents<Creep>().Count( c => !c.IsDead );
	public int TowerCount => Scene.GetAllComponents<Tower>().Count();

	private const string SavePath = "tower-defense/save.json";
	private bool _navMeshReady;
	private bool _hasStarted;
	private float _autoTowerTimer;
	private int _autoTowersPlaced;

	protected override void OnStart()
	{
		base.OnStart();

		Base = Scene.GetAllComponents<BaseObject>().FirstOrDefault();

		// Kick off navmesh bake (runtime).
		KickNavMeshGen();

		// Load save state (gold / wave / besWave / base hp / towers).
		var loaded = LoadState();
		if ( loaded != null )
		{
			Gold = loaded.Gold;
			Wave = loaded.Wave;
			BestWave = loaded.BestWave;
			if ( Base != null && loaded.BaseHp > 0 )
				Base.CurrentHp = loaded.BaseHp;

			Log.Info( $"[TowerDefense] Loaded save: gold={Gold} wave={Wave} best={BestWave} baseHp={loaded.BaseHp} towers={loaded.Towers?.Count ?? 0}" );

			// Restore towers (deferred until navmesh ready isn't strictly required,
			// but towers don't use the navmesh — so do it now).
			if ( loaded.Towers != null )
			{
				foreach ( var t in loaded.Towers )
					PlaceTowerAt( new Vector3( t.X, t.Y, t.Z ), saveAfter: false );
			}
		}
		else
		{
			Log.Info( "[TowerDefense] No save found — fresh run." );
		}

		// Initial state.
		SetState<MenuState>();
	}

	private async void KickNavMeshGen()
	{
		try
		{
			var nm = Scene.NavMesh;
			nm.AgentRadius = 16;
			nm.AgentHeight = 64;
			nm.AgentStepSize = 18;
			nm.AgentMaxSlope = 40;
			await nm.Generate( Scene.PhysicsWorld );
			_navMeshReady = true;
			Log.Info( "[TowerDefense] NavMesh ready." );
		}
		catch ( Exception e )
		{
			Log.Warning( $"[TowerDefense] NavMesh gen failed: {e.Message}" );
			_navMeshReady = true;
		}
	}

	protected override void OnUpdate()
	{
		if ( CurrentState == null ) return;

		CurrentState.TimeInState += Time.Delta;
		CurrentState.OnStateUpdate();

		// AutoPlay tower-placement logic — drops up to 3 towers spaced along
		// the path during Preparing. Skip if we already have 3+ (e.g. saved
		// game restored).
		if ( AutoPlay && CurrentState is PreparingState && _autoTowersPlaced < 3 && TowerCount < 3 )
		{
			_autoTowerTimer -= Time.Delta;
			if ( _autoTowerTimer <= 0f )
			{
				_autoTowerTimer = 0.8f;
				// Three positions: 1/4, 1/2, 3/4 along path, offset to one side.
				var positions = new[]
				{
					new Vector3( -300f, 150f, 0f ),
					new Vector3( 0f, -150f, 0f ),
					new Vector3( 300f, 150f, 0f ),
				};
				var pos = positions[_autoTowersPlaced];
				if ( TryPlaceTower( pos ) )
					_autoTowersPlaced++;
				else
					_autoTowerTimer = 0.3f;
			}
		}
	}

	// ─────────────────────────────────────────── State machine ───────────────────

	public void SetState<T>() where T : BaseState, new()
	{
		if ( CurrentState != null )
		{
			CurrentState.OnStateExit();
		}
		var st = new T { GM = this, TimeInState = 0f };
		CurrentState = st;
		st.OnStateEnter();
		Log.Info( $"[TowerDefense] State → {st.Name}" );
	}

	// ─────────────────────────────────────────── Money / waves ───────────────────

	public void AddGold( int amount )
	{
		Gold += amount;
		Save();
	}

	public void NotifyBaseDamaged()
	{
		Save();
	}

	public void NotifyBaseDestroyed()
	{
		SetState<GameOverState>();
	}

	public void ResetRun()
	{
		Gold = StartingGold;
		Wave = 0;
		if ( Base != null ) Base.CurrentHp = Base.MaxHp;

		foreach ( var t in Scene.GetAllComponents<Tower>().ToList() )
			t.GameObject.Destroy();
		foreach ( var c in Scene.GetAllComponents<Creep>().ToList() )
			c.GameObject.Destroy();

		_autoTowersPlaced = 0;
		Save();
	}

	// ─────────────────────────────────────────── Tower placement ─────────────────

	public bool TryPlaceTower( Vector3 worldPos )
	{
		if ( Gold < TowerCost ) return false;
		if ( TowerCount >= MaxTowers ) return false;

		// Don't place too close to the path centerline (creeps walk along Y≈0)
		// to keep towers off the lane. We allow placement anywhere except a
		// narrow corridor.
		if ( MathF.Abs( worldPos.y ) < 50f && MathF.Abs( worldPos.x ) < 700f )
			return false;

		Gold -= TowerCost;
		PlaceTowerAt( worldPos, saveAfter: true );
		return true;
	}

	private void PlaceTowerAt( Vector3 worldPos, bool saveAfter )
	{
		var go = new GameObject( true, "Tower" )
		{
			WorldPosition = worldPos.WithZ( 0f ),
		};
		go.Tags.Add( "tower" );

		// Base
		var baseModel = new GameObject( true, "Base" ) { Parent = go };
		baseModel.LocalPosition = new Vector3( 0, 0, 30f );
		baseModel.LocalScale = new Vector3( 50f / 32f, 50f / 32f, 60f / 32f );
		var bmr = baseModel.Components.Create<ModelRenderer>();
		bmr.Model = Model.Load( "models/dev/box.vmdl" );
		bmr.Tint = new Color( 0.35f, 0.35f, 0.45f );

		// Head (rotated to face target)
		var head = new GameObject( true, "Head" ) { Parent = go };
		head.LocalPosition = new Vector3( 0, 0, 80f );
		head.LocalScale = new Vector3( 30f / 32f, 30f / 32f, 30f / 32f );
		var hmr = head.Components.Create<ModelRenderer>();
		hmr.Model = Model.Load( "models/dev/sphere.vmdl" );
		hmr.Tint = new Color( 0.9f, 0.8f, 0.2f );

		// Barrel — a long thin box extending forward from the head.
		var barrel = new GameObject( true, "Barrel" ) { Parent = head };
		barrel.LocalPosition = new Vector3( 28f, 0, 0 );
		barrel.LocalScale = new Vector3( 50f / 32f, 12f / 32f, 12f / 32f );
		var brm = barrel.Components.Create<ModelRenderer>();
		brm.Model = Model.Load( "models/dev/box.vmdl" );
		brm.Tint = new Color( 0.7f, 0.6f, 0.15f );

		// The Tower Component itself
		var t = go.Components.Create<Tower>();
		t.Type = TowerType.Basic;

		if ( saveAfter ) Save();
	}

	// ─────────────────────────────────────────── Creep spawn ─────────────────────

	public void SpawnCreep()
	{
		var spawnPos = SpawnPoint;
		// Snap to navmesh if available.
		var snapped = Scene.NavMesh?.GetClosestPoint( spawnPos, 200f );
		if ( snapped.HasValue ) spawnPos = snapped.Value;

		var go = new GameObject( true, "Creep" )
		{
			WorldPosition = spawnPos,
		};

		var body = new GameObject( true, "Body" ) { Parent = go };
		var smr = body.Components.Create<SkinnedModelRenderer>();
		smr.Model = Model.Load( "models/citizen_human/citizen_human_male.vmdl" );
		smr.UseAnimGraph = true;
		// Reddish tint to mark them as enemies.
		smr.Tint = new Color(
			Game.Random.Float( 0.7f, 1.0f ),
			Game.Random.Float( 0.2f, 0.45f ),
			Game.Random.Float( 0.2f, 0.45f ) );

		var creep = go.Components.Create<Creep>();
		// Scale HP gently with wave so wave 5 isn't a one-shot.
		creep.MaxHpStat = 25f + Wave * 8f;
	}

	// ─────────────────────────────────────────── Mouse → ground ──────────────────

	public bool GetMouseGroundPos( out Vector3 pos )
	{
		pos = default;
		var cam = Scene.Camera;
		if ( cam == null ) return false;

		var ray = cam.ScreenPixelToRay( Mouse.Position );
		var tr = Scene.Trace.Ray( ray.Position, ray.Position + ray.Forward * 10000f )
			.WithoutTags( "tower" )
			.Run();
		if ( tr.Hit )
		{
			pos = tr.HitPosition;
			return true;
		}
		// Fallback: intersect with z=0 plane.
		if ( MathF.Abs( ray.Forward.z ) > 0.001f )
		{
			float t = -ray.Position.z / ray.Forward.z;
			if ( t > 0 )
			{
				pos = ray.Position + ray.Forward * t;
				return true;
			}
		}
		return false;
	}

	// ─────────────────────────────────────────── Save / Load (PRIMARY) ───────────

	private record SaveState(
		int Gold,
		int Wave,
		int BestWave,
		float BaseHp,
		List<TowerData> Towers );

	private record TowerData( string Type, float X, float Y, float Z );

	public void Save()
	{
		try
		{
			var towers = Scene.GetAllComponents<Tower>()
				.Select( t => new TowerData(
					t.Type.ToString(),
					t.WorldPosition.x,
					t.WorldPosition.y,
					t.WorldPosition.z ) )
				.ToList();

			var state = new SaveState(
				Gold,
				Wave,
				BestWave,
				Base?.CurrentHp ?? 100f,
				towers );

			var json = JsonSerializer.Serialize( state );
			FileSystem.Data.WriteAllText( SavePath, json );
		}
		catch ( Exception e )
		{
			Log.Warning( $"[TowerDefense] Save failed: {e.Message}" );
		}
	}

	private SaveState LoadState()
	{
		try
		{
			if ( !FileSystem.Data.FileExists( SavePath ) ) return null;
			var json = FileSystem.Data.ReadAllText( SavePath );
			return JsonSerializer.Deserialize<SaveState>( json );
		}
		catch ( Exception e )
		{
			Log.Warning( $"[TowerDefense] Load failed: {e.Message}" );
			return null;
		}
	}

	/// <summary>Used by an external tester to wipe the save file (sbox-eval).</summary>
	public static void DeleteSave()
	{
		try
		{
			if ( FileSystem.Data.FileExists( SavePath ) )
				FileSystem.Data.DeleteFile( SavePath );
		}
		catch { }
	}
}
