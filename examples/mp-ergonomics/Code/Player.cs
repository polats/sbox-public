using Sandbox;
using System;
using System.Linq;

namespace Local.MpErgonomics;

/// <summary>
/// The active local player — citizen with WASD movement and a 3rd-person
/// camera. In single-process demo mode this is the only "real" entity; the
/// other four citizens are <see cref="FakePlayer"/> NPCs standing in place.
/// </summary>
public sealed class Player : Component
{
	[Property] public float WalkSpeed { get; set; } = 180f;
	[Property] public float RunSpeed { get; set; } = 320f;
	[Property] public float RotationSpeed { get; set; } = 10f;

	[Property] public bool AutoPlay { get; set; } = false;

	private SkinnedModelRenderer _smr;
	private CharacterController _cc;
	private GameObject _cameraGo;

	private float _camYaw = 0f;
	private float _camPitch = 20f;
	private float _camDistance = 220f;

	private TimeSince _sinceAutoStart;

	protected override void OnStart()
	{
		_smr = Components.GetInDescendantsOrSelf<SkinnedModelRenderer>();
		_cc = Components.GetOrCreate<CharacterController>();
		_cc.Radius = 16f;
		_cc.Height = 64f;
		_cc.StepHeight = 18f;
		_cc.Acceleration = 12f;
		_cc.GroundAngle = 50f;

		_cameraGo = Scene.Camera?.GameObject;
		_sinceAutoStart = 0f;
		UpdateCamera( true );
	}

	protected override void OnUpdate()
	{
		if ( _cc is null ) return;

		// Camera input
		if ( !AutoPlay )
		{
			_camYaw   -= Input.MouseDelta.x * 0.15f;
			_camPitch += Input.MouseDelta.y * 0.10f;
			_camPitch = Math.Clamp( _camPitch, -10f, 60f );
		}
		else
		{
			// slow orbit so video looks lively
			_camYaw -= Time.Delta * 8f;
		}

		Vector3 wishDir = GetWishDir();
		bool sprint = Input.Down( "Run" );
		float speed = sprint ? RunSpeed : WalkSpeed;
		Vector3 wishVel = wishDir * speed;

		if ( _cc.IsOnGround )
		{
			_cc.Velocity = new Vector3( wishVel.x, wishVel.y, _cc.Velocity.z );
			if ( _cc.Velocity.z < 0 ) _cc.Velocity = _cc.Velocity.WithZ( 0 );
		}
		else
		{
			var horiz = _cc.Velocity.WithZ( 0 );
			horiz = Vector3.Lerp( horiz, wishVel, Time.Delta * 2f );
			_cc.Velocity = new Vector3( horiz.x, horiz.y, _cc.Velocity.z - 900f * Time.Delta );
		}

		_cc.Move();

		// Face movement direction
		var horizVel = _cc.Velocity.WithZ( 0 );
		if ( horizVel.Length > 5f )
		{
			var wantRot = Rotation.LookAt( horizVel.Normal, Vector3.Up );
			GameObject.WorldRotation = Rotation.Lerp( GameObject.WorldRotation, wantRot, Time.Delta * RotationSpeed );
		}

		UpdateAnimGraph( wishDir, horizVel, _cc.IsOnGround );
		UpdateCamera( false );
	}

	private Vector3 GetWishDir()
	{
		Vector3 input;
		if ( AutoPlay )
		{
			// Gentle walk in a circle around lobby center.
			float t = _sinceAutoStart;
			input = new Vector3( MathF.Cos( t * 0.5f ) * 0.3f, MathF.Sin( t * 0.5f ) * 0.3f, 0 );
		}
		else
		{
			input = Input.AnalogMove;
		}

		var camRot = Rotation.FromYaw( _camYaw );
		var wish = camRot * new Vector3( input.x, input.y, 0 );
		return wish.Length > 1f ? wish.Normal : wish;
	}

	private void UpdateAnimGraph( Vector3 wishDir, Vector3 horizVel, bool grounded )
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
		_smr.Set( "b_grounded", grounded );
		_smr.Set( "b_jump", false );
		_smr.Set( "b_swim", false );
		_smr.Set( "b_climbing", false );
		_smr.Set( "b_noclip", false );
		_smr.Set( "duck", 0f );
	}

	private void UpdateCamera( bool instant )
	{
		if ( _cameraGo is null ) return;

		// In autoplay mode the camera orbits around the lobby centre with a
		// wider distance so all four NPCs (placed ~200u from origin) stay in
		// frame for the video. In manual mode it tracks the local player.
		float dist = AutoPlay ? 550f : _camDistance;
		float pitch = AutoPlay ? 22f : _camPitch;
		var lookAt = AutoPlay ? new Vector3( 0, 0, 60 ) : GameObject.WorldPosition + new Vector3( 0, 0, 55 );

		var pitchRad = pitch * MathF.PI / 180f;
		var yawRad = _camYaw * MathF.PI / 180f;
		var offset = new Vector3(
			-MathF.Cos( yawRad ) * MathF.Cos( pitchRad ) * dist,
			-MathF.Sin( yawRad ) * MathF.Cos( pitchRad ) * dist,
			MathF.Sin( pitchRad ) * dist + 40f );
		var target = lookAt + offset;
		_cameraGo.WorldPosition = instant ? target : Vector3.Lerp( _cameraGo.WorldPosition, target, Time.Delta * 6f );
		_cameraGo.WorldRotation = Rotation.LookAt( (lookAt - _cameraGo.WorldPosition).Normal, Vector3.Up );
	}
}
