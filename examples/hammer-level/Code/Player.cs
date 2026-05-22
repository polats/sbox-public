using Sandbox;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Local.HammerLevel;

/// <summary>
/// First-person player using CharacterController. Yaw on body, pitch on camera.
/// WASD walks; LMB fires the pistol; R reloads. AutoPlay walks a path through
/// both rooms and shoots targets in each.
/// </summary>
public sealed class Player : Component
{
	[Property] public float MouseSensitivity { get; set; } = 0.10f;
	[Property] public float MaxTraceDistance { get; set; } = 12000f;
	[Property] public float WalkSpeed { get; set; } = 180f;

	[Property] public bool AutoPlay { get; set; } = false;
	[Property] public float AutoFireInterval { get; set; } = 0.5f;

	public GameObject CameraGameObject { get; private set; }
	public CameraComponent Camera { get; private set; }
	public CharacterController CC { get; private set; }
	public GameController Controller { get; private set; }
	public PistolWeapon Pistol { get; private set; }

	private Angles _eyeAngles;
	private float _recoilPitch = 0f;
	private float _shakeUntil = 0f;
	private float _shakeAmount = 0f;
	private GameObject _tracerGo;
	private float _tracerUntil = 0f;

	// AutoPlay state
	private List<Vector3> _waypoints;
	private int _waypointIdx;
	private float _autoNextFire;
	private float _autoStart;

	protected override void OnStart()
	{
		base.OnStart();
		CameraGameObject = GameObject.Children.FirstOrDefault( c => c.Components.Get<CameraComponent>() != null );
		if ( CameraGameObject == null ) CameraGameObject = Scene.Camera?.GameObject;
		Camera = CameraGameObject?.Components.Get<CameraComponent>();
		CC = Components.Get<CharacterController>();
		Controller = Scene.GetAllComponents<GameController>().FirstOrDefault();

		// Create pistol as child of camera
		var pistolGo = Scene.CreateObject();
		pistolGo.Name = "PistolWeapon";
		pistolGo.Parent = CameraGameObject;
		pistolGo.LocalPosition = Vector3.Zero;
		pistolGo.LocalRotation = Rotation.Identity;
		Pistol = pistolGo.Components.Create<PistolWeapon>();
		Pistol.Owner = this;
		Pistol.Deploy();

		_eyeAngles = new Angles( 0, 0, 0 );
		ApplyAngles();
		Mouse.Visible = false;

		// Walk path: lobby center -> through corridor -> roomB center -> back
		_waypoints = new List<Vector3>
		{
			new Vector3( -400, -100, 16 ),
			new Vector3( -200,    0, 16 ),
			new Vector3(    0,    0, 16 ),
			new Vector3(  200,    0, 16 ),
			new Vector3(  400,  100, 16 ),
			new Vector3(  400, -100, 16 ),
			new Vector3(  200,    0, 16 ),
			new Vector3( -200,    0, 16 ),
			new Vector3( -400,  100, 16 ),
		};
		_waypointIdx = 0;
		_autoNextFire = 0f;
		_autoStart = Time.Now;
	}

	protected override void OnUpdate()
	{
		if ( Camera == null ) return;
		Mouse.Visible = false;

		if ( AutoPlay )
			RunAutoPlay();
		else
			HandleInput();

		_recoilPitch = MathX.Lerp( _recoilPitch, 0f, Time.Delta * 10f );
		ApplyAngles();

		// Movement via CharacterController
		MovePlayer();

		Pistol?.Tick();

		if ( _tracerGo != null && _tracerGo.IsValid() && Time.Now > _tracerUntil )
		{
			_tracerGo.Destroy();
			_tracerGo = null;
		}
	}

	private Vector3 _wishMove;

	private void HandleInput()
	{
		_eyeAngles.yaw -= Input.MouseDelta.x * MouseSensitivity;
		_eyeAngles.pitch = Math.Clamp( _eyeAngles.pitch + Input.MouseDelta.y * MouseSensitivity, -89f, 89f );

		var fwd = Rotation.FromYaw( _eyeAngles.yaw ).Forward;
		var rgt = Rotation.FromYaw( _eyeAngles.yaw ).Right;
		var move = Vector3.Zero;
		if ( Input.Down( "Forward" ) ) move += fwd;
		if ( Input.Down( "Backward" ) ) move -= fwd;
		if ( Input.Down( "Right" ) ) move += rgt;
		if ( Input.Down( "Left" ) ) move -= rgt;
		_wishMove = move.WithZ( 0 ).Normal * WalkSpeed;

		if ( Input.Down( "Attack1" ) ) Pistol?.OnPrimaryAttack();
		if ( Input.Pressed( "Reload" ) ) Pistol?.OnReload();
	}

