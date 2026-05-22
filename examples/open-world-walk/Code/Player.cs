using Sandbox;
using System;
using System.Linq;

namespace Local.OpenWorldWalk;

/// <summary>
/// Third-person open-world walker. CharacterController-based; samples
/// terrain height via a downward Scene.Trace.Ray each frame so the citizen
/// hugs the ground. WASD walks, Shift sprints, mouse-look.
/// </summary>
public sealed class Player : Component
{
	[Property] public float WalkSpeed { get; set; } = 200f;
	[Property] public float RunSpeed { get; set; } = 400f;
	[Property] public float RotationSpeed { get; set; } = 10f;
	[Property] public bool AutoPlay { get; set; } = false;

	[Property] public SoundEvent FootstepSound { get; set; }
	[Property] public SoundEvent BirdSound { get; set; }
	[Property] public SoundEvent WindSound { get; set; }

	public Vector3 Coordinates => GameObject.WorldPosition;
	public string Biome { get; private set; } = "open";

	private SkinnedModelRenderer _smr;
	private CharacterController _cc;
	private GameObject _cameraGo;

	private float _camYaw = 0f;
	private float _camPitch = 18f;
	private float _camDistance = 220f;

	private float _footstepTimer = 0f;
	private float _birdTimer = 5f;
	private float _windTimer = 0.5f;
	private SoundHandle _windHandle;

	// Autoplay path: 4 waypoints around the terrain
	private Vector3[] _autoPath = new[]
	{
		new Vector3( 200, 200, 0 ),
		new Vector3( -250, 300, 0 ),
		new Vector3( -300, -250, 0 ),
		new Vector3( 350, -200, 0 ),
		new Vector3( 0, 0, 0 ),
	};
	private int _autoSegment = 0;
	private float _autoStartDelay = 0.8f;
	private float _autoTime = 0f;

	protected override void OnStart()
	{
		base.OnStart();

		_smr = Components.GetInDescendantsOrSelf<SkinnedModelRenderer>();
		_cc = Components.GetOrCreate<CharacterController>();
		_cc.Radius = 16f;
		_cc.Height = 64f;
		_cc.StepHeight = 32f;
		_cc.Acceleration = 12f;
		_cc.GroundAngle = 60f;

		_cameraGo = Scene.Camera?.GameObject;

		FootstepSound ??= ResourceLibrary.Get<SoundEvent>( "sounds/impacts/melee/impact-melee-cloth.sound" );
		BirdSound     ??= ResourceLibrary.Get<SoundEvent>( "sounds/kenney/ui/ui.button.press.sound" );
		WindSound     ??= ResourceLibrary.Get<SoundEvent>( "sounds/kenney/ui/ui.button.click.sound" );

		// Snap to ground at spawn
		SnapToGround( startup: true );
		UpdateCameraInstant();
	}

	protected override void OnUpdate()
	{
		if ( _cc == null ) return;

		// Mouse camera (skip when autoplay running)
		if ( !AutoPlay )
		{
			_camYaw   -= Input.MouseDelta.x * 0.15f;
			_camPitch += Input.MouseDelta.y * 0.10f;
			_camPitch = Math.Clamp( _camPitch, -10f, 60f );
		}

		// Wish input
		GetWishInput( out var wishDir, out var wantSprint );
		float speed = wantSprint ? RunSpeed : WalkSpeed;
		Vector3 wishVel = wishDir * speed;

		// Pin the player to ground each frame via downward raycast.
		// Terrain implements Collider so Scene.Trace.Ray hits it.
		var groundZ = SampleGroundHeight( GameObject.WorldPosition );

		// Apply horizontal velocity
		_cc.Velocity = new Vector3( wishVel.x, wishVel.y, 0 );
		_cc.Move();

		// Snap z to terrain (with foot offset)
		var pos = GameObject.WorldPosition;
		pos.z = MathX.Lerp( pos.z, groundZ + 2f, Time.Delta * 12f );
		GameObject.WorldPosition = pos;

		// Rotate to face movement
		var horiz = _cc.Velocity.WithZ( 0 );
		if ( horiz.Length > 5f )
		{
			var want = Rotation.LookAt( horiz.Normal, Vector3.Up );
			GameObject.WorldRotation = Rotation.Lerp( GameObject.WorldRotation, want, Time.Delta * RotationSpeed );
		}

		UpdateAnimGraph( wishDir, horiz );

		// Footsteps
		_footstepTimer -= Time.Delta;
		if ( _footstepTimer <= 0f && horiz.Length > 50f )
		{
			float period = wantSprint ? 0.28f : 0.42f;
			_footstepTimer = period;
			if ( FootstepSound != null ) Sound.Play( FootstepSound, GameObject.WorldPosition );
		}

		// Ambient bird chirp every ~6-10s
		_birdTimer -= Time.Delta;
		if ( _birdTimer <= 0f )
		{
			_birdTimer = 6f + Game.Random.Float( 0f, 4f );
			if ( BirdSound != null )
			{
				var offset = new Vector3(
					Game.Random.Float( -400, 400 ),
					Game.Random.Float( -400, 400 ),
					Game.Random.Float( 100, 250 ) );
				Sound.Play( BirdSound, GameObject.WorldPosition + offset );
			}
		}

		// Long wind loop (cheap stand-in for full soundscape)
		_windTimer -= Time.Delta;
		if ( _windTimer <= 0f )
		{
			_windTimer = 3f;
			if ( WindSound != null )
			{
				var h = Sound.Play( WindSound, GameObject.WorldPosition + Vector3.Up * 200 );
				if ( h.IsValid() ) h.Volume = 0.15f;
			}
		}

		// Biome classification by relative height
		var hNorm = Math.Clamp( (groundZ + 50f) / 350f, 0f, 1f );
		Biome = hNorm < 0.25f ? "valley" : hNorm < 0.55f ? "meadow" : "highland";

		_autoTime += Time.Delta;
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
		sprint = true;
		if ( _autoTime < _autoStartDelay )
			return Vector3.Zero;

		if ( _autoSegment >= _autoPath.Length )
		{
			_autoSegment = 0; // loop
		}

		var target = _autoPath[_autoSegment];
		var to = (target - GameObject.WorldPosition).WithZ( 0 );
		float dist = to.Length;
		if ( dist < 60f )
		{
			_autoSegment = (_autoSegment + 1) % _autoPath.Length;
			target = _autoPath[_autoSegment];
			to = (target - GameObject.WorldPosition).WithZ( 0 );
		}

		// Slowly orbit yaw so the camera reveals different angles
		_camYaw += Time.Delta * 8f;

		var dir = to.Normal;
		var camRot = Rotation.FromYaw( _camYaw );
		var local = camRot.Inverse * dir;
		return new Vector3( local.x, local.y, 0 );
	}

