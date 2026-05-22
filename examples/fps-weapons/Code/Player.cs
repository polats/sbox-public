using Sandbox;
using System;
using System.Linq;

namespace Local.FpsWeapons;

/// <summary>
/// First-person player. Yaw on body, pitch on camera. Owns inventory,
/// runs the active weapon, handles recoil + screen-shake.
/// </summary>
public sealed class Player : Component
{
	[Property] public float MouseSensitivity { get; set; } = 0.10f;
	[Property] public float MaxTraceDistance { get; set; } = 12000f;

	[Property] public bool AutoPlay { get; set; } = false;
	[Property] public float AutoSwapInterval { get; set; } = 4f;
	[Property] public float AutoFireInterval { get; set; } = 0.55f;

	public GameObject CameraGameObject { get; private set; }
	public CameraComponent Camera { get; private set; }
	public PlayerInventory Inventory { get; private set; }
	public GameController Controller { get; private set; }

	private Angles _eyeAngles;

	// Recoil
	private float _recoilPitch = 0f;     // current pitch offset added to camera
	private float _recoilPitchVel = 0f;  // remaining decay
	private float _shakeUntil = 0f;
	private float _shakeAmount = 0f;

	// Tracer
	private GameObject _tracerGo;
	private float _tracerUntil = 0f;

	// AutoPlay state
	private float _autoNextSwap = 0f;
	private float _autoNextFire = 0f;
	private int _autoFiresLeft = 0;
	private float _autoSweepT = 0f;

	protected override void OnStart()
	{
		base.OnStart();

		CameraGameObject = GameObject.Children.FirstOrDefault( c => c.Components.Get<CameraComponent>() != null );
		if ( CameraGameObject == null ) CameraGameObject = Scene.Camera?.GameObject;
		Camera = CameraGameObject?.Components.Get<CameraComponent>();

		Controller = Scene.GetAllComponents<GameController>().FirstOrDefault();
		Inventory = Components.Get<PlayerInventory>() ?? Components.Create<PlayerInventory>();

		// Create weapons as child GameObjects of the CAMERA — viewmodels follow yaw + pitch
		var pistolGo = Scene.CreateObject();
		pistolGo.Name = "PistolWeapon";
		pistolGo.Parent = CameraGameObject;
		pistolGo.LocalPosition = Vector3.Zero;
		pistolGo.LocalRotation = Rotation.Identity;
		var pistol = pistolGo.Components.Create<PistolWeapon>();
		pistol.Owner = this;

		var shotgunGo = Scene.CreateObject();
		shotgunGo.Name = "ShotgunWeapon";
		shotgunGo.Parent = CameraGameObject;
		shotgunGo.LocalPosition = Vector3.Zero;
		shotgunGo.LocalRotation = Rotation.Identity;
		var shotgun = shotgunGo.Components.Create<ShotgunWeapon>();
		shotgun.Owner = this;

		var physGo = Scene.CreateObject();
		physGo.Name = "PhysGunWeapon";
		physGo.Parent = CameraGameObject;
		physGo.LocalPosition = Vector3.Zero;
		physGo.LocalRotation = Rotation.Identity;
		var phys = physGo.Components.Create<PhysGunWeapon>();
		phys.Owner = this;

		Inventory.Add( pistol );
		Inventory.Add( shotgun );
		Inventory.Add( phys );

		// Defer Deploy until weapons have had OnStart (next frame); we just call Switch now —
		// BaseWeapon.OnStart will run Holster, but Switch will Deploy on Active next.
		// To make it work even before first OnStart of weapons, force-enable here:
		Inventory.Switch( 0 );

		_eyeAngles = new Angles( 0, 0, 0 );
		ApplyAngles();
		Mouse.Visible = false;
	}

	protected override void OnUpdate()
	{
		if ( Camera == null ) return;

		Mouse.Visible = false;

		if ( AutoPlay )
		{
			RunAutoPlay();
		}
		else
		{
			HandleInput();
		}

		// Decay recoil pitch
		_recoilPitch = MathX.Lerp( _recoilPitch, 0f, Time.Delta * 10f );

		ApplyAngles();

		// Tick active weapon
		Inventory?.Active?.Tick();

		// Tracer fade
		if ( _tracerGo != null && _tracerGo.IsValid() && Time.Now > _tracerUntil )
		{
			_tracerGo.Destroy();
			_tracerGo = null;
		}
	}

