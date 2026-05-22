using Sandbox;
using System;
using System.Collections.Generic;

namespace Local.NpcPatrol;

/// <summary>
/// Third-person citizen player. WASD + mouse-look in normal play.
///
/// In AutoPlay mode the player walks along a predefined waypoint loop that crosses
/// the guard's sight cone, triggering the Alert → Chase → Lose → Investigate →
/// Patrol cycle for the demo video.
/// </summary>
public sealed class Player : Component
{
	[Property] public float WalkSpeed { get; set; } = 200f;
	[Property] public float RunSpeed { get; set; } = 400f;
	[Property] public float Gravity { get; set; } = 900f;
	[Property] public float RotationSpeed { get; set; } = 10f;

	[Property] public bool AutoPlay { get; set; } = false;
	[Property] public List<Vector3> AutoplayPath { get; set; } = new()
	{
		new Vector3( -600, -350, 40 ),    // start (spawn)
		new Vector3( 100, -350, 40 ),     // walk across the front of the compound — crosses guard sight cone
		new Vector3( 100, -650, 40 ),     // flee south
		new Vector3( -600, -650, 40 ),    // hide behind the south wall
		new Vector3( -600, -350, 40 ),    // come back into view
	};

	private SkinnedModelRenderer _smr;
	private CharacterController _cc;
	private GameObject _cameraGo;

	private float _camYaw = 0f;
	private float _camPitch = 28f;
	private float _camDistance = 380f;

	private int _autoIndex = 0;
	private float _autoHold = 0f;
	private Vector3 _spawn;

	protected override void OnStart()
	{
		base.OnStart();

		GameObject.Tags.Add( "player" );

		_smr = Components.GetInDescendantsOrSelf<SkinnedModelRenderer>();
		_cc = Components.GetOrCreate<CharacterController>();
		_cc.Radius = 16f;
		_cc.Height = 64f;
		_cc.StepHeight = 18f;
		_cc.Acceleration = 12f;
		_cc.GroundAngle = 50f;

		_cameraGo = Scene.Camera?.GameObject;
		_spawn = GameObject.WorldPosition;
		UpdateCameraInstant();
	}

	protected override void OnUpdate()
	{
		if ( _cc is null ) return;

		if ( GameObject.WorldPosition.z < -200 )
		{
			GameObject.WorldPosition = _spawn;
			_cc.Velocity = Vector3.Zero;
			return;
		}

		bool grounded = _cc.IsOnGround;

		if ( !AutoPlay )
		{
			_camYaw -= Input.MouseDelta.x * 0.15f;
			_camPitch += Input.MouseDelta.y * 0.10f;
			_camPitch = Math.Clamp( _camPitch, -10f, 60f );
		}

		Vector3 wishDir;
		bool sprint;
		GetWishInput( out wishDir, out sprint );

		float speed = sprint ? RunSpeed : WalkSpeed;
		var wishVel = wishDir * speed;

		if ( grounded )
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

		var horizVel = _cc.Velocity.WithZ( 0 );
		if ( horizVel.Length > 5f )
		{
			var wantRot = Rotation.LookAt( horizVel.Normal, Vector3.Up );
			GameObject.WorldRotation = Rotation.Lerp( GameObject.WorldRotation, wantRot, Time.Delta * RotationSpeed );
		}

		UpdateAnimGraph( wishDir, horizVel, grounded, sprint );
		UpdateCamera();
	}

	private void GetWishInput( out Vector3 wishDir, out bool sprint )
	{
		Vector3 input;
		if ( AutoPlay )
		{
			input = AutoplayInput( out sprint );
		}
		else
		{
			input = Input.AnalogMove;
			sprint = Input.Down( "Run" );
		}

		var camRot = Rotation.FromYaw( _camYaw );
		wishDir = camRot * new Vector3( input.x, input.y, 0 );
		if ( wishDir.Length > 1f ) wishDir = wishDir.Normal;
	}

