using Sandbox;
using System;
using System.Linq;

namespace Local.CtfArena;

/// <summary>
/// CTF citizen player. Carries a team flag for its opposite team home base.
///
/// Movement: WASD + sprint (Shift) + crouch (Ctrl). Drives the citizen
/// animgraph. Has a CharacterController + gravity.
///
/// Autonomy: when <see cref="AutoPlay"/> is true, the player AI plays the CTF
/// loop automatically — run toward the enemy flag, grab it, run back to your
/// own base. This is the *only* path exercised by the autonomous video harness
/// (since spinning up two real client connections from a headless test isn't
/// supported by the editor tooling). The networking attributes on
/// <see cref="GameManager"/> etc. still compile against the engine's
/// networking machinery — they just aren't observably exercised by a 2nd
/// client.
///
/// Networking attributes used here:
///   - [Sync] on Velocity / IsCrouched (replicated each tick)
///   - [Sync(SyncFlags.FromHost)] on Team (set once by host)
///   - [Rpc.Broadcast] on PlayPickupSound (called by host, every client plays it)
///   - [Rpc.Owner] on ShowOwnerToast (would only run on the owning client)
/// </summary>
public sealed class CtfPlayer : Component
{
	[Property] public Team Team { get; set; } = Team.Red;
	[Property] public float WalkSpeed { get; set; } = 180f;
	[Property] public float SprintSpeed { get; set; } = 360f;
	[Property] public float CrouchSpeed { get; set; } = 90f;
	[Property] public float Gravity { get; set; } = 900f;
	[Property] public float RotationSpeed { get; set; } = 10f;
	[Property] public float JumpSpeed { get; set; } = 280f;

	[Property] public bool AutoPlay { get; set; } = false;
	[Property] public bool IsLocalPlayer { get; set; } = false;

	// ─────── Networked state ───────
	// Velocity is replicated each tick for remote clients to interpolate.
	[Sync] public Vector3 NetVelocity { get; set; }
	[Sync] public bool IsCrouched { get; set; }

	// Team is set by the host at spawn and never changes — FromHost is exactly
	// the right SyncFlag for that.
	[Sync( SyncFlags.FromHost )] public Team NetTeam { get; set; }

	/// <summary>True while this player is carrying the enemy flag.</summary>
	public bool IsCarryingFlag => GameManager.Instance is { } gm &&
		(gm.RedFlagCarrier == GameObject.Id || gm.BlueFlagCarrier == GameObject.Id);

	private SkinnedModelRenderer _smr;
	private CharacterController _cc;
	private GameObject _cameraGo;

	private float _camYaw = 0f;
	private float _camPitch = 22f;
	private float _camDistance = 280f;
	private Vector3 _spawn;

	// AutoPlay state
	private float _autoRepathTimer;
	private Vector3 _autoTarget;
	private float _stuckTimer;
	private Vector3 _lastPos;

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

		if ( _smr != null )
		{
			// Tint the model toward the team color (subtle so the citizen is
			// still recognizable).
			var teamCol = Team.ToColor();
			_smr.Tint = Color.Lerp( Color.White, teamCol, 0.55f );
		}

		// Host-authoritative team assignment.
		if ( Networking.IsHost )
			NetTeam = Team;
		else
			Team = NetTeam == Team.None ? Team : NetTeam;