	private void RunAutoPlay()
	{
		// Steer towards current waypoint, advance when close.
		if ( _waypoints == null || _waypoints.Count == 0 ) return;
		var here = GameObject.WorldPosition;
		// Advance through any waypoints we're already close to (in case we tunnel/skip)
		for ( int safety = 0; safety < _waypoints.Count; safety++ )
		{
			var w = _waypoints[_waypointIdx];
			var d = (w - here).WithZ( 0 ).Length;
			if ( d < 50f ) _waypointIdx = (_waypointIdx + 1) % _waypoints.Count;
			else break;
		}
		var wp = _waypoints[_waypointIdx];
		var toWp = (wp - here).WithZ( 0 );

		var moveDir = toWp.Normal;
		_wishMove = moveDir * WalkSpeed;

		// Aim at the nearest visible target; if none, look down corridor
		var targets = Scene.GetAllComponents<Target>().Where( t => t != null && t.IsValid() ).ToList();
		Vector3 lookAt = here + moveDir * 400f + Vector3.Up * 32f;
		if ( targets.Count > 0 )
		{
			Target best = null; float bestD = float.MaxValue;
			foreach ( var t in targets )
			{
				float d = Vector3.DistanceBetween( t.GameObject.WorldPosition, here );
				if ( d < bestD ) { bestD = d; best = t; }
			}
			if ( best != null && bestD < 700f )
			{
				lookAt = best.GameObject.WorldPosition + Vector3.Up * 36f;
			}
		}

		var aimDir = (lookAt - (CameraGameObject?.WorldPosition ?? here + Vector3.Up * 64f)).Normal;
		var want = Rotation.LookAt( aimDir, Vector3.Up ).Angles();
		_eyeAngles.yaw = MathX.Lerp( _eyeAngles.yaw, want.yaw, Time.Delta * 3f );
		_eyeAngles.pitch = MathX.Lerp( _eyeAngles.pitch, want.pitch, Time.Delta * 3f );
		_eyeAngles.pitch = Math.Clamp( _eyeAngles.pitch, -89f, 89f );

		// Fire periodically when a target is in front
		if ( Time.Now >= _autoNextFire && targets.Count > 0 )
		{
			Pistol?.OnPrimaryAttack();
			_autoNextFire = Time.Now + AutoFireInterval;
		}
	}

	private void MovePlayer()
	{
		if ( CC == null ) return;
		// Gravity
		if ( CC.IsOnGround )
		{
			CC.Velocity = CC.Velocity.WithZ( 0 );
		}
		else
		{
			CC.Velocity += Scene.PhysicsWorld.Gravity * Time.Delta;
		}
		// Apply horizontal wish move
		var horiz = _wishMove.WithZ( 0 );
		CC.Velocity = new Vector3( horiz.x, horiz.y, CC.Velocity.z );
		CC.Move();
	}

	private void ApplyAngles()
	{
		GameObject.WorldRotation = Rotation.FromYaw( _eyeAngles.yaw );
		var pitch = _eyeAngles.pitch + _recoilPitch;
		if ( CameraGameObject != null )
		{
			CameraGameObject.LocalRotation = Rotation.From( pitch, 0, 0 );
			if ( Time.Now < _shakeUntil )
			{
				float t = (_shakeUntil - Time.Now);
				float intensity = _shakeAmount * t;
				var shake = new Vector3( 0,
					MathF.Sin( Time.Now * 73f ) * 0.5f * intensity,
					MathF.Cos( Time.Now * 91f ) * 0.5f * intensity );
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
		_recoilPitch -= degrees;
		_shakeAmount = shake;
		_shakeUntil = Time.Now + 0.18f;
	}

	public void FireHitscan( float spread, int pellets, float damage )
	{
		if ( Controller != null ) Controller.OnShotFired( Pistol );
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
