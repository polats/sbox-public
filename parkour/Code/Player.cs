using Sandbox;
using System;
using System.Linq;

namespace Local.Parkour;

/// <summary>
/// Third-person parkour player. Drives the citizen model via CharacterController,
/// pushes movement state into the animgraph each frame, and orbits a follow
/// camera behind the citizen. Mouse Y pitches, Mouse X yaws.
/// </summary>
public sealed class Player : Component
{
	[Property] public float WalkSpeed { get; set; } = 200f;
	[Property] public float RunSpeed { get; set; } = 350f;
	[Property] public float JumpStrength { get; set; } = 420f;
	[Property] public float Gravity { get; set; } = 900f;
	[Property] public float RotationSpeed { get; set; } = 10f;

	[Property] public SoundEvent FootstepSound { get; set; }
	[Property] public SoundEvent JumpSound { get; set; }
	[Property] public SoundEvent LandSound { get; set; }
	[Property] public SoundEvent GoalSound { get; set; }

	[Property] public bool AutoPlay { get; set; } = false;

	public float CourseTime { get; private set; }
	public bool ReachedGoal { get; private set; }
	public string Message { get; set; } = "";

	private SkinnedModelRenderer _smr;
	private CharacterController _cc;
	private GameObject _cameraGo;
	private CameraComponent _camera;

	private float _camYaw = 0f;
	private float _camPitch = 18f;
	private float _camDistance = 180f;
	private Vector3 _camPos;

	private float _footstepTimer = 0f;
	private bool _wasGrounded = true;
	private bool _justJumped = false;

	// Autoplay
	private float _autoStartDelay = 1.0f;
	private float _autoTimer = 0f;
	private int _autoSegment = 0;
	private Vector3 _autoTarget;

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
		_camera = Scene.Camera;

		FootstepSound ??= ResourceLibrary.Get<SoundEvent>( "sounds/impacts/melee/impact-melee-cloth.sound" );
		JumpSound     ??= ResourceLibrary.Get<SoundEvent>( "sounds/kenney/ui/ui.button.press.sound" );
		LandSound     ??= ResourceLibrary.Get<SoundEvent>( "sounds/impacts/melee/impact-melee-wood.sound" );
		GoalSound     ??= ResourceLibrary.Get<SoundEvent>( "sounds/editor/success.sound" );

		var course = Scene.GetAllComponents<Course>().FirstOrDefault();
		if ( course != null )
		{
			_spawn = course.StartSpawn;
			GameObject.WorldPosition = _spawn;
		}
		else
		{
			_spawn = GameObject.WorldPosition;
		}

