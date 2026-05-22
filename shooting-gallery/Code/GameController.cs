using Sandbox;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Local.ShootingGallery;

/// <summary>
/// Builds the shooting range room + targets in code at OnStart, owns
/// the score/timer state for the HUD.
/// 1 unit = 1 inch.
/// </summary>
public sealed class GameController : Component
{
	public const float RoomWidth = 800f;
	public const float RoomDepth = 1200f;
	public const float RoomHeight = 280f;
	public const float BackWallX = 900f;
	public const float PlayerX = -200f;
	public const float PlayerZ = 64f;

	[Property] public float GameDuration { get; set; } = 60f;
	[Property] public SoundEvent GunshotSound { get; set; }
	[Property] public SoundEvent ImpactSound { get; set; }
	[Property] public SoundEvent BoardHitSound { get; set; }
	[Property] public SoundEvent DummyHitSound { get; set; }
	[Property] public SoundEvent GameOverSound { get; set; }

	public int Score { get; private set; }
	public int Hits { get; private set; }
	public int Shots { get; private set; }
	public float TimeRemaining { get; private set; }
	public bool GameOver { get; private set; }

	public List<Target> Targets { get; } = new();

	protected override void OnStart()
	{
		base.OnStart();

		GunshotSound ??= ResourceLibrary.Get<SoundEvent>( "sounds/effects/explosion/explosion_small.sound" );
		ImpactSound ??= ResourceLibrary.Get<SoundEvent>( "sounds/impacts/bullets/impact-bullet-metal.sound" );
		BoardHitSound ??= ResourceLibrary.Get<SoundEvent>( "sounds/impacts/bullets/impact-bullet-wood.sound" );
		DummyHitSound ??= ResourceLibrary.Get<SoundEvent>( "sounds/impacts/bullets/impact-bullet-flesh.sound" );
		GameOverSound ??= ResourceLibrary.Get<SoundEvent>( "sounds/kenney/ui/ui.favourite.sound" );

		TimeRemaining = GameDuration;

		BuildRoom();
		BuildTargets();
	}

	private void BuildRoom()
	{
		// Floor
		AddBox( "Floor", new Vector3( 350, 0, -5 ), new Vector3( 1400, RoomWidth, 10 ), new Color( 0.35f, 0.30f, 0.25f ) );
		// Back wall
		AddBox( "BackWall", new Vector3( BackWallX + 10, 0, RoomHeight * 0.5f ), new Vector3( 20, RoomWidth, RoomHeight ), new Color( 0.45f, 0.38f, 0.30f ) );
		// Side walls
		AddBox( "WallLeft", new Vector3( 350, RoomWidth * 0.5f + 10, RoomHeight * 0.5f ), new Vector3( 1400, 20, RoomHeight ), new Color( 0.40f, 0.34f, 0.28f ) );
		AddBox( "WallRight", new Vector3( 350, -RoomWidth * 0.5f - 10, RoomHeight * 0.5f ), new Vector3( 1400, 20, RoomHeight ), new Color( 0.40f, 0.34f, 0.28f ) );
		// Back-player wall
		AddBox( "WallBehind", new Vector3( -350, 0, RoomHeight * 0.5f ), new Vector3( 20, RoomWidth, RoomHeight ), new Color( 0.40f, 0.34f, 0.28f ) );
		// Ceiling
		AddBox( "Ceiling", new Vector3( 350, 0, RoomHeight + 5 ), new Vector3( 1400, RoomWidth, 10 ), new Color( 0.50f, 0.45f, 0.40f ) );

		// Counter in front of player (a thin desk so we look over it)
		AddBox( "Counter", new Vector3( -120, 0, 36 ), new Vector3( 80, 320, 4 ), new Color( 0.55f, 0.40f, 0.25f ) );
		AddBox( "CounterFront", new Vector3( -160, 0, 18 ), new Vector3( 4, 320, 36 ), new Color( 0.40f, 0.28f, 0.18f ) );

		// Shelf back wall — visual stripe at the targets line
		AddBox( "Shelf", new Vector3( 600, 0, 50 ), new Vector3( 240, RoomWidth - 60, 6 ), new Color( 0.45f, 0.35f, 0.28f ) );
	}

