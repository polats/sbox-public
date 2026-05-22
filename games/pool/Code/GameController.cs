using Sandbox;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Local.Pool;

public enum GameState
{
	Aiming,
	Shooting,
	Won,
}

/// <summary>
/// Orchestrates the pool game: builds the table + rack + cue ball at start,
/// drives aim/shoot input, tracks pocketed balls, plays sounds.
/// 1 unit = 1 inch. Table = 112" x 56" (regulation 9-foot).
/// </summary>
public sealed class GameController : Component
{
	// Physical table dimensions (playable felt area, inside cushions)
	public const float TableLength = 100f;   // X axis
	public const float TableWidth  = 50f;    // Y axis
	public const float TableHeight = 1f;     // surface Z thickness
	public const float CushionHeight = 8f;
	public const float CushionThickness = 4f;
	public const float PocketRadius = 4f;

	// Ball: real pool ball is 2.25" diameter. Sphere model is 64" → scale = 2.25/64 ≈ 0.0352
	public const float BallDiameter = 2.4f;
	public const float BallRadius   = BallDiameter * 0.5f;
	public const float BallScale    = BallDiameter / 64f;
	public const float BallSurfaceZ = TableHeight + BallRadius + 0.01f;

	public const float AimMaxDistance = 50f;
	public const float ImpulseMin = 80f;
	public const float ImpulseMax = 350f;

	public GameState State { get; private set; } = GameState.Aiming;
	public GameObject CueBall { get; private set; }
	public List<GameObject> ObjectBalls { get; } = new();
	public string Message { get; set; } = "";
	public float MessageUntil { get; private set; } = 0f;
	public float PowerNormalized { get; private set; } = 0f;
	public bool IsCharging { get; private set; } = false;
	public Vector3 AimDirection { get; private set; } = Vector3.Forward;
	public bool Scratched { get; private set; } = false;

	// Sounds
	[Property] public SoundEvent CueStrikeSound { get; set; }
	[Property] public SoundEvent BallHitSound { get; set; }
	[Property] public SoundEvent PocketSound { get; set; }
	[Property] public SoundEvent WinSound { get; set; }

	[Property] public bool AutoPlay { get; set; } = false;
	[Property] public float AutoFirstShotDelay { get; set; } = 1.5f;
	[Property] public float AutoBetweenShotDelay { get; set; } = 1.2f;

	private float _autoNextShotTime = -1f;
	private int _autoShotCount = 0;

	private CameraComponent _cam;
	private float _dragStartPower = 0f;
	private Vector2 _mouseDownPos;
	private float _winTimer = 0f;

	protected override void OnStart()
	{
		base.OnStart();
		_cam = Scene.Camera;

		// Hook up sound resources by name. These are fully-qualified .sound paths
		// that the engine ships with — see core/sounds/.
		CueStrikeSound ??= ResourceLibrary.Get<SoundEvent>( "sounds/impacts/melee/impact-melee-wood.sound" );
		BallHitSound  ??= ResourceLibrary.Get<SoundEvent>( "sounds/kenney/ui/ui.button.press.sound" );
		PocketSound   ??= ResourceLibrary.Get<SoundEvent>( "sounds/impacts/melee/impact-melee-cloth.sound" );
		WinSound      ??= ResourceLibrary.Get<SoundEvent>( "sounds/editor/success.sound" );

		BuildTable();
		BuildPockets();
		BuildCueBall();
		BuildRack();
	}