	private Vector3 AutoplayInput( out bool sprint )
	{
		sprint = false;
		if ( AutoplayPath is null || AutoplayPath.Count == 0 ) return Vector3.Zero;

		var here = GameObject.WorldPosition.WithZ( 0 );
		var goal = AutoplayPath[_autoIndex % AutoplayPath.Count].WithZ( 0 );
		var toGoal = (goal - here);

		if ( toGoal.Length < 50f )
		{
			_autoHold += Time.Delta;
			if ( _autoHold > 0.4f )
			{
				_autoHold = 0f;
				_autoIndex = (_autoIndex + 1) % AutoplayPath.Count;
			}
			return Vector3.Zero;
		}

		// Very slow yaw drift; mostly stays fixed so the guard is in frame.
		_camYaw += Time.Delta * 1.5f;

		var dir = toGoal.Normal;
		var camRot = Rotation.FromYaw( _camYaw );
		var local = camRot.Inverse * dir;
		return new Vector3( local.x, local.y, 0 );
	}

	private void UpdateAnimGraph( Vector3 wishDir, Vector3 horizVel, bool grounded, bool sprinting )
	{
		if ( _smr is null || _smr.Model is null ) return;

		var rot = GameObject.WorldRotation;
		var forward = rot.Forward.Dot( horizVel );
		var sideward = rot.Right.Dot( horizVel );
		float speed = horizVel.Length;
		float angle = MathF.Atan2( sideward, forward ).RadianToDegree().NormalizeDegrees();

		_smr.Set( "move_direction", angle );
		_smr.Set( "move_speed", speed );
		_smr.Set( "move_groundspeed", speed );
		_smr.Set( "move_x", forward );
		_smr.Set( "move_y", sideward );
		_smr.Set( "move_z", _cc.Velocity.z );

		var wishVel = wishDir * (sprinting ? RunSpeed : WalkSpeed);
		var wishForward = rot.Forward.Dot( wishVel );
		var wishSide = rot.Right.Dot( wishVel );
		_smr.Set( "wish_direction", MathF.Atan2( wishSide, wishForward ).RadianToDegree().NormalizeDegrees() );
		_smr.Set( "wish_speed", wishVel.Length );
		_smr.Set( "wish_groundspeed", wishVel.WithZ( 0 ).Length );
		_smr.Set( "wish_x", wishForward );
		_smr.Set( "wish_y", wishSide );

		_smr.Set( "b_grounded", grounded );
		_smr.Set( "b_jump", false );
		_smr.Set( "duck", 0f );
	}

	private void UpdateCamera()
	{
		if ( _cameraGo is null ) return;
		var head = GameObject.WorldPosition + new Vector3( 0, 0, 55 );
		var pitchRad = _camPitch * MathF.PI / 180f;
		var yawRad = _camYaw * MathF.PI / 180f;
		var offset = new Vector3(
			-MathF.Cos( yawRad ) * MathF.Cos( pitchRad ) * _camDistance,
			-MathF.Sin( yawRad ) * MathF.Cos( pitchRad ) * _camDistance,
			MathF.Sin( pitchRad ) * _camDistance + 40f
		);
		var target = head + offset;
		_cameraGo.WorldPosition = Vector3.Lerp( _cameraGo.WorldPosition, target, Time.Delta * 6f );
		_cameraGo.WorldRotation = Rotation.LookAt( (head - _cameraGo.WorldPosition).Normal, Vector3.Up );
	}

	private void UpdateCameraInstant()
	{
		if ( _cameraGo is null ) return;
		var head = GameObject.WorldPosition + new Vector3( 0, 0, 55 );
		var pitchRad = _camPitch * MathF.PI / 180f;
		var yawRad = _camYaw * MathF.PI / 180f;
		var offset = new Vector3(
			-MathF.Cos( yawRad ) * MathF.Cos( pitchRad ) * _camDistance,
			-MathF.Sin( yawRad ) * MathF.Cos( pitchRad ) * _camDistance,
			MathF.Sin( pitchRad ) * _camDistance + 40f
		);
		_cameraGo.WorldPosition = head + offset;
		_cameraGo.WorldRotation = Rotation.LookAt( (head - _cameraGo.WorldPosition).Normal, Vector3.Up );
	}
}