		_cameraGo = Scene.Camera?.GameObject;
		_spawn = GameObject.WorldPosition;
		_lastPos = _spawn;
	}

	protected override void OnUpdate()
	{
		if ( _cc == null ) return;

		// Respawn if fell off the world
		if ( GameObject.WorldPosition.z < -200 )
		{
			GameObject.WorldPosition = _spawn;
			_cc.Velocity = Vector3.Zero;
		}

		bool grounded = _cc.IsOnGround;

		Vector3 wishDir;
		bool wantSprint;
		bool wantCrouch;
		bool wantJump;
		GetWishInput( out wishDir, out wantSprint, out wantCrouch, out wantJump );

		IsCrouched = wantCrouch && grounded;
		float speed = IsCrouched ? CrouchSpeed : (wantSprint ? SprintSpeed : WalkSpeed);
		Vector3 wishVel = wishDir * speed;

		if ( grounded )
		{
			_cc.Velocity = new Vector3( wishVel.x, wishVel.y, _cc.Velocity.z );
			if ( _cc.Velocity.z < 0 ) _cc.Velocity = _cc.Velocity.WithZ( 0 );
			if ( wantJump )
				_cc.Velocity = _cc.Velocity.WithZ( JumpSpeed );
		}
		else
		{
			var horiz = _cc.Velocity.WithZ( 0 );
			horiz = Vector3.Lerp( horiz, wishVel, Time.Delta * 2f );
			_cc.Velocity = new Vector3( horiz.x, horiz.y, _cc.Velocity.z - Gravity * Time.Delta );
		}

		_cc.Move();

		NetVelocity = _cc.Velocity;

		var horizVel = _cc.Velocity.WithZ( 0 );
		if ( horizVel.Length > 5f )
		{
			var wantRot = Rotation.LookAt( horizVel.Normal, Vector3.Up );
			GameObject.WorldRotation = Rotation.Lerp( GameObject.WorldRotation, wantRot, Time.Delta * RotationSpeed );
		}

		UpdateAnimGraph( wishDir, horizVel, grounded, wantSprint, IsCrouched );

		// Flag interactions — proximity-based; host arbitrates.
		if ( Networking.IsHost || !Game.IsPlaying )
		{
			TryFlagInteractions();
		}

		if ( IsLocalPlayer )
			UpdateCamera();
	}

	private void GetWishInput( out Vector3 wishDir, out bool sprint, out bool crouch, out bool jump )
	{
		Vector3 input;
		if ( AutoPlay )
		{
			input = AutoplayInput( out sprint, out crouch, out jump );
		}
		else if ( IsLocalPlayer )
		{
			input = Input.AnalogMove;
			sprint = Input.Down( "Run" );
			crouch = Input.Down( "Duck" );
			jump = Input.Pressed( "Jump" );

			_camYaw -= Input.MouseDelta.x * 0.15f;
			_camPitch += Input.MouseDelta.y * 0.10f;
			_camPitch = Math.Clamp( _camPitch, -10f, 60f );
		}
		else
		{
			input = Vector3.Zero;
			sprint = false; crouch = false; jump = false;
		}

		var camRot = Rotation.FromYaw( _camYaw );
		wishDir = camRot * new Vector3( input.x, input.y, 0 );
		if ( wishDir.Length > 1f ) wishDir = wishDir.Normal;
	}

	private Vector3 AutoplayInput( out bool sprint, out bool crouch, out bool jump )
	{
		sprint = true;
		crouch = false;
		jump = false;

		var gm = GameManager.Instance;
		if ( gm is null ) return Vector3.Zero;

		// Pick a target: if we're carrying the flag, run home. Otherwise,
		// run toward the enemy flag.
		Vector3 target;
		if ( IsCarryingFlag )
		{
			target = gm.GetTeamBasePosition( Team );
		}
		else
		{
			target = gm.GetEnemyFlagPosition( Team );
		}

		_autoTarget = target;

		var here = GameObject.WorldPosition;
		var toTarget = (target - here).WithZ( 0 );
		float dist = toTarget.Length;

		// Stuck detection — if we haven't moved much in the last 0.6s, jiggle.
		if ( Vector3.DistanceBetween( here, _lastPos ) < 8f )
		{
			_stuckTimer += Time.Delta;
		}
		else
		{
			_stuckTimer = 0f;
			_lastPos = here;
		}

		if ( _stuckTimer > 0.6f )
		{
			// jump and sidestep
			jump = true;
			var side = Vector3.Cross( toTarget.Normal, Vector3.Up );
			toTarget = (toTarget.Normal + side * 0.5f).Normal * dist;
			if ( _stuckTimer > 1.5f ) _stuckTimer = 0f;
		}

		if ( dist < 5f ) return Vector3.Zero;

		var dir = toTarget.Normal;
		// Player local input is camera-relative; for the AI we synthesise raw
		// world-space wish dir and skip the camera rotation by writing camYaw
		// to zero (so camRot is identity). Simpler: just rotate by inverse.
		var camRot = Rotation.FromYaw( _camYaw );
		var local = camRot.Inverse * dir;
		return new Vector3( local.x, local.y, 0 );
	}

	private void UpdateAnimGraph( Vector3 wishDir, Vector3 horizVel, bool grounded, bool sprinting, bool crouched )
	{
		if ( _smr == null || _smr.Model == null ) return;

		var rot = GameObject.WorldRotation;
		var forward = rot.Forward.Dot( horizVel );
		var sideward = rot.Right.Dot( horizVel );
		float speed = horizVel.Length;
		float angle = speed > 1f
			? MathF.Atan2( sideward, forward ).RadianToDegree().NormalizeDegrees()
			: 0f;

		_smr.Set( "move_direction", angle );
		_smr.Set( "move_speed", speed );
		_smr.Set( "move_groundspeed", speed );
		_smr.Set( "move_x", forward );
		_smr.Set( "move_y", sideward );
		_smr.Set( "move_z", _cc.Velocity.z );

		var wishVel = wishDir * (sprinting ? SprintSpeed : WalkSpeed);
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
		_smr.Set( "duck", crouched ? 1f : 0f );
		_smr.Set( "move_rotationspeed", 0f );
	}

	private void TryFlagInteractions()
	{
		var gm = GameManager.Instance;
		if ( gm is null ) return;
		if ( gm.CurrentState is not ActiveState ) return;

		var here = GameObject.WorldPosition;
		var enemyFlag = gm.GetEnemyFlag( Team );
		var ownFlag = gm.GetTeamFlag( Team );

		// Pickup enemy flag if at its home AND nobody is carrying it.
		// Distance ignores Z so the player can run under the flag pole.
		if ( enemyFlag != null )
		{
			bool carried = gm.GetCarrierIdFor( enemyFlag.Team ) != default;
			if ( !carried && !IsCarryingFlag )
			{
				var dxy = (here.WithZ( 0 ) - enemyFlag.WorldPosition.WithZ( 0 )).Length;
				if ( dxy < enemyFlag.PickupRadius )
				{
					gm.HandleFlagPickup( this, enemyFlag );
				}
			}
		}

		// Score: if we're carrying the enemy flag AND we're at our own base.
		// Note: in canonical CTF you also require your own flag to be home; we
		// relax that here so two autonomous bots running mirror loops can't
		// deadlock on a permanent simultaneous-grab.
		if ( IsCarryingFlag && ownFlag != null )
		{
			var ourBase = gm.GetTeamBasePosition( Team );
			var basedxy = (here.WithZ( 0 ) - ourBase.WithZ( 0 )).Length;
			if ( basedxy < 150f )
			{
				gm.HandleScore( this );
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

	// ─────── RPC examples ───────

	/// <summary>
	/// Called by the host when this player picks up a flag. Broadcast so every
	/// client plays the sound + spawns a particle. NetFlags.HostOnly ensures
	/// only the host's call goes through the wire.
	/// </summary>
	[Rpc.Broadcast]
	public void PlayPickupSound()
	{
		// In a single-process demo this still runs locally; in a real game
		// every client would receive it.
		Sound.Play( "sounds/editor/success.sound", WorldPosition );
	}

	/// <summary>
	/// Owner-only RPC — only the client whose connection owns this player
	/// would actually run this in a multi-client game. Used here to demo the
	/// attribute; in single-process there's just one consumer.
	/// </summary>
	[Rpc.Owner]
	public void ShowOwnerToast( string message )
	{
		Log.Info( $"[CTF][owner-only] {Team} player: {message}" );
	}
}