	private GameObject AddBox( string name, Vector3 pos, Vector3 size, Color tint )
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
		return go;
	}

	private void BuildTargets()
	{
		// Row of cans on the front shelf at z ~= 56
		float shelfTopZ = 53f + 4.3f; // sodacan half-height
		for ( int i = 0; i < 5; i++ )
		{
			float y = -200f + i * 100f;
			SpawnCan( new Vector3( 600f, y, shelfTopZ ) );
		}

		// Boards in the back at varied heights
		SpawnBoard( new Vector3( 850f, -260f, 90f ) );
		SpawnBoard( new Vector3( 850f, -100f, 160f ) );
		SpawnBoard( new Vector3( 850f, 100f, 110f ) );
		SpawnBoard( new Vector3( 850f, 260f, 180f ) );

		// Citizen dummies as a centerpiece — one each side of the room
		SpawnDummy( new Vector3( 720f, -340f, 0f ) );
		SpawnDummy( new Vector3( 720f, 340f, 0f ) );
		SpawnDummy( new Vector3( 800f, 0f, 0f ) );
	}

	private void SpawnCan( Vector3 pos )
	{
		var go = Scene.CreateObject();
		go.Name = "Can";
		go.Parent = GameObject;
		go.WorldPosition = pos;

		var mr = go.Components.Create<ModelRenderer>();
		mr.Model = Model.Load( "models/citizen_props/sodacan01.vmdl" );

		var col = go.Components.Create<BoxCollider>();
		col.Scale = new Vector3( 5.1f, 5.3f, 8.5f );
		col.Center = new Vector3( 0, 0, 4.25f );
		col.Friction = 0.4f;
		col.Elasticity = 0.3f;

		var rb = go.Components.Create<Rigidbody>();
		rb.MassOverride = 0.35f;
		rb.LinearDamping = 0.3f;
		rb.AngularDamping = 0.3f;
		rb.Gravity = true;

		var t = go.Components.Create<Target>();
		t.Kind = TargetKind.Can;
		t.Score = 10;
		t.RespawnDelay = 3f;
		t.Controller = this;
		Targets.Add( t );
	}

	private void SpawnBoard( Vector3 pos )
	{
		// Stand
		var stand = Scene.CreateObject();
		stand.Name = "BoardStand";
		stand.Parent = GameObject;
		stand.WorldPosition = pos.WithZ( pos.z * 0.5f );
		stand.WorldScale = new Vector3( 4f, 4f, pos.z ) / 50f;
		var standMr = stand.Components.Create<ModelRenderer>();
		standMr.Model = Model.Load( "models/dev/box.vmdl" );
		standMr.Tint = new Color( 0.30f, 0.22f, 0.15f );

		// Board (the target)
		var go = Scene.CreateObject();
		go.Name = "Board";
		go.Parent = GameObject;
		go.WorldPosition = pos.WithZ( pos.z + 18 );
		go.WorldScale = new Vector3( 4f, 30f, 36f ) / 50f;
		var mr = go.Components.Create<ModelRenderer>();
		mr.Model = Model.Load( "models/dev/box.vmdl" );
		mr.Tint = new Color( 0.9f, 0.25f, 0.20f );
		var col = go.Components.Create<BoxCollider>();
		col.Scale = new Vector3( 50f, 50f, 50f );

		var t = go.Components.Create<Target>();
		t.Kind = TargetKind.Board;
		t.Score = 25;
		t.RespawnDelay = 2.5f;
		t.Controller = this;
		Targets.Add( t );
	}

	private void SpawnDummy( Vector3 pos )
	{
		var go = Scene.CreateObject();
		go.Name = "Dummy";
		go.Parent = GameObject;
		go.WorldPosition = pos;
		// Face the player (-X side)
		go.WorldRotation = Rotation.FromYaw( 180f );

		var smr = go.Components.Create<SkinnedModelRenderer>();
		smr.Model = Model.Load( "models/citizen/citizen.vmdl" );
		smr.UseAnimGraph = true;
		smr.Tint = new Color( 0.8f, 0.7f, 0.6f );

		// Big collider covering the citizen (~69" tall)
		var col = go.Components.Create<BoxCollider>();
		col.Scale = new Vector3( 24f, 24f, 72f );
		col.Center = new Vector3( 0, 0, 36f );

		var t = go.Components.Create<Target>();
		t.Kind = TargetKind.Dummy;
		t.Score = 50;
		t.RespawnDelay = 5f;
		t.Controller = this;
		Targets.Add( t );
	}

	protected override void OnUpdate()
	{
		if ( GameOver ) return;

		TimeRemaining -= Time.Delta;
		if ( TimeRemaining <= 0f )
		{
			TimeRemaining = 0f;
			GameOver = true;
			Mouse.Visible = true;
			if ( GameOverSound != null ) Sound.Play( GameOverSound );
		}
	}

	public void OnShotFired()
	{
		Shots++;
		if ( GunshotSound != null ) Sound.Play( GunshotSound );
	}

	public void OnTargetHit( Target t, Vector3 pos )
	{
		Hits++;
		Score += t.Score;

		SoundEvent ev = t.Kind switch
		{
			TargetKind.Board => BoardHitSound,
			TargetKind.Dummy => DummyHitSound,
			_ => ImpactSound,
		};
		if ( ev != null ) Sound.Play( ev, pos );
	}

	public float Accuracy => Shots > 0 ? (float)Hits / Shots : 0f;
}
