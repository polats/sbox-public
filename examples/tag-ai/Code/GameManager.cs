using Sandbox;
using Sandbox.Navigation;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Local.TagAi;

/// <summary>
/// Spawns NPCs at random navmesh positions on Start, tracks frozen count,
/// declares win when all NPCs are frozen. Optionally bakes/generates the
/// navmesh at runtime if the scene-baked one is empty.
/// </summary>
public sealed class GameManager : Component
{
	[Property, Range( 2, 10 )] public int NpcCount { get; set; } = 5;
	[Property] public float SpawnRadiusMin { get; set; } = 300f;
	[Property] public float SpawnRadiusMax { get; set; } = 700f;
	[Property] public GameObject NpcPrefabSource { get; set; }
	[Property] public bool BakeAtRuntime { get; set; } = true;

	public int Frozen { get; private set; }
	public int Total { get; private set; }
	public bool Won { get; private set; }
	public string Status { get; private set; } = "Bringing up navmesh…";

	private List<EnemyNpc> _spawned = new();
	private bool _navMeshReady = false;
	private bool _spawnDone = false;
	private float _spawnDelay = 3.0f;
	private float _delayTimer = 0f;

	protected override void OnStart()
	{
		base.OnStart();

		Total = NpcCount;
		if ( BakeAtRuntime )
		{
			KickNavMeshGen();
		}
		else
		{
			_navMeshReady = Scene.NavMesh.IsEnabled;
		}
	}

	private async void KickNavMeshGen()
	{
		try
		{
			var nm = Scene.NavMesh;
			// Force enable + generate if not already baked.
			nm.AgentRadius = 16;
			nm.AgentHeight = 64;
			nm.AgentStepSize = 18;
			nm.AgentMaxSlope = 40;
			Status = "Generating navmesh…";
			await nm.Generate( Scene.PhysicsWorld );
			Status = "Navmesh ready, spawning NPCs…";
			_navMeshReady = true;
			Log.Info( "[TagAI] NavMesh generation complete." );
		}
		catch ( Exception e )
		{
			Log.Warning( $"[TagAI] NavMesh generate failed: {e.Message}" );
			_navMeshReady = true; // try anyway
		}
	}

	protected override void OnUpdate()
	{
		if ( !_spawnDone )
		{
			if ( _navMeshReady )
			{
				_delayTimer += Time.Delta;
				if ( _delayTimer >= _spawnDelay )
				{
					SpawnNpcs();
					_spawnDone = true;
				}
			}
			return;
		}

		Frozen = _spawned.Count( n => n != null && n.IsFrozen );

		if ( !Won && Total > 0 && Frozen >= Total )
		{
			Won = true;
			Status = "You Win!";
		}
		else if ( !Won )
		{
			Status = $"Tag the citizens! ({Frozen}/{Total})";
		}
	}

	private void SpawnNpcs()
	{
		var rng = new Random( 17 );
		var player = Scene.GetAllComponents<Player>().FirstOrDefault();
		var playerPos = player?.WorldPosition ?? Vector3.Zero;

		for ( int i = 0; i < NpcCount; i++ )
		{
			Vector3 pos = SamplePosAwayFromPlayer( playerPos, rng );
			var go = BuildNpc( pos, i );
			var npc = go.Components.Get<EnemyNpc>();
			if ( npc != null ) _spawned.Add( npc );
		}

		Log.Info( $"[TagAI] Spawned {_spawned.Count} NPCs." );
	}

	private Vector3 SamplePosAwayFromPlayer( Vector3 playerPos, Random rng )
	{
		for ( int tries = 0; tries < 20; tries++ )
		{
			float ang = (float)(rng.NextDouble() * Math.PI * 2);
			float r = SpawnRadiusMin + (float)rng.NextDouble() * (SpawnRadiusMax - SpawnRadiusMin);
			var cand = playerPos + new Vector3( MathF.Cos( ang ) * r, MathF.Sin( ang ) * r, 0 );
			cand = cand.WithZ( 0 );
			var sampled = Scene.NavMesh?.GetClosestPoint( cand, 200f );
			if ( sampled.HasValue ) return sampled.Value;
		}
		// Last resort: just use the candidate
		return playerPos + new Vector3( 400, 0, 0 );
	}

	private GameObject BuildNpc( Vector3 pos, int index )
	{
		var go = new GameObject( true, $"Npc_{index}" )
		{
			WorldPosition = pos,
			WorldRotation = Rotation.FromYaw( index * 47f ),
		};

		go.Components.Create<EnemyNpc>();

		var modelGo = new GameObject( true, "Body" ) { Parent = go };
		var smr = modelGo.Components.Create<SkinnedModelRenderer>();
		smr.Model = Model.Load( "models/citizen_human/citizen_human_male.vmdl" );
		smr.UseAnimGraph = true;
		// Differentiate NPCs from player visually with a tint
		var palette = new Color[]
		{
			new Color( 1.0f, 0.55f, 0.55f ),
			new Color( 0.55f, 1.0f, 0.6f ),
			new Color( 1.0f, 0.85f, 0.4f ),
			new Color( 0.7f, 0.55f, 1.0f ),
			new Color( 1.0f, 0.6f, 0.95f ),
			new Color( 0.5f, 0.9f, 1.0f ),
		};
		smr.Tint = palette[index % palette.Length];

		return go;
	}
}