	private void BuildTable()
	{
		// Felt surface (a flat box). Top face sits at z = TableHeight.
		var feltGo = Scene.CreateObject();
		feltGo.Name = "Table_Felt";
		feltGo.Parent = GameObject;
		feltGo.WorldPosition = new Vector3( 0, 0, TableHeight * 0.5f );
		// scale a 50x50x50 box → TableLength x TableWidth x TableHeight
		feltGo.WorldScale = new Vector3( TableLength / 50f, TableWidth / 50f, TableHeight / 50f );
		var feltModel = feltGo.Components.Create<ModelRenderer>();
		feltModel.Model = Model.Load( "models/dev/box.vmdl" );
		feltModel.Tint = new Color( 0.10f, 0.45f, 0.18f );
		var feltCol = feltGo.Components.Create<BoxCollider>();
		feltCol.Scale = new Vector3( 50f, 50f, 50f );
		feltCol.Center = Vector3.Zero;

		// Cushions: 4 boxes around the table top, sitting on top of the felt.
		// Pockets break the long cushions in half (at the side pockets).
		var cushTint = new Color( 0.30f, 0.18f, 0.08f );
		float halfL = TableLength * 0.5f;
		float halfW = TableWidth * 0.5f;
		// Cushion sits flush with felt bottom: span z=0..z=CushionHeight so balls cannot pass under or over
		float cushTopZ = CushionHeight * 0.5f;
		float gap = PocketRadius * 2.2f;

		// Long cushions: split into two segments by the side pocket gap at x=0
		float longSegLen = (halfL - gap * 0.5f) - PocketRadius; // from corner pocket inset to side pocket gap
		// each segment center x:
		float longSegX = (PocketRadius + halfL - gap * 0.5f) * 0.5f;

		// Top (+Y) cushions
		AddCushion( "Cushion_TopLeft",  new Vector3( -longSegX, halfW + CushionThickness * 0.5f, cushTopZ ), new Vector3( longSegLen, CushionThickness, CushionHeight ), cushTint );
		AddCushion( "Cushion_TopRight", new Vector3(  longSegX, halfW + CushionThickness * 0.5f, cushTopZ ), new Vector3( longSegLen, CushionThickness, CushionHeight ), cushTint );
		AddCushion( "Cushion_BotLeft",  new Vector3( -longSegX, -halfW - CushionThickness * 0.5f, cushTopZ ), new Vector3( longSegLen, CushionThickness, CushionHeight ), cushTint );
		AddCushion( "Cushion_BotRight", new Vector3(  longSegX, -halfW - CushionThickness * 0.5f, cushTopZ ), new Vector3( longSegLen, CushionThickness, CushionHeight ), cushTint );

		// Short cushions (single each), inset for corner pockets
		float shortLen = TableWidth - PocketRadius * 2f;
		AddCushion( "Cushion_Left",  new Vector3( -halfL - CushionThickness * 0.5f, 0, cushTopZ ), new Vector3( CushionThickness, shortLen, CushionHeight ), cushTint );
		AddCushion( "Cushion_Right", new Vector3(  halfL + CushionThickness * 0.5f, 0, cushTopZ ), new Vector3( CushionThickness, shortLen, CushionHeight ), cushTint );
	}

	private void AddCushion( string name, Vector3 pos, Vector3 size, Color tint )
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

	private void BuildPockets()
	{
		// 6 pockets: 4 corners + 2 sides (mid-X).
		float halfL = TableLength * 0.5f;
		float halfW = TableWidth * 0.5f;
		var positions = new[]
		{
			new Vector3( -halfL,  halfW, TableHeight ),
			new Vector3(  halfL,  halfW, TableHeight ),
			new Vector3( -halfL, -halfW, TableHeight ),
			new Vector3(  halfL, -halfW, TableHeight ),
			new Vector3(  0,      halfW, TableHeight ),
			new Vector3(  0,     -halfW, TableHeight ),
		};
		for ( int i = 0; i < positions.Length; i++ )
		{
			var go = Scene.CreateObject();
			go.Name = $"Pocket_{i}";
			go.Parent = GameObject;
			go.WorldPosition = positions[i];

			// Visual: a small black disc (use scaled sphere, flattened, dark)
			var visualGo = Scene.CreateObject();
			visualGo.Name = "PocketVisual";
			visualGo.Parent = go;
			visualGo.LocalPosition = new Vector3( 0, 0, -TableHeight * 0.4f );
			visualGo.WorldScale = new Vector3( PocketRadius * 2f / 32f, PocketRadius * 2f / 32f, 0.05f );
			var mr = visualGo.Components.Create<ModelRenderer>();
			mr.Model = Model.Load( "models/dev/sphere.vmdl" );
			mr.Tint = new Color( 0.04f, 0.04f, 0.04f );

			// Trigger
			var col = go.Components.Create<BoxCollider>();
			col.IsTrigger = true;
			col.Scale = new Vector3( PocketRadius * 2f, PocketRadius * 2f, CushionHeight * 2f );
			col.Center = new Vector3( 0, 0, CushionHeight * 0.5f );

			var pocket = go.Components.Create<Pocket>();
			pocket.Controller = this;
		}
	}

	private void BuildCueBall()
	{
		CueBall = CreateBall( "CueBall", new Vector3( -TableLength * 0.25f, 0, BallSurfaceZ ), Color.White, true );
	}

