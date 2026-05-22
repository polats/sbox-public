using Sandbox;
using System;
using System.Linq;

namespace Local.ArenaThemes;

/// <summary>
/// Simple third-person walking player. WASD moves, mouse orbits camera.
/// Drives the citizen animgraph so the model actually walks.
/// </summary>
public sealed class Player : Component
{
	[Property] public float WalkSpeed { get; set; } = 180f;
	[Property] public float RunSpeed { get; set; } = 320f;
	[Property] public float Gravity { get; set; } = 900f;
	[Property] public float RotationSpeed { get; set; } = 10f;

	[Property] public bool AutoPlay { get; set; } = false;

	private SkinnedModelRenderer _smr;
	private CharacterController _cc;
	private GameObject _cameraGo;

	private float _camYaw = 0f;
	private float _camPitch = 22f;
	private float _camDistance = 240f;

	// Autoplay wander
	private Vector3 _wanderTarget;
	private float _wanderTimer = 0f;

	protected override void OnStart()
	{
		base.OnStart();

		_smr = Components.GetInDescendantsOrSelf<SkinnedModelRenderer>();
		_cc = Components.GetOrCreate<CharacterController>();
		_cc.Radius = 16f;
		_cc.Height = 64f;
		_cc.StepHeight = 18f;
		_cc.Acceleration = 12f;
		_cc.GroundAngle = 50f;

		_cameraGo = Scene.Camera?.GameObject;

		PickWander();
		UpdateCameraInstant();
	}

	protected override void OnUpdate()
	{
		if ( _cc == null ) return;

		// Mouse camera control
		if ( !AutoPlay )
		{
			_camYaw -= Input.MouseDelta.x * 0.15f;
			_camPitch += Input.MouseDelta.y * 0.10f;
			_camPitch = Math.Clamp( _camPitch, -10f, 60f );
		}
		else
		{
			// Slow orbit so the camera sweeps and shows off the arena.
			_camYaw += Time.Delta * 10f;
		}

		// Wish input
		Vector3 input;
		bool wantRun;
		if ( AutoPlay )
		{
			input = AutoplayInput( out wantRun );
		}
		else
		{
			input = Input.AnalogMove;
			wantRun = Input.Down( "Run" );
		}

		var camRot = Rotation.FromYaw( _camYaw );
		var wishDir = camRot * new Vector3( input.x, input.y, 0 );
		if ( wishDir.Length > 1f ) wishDir = wishDir.Normal;

		float speed = wantRun ? RunSpeed : WalkSpeed;
		var wishVel = wishDir * speed;

		bool isGrounded = _cc.IsOnGround;
		if ( isGrounded )
		{
			_cc.Velocity = new Vector3( wishVel.x, wishVel.y, _cc.Velocity.z );
			if ( _cc.Velocity.z < 0 ) _cc.Velocity = _cc.Velocity.WithZ( 0 );
		}
		else
		{
			var horiz = _cc.Velocity.WithZ( 0 );
			horiz = Vector3.Lerp( horiz, wishVel, Time.Delta * 2f );
			_cc.Velocity = new Vector3( horiz.x, horiz.y, _cc.Velocity.z - Gravity * Time.Delta );
		}

		_cc.Move();

		// Face direction of motion
		var horizVel = _cc.Velocity.WithZ( 0 );
		if ( horizVel.Length > 5f )
		{
			var wantRot = Rotation.LookAt( horizVel.Normal, Vector3.Up );
			GameObject.WorldRotation = Rotation.Lerp( GameObject.WorldRotation, wantRot, Time.Delta * RotationSpeed );
		}

		UpdateAnimGraph( horizVel, isGrounded );
		UpdateCamera();
	}

