using Sandbox;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Local.RagdollCannon;

/// <summary>
/// Builds the course at OnStart: cannon, ground, ramps, pendulums (HingeJoint),
/// trampoline (SpringJoint), and goal pit. Fires citizen ragdolls on Attack1.
/// Tracks Score and Shots. AutoPlay = true fires every 2.5s for video capture.
/// </summary>
public sealed class GameManager : Component
{
	[Property] public bool AutoPlay { get; set; } = false;
	[Property] public float AutoInterval { get; set; } = 2.5f;
	[Property] public float CooldownSec { get; set; } = 1.5f;

	[Property] public SoundEvent CannonSound { get; set; }
	[Property] public SoundEvent ThudSound { get; set; }
	[Property] public SoundEvent ScoreSound { get; set; }

	public int Score { get; private set; } = 0;
	public int Shots { get; private set; } = 0;
	public int LastShotPoints { get; private set; } = 0;
	public string Message { get; private set; } = "";
	private float _messageUntil = 0f;

	// Course geometry constants. 1 unit = 1 inch.
	public const float CourseLengthX = 1800f; // from cannon (x=-900) to pit (x=900)
	public const float GroundZ = 0f;

	// Cannon
	public Vector3 CannonMuzzle { get; private set; } = new Vector3( -650f, 0f, 110f );
	public Vector3 CannonFireDir { get; private set; } = Vector3.Zero;
	public float FireSpeed { get; set; } = 1300f; // imparted speed (inches/sec) to each body
	public float ArcAngleDeg { get; set; } = 32f; // up tilt

	// Pit
	public Vector3 PitCenter => new Vector3( 650f, 0f, 0f );
	public float PitHalfSize = 220f;
	public Vector3 BullseyeWorld => PitCenter;

	private float _nextFireAllowedAt = 0f;
	private float _nextAutoAt = 0f;
	private float _shakeUntil = 0f;
	private float _shakeAmount = 0f;
	private Vector3 _camHomePos;
	private Rotation _camHomeRot;
	private CameraComponent _cam;

	private GameObject _trampolineSurface;

	protected override void OnStart()
	{
		base.OnStart();

		CannonSound ??= ResourceLibrary.Get<SoundEvent>( "sounds/effects/explosion/explosion_small.sound" );
		ThudSound ??= ResourceLibrary.Get<SoundEvent>( "sounds/impacts/melee/impact-melee-flesh.sound" );
		ScoreSound ??= ResourceLibrary.Get<SoundEvent>( "sounds/kenney/ui/ui.favourite.sound" );

		// Compute cannon firing direction (forward = +X, slight up tilt)
		float a = ArcAngleDeg * MathF.PI / 180f;
		CannonFireDir = new Vector3( MathF.Cos( a ), 0f, MathF.Sin( a ) );

		_cam = Scene.Camera;
		if ( _cam != null )
		{
			_camHomePos = _cam.WorldPosition;
			_camHomeRot = _cam.WorldRotation;
		}

		BuildGround();
		BuildCannon();
		BuildPendulum( new Vector3( -100f, 0f, 280f ), 1 );
		BuildPendulum( new Vector3( 250f, 0f, 280f ), 2 );
		BuildTrampoline( new Vector3( 400f, 0f, 12f ) );
		BuildGoalPit();
		BuildBullseye();

		_nextAutoAt = Time.Now + 1.0f;
	}

	private void BuildGround()
	{
		var go = Scene.CreateObject();
		go.Name = "Ground";
		go.Parent = GameObject;
		go.WorldPosition = new Vector3( 0f, 0f, -10f );
		go.WorldScale = new Vector3( 40f, 20f, 0.4f ); // 2000x1000x20
		var mr = go.Components.Create<ModelRenderer>();
		mr.Model = Model.Load( "models/dev/box.vmdl" );
		mr.Tint = new Color( 0.18f, 0.20f, 0.18f );
		var col = go.Components.Create<BoxCollider>();
		col.Scale = new Vector3( 50f, 50f, 50f );
		col.Friction = 0.6f;
	}