	private void BuildRack()
	{
		// Triangle rack apex at +X side, point at x = 0.25 * TableLength
		float apexX = TableLength * 0.25f;
		float spacing = BallDiameter + 0.05f;
		var colors = new[]
		{
			new Color( 1f, 0.9f, 0.1f ),    // 1 yellow
			new Color( 0.2f, 0.4f, 1f ),    // 2 blue
			new Color( 1f, 0.2f, 0.2f ),    // 3 red
			new Color( 0.6f, 0.1f, 0.7f ),  // 4 purple
			new Color( 1f, 0.5f, 0.1f ),    // 5 orange
			new Color( 0.1f, 0.6f, 0.2f ),  // 6 green
			new Color( 0.5f, 0.0f, 0.0f ),  // 7 maroon
			new Color( 0.05f, 0.05f, 0.05f ),// 8 black
			new Color( 0.9f, 0.7f, 0.1f ),  // 9 gold
		};
		// Rack rows: 1 in row 0, 2 in row 1, 3 in row 2, 2 in row 3, 1 in row 4 (diamond rack)
		int idx = 0;
		int[] rowCounts = { 1, 2, 3, 2, 1 };
		for ( int r = 0; r < rowCounts.Length; r++ )
		{
			float xRow = apexX + r * spacing * 0.866f;
			int n = rowCounts[r];
			for ( int c = 0; c < n; c++ )
			{
				float y = (c - (n - 1) * 0.5f) * spacing;
				var color = colors[idx];
				var go = CreateBall( $"Ball_{idx + 1}", new Vector3( xRow, y, BallSurfaceZ ), color, false );
				ObjectBalls.Add( go );
				idx++;
			}
		}
	}

	private GameObject CreateBall( string name, Vector3 pos, Color tint, bool isCue )
	{
		var go = Scene.CreateObject();
		go.Name = name;
		go.Parent = GameObject;
		go.WorldPosition = pos;
		go.WorldScale = new Vector3( BallScale, BallScale, BallScale );

		var mr = go.Components.Create<ModelRenderer>();
		mr.Model = Model.Load( "models/dev/sphere.vmdl" );
		mr.Tint = tint;

		var col = go.Components.Create<SphereCollider>();
		col.Radius = 32f;       // local-space (sphere is 64" diameter → radius 32"); scaled by BallScale
		col.Center = Vector3.Zero;
		// Bouncy ball-ball collisions
		col.Elasticity = 0.92f;
		col.Friction = 0.2f;

		var rb = go.Components.Create<Rigidbody>();
		rb.MassOverride = 0.17f; // ~170g pool ball
		rb.LinearDamping = 0.6f;
		rb.AngularDamping = 0.6f;
		rb.Gravity = true;
		rb.EnableImpactDamage = false;
		rb.EnhancedCcd = true;
		rb.SleepThreshold = 5f;
		// Lock Z motion isn't strictly needed; gravity + table collider keeps them on the felt.

		var ball = go.Components.Create<PoolBall>();
		ball.Controller = this;
		ball.IsCueBall = isCue;
		ball.LastSoundTime = 0f;

		return go;
	}

	protected override void OnUpdate()
	{
		// Aim direction: ray from camera through cursor → intersect ball-height plane,
		// then direction = aim point - cue ball.
		if ( _cam == null || CueBall == null || !CueBall.IsValid() ) return;

		var ray = _cam.ScreenPixelToRay( Mouse.Position );
		// Intersect plane z = BallSurfaceZ
		Vector3 aimPoint = CueBall.WorldPosition + Vector3.Forward * 30f;
		if ( MathF.Abs( ray.Forward.z ) > 0.0001f )
		{
			float t = (BallSurfaceZ - ray.Position.z) / ray.Forward.z;
			if ( t > 0 )
			{
				aimPoint = ray.Position + ray.Forward * t;
			}
		}
		var dir = (aimPoint - CueBall.WorldPosition).WithZ( 0 );
		if ( dir.Length > 0.01f ) AimDirection = dir.Normal;

		// Shooting input
		if ( State == GameState.Aiming )
		{
			if ( Input.Pressed( "Attack1" ) )
			{
				IsCharging = true;
				_mouseDownPos = Mouse.Position;
			}
			if ( IsCharging )
			{
				// Power = distance dragged on screen, normalized
				var dragPx = (Mouse.Position - _mouseDownPos).Length;
				PowerNormalized = Math.Clamp( dragPx / 250f, 0f, 1f );
			}
			if ( Input.Released( "Attack1" ) && IsCharging )
			{
				FireCue( PowerNormalized );
				IsCharging = false;
				PowerNormalized = 0f;
			}

			// AutoPlay: fire shots on a timer toward the rack
			if ( AutoPlay && !IsCharging )
			{
				if ( _autoNextShotTime < 0f ) _autoNextShotTime = Time.Now + AutoFirstShotDelay;
				if ( Time.Now >= _autoNextShotTime )
				{
					AimAtRackAndFire();
					_autoNextShotTime = Time.Now + 999f; // wait for shooting state to clear it
				}
			}
		}

		// Transition Shooting → Aiming when balls settle
		if ( State == GameState.Shooting )
		{
			if ( AllBallsSettled() )
			{
				State = GameState.Aiming;
				if ( Scratched ) { RespawnCueBall(); Scratched = false; }
				if ( AutoPlay ) _autoNextShotTime = Time.Now + AutoBetweenShotDelay;
			}
		}

		if ( State == GameState.Won )
		{
			_winTimer += Time.Delta;
		}

		if ( MessageUntil > 0f && Time.Now > MessageUntil ) { Message = ""; MessageUntil = 0f; }
	}