	private void HandleInput()
	{
		_eyeAngles.yaw -= Input.MouseDelta.x * MouseSensitivity;
		_eyeAngles.pitch = Math.Clamp( _eyeAngles.pitch + Input.MouseDelta.y * MouseSensitivity, -89f, 89f );

		// Weapon switching
		if ( Input.Pressed( "Slot1" ) ) Inventory.Switch( 0 );
		if ( Input.Pressed( "Slot2" ) ) Inventory.Switch( 1 );
		if ( Input.Pressed( "Slot3" ) ) Inventory.Switch( 2 );

		var active = Inventory?.Active;
		if ( active == null ) return;

		if ( Input.Down( "Attack1" ) || Input.Pressed( "Attack1" ) )
		{
			active.OnPrimaryAttack();
		}
		if ( Input.Released( "Attack1" ) ) active.OnPrimaryRelease();
		if ( Input.Pressed( "Attack2" ) ) active.OnSecondaryAttack();
		if ( Input.Released( "Attack2" ) ) active.OnSecondaryRelease();
		if ( Input.Pressed( "Reload" ) ) active.OnReload();

		var scroll = (int)Input.MouseWheel.y;
		if ( scroll != 0 ) active.OnScroll( scroll );
	}

	private void RunAutoPlay()
	{
		_autoSweepT += Time.Delta;

		// Aim at a target if any exist
		var targets = Scene.GetAllComponents<Target>().Where( t => t != null && t.IsValid() ).ToList();
		if ( targets.Count > 0 )
		{
			int idx = ((int)(_autoSweepT * 0.5f)) % targets.Count;
			var tgt = targets[idx];
			var aimPos = tgt.GameObject.WorldPosition + new Vector3( 0, 0, 36f );
			var dir = (aimPos - CameraGameObject.WorldPosition).Normal;
			var want = Rotation.LookAt( dir, Vector3.Up ).Angles();
			want.yaw += MathF.Sin( _autoSweepT * 4.7f ) * 1.2f;
			want.pitch += MathF.Cos( _autoSweepT * 3.3f ) * 0.7f;
			_eyeAngles.yaw = MathX.Lerp( _eyeAngles.yaw, want.yaw, Time.Delta * 4f );
			_eyeAngles.pitch = MathX.Lerp( _eyeAngles.pitch, want.pitch, Time.Delta * 4f );
			_eyeAngles.pitch = Math.Clamp( _eyeAngles.pitch, -89f, 89f );
		}

		// Swap weapons every interval
		if ( Time.Now >= _autoNextSwap )
		{
			int next = (Inventory.ActiveIndex + 1) % 3;
			Inventory.Switch( next );
			_autoNextSwap = Time.Now + AutoSwapInterval;
			_autoFiresLeft = (next == 2) ? 1 : 3; // physgun: 1 "grab" action
			_autoNextFire = Time.Now + 0.5f;
		}

		// Fire/Grab cadence
		if ( _autoFiresLeft > 0 && Time.Now >= _autoNextFire )
		{
			var active = Inventory.Active;
			if ( active is PhysGunWeapon pg )
			{
				if ( !pg.IsHolding )
				{
					pg.OnPrimaryAttack();
				}
				else
				{
					// Wiggle hold distance via scroll, then release after a moment
					pg.OnScroll( (int)(MathF.Sin( _autoSweepT * 4f ) * 2f) );
				}
				_autoNextFire = Time.Now + 0.4f;
				_autoFiresLeft--;
				if ( _autoFiresLeft <= 0 )
				{
					// release in 1s
				}
			}
			else
			{
				active?.OnPrimaryAttack();
				_autoNextFire = Time.Now + AutoFireInterval;
				_autoFiresLeft--;
			}
		}

		// Auto-release physgun about halfway through its swap window
		if ( Inventory.Active is PhysGunWeapon phys && phys.IsHolding )
		{
			var elapsed = AutoSwapInterval - (_autoNextSwap - Time.Now);
			if ( elapsed > AutoSwapInterval * 0.75f )
			{
				phys.OnPrimaryRelease();
			}
		}
	}