	private void BuildRamp( Vector3 pos, Vector3 size )
	{
		var go = Scene.CreateObject();
		go.Name = "Ramp";
		go.Parent = GameObject;
		go.WorldPosition = pos;
		go.WorldScale = size / 50f;
		// Tilt slightly so ragdolls roll
		go.WorldRotation = Rotation.From( 0f, 0f, 0f ) * Rotation.FromAxis( Vector3.Forward, -15f );
		var mr = go.Components.Create<ModelRenderer>();
		mr.Model = Model.Load( "models/dev/box.vmdl" );
		mr.Tint = new Color( 0.45f, 0.30f, 0.18f );
		var col = go.Components.Create<BoxCollider>();
		col.Scale = new Vector3( 50f, 50f, 50f );
	}

	private void BuildCannon()
	{
		// Visual cannon (a long box tilted up). Static — no rigidbody.
		var cannonGo = Scene.CreateObject();
		cannonGo.Name = "Cannon";
		cannonGo.Parent = GameObject;
		cannonGo.WorldPosition = new Vector3( -720f, 0f, 90f );
		// Pitch up around Y so +X axis points slightly up
		cannonGo.WorldRotation = Rotation.FromAxis( Vector3.Left, ArcAngleDeg );
		cannonGo.WorldScale = new Vector3( 3.5f, 1.2f, 1.2f ); // 175 x 60 x 60
		var mr = cannonGo.Components.Create<ModelRenderer>();
		mr.Model = Model.Load( "models/dev/box.vmdl" );
		mr.Tint = new Color( 0.15f, 0.15f, 0.18f );

		// Cannon base (cylinder-ish using a box)
		var baseGo = Scene.CreateObject();
		baseGo.Name = "CannonBase";
		baseGo.Parent = GameObject;
		baseGo.WorldPosition = new Vector3( -720f, 0f, 35f );
		baseGo.WorldScale = new Vector3( 1.4f, 1.6f, 1.4f );
		var bmr = baseGo.Components.Create<ModelRenderer>();
		bmr.Model = Model.Load( "models/dev/box.vmdl" );
		bmr.Tint = new Color( 0.30f, 0.22f, 0.10f );
	}

	private void BuildPendulum( Vector3 anchorPos, int index )
	{
		// Anchor: needs a rigidbody with MotionEnabled=false so the joint can attach to it
		var anchorGo = Scene.CreateObject();
		anchorGo.Name = $"PendulumAnchor_{index}";
		anchorGo.Parent = GameObject;
		anchorGo.WorldPosition = anchorPos;
		anchorGo.WorldScale = new Vector3( 1.6f, 4f, 0.3f );
		var amr = anchorGo.Components.Create<ModelRenderer>();
		amr.Model = Model.Load( "models/dev/box.vmdl" );
		amr.Tint = new Color( 0.5f, 0.5f, 0.55f );
		var acol = anchorGo.Components.Create<BoxCollider>();
		acol.Scale = new Vector3( 50f, 50f, 50f );
		var arb = anchorGo.Components.Create<Rigidbody>();
		arb.MotionEnabled = false; // static
		arb.Gravity = false;

		// Pendulum bob: hanging box that swings.
		// In AttachmentMode.Auto the hinge will lock the bob's current world position
		// relative to the anchor — so place the bob at its desired hanging position first.
		var bobGo = Scene.CreateObject();
		bobGo.Name = $"PendulumBob_{index}";
		bobGo.Parent = GameObject;
		// Hang directly below the anchor
		bobGo.WorldPosition = anchorPos + new Vector3( 0f, 0f, -140f );
		bobGo.WorldScale = new Vector3( 0.9f, 1.6f, 1.8f );
		var bmr = bobGo.Components.Create<ModelRenderer>();
		bmr.Model = Model.Load( "models/dev/box.vmdl" );
		bmr.Tint = new Color( 0.85f, 0.15f, 0.10f );
		var bcol = bobGo.Components.Create<BoxCollider>();
		bcol.Scale = new Vector3( 50f, 50f, 50f );
		bcol.Friction = 0.4f;
		var brb = bobGo.Components.Create<Rigidbody>();
		brb.MassOverride = 25f;
		brb.LinearDamping = 0.1f;
		brb.AngularDamping = 0.1f;
		brb.Gravity = true;

		// Hinge with Attachment=Auto connects the two bodies at their current relative pose.
		// We set Body=anchor and AnchorBody=bob so the hinge axis runs through the anchor.
		var hinge = bobGo.Components.Create<HingeJoint>();
		hinge.Body = bobGo;
		hinge.AnchorBody = anchorGo;
		hinge.EnableCollision = false;

		// Initial nudge to start swinging
		var pb = brb.PhysicsBody;
		if ( pb != null )
		{
			pb.ApplyImpulse( new Vector3( index % 2 == 0 ? -1500f : 1500f, 0f, 0f ) );
		}

		bobGo.Components.Create<ObstacleImpactSound>();
	}