	private bool AllBallsSettled()
	{
		foreach ( var b in ObjectBalls.Concat( new[] { CueBall } ) )
		{
			if ( b == null || !b.IsValid() ) continue;
			var rb = b.Components.Get<Rigidbody>();
			if ( rb != null && rb.Velocity.Length > 4f ) return false;
		}
		return true;
	}

	private void AimAtRackAndFire()
	{
		if ( CueBall == null || !CueBall.IsValid() ) return;

		// Aim toward an object ball (rotate through them each shot for variety)
		Vector3 target;
		if ( ObjectBalls.Count == 0 ) return;
		var live = ObjectBalls.Where( b => b != null && b.IsValid() ).ToList();
		if ( live.Count == 0 ) return;
		var pick = live[_autoShotCount % live.Count];
		target = pick.WorldPosition;

		var dir = (target - CueBall.WorldPosition).WithZ( 0 );
		if ( dir.Length > 0.01f )
		{
			// Add small jitter so shots vary slightly
			float jitterDeg = (float)((_autoShotCount * 23) % 30 - 15);
			AimDirection = (Rotation.FromYaw( jitterDeg ) * dir.Normal).Normal;
		}

		// Power varies between 0.75 and 1.0
		float power = 0.85f + ((_autoShotCount * 0.13f) % 0.15f);
		_autoShotCount++;
		FireCue( power );
	}

	private void FireCue( float power01 )
	{
		if ( CueBall == null || !CueBall.IsValid() ) return;
		var rb = CueBall.Components.Get<Rigidbody>();
		if ( rb == null ) return;
		float speed = ImpulseMin + (ImpulseMax - ImpulseMin) * power01;
		rb.ApplyImpulse( AimDirection * speed * rb.Mass );
		PlaySound( CueStrikeSound, CueBall.WorldPosition );
		State = GameState.Shooting;
	}

	public void OnBallPocketed( PoolBall ball )
	{
		PlaySound( PocketSound, ball.WorldPosition );
		if ( ball.IsCueBall )
		{
			Scratched = true;
			ShowMessage( "Cue Ball Scratched!", 2.5f );
			// Park it in midair with motion disabled so it doesn't fall forever
			var rb = ball.Components.Get<Rigidbody>();
			if ( rb != null )
			{
				rb.Velocity = Vector3.Zero;
				rb.AngularVelocity = Vector3.Zero;
				rb.MotionEnabled = false;
			}
			ball.WorldPosition = new Vector3( 0, 0, -50 );
		}
		else
		{
			ObjectBalls.Remove( ball.GameObject );
			ball.GameObject.Destroy();
			if ( ObjectBalls.Count == 0 && State != GameState.Won )
			{
				State = GameState.Won;
				ShowMessage( "Game Won!", 10f );
				PlaySound( WinSound, CueBall?.WorldPosition ?? Vector3.Zero );
			}
		}
	}

	private void RespawnCueBall()
	{
		if ( CueBall == null || !CueBall.IsValid() ) return;
		CueBall.WorldPosition = new Vector3( -TableLength * 0.25f, 0, BallSurfaceZ );
		var rb = CueBall.Components.Get<Rigidbody>();
		if ( rb != null )
		{
			rb.MotionEnabled = true;
			rb.Velocity = Vector3.Zero;
			rb.AngularVelocity = Vector3.Zero;
		}
	}

	public void ShowMessage( string msg, float seconds )
	{
		Message = msg;
		MessageUntil = Time.Now + seconds;
	}

	public void PlaySound( SoundEvent ev, Vector3 pos )
	{
		if ( ev == null ) return;
		try { Sound.Play( ev, pos ); } catch { }
	}

	public int BallsRemaining => ObjectBalls.Count;
}
