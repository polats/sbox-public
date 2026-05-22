using Sandbox;
using System;
using System.Linq;

namespace Local.TagAi;

/// <summary>
/// Third-person tagger player. WASD + mouse-look + sprint. Drives the citizen
/// animgraph so the player visibly runs. When AutoPlay is true, the player
/// autonomously chases the nearest unfrozen NPC for video verification.
/// </summary>
public sealed class Player : Component
{
	[Property] public float WalkSpeed { get; set; } = 200f;
	[Property] public float RunSpeed { get; set; } = 400f;
	[Property] public float Gravity { get; set; } = 900f;
	[Property] public float RotationSpeed { get; set; } = 10f;
	[Property] public float TagRange { get; set; } = 80f;

	[Property] public SoundEvent FootstepSound { get; set; }
	[Property] public SoundEvent TagSound { get; set; }

	[Property] public bool AutoPlay { get; set; } = false;

	private SkinnedModelRenderer _smr;
	private CharacterController _cc;
	private GameObject _cameraGo;

	private float _camYaw = 0f;
	private float _camPitch = 22f;
	private float _camDistance = 260f;

	private float _footstepTimer = 0f;
	private Vector3 _spawn;

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

		FootstepSound ??= ResourceLibrary.Get<SoundEvent>( "sounds/impacts/melee/impact-melee-cloth.sound" );
		TagSound ??= ResourceLibrary.Get<SoundEvent>( "sounds/editor/success.sound" );

		_spawn = GameObject.WorldPosition;
		UpdateCameraInstant();
	}

	protected override void OnUpdate()
	{
		if ( _cc == null ) return;

		// Respawn if fell off the world
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
		bool wantSprint;
		GetWishInput( out wishDir, out wantSprint );

		float speed = wantSprint ? RunSpeed : WalkSpeed;
		Vector3 wishVel = wishDir * speed;

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

		UpdateAnimGraph( wishDir, horizVel, grounded, wantSprint );

		// Footsteps
		_footstepTimer -= Time.Delta;
		if ( _footstepTimer <= 0f && grounded && horizVel.Length > 50f )
		{
			float period = wantSprint ? 0.28f : 0.42f;
			_footstepTimer = period;
			if ( FootstepSound != null ) Sound.Play( FootstepSound, GameObject.WorldPosition );
		}

		// Tag NPCs in range
		TryTag();

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
		sprint = false; // walk speed during demo so the chase is readable

		// Find nearest unfrozen NPC, chase it.
		var npcs = Scene.GetAllComponents<EnemyNpc>()
			.Where( n => !n.IsFrozen )
			.OrderBy( n => Vector3.DistanceBetween( n.WorldPosition, WorldPosition ) )
			.ToList();

		if ( npcs.Count == 0 ) return Vector3.Zero;

		var target = npcs[0].WorldPosition;
		var toTarget = (target - GameObject.WorldPosition).WithZ( 0 );

		// Slow-orbit camera to keep things filmic during autoplay
		_camYaw += Time.Delta * 8f;

		if ( toTarget.Length < 5f ) return Vector3.Zero;

		var dir = toTarget.Normal;
		var camRot = Rotation.FromYaw( _camYaw );
		var local = camRot.Inverse * dir;
		return new Vector3( local.x, local.y, 0 );
	}

	private void UpdateAnimGraph( Vector3 wishDir, Vector3 horizVel, bool grounded, bool sprinting )
	{
		if ( _smr == null || _smr.Model == null ) return;

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
		_smr.Set( "wish_z", 0f );

		_smr.Set( "b_grounded", grounded );
		_smr.Set( "b_jump", false );
		_smr.Set( "b_swim", false );
		_smr.Set( "b_climbing", false );
		_smr.Set( "b_noclip", false );
		_smr.Set( "duck", 0f );
		_smr.Set( "move_rotationspeed", 0f );
	}

	private void TryTag()
	{
		var here = GameObject.WorldPosition;
		foreach ( var npc in Scene.GetAllComponents<EnemyNpc>() )
		{
			if ( npc.IsFrozen ) continue;
			if ( Vector3.DistanceBetween( npc.WorldPosition, here ) < TagRange )
			{
				npc.Freeze();
				if ( TagSound != null ) Sound.Play( TagSound, npc.WorldPosition );
			}
		}
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
			MathF.Sin( pitchRad ) * _camDistance + 40f
		);
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
			MathF.Sin( pitchRad ) * _camDistance + 40f
		);
		_cameraGo.WorldPosition = head + offset;
		_cameraGo.WorldRotation = Rotation.LookAt( (head - _cameraGo.WorldPosition).Normal, Vector3.Up );
	}
}