	private void BuildTrampoline( Vector3 pos )
	{
		// SpringJoint trampoline: a small platform attached by a spring to a static anchor above.
		// When a ragdoll lands on it, the spring compresses and bounces it back.
		// Build a sturdier approach: heavy bouncy box on the ground (elastic collider) is more reliable.
		// We still use a SpringJoint for the secondary-gap goal.

		// Static anchor below ground
		var anchorGo = Scene.CreateObject();
		anchorGo.Name = "TrampolineAnchor";
		anchorGo.Parent = GameObject;
		anchorGo.WorldPosition = pos + new Vector3( 0f, 0f, -60f );
		anchorGo.WorldScale = new Vector3( 2.5f, 2.5f, 0.1f );
		var amr = anchorGo.Components.Create<ModelRenderer>();
		amr.Model = Model.Load( "models/dev/box.vmdl" );
		amr.Tint = new Color( 0.20f, 0.20f, 0.20f );
		var arb = anchorGo.Components.Create<Rigidbody>();
		arb.MotionEnabled = false;
		arb.Gravity = false;

		// Bouncy surface
		var padGo = Scene.CreateObject();
		padGo.Name = "TrampolinePad";
		padGo.Parent = GameObject;
		padGo.WorldPosition = pos + new Vector3( 0f, 0f, 18f );
		padGo.WorldScale = new Vector3( 2.2f, 2.2f, 0.3f );
		var pmr = padGo.Components.Create<ModelRenderer>();
		pmr.Model = Model.Load( "models/dev/box.vmdl" );
		pmr.Tint = new Color( 0.15f, 0.85f, 0.95f );
		var pcol = padGo.Components.Create<BoxCollider>();
		pcol.Scale = new Vector3( 50f, 50f, 50f );
		pcol.Elasticity = 1.2f; // super-bouncy
		pcol.Friction = 0.2f;
		var prb = padGo.Components.Create<Rigidbody>();
		prb.MassOverride = 30f;
		prb.LinearDamping = 4f;
		prb.AngularDamping = 8f;
		prb.Gravity = true;

		// Spring joint anchored below
		var spring = padGo.Components.Create<SpringJoint>();
		spring.Body = padGo;
		spring.AnchorBody = anchorGo;
		spring.Frequency = 8f;
		spring.Damping = 0.5f;
		spring.MinLength = 50f;
		spring.MaxLength = 90f;
		spring.RestLength = 75f;
		spring.EnableCollision = false;

		_trampolineSurface = padGo;
		padGo.Components.Create<ObstacleImpactSound>();
	}

	private void BuildGoalPit()
	{
		// Visual pit walls (4 boxes forming a sunken square)
		var halfX = PitHalfSize;
		var halfY = PitHalfSize;
		var wallThick = 12f;
		var wallH = 40f;
		var tint = new Color( 0.85f, 0.75f, 0.20f );

		AddWall( "Pit_N", PitCenter + new Vector3( 0f, halfY, wallH * 0.5f ), new Vector3( halfX * 2f + wallThick * 2f, wallThick, wallH ), tint );
		AddWall( "Pit_S", PitCenter + new Vector3( 0f, -halfY, wallH * 0.5f ), new Vector3( halfX * 2f + wallThick * 2f, wallThick, wallH ), tint );
		AddWall( "Pit_E", PitCenter + new Vector3( halfX, 0f, wallH * 0.5f ), new Vector3( wallThick, halfY * 2f, wallH ), tint );
		AddWall( "Pit_W", PitCenter + new Vector3( -halfX, 0f, wallH * 0.5f ), new Vector3( wallThick, halfY * 2f, wallH ), tint );

		// Trigger volume covering the inside of the pit, tall so high-arc ragdolls register too
		var triggerGo = Scene.CreateObject();
		triggerGo.Name = "PitTrigger";
		triggerGo.Parent = GameObject;
		triggerGo.WorldPosition = PitCenter + new Vector3( 0f, 0f, 200f );
		var trig = triggerGo.Components.Create<BoxCollider>();
		trig.IsTrigger = true;
		trig.Scale = new Vector3( halfX * 2f - 2f, halfY * 2f - 2f, 420f );
		var pit = triggerGo.Components.Create<PitTrigger>();
		pit.Manager = this;
	}