	private void ApplyAngles()
	{
		// Yaw on body, pitch on camera (FP split)
		GameObject.WorldRotation = Rotation.FromYaw( _eyeAngles.yaw );

		var pitch = _eyeAngles.pitch + _recoilPitch;
		// Camera child gets the pitch
		if ( CameraGameObject != null )
		{
			CameraGameObject.LocalRotation = Rotation.From( pitch, 0, 0 );

			// Screen shake: tiny offset on camera local position
			if ( Time.Now < _shakeUntil )
			{
				float t = (_shakeUntil - Time.Now);
				float intensity = _shakeAmount * t;
				var shake = new Vector3( 0,
					MathF.Sin( Time.Now * 73f ) * 0.5f * intensity,
					MathF.Cos( Time.Now * 91f ) * 0.5f * intensity );
				// preserve original local Z=64 offset
				CameraGameObject.LocalPosition = new Vector3( 0, shake.y, 64f + shake.z );
			}
			else
			{
				CameraGameObject.LocalPosition = new Vector3( 0, 0, 64f );
			}
		}
	}

	public void AddRecoil( float degrees, float shake )
	{
		_recoilPitch -= degrees; // negative pitch = up
		_shakeAmount = shake;
		_shakeUntil = Time.Now + 0.18f;
	}

	/// <summary>Fires `pellets` rays with `spread` (radian-ish cone) doing `damage` each.</summary>
	public void FireHitscan( float spread, int pellets, float damage )
	{
		if ( Controller != null ) Controller.OnShotFired( Inventory?.Active );

		var camPos = CameraGameObject.WorldPosition;
		var camFwd = CameraGameObject.WorldRotation.Forward;
		var camRot = CameraGameObject.WorldRotation;

		Vector3? firstHit = null;

		for ( int i = 0; i < pellets; i++ )
		{
			var dir = camFwd;
			if ( spread > 0f )
			{
				float ax = (Random.Shared.Float() - 0.5f) * spread * 2f;
				float ay = (Random.Shared.Float() - 0.5f) * spread * 2f;
				dir = (camFwd + camRot.Right * ax + camRot.Up * ay).Normal;
			}
			var end = camPos + dir * MaxTraceDistance;
			var tr = Scene.Trace.Ray( camPos, end )
				.IgnoreGameObjectHierarchy( GameObject )
				.Run();

			var hitEnd = tr.Hit ? tr.HitPosition : end;
			if ( i == 0 ) firstHit = hitEnd;

			if ( tr.Hit )
			{
				// Walk up to find Target
				var go = tr.GameObject;
				Target target = null;
				while ( go != null )
				{
					target = go.Components.Get<Target>();
					if ( target != null ) break;
					go = go.Parent;
				}
				if ( target != null )
				{
					target.OnShot( tr.HitPosition, dir, damage );
				}
				else
				{
					// Generic impact sound
					var impact = Controller?.ImpactSound;
					if ( impact != null ) Sound.Play( impact, tr.HitPosition );
				}
			}
		}

		if ( firstHit.HasValue ) SpawnTracer( camPos, firstHit.Value );
	}

	private void SpawnTracer( Vector3 from, Vector3 to )
	{
		if ( _tracerGo != null && _tracerGo.IsValid() ) _tracerGo.Destroy();
		var go = Scene.CreateObject();
		go.Name = "Tracer";
		var mid = (from + to) * 0.5f;
		var diff = to - from;
		float length = MathF.Max( diff.Length, 1f );
		go.WorldPosition = mid;
		go.WorldRotation = Rotation.LookAt( diff.Normal, Vector3.Up );
		go.WorldScale = new Vector3( length / 50f, 0.6f / 50f, 0.6f / 50f );
		var mr = go.Components.Create<ModelRenderer>();
		mr.Model = Model.Load( "models/dev/box.vmdl" );
		mr.Tint = new Color( 1.0f, 0.85f, 0.20f, 0.85f );
		_tracerGo = go;
		_tracerUntil = Time.Now + 0.08f;
	}
}