		_camYaw = 0f;
		UpdateCameraInstant();
	}

	protected override void OnUpdate()
	{
		if ( _cc == null ) return;

		// Respawn if fell off the world
		if ( GameObject.WorldPosition.z < -200 )
		{
			Respawn();
			return;
		}

		bool isGrounded = _cc.IsOnGround;

		// Camera input (mouse) — autoplay can also drive it
		if ( !AutoPlay || ReachedGoal )
		{
			_camYaw   -= Input.MouseDelta.x * 0.15f;
			_camPitch += Input.MouseDelta.y * 0.10f;
			_camPitch = Math.Clamp( _camPitch, -10f, 60f );
		}

		// Movement input
		Vector3 wishDir;
		bool wantSprint;
		bool wantJump;
		GetWishInput( isGrounded, out wishDir, out wantSprint, out wantJump );

		float speed = wantSprint ? RunSpeed : WalkSpeed;
		Vector3 wishVel = wishDir * speed;

		// Apply horizontal velocity
		if ( isGrounded )
		{
			// Snap horizontal velocity directly toward wish (smooth via accel)
			_cc.Velocity = new Vector3( wishVel.x, wishVel.y, _cc.Velocity.z );
			// kill vertical drift when grounded
			if ( _cc.Velocity.z < 0 ) _cc.Velocity = _cc.Velocity.WithZ( 0 );
		}
		else
		{
			// Air control — partial
			var horiz = _cc.Velocity.WithZ( 0 );
			horiz = Vector3.Lerp( horiz, wishVel, Time.Delta * 2f );
			_cc.Velocity = new Vector3( horiz.x, horiz.y, _cc.Velocity.z - Gravity * Time.Delta );
		}

		// Jump
		_justJumped = false;
		if ( wantJump && isGrounded )
		{
			_cc.Punch( Vector3.Up * JumpStrength );
			_justJumped = true;
			if ( JumpSound != null ) Sound.Play( JumpSound, GameObject.WorldPosition );
		}

		_cc.Move();

		// Landing detection
		if ( !_wasGrounded && _cc.IsOnGround )
		{
			if ( LandSound != null ) Sound.Play( LandSound, GameObject.WorldPosition );
		}
		_wasGrounded = _cc.IsOnGround;

		// Rotate citizen to face movement dir
		var horizVel = _cc.Velocity.WithZ( 0 );
		if ( horizVel.Length > 5f )
		{
			var wantRot = Rotation.LookAt( horizVel.Normal, Vector3.Up );
			GameObject.WorldRotation = Rotation.Lerp( GameObject.WorldRotation, wantRot, Time.Delta * RotationSpeed );
		}

		// Animgraph: push the actual state into the citizen
		UpdateAnimGraph( wishDir, horizVel, _cc.IsOnGround, _justJumped );

		// Footstep tick
		_footstepTimer -= Time.Delta;
		if ( _footstepTimer <= 0f && _cc.IsOnGround && horizVel.Length > 50f )
		{
			float period = wantSprint ? 0.28f : 0.42f;
			_footstepTimer = period;
			if ( FootstepSound != null ) Sound.Play( FootstepSound, GameObject.WorldPosition );
		}

		// Timer
		if ( !ReachedGoal )
			CourseTime += Time.Delta;

		// Goal check (CharacterController isn't a Collider, so we poll distance
		// to the GoalTrigger volume each frame instead of using ITriggerListener).
		if ( !ReachedGoal )
		{
			var course = Scene.GetAllComponents<Course>().FirstOrDefault();
			if ( course != null )
			{
				var goal = course.GoalPosition;
				var to = GameObject.WorldPosition - goal;
				if ( MathF.Abs( to.x ) < 90f && MathF.Abs( to.y ) < 90f && MathF.Abs( to.z ) < 60f )
				{
					OnReachedGoal();
				}
			}
		}

		// Camera follow
		UpdateCamera();
	}

	private void GetWishInput( bool isGrounded, out Vector3 wishDir, out bool sprint, out bool jump )
	{
		Vector3 input;
		if ( AutoPlay && !ReachedGoal )
		{
			input = AutoplayInput( out sprint, out jump );
		}
		else
		{
			// Vector3 AnalogMove is (Forward, Right, 0) in some builds, or
			// XYZ relative to view — use Input.AnalogMove.WithZ(0) and treat
			// .x as forward, .y as left/right.
			var am = Input.AnalogMove;
			input = am;
			sprint = Input.Down( "Run" );
			jump = Input.Pressed( "Jump" );
		}

		// Rotate wish dir by camera yaw (so W = away from camera)
		var camRot = Rotation.FromYaw( _camYaw );
		wishDir = camRot * new Vector3( input.x, input.y, 0 );
		if ( wishDir.Length > 1f ) wishDir = wishDir.Normal;
	}

	private Vector3 AutoplayInput( out bool sprint, out bool jump )
	{
		sprint = true;
		jump = false;

		// Autoplay drives toward sequential waypoints along the course.
		// Wait briefly at start so camera frames the scene.
		if ( CourseTime < _autoStartDelay )
		{
			return Vector3.Zero;
		}

		var waypoints = new Vector3[]
		{
			new Vector3( 240, 0, 60 ),
			new Vector3( 440, 0, 60 ),
			new Vector3( 620, -30, 60 ),
			new Vector3( 820, -60, 90 ),
			new Vector3( 820, 120, 90 ),
			new Vector3( 820, 280, 90 ),
			new Vector3( 820, 440, 110 ),
			new Vector3( 1020, 440, 90 ),
			new Vector3( 1220, 440, 90 ),
			new Vector3( 1420, 440, 90 ),
		};

		if ( _autoSegment >= waypoints.Length )
		{
			return Vector3.Zero;
		}

		_autoTarget = waypoints[_autoSegment];
		var toTarget = (_autoTarget - GameObject.WorldPosition).WithZ( 0 );
		float dist = toTarget.Length;

		if ( dist < 50f )
		{
			_autoSegment++;
			if ( _autoSegment < waypoints.Length )
				_autoTarget = waypoints[_autoSegment];
		}

		var dir = toTarget.Normal;

		// Jump when close to a known gap edge or whenever airborne distance
		// to the next waypoint exceeds platform spacing.
		bool wpTallerThanUs = _autoTarget.z > GameObject.WorldPosition.z + 5f;
		// Jump if approaching a gap: distance to next waypoint > 90 and we're grounded.
		// More eager (start jump earlier) so the parabola actually clears the gap.
		if ( _cc.IsOnGround && (dist > 90f || wpTallerThanUs) && dist < 240f )
		{
			jump = true;
		}

		// Convert world-space wish dir back to "input-space" (relative to camera yaw)
		var camRot = Rotation.FromYaw( _camYaw );
		var local = camRot.Inverse * dir;
		return new Vector3( local.x, local.y, 0 );
	}

	private void UpdateAnimGraph( Vector3 wishDir, Vector3 horizVel, bool grounded, bool justJumped )
	{
		if ( _smr == null || _smr.Model == null ) return;

		var rot = GameObject.WorldRotation;
		// Convert world velocity to local (forward = x, sideward = y, up = z) — matches engine convention.
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

		// wish_* drives upper-body lean / anticipation
		var wishVel = wishDir * (Input.Down( "Run" ) || AutoPlay ? RunSpeed : WalkSpeed);
		var wishLocal = new Vector3( rot.Forward.Dot( wishVel ), rot.Right.Dot( wishVel ), 0 );
		_smr.Set( "wish_direction", MathF.Atan2( wishLocal.y, wishLocal.x ).RadianToDegree().NormalizeDegrees() );
		_smr.Set( "wish_speed", wishVel.Length );
		_smr.Set( "wish_groundspeed", wishVel.WithZ( 0 ).Length );
		_smr.Set( "wish_x", wishLocal.x );
		_smr.Set( "wish_y", wishLocal.y );
		_smr.Set( "wish_z", 0f );

		_smr.Set( "b_grounded", grounded );
		_smr.Set( "b_jump", justJumped );
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

		// Camera position from yaw/pitch
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

	public void OnReachedGoal()
	{
		if ( ReachedGoal ) return;
		ReachedGoal = true;
		Message = "Reached the goal!";
		if ( GoalSound != null ) Sound.Play( GoalSound, GameObject.WorldPosition );
	}

	private void Respawn()
	{
		GameObject.WorldPosition = _spawn;
		if ( _cc != null )
		{
			_cc.Velocity = Vector3.Zero;
		}
		_autoSegment = 0;
		CourseTime = 0f;
		ReachedGoal = false;
		Message = "";
	}
}