	private void AddWall( string name, Vector3 pos, Vector3 size, Color tint )
	{
		var go = Scene.CreateObject();
		go.Name = name;
		go.Parent = GameObject;
		go.WorldPosition = pos;
		go.WorldScale = size / 50f;
		var mr = go.Components.Create<ModelRenderer>();
		mr.Model = Model.Load( "models/dev/box.vmdl" );
		mr.Tint = tint;
		var col = go.Components.Create<BoxCollider>();
		col.Scale = new Vector3( 50f, 50f, 50f );
	}

	private void BuildBullseye()
	{
		// A flat red disc at the pit center
		var go = Scene.CreateObject();
		go.Name = "Bullseye";
		go.Parent = GameObject;
		go.WorldPosition = PitCenter + new Vector3( 0f, 0f, 0.5f );
		go.WorldScale = new Vector3( 60f / 32f, 60f / 32f, 0.05f );
		var mr = go.Components.Create<ModelRenderer>();
		mr.Model = Model.Load( "models/dev/sphere.vmdl" );
		mr.Tint = new Color( 0.95f, 0.10f, 0.10f );
	}

	protected override void OnUpdate()
	{
		// Manual fire
		if ( Input.Pressed( "Attack1" ) && Time.Now >= _nextFireAllowedAt )
		{
			FireRagdoll();
		}

		// Autoplay
		if ( AutoPlay && Time.Now >= _nextAutoAt && Time.Now >= _nextFireAllowedAt )
		{
			FireRagdoll();
			_nextAutoAt = Time.Now + AutoInterval;
		}

		// Screen shake decay
		if ( _cam != null && Time.Now < _shakeUntil )
		{
			float t = (_shakeUntil - Time.Now) / 0.3f;
			float amp = _shakeAmount * t;
			var jitter = new Vector3(
				(Game.Random.Float( -1f, 1f )) * amp,
				(Game.Random.Float( -1f, 1f )) * amp,
				(Game.Random.Float( -1f, 1f )) * amp );
			_cam.WorldPosition = _camHomePos + jitter;
		}
		else if ( _cam != null && _cam.WorldPosition != _camHomePos )
		{
			_cam.WorldPosition = _camHomePos;
		}

		if ( _messageUntil > 0f && Time.Now > _messageUntil )
		{
			Message = "";
			_messageUntil = 0f;
		}
	}

	private void FireRagdoll()
	{
		_nextFireAllowedAt = Time.Now + CooldownSec;
		Shots++;

		// Spawn the citizen, attach SkinnedModelRenderer + ModelPhysics, kill the anim graph.
		var go = Scene.CreateObject();
		go.Name = $"Ragdoll_{Shots}";
		go.WorldPosition = CannonMuzzle;
		// Face the firing direction so impulse direction matches forward (cosmetic only)
		go.WorldRotation = Rotation.LookAt( CannonFireDir.WithZ( 0f ).Normal );

		var smr = go.Components.Create<SkinnedModelRenderer>();
		smr.Model = Model.Load( "models/citizen_human/citizen_human_male.vmdl" );
		// Keep CreateBoneObjects so bone GameObjects exist and ModelPhysics
		// can drive them. ModelPhysics overrides the anim graph at runtime.
		smr.CreateBoneObjects = true;
		smr.UseAnimGraph = false;

		var mp = go.Components.Create<ModelPhysics>();
		mp.Renderer = smr;
		mp.Model = smr.Model;
		mp.IgnoreRoot = true; // critical: don't pin to root; all bodies move freely
		mp.MotionEnabled = true;

		// Self-destruct after 8s to keep the scene from filling up
		var td = go.Components.Create<RagdollLifetime>();
		td.Lifetime = 6f;
		td.Manager = this;

		// Tag it as a ragdoll so the pit trigger can find it
		go.Tags.Add( "ragdoll" );

		// Apply impulse to every body in the ragdoll along firing direction.
		// ModelPhysics populates Bodies eagerly when Model is assigned, so we can launch now.
		var fireDir = CannonFireDir;
		var fireSpeed = FireSpeed;
		var bodies = mp.Bodies;
		if ( bodies != null && bodies.Count > 0 )
		{
			foreach ( var body in bodies )
			{
				var rb = body.Component;
				if ( rb == null ) continue;
				// Low damping so the ragdoll keeps flying
				rb.LinearDamping = 0.05f;
				rb.AngularDamping = 0.1f;
				rb.EnhancedCcd = true;
				// Tiny per-body jitter so the ragdoll breaks symmetry and tumbles.
				var jitter = new Vector3(
					Game.Random.Float( -30f, 30f ),
					Game.Random.Float( -30f, 30f ),
					Game.Random.Float( -30f, 30f ) );
				rb.ApplyImpulse( (fireDir * fireSpeed + jitter) * rb.Mass );
				// Light angular kick so limbs flail without spinning out of control
				if ( rb.PhysicsBody != null )
				{
					rb.PhysicsBody.ApplyAngularImpulse( new Vector3(
						Game.Random.Float( -60f, 60f ),
						Game.Random.Float( -60f, 60f ),
						Game.Random.Float( -60f, 60f ) ) * rb.Mass );
				}
			}
		}
		else
		{
			// Fallback: defer if bodies aren't ready yet
			_pendingLaunches.Add( new PendingLaunch { Mp = mp, Dir = fireDir, Speed = fireSpeed, Frame = 0 } );
		}

		// FX
		MuzzleFlash();
		ScreenShake( 6f, 0.35f );
		if ( CannonSound != null ) Sound.Play( CannonSound, CannonMuzzle );
	}

