using Sandbox;
using System.Collections.Generic;
using Local.FpsWeapons;

namespace Local.FpsWeapons;

/// <summary>
/// Builds the range, spawns dummies + grabbable crates.
/// </summary>
public sealed class GameController : Component
{
	public const float RoomWidth = 900f;
	public const float RoomDepth = 1500f;
	public const float RoomHeight = 320f;
	public const float BackWallX = 1100f;

	[Property] public SoundEvent PistolSound { get; set; }
	[Property] public SoundEvent ShotgunSound { get; set; }
	[Property] public SoundEvent PhysGunSound { get; set; }
	[Property] public SoundEvent ImpactSound { get; set; }
	[Property] public SoundEvent DummyHitSound { get; set; }

	public List<Target> Targets { get; } = new();
	public int Hits { get; private set; }
	public int Shots { get; private set; }

	protected override void OnStart()
	{
		base.OnStart();

		PistolSound ??= ResourceLibrary.Get<SoundEvent>( "sounds/effects/explosion/explosion_small.sound" );
		ShotgunSound ??= ResourceLibrary.Get<SoundEvent>( "sounds/effects/explosion/explosion_small.sound" );
		PhysGunSound ??= ResourceLibrary.Get<SoundEvent>( "sounds/kenney/ui/ui.favourite.sound" );
		ImpactSound ??= ResourceLibrary.Get<SoundEvent>( "sounds/impacts/bullets/impact-bullet-metal.sound" );
		DummyHitSound ??= ResourceLibrary.Get<SoundEvent>( "sounds/impacts/bullets/impact-bullet-flesh.sound" );

		BuildRoom();
		BuildTargets();
		BuildPhysProps();
	}

	private void BuildRoom()
	{
		AddBox( "Floor", new Vector3( 350, 0, -5 ), new Vector3( 1700, RoomWidth, 10 ), new Color( 0.30f, 0.28f, 0.25f ) );
		AddBox( "BackWall", new Vector3( BackWallX + 10, 0, RoomHeight * 0.5f ), new Vector3( 20, RoomWidth, RoomHeight ), new Color( 0.42f, 0.36f, 0.30f ) );
		AddBox( "WallLeft", new Vector3( 350, RoomWidth * 0.5f + 10, RoomHeight * 0.5f ), new Vector3( 1700, 20, RoomHeight ), new Color( 0.38f, 0.32f, 0.28f ) );
		AddBox( "WallRight", new Vector3( 350, -RoomWidth * 0.5f - 10, RoomHeight * 0.5f ), new Vector3( 1700, 20, RoomHeight ), new Color( 0.38f, 0.32f, 0.28f ) );
		AddBox( "WallBehind", new Vector3( -450, 0, RoomHeight * 0.5f ), new Vector3( 20, RoomWidth, RoomHeight ), new Color( 0.38f, 0.32f, 0.28f ) );
		AddBox( "Ceiling", new Vector3( 350, 0, RoomHeight + 5 ), new Vector3( 1700, RoomWidth, 10 ), new Color( 0.48f, 0.44f, 0.40f ) );

		// Range divider stripes on the floor
		for ( int i = 0; i < 4; i++ )
		{
			float x = 200f + i * 200f;
			AddBox( $"Stripe_{i}", new Vector3( x, 0, 0.5f ), new Vector3( 4, RoomWidth - 40, 1 ), new Color( 0.9f, 0.85f, 0.20f ) );
		}
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
		// Citizen dummies across the back
		float[] ys = { -300f, -150f, 0f, 150f, 300f };
		for ( int i = 0; i < ys.Length; i++ )
		{
			SpawnDummy( new Vector3( 900f + (i % 2) * 30f, ys[i], 0f ) );
		}
	}

	private void SpawnDummy( Vector3 pos )
	{
		var go = Scene.CreateObject();
		go.Name = "Dummy";
		go.Parent = GameObject;
		go.WorldPosition = pos;
		go.WorldRotation = Rotation.FromYaw( 180f );

		var smr = go.Components.Create<SkinnedModelRenderer>();
		smr.Model = Model.Load( "models/citizen/citizen.vmdl" );
		smr.UseAnimGraph = true;
		smr.Tint = new Color( 0.85f, 0.75f, 0.65f );

		var col = go.Components.Create<BoxCollider>();
		col.Scale = new Vector3( 24f, 24f, 72f );
		col.Center = new Vector3( 0, 0, 36f );

		var rb = go.Components.Create<Rigidbody>();
		rb.MassOverride = 60f;
		rb.LinearDamping = 1.5f;
		rb.AngularDamping = 1.5f;
		rb.Gravity = true;
		rb.MotionEnabled = false; // pinned upright initially; PhysGun will re-enable

		var t = go.Components.Create<Target>();
		t.Controller = this;
		Targets.Add( t );
	}

	private void BuildPhysProps()
	{
		// A few free-floating crates and barrels around the range for PhysGun fun
		SpawnPhysCrate( new Vector3( 400, -150, 12 ), new Vector3( 24, 24, 24 ), new Color( 0.6f, 0.45f, 0.25f ) );
		SpawnPhysCrate( new Vector3( 450, 150, 12 ), new Vector3( 24, 24, 24 ), new Color( 0.6f, 0.45f, 0.25f ) );
		SpawnPhysCrate( new Vector3( 600, -50, 12 ), new Vector3( 30, 20, 20 ), new Color( 0.5f, 0.4f, 0.25f ) );
		SpawnPhysCrate( new Vector3( 600, 50, 12 ), new Vector3( 20, 28, 20 ), new Color( 0.55f, 0.42f, 0.22f ) );
		SpawnPhysCrate( new Vector3( 700, 0, 16 ), new Vector3( 28, 28, 30 ), new Color( 0.45f, 0.35f, 0.20f ) );
	}

	private void SpawnPhysCrate( Vector3 pos, Vector3 size, Color tint )
	{
		var go = Scene.CreateObject();
		go.Name = "PhysCrate";
		go.Parent = GameObject;
		go.WorldPosition = pos;
		go.WorldScale = size / 50f;
		var mr = go.Components.Create<ModelRenderer>();
		mr.Model = Model.Load( "models/dev/box.vmdl" );
		mr.Tint = tint;
		var col = go.Components.Create<BoxCollider>();
		col.Scale = new Vector3( 50f, 50f, 50f );
		col.Friction = 0.5f;
		col.Elasticity = 0.2f;
		var rb = go.Components.Create<Rigidbody>();
		rb.MassOverride = 8f;
		rb.LinearDamping = 0.5f;
		rb.AngularDamping = 0.5f;
		rb.Gravity = true;
	}

	public void OnShotFired( BaseWeapon weapon )
	{
		Shots++;
		SoundEvent ev = weapon switch
		{
			ShotgunWeapon => ShotgunSound,
			PhysGunWeapon => PhysGunSound,
			_ => PistolSound
		};
		if ( ev != null ) Sound.Play( ev );
	}

	public void OnTargetHit( Target t, Vector3 pos )
	{
		Hits++;
		if ( DummyHitSound != null ) Sound.Play( DummyHitSound, pos );
	}

	public void OnTargetGrabbed( Target t )
	{
		if ( PhysGunSound != null ) Sound.Play( PhysGunSound, t.GameObject.WorldPosition );
	}
}