	private float SampleGroundHeight( Vector3 pos )
	{
		var from = pos + Vector3.Up * 1000f;
		var to = pos - Vector3.Up * 2000f;
		var tr = Scene.Trace.Ray( from, to ).IgnoreGameObjectHierarchy( GameObject ).Run();
		if ( tr.Hit )
			return tr.HitPosition.z;
		return 0f;
	}

	private void SnapToGround( bool startup )
	{
		var z = SampleGroundHeight( GameObject.WorldPosition );
		var p = GameObject.WorldPosition;
		p.z = z + 2f;
		GameObject.WorldPosition = p;
		if ( startup && _cc != null )
			_cc.Velocity = Vector3.Zero;
	}

	private void UpdateAnimGraph( Vector3 wishDir, Vector3 horizVel )
	{
		if ( _smr == null || _smr.Model == null ) return;

		var rot = GameObject.WorldRotation;
		var forward = rot.Forward.Dot( horizVel );
		var sideward = rot.Right.Dot( horizVel );
		var localVel = new Vector3( forward, sideward, 0 );

		float speed = horizVel.Length;
		float angle = MathF.Atan2( localVel.y, localVel.x ).RadianToDegree().NormalizeDegrees();

		_smr.Set( "move_direction", angle );
		_smr.Set( "move_speed", speed );
		_smr.Set( "move_groundspeed", speed );
		_smr.Set( "move_x", localVel.x );
		_smr.Set( "move_y", localVel.y );
		_smr.Set( "move_z", 0f );

		var wishVel = wishDir * (AutoPlay || Input.Down( "Run" ) ? RunSpeed : WalkSpeed);
		var wishLocal = new Vector3( rot.Forward.Dot( wishVel ), rot.Right.Dot( wishVel ), 0 );
		_smr.Set( "wish_direction", MathF.Atan2( wishLocal.y, wishLocal.x ).RadianToDegree().NormalizeDegrees() );
		_smr.Set( "wish_speed", wishVel.Length );
		_smr.Set( "wish_groundspeed", wishVel.WithZ( 0 ).Length );
		_smr.Set( "wish_x", wishLocal.x );
		_smr.Set( "wish_y", wishLocal.y );
		_smr.Set( "wish_z", 0f );

		_smr.Set( "b_grounded", true );
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

		var head = GameObject.WorldPosition + new Vector3( 0, 0, 65 );
		var pitchRad = _camPitch * MathF.PI / 180f;
		var yawRad = _camYaw * MathF.PI / 180f;
		var offset = new Vector3(
			-MathF.Cos( yawRad ) * MathF.Cos( pitchRad ) * _camDistance,
			-MathF.Sin( yawRad ) * MathF.Cos( pitchRad ) * _camDistance,
			MathF.Sin( pitchRad ) * _camDistance + 60f
		);
		var target = head + offset;

		_cameraGo.WorldPosition = Vector3.Lerp( _cameraGo.WorldPosition, target, Time.Delta * 5f );
		_cameraGo.WorldRotation = Rotation.LookAt( (head - _cameraGo.WorldPosition).Normal, Vector3.Up );
	}

	private void UpdateCameraInstant()
	{
		if ( _cameraGo == null ) return;
		var head = GameObject.WorldPosition + new Vector3( 0, 0, 65 );
		var pitchRad = _camPitch * MathF.PI / 180f;
		var yawRad = _camYaw * MathF.PI / 180f;
		var offset = new Vector3(
			-MathF.Cos( yawRad ) * MathF.Cos( pitchRad ) * _camDistance,
			-MathF.Sin( yawRad ) * MathF.Cos( pitchRad ) * _camDistance,
			MathF.Sin( pitchRad ) * _camDistance + 60f
		);
		_cameraGo.WorldPosition = head + offset;
		_cameraGo.WorldRotation = Rotation.LookAt( (head - _cameraGo.WorldPosition).Normal, Vector3.Up );
	}
}
