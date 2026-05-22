using Sandbox;
using System;
using System.Linq;

namespace Local.ToyBox;

/// <summary>
/// Third-person citizen for the Toy Box. Drives a CharacterController, pushes
/// animgraph state, and supports an AutoPlay mode that walks the predefined
/// autopath from <see cref="Diorama.AutoPath"/>.
///
/// Adapted from examples/parkour/Code/Player.cs.
/// </summary>
public sealed class Player : Component
{
	[Property] public float WalkSpeed { get; set; } = 180f;
	[Property] public float RunSpeed { get; set; } = 280f;
	[Property] public float JumpStrength { get; set; } = 380f;
	[Property] public float Gravity { get; set; } = 900f;
	[Property] public float RotationSpeed { get; set; } = 10f;

	[Property] public bool AutoPlay { get; set; } = false;
	[Property] public float AutoStartDelay { get; set; } = 1.5f;

	public string CurrentSegmentLabel { get; private set; } = "Starting…";
	public float Elapsed { get; private set; }
	public bool InWater { get; private set; }

	private SkinnedModelRenderer _smr;
	private CharacterController _cc;
	private GameObject _cameraGo;
	private CameraComponent _camera;
	private Diorama _diorama;

	private float _camYaw = 0f;
	private float _camPitch = 18f;
	private float _camDistance = 320f;

	private int _autoSegment = 0;
	private float _swimTimer = 0f;
	private bool _wasGrounded = true;

	protected override void OnStart()
	{
		_smr = Components.GetInDescendantsOrSelf<SkinnedModelRenderer>();
		_cc = Components.GetOrCreate<CharacterController>();
		_cc.Radius = 16f;
		_cc.Height = 64f;
		_cc.StepHeight = 18f;
		_cc.Acceleration = 12f;
		_cc.GroundAngle = 50f;

		_camera = Scene.Camera;
		_cameraGo = _camera?.GameObject;
		_diorama = Scene.GetAllComponents<Diorama>().FirstOrDefault();

		if ( _diorama != null )
		{
			GameObject.WorldPosition = _diorama.SpawnPoint;
		}

		_camYaw = 0f;
	}

	protected override void OnUpdate()
	{
		if ( _cc == null ) return;

		Elapsed += Time.Delta;

		// Respawn safety net
		if ( GameObject.WorldPosition.z < -300 )
		{
			GameObject.WorldPosition = _diorama?.SpawnPoint ?? new Vector3( -300, 0, 60 );
			_cc.Velocity = Vector3.Zero;
		}

		// Water detection (for swim flag + speed mod)
		InWater = false;
		var water = _diorama?.Water;
		if ( water != null && water.IsValid() )
		{
			InWater = water.IsPointInside( GameObject.WorldPosition );
			if ( InWater ) _swimTimer += Time.Delta;
			else _swimTimer = 0f;
		}

		// Manual camera input (mouse) when not auto
		if ( !AutoPlay )
		{
			_camYaw -= Input.MouseDelta.x * 0.15f;
			_camPitch += Input.MouseDelta.y * 0.10f;
			_camPitch = Math.Clamp( _camPitch, -10f, 60f );
		}

		bool isGrounded = _cc.IsOnGround;

		Vector3 wishDir;
		bool wantSprint;
		bool wantJump;
		GetWishInput( isGrounded, out wishDir, out wantSprint, out wantJump );

		float speed = wantSprint ? RunSpeed : WalkSpeed;
		if ( InWater ) speed *= (water?.SwimSpeedMult ?? 0.5f);

		var wishVel = wishDir * speed;

		if ( isGrounded )
		{
			_cc.Velocity = new Vector3( wishVel.x, wishVel.y, _cc.Velocity.z );
			if ( _cc.Velocity.z < 0 ) _cc.Velocity = _cc.Velocity.WithZ( 0 );
		}
		else
		{
			var horiz = _cc.Velocity.WithZ( 0 );
			horiz = Vector3.Lerp( horiz, wishVel, Time.Delta * 2f );
			float g = InWater ? Gravity * 0.2f : Gravity;
			_cc.Velocity = new Vector3( horiz.x, horiz.y, _cc.Velocity.z - g * Time.Delta );
			// in-water buoyancy on the CC: float up gently if submerged below midline
			if ( InWater && water != null )
			{
				var target = water.WorldPosition.z + water.Half.z * 0.4f;
				var dz = target - GameObject.WorldPosition.z;
				_cc.Velocity = _cc.Velocity.WithZ( _cc.Velocity.z + dz * Time.Delta * 4f );
			}
		}

		bool justJumped = false;
		if ( wantJump && isGrounded )
		{
			_cc.Punch( Vector3.Up * JumpStrength );
			justJumped = true;
		}

		_cc.Move();
		_wasGrounded = _cc.IsOnGround;

		// Face movement
		var horizVel = _cc.Velocity.WithZ( 0 );
		if ( horizVel.Length > 5f )
		{
			var wantRot = Rotation.LookAt( horizVel.Normal, Vector3.Up );
			GameObject.WorldRotation = Rotation.Lerp( GameObject.WorldRotation, wantRot, Time.Delta * RotationSpeed );
		}

		UpdateAnimGraph( wishDir, horizVel, _cc.IsOnGround, justJumped );
		UpdateCamera();
	}