	private Vector3 AutoplayInput( out bool sprint )
	{
		sprint = false;
		_wanderTimer -= Time.Delta;
		var to = (_wanderTarget - GameObject.WorldPosition).WithZ( 0 );
		if ( _wanderTimer <= 0f || to.Length < 60f )
		{
			PickWander();
			to = (_wanderTarget - GameObject.WorldPosition).WithZ( 0 );
		}
		var dir = to.Normal;
		var camRot = Rotation.FromYaw( _camYaw );
		var local = camRot.Inverse * dir;
		return new Vector3( local.x, local.y, 0 );
	}

	private void PickWander()
	{
		var rng = new Random();
		float ang = (float)(rng.NextDouble() * Math.PI * 2f);
		float r = 100f + (float)rng.NextDouble() * 250f;
		_wanderTarget = new Vector3( MathF.Cos( ang ) * r, MathF.Sin( ang ) * r, 0 );
		_wanderTimer = 3f + (float)rng.NextDouble() * 2f;
	}

	private void UpdateAnimGraph( Vector3 horizVel, bool grounded )
	{
		if ( _smr == null || _smr.Model == null ) return;

		var rot = GameObject.WorldRotation;
		var forward = rot.Forward.Dot( horizVel );
		var sideward = rot.Right.Dot( horizVel );
		var localVel = new Vector3( forward, sideward, _cc.Velocity.z );

		float speed = horizVel.Length;
		float angle = MathF.Atan2( localVel.y, localVel.x ).RadianToDegree().NormalizeDegrees();

		_smr.Set( "move_direction", angle );
		_smr.Set( "move_speed", speed );
		_smr.Set( "move_groundspeed", speed );
		_smr.Set( "move_x", localVel.x );
		_smr.Set( "move_y", localVel.y );
		_smr.Set( "move_z", localVel.z );
		_smr.Set( "wish_direction", angle );
		_smr.Set( "wish_speed", speed );
		_smr.Set( "wish_groundspeed", speed );
		_smr.Set( "wish_x", localVel.x );
		_smr.Set( "wish_y", localVel.y );
		_smr.Set( "wish_z", 0f );
		_smr.Set( "b_grounded", grounded );
		_smr.Set( "b_jump", false );
		_smr.Set( "b_swim", false );
		_smr.Set( "b_climbing", false );
		_smr.Set( "b_noclip", false );
		_smr.Set( "duck", 0f );
		_smr.Set( "move_rotationspeed", 0f );
	}

	private void UpdateCamera()
	{
		if ( _cameraGo == null ) return;
		var head = GameObject.WorldPosition + new Vector3( 0, 0, 55 );
		var pitchRad = _camPitch * MathF.PI / 180f;
		var yawRad = _camYaw * MathF.PI / 180f;
		var offset = new Vector3(
			-MathF.Cos( yawRad ) * MathF.Cos( pitchRad ) * _camDistance,
			-MathF.Sin( yawRad ) * MathF.Cos( pitchRad ) * _camDistance,
			MathF.Sin( pitchRad ) * _camDistance + 40f );
		var target = head + offset;
		_cameraGo.WorldPosition = Vector3.Lerp( _cameraGo.WorldPosition, target, Time.Delta * 6f );
		_cameraGo.WorldRotation = Rotation.LookAt( (head - _cameraGo.WorldPosition).Normal, Vector3.Up );
	}

	private void UpdateCameraInstant()
	{
		if ( _cameraGo == null ) return;
		var head = GameObject.WorldPosition + new Vector3( 0, 0, 55 );
		var pitchRad = _camPitch * MathF.PI / 180f;
		var yawRad = _camYaw * MathF.PI / 180f;
		var offset = new Vector3(
			-MathF.Cos( yawRad ) * MathF.Cos( pitchRad ) * _camDistance,
			-MathF.Sin( yawRad ) * MathF.Cos( pitchRad ) * _camDistance,
			MathF.Sin( pitchRad ) * _camDistance + 40f );
		_cameraGo.WorldPosition = head + offset;
		_cameraGo.WorldRotation = Rotation.LookAt( (head - _cameraGo.WorldPosition).Normal, Vector3.Up );
	}
}