	private readonly List<PendingLaunch> _pendingLaunches = new();

	protected override void OnFixedUpdate()
	{
		// Apply launch impulse after one or two physics ticks so PhysicsGroup is populated.
		for ( int i = _pendingLaunches.Count - 1; i >= 0; i-- )
		{
			var p = _pendingLaunches[i];
			p.Frame++;
			if ( p.Mp == null || !p.Mp.IsValid() )
			{
				_pendingLaunches.RemoveAt( i );
				continue;
			}
			var bodies = p.Mp.Bodies;
			if ( bodies != null && bodies.Count > 0 )
			{
				foreach ( var body in bodies )
				{
					var rb = body.Component;
					if ( rb == null ) continue;
					rb.ApplyImpulse( p.Dir * p.Speed * rb.Mass );
				}
				_pendingLaunches.RemoveAt( i );
			}
			else if ( p.Frame > 20 )
			{
				_pendingLaunches.RemoveAt( i );
			}
			else
			{
				_pendingLaunches[i] = p; // write back frame counter
			}
		}
	}

	private void MuzzleFlash()
	{
		var go = Scene.CreateObject();
		go.Name = "MuzzleFlash";
		var muzzleTip = CannonMuzzle + CannonFireDir * 40f;
		go.WorldPosition = muzzleTip;
		go.WorldScale = new Vector3( 1.5f, 1.5f, 1.5f );
		var mr = go.Components.Create<ModelRenderer>();
		mr.Model = Model.Load( "models/dev/sphere.vmdl" );
		mr.Tint = new Color( 1.0f, 0.85f, 0.30f );
		var fade = go.Components.Create<MuzzleFlashFade>();
		fade.Lifetime = 0.18f;
	}

	private void ScreenShake( float amount, float duration )
	{
		_shakeAmount = amount;
		_shakeUntil = Time.Now + duration;
	}

	public void OnRagdollEnteredPit( GameObject ragdoll )
	{
		// Compute distance from bullseye to pelvis (use ragdoll's WorldPosition — root)
		float d = (ragdoll.WorldPosition.WithZ( 0f ) - BullseyeWorld.WithZ( 0f ) ).Length;
		int pts;
		if ( d < 30f ) pts = 100;
		else if ( d < 80f ) pts = 50;
		else if ( d < 140f ) pts = 25;
		else if ( d < 200f ) pts = 10;
		else pts = 5;

		Score += pts;
		LastShotPoints = pts;
		Message = $"+{pts}";
		_messageUntil = Time.Now + 1.5f;
		if ( ScoreSound != null ) Sound.Play( ScoreSound, ragdoll.WorldPosition );
	}

	public void PlayThud( Vector3 pos )
	{
		if ( ThudSound != null ) Sound.Play( ThudSound, pos );
	}

	private struct PendingLaunch
	{
		public ModelPhysics Mp;
		public Vector3 Dir;
		public float Speed;
		public int Frame;
	}
}