	private void GetWishInput( bool isGrounded, out Vector3 wishDir, out bool sprint, out bool jump )
	{
		Vector3 input;

		if ( AutoPlay )
		{
			input = AutoplayInput( out sprint, out jump );
		}
		else
		{
			input = Input.AnalogMove;
			sprint = Input.Down( "Run" );
			jump = Input.Pressed( "Jump" );
		}

		var camRot = Rotation.FromYaw( _camYaw );
		wishDir = camRot * new Vector3( input.x, input.y, 0 );
		if ( wishDir.Length > 1f ) wishDir = wishDir.Normal;
	}

	private Vector3 AutoplayInput( out bool sprint, out bool jump )
	{
		sprint = true;
		jump = false;

		if ( _diorama == null || _diorama.AutoPath.Count == 0 )
			return Vector3.Zero;

		if ( Elapsed < AutoStartDelay ) return Vector3.Zero;

		if ( _autoSegment >= _diorama.AutoPath.Count )
		{
			CurrentSegmentLabel = "Done — enjoy the toys";
			return Vector3.Zero;
		}

		var target = _diorama.AutoPath[_autoSegment];
		var to = (target - GameObject.WorldPosition).WithZ( 0 );
		float dist = to.Length;

		if ( dist < 40f )
		{
			_autoSegment++;
			return Vector3.Zero;
		}

		CurrentSegmentLabel = _autoSegment switch
		{
			0 or 1 or 2 => "→ TriggerPush",
			3 or 4 => "Riding FuncMover",
			5 or 6 => "Walking to pool",
			7 => "Splashing into Water",
			8 or 9 => "→ TriggerTeleport",
			_ => "Among the balloons",
		};

		// Don't sprint into the pool — walk speed in/around it
		sprint = !InWater;

		// Jump if we need to climb up to next waypoint
		if ( _cc.IsOnGround && (target.z - GameObject.WorldPosition.z) > 25f && dist < 180f )
			jump = true;

		var camRot = Rotation.FromYaw( _camYaw );
		var local = camRot.Inverse * to.Normal;
		return new Vector3( local.x, local.y, 0 );
	}

	private void UpdateAnimGraph( Vector3 wishDir, Vector3 horizVel, bool grounded, bool justJumped )
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

		var wishVel = wishDir * (AutoPlay ? RunSpeed : (Input.Down( "Run" ) ? RunSpeed : WalkSpeed));
		var wishLocal = new Vector3( rot.Forward.Dot( wishVel ), rot.Right.Dot( wishVel ), 0 );
		_smr.Set( "wish_direction", MathF.Atan2( wishLocal.y, wishLocal.x ).RadianToDegree().NormalizeDegrees() );
		_smr.Set( "wish_speed", wishVel.Length );
		_smr.Set( "wish_groundspeed", wishVel.WithZ( 0 ).Length );
		_smr.Set( "wish_x", wishLocal.x );
		_smr.Set( "wish_y", wishLocal.y );
		_smr.Set( "wish_z", 0f );

		_smr.Set( "b_grounded", grounded );
		_smr.Set( "b_jump", justJumped );
		_smr.Set( "b_swim", InWater );
		_smr.Set( "b_climbing", false );
		_smr.Set( "b_noclip", false );
		_smr.Set( "duck", 0f );
	}

	private void UpdateCamera()
	{
		if ( _cameraGo == null ) return;

		// In AutoPlay, slow orbit of the diorama centered on the action.
		if ( AutoPlay )
		{
			_camYaw = 165f + MathF.Sin( Time.Now * 0.15f ) * 40f;
			_camPitch = 28f;
			_camDistance = 600f;
		}

		var head = GameObject.WorldPosition + new Vector3( 0, 0, 55 );
		var pitchRad = _camPitch * MathF.PI / 180f;
		var yawRad = _camYaw * MathF.PI / 180f;
		var offset = new Vector3(
			-MathF.Cos( yawRad ) * MathF.Cos( pitchRad ) * _camDistance,
			-MathF.Sin( yawRad ) * MathF.Cos( pitchRad ) * _camDistance,
			MathF.Sin( pitchRad ) * _camDistance + 60f
		);
		var target = head + offset;
		_cameraGo.WorldPosition = Vector3.Lerp( _cameraGo.WorldPosition, target, Time.Delta * (AutoPlay ? 2f : 6f) );
		_cameraGo.WorldRotation = Rotation.LookAt( (head - _cameraGo.WorldPosition).Normal, Vector3.Up );
	}
}
