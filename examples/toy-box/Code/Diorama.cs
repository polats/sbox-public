using Sandbox;
using System;
using System.Collections.Generic;

namespace Local.ToyBox;

/// <summary>
/// Builds the toy-box diorama at OnStart: floor, water pool, balloons,
/// hoverball, hydraulic piston, thruster cube, mover platform, push trigger,
/// teleport pads. Holds references the Player and HUD can read.
///
/// Coordinates: 1 unit = 1 inch. Citizen ~69" tall.
/// X+ = forward through the course.
/// </summary>
public sealed class Diorama : Component
{
	public Vector3 SpawnPoint => new Vector3( -300, 0, 60 );
	public Vector3 PushTriggerCenter => new Vector3( -100, 0, 60 );
	public Vector3 RidePlatformLow => new Vector3( 250, 0, 30 );
	public Vector3 PoolCenter => new Vector3( 650, 0, 20 );
	public Vector3 TeleportEntryPad => new Vector3( 900, 0, 12 );
	public Vector3 TeleportExitPad  => new Vector3( -50, 350, 60 );
	public Vector3 BalloonArea     => new Vector3( 0, 350, 200 );

	public List<Vector3> AutoPath { get; } = new();

	public WaterVolume Water { get; private set; }

	protected override void OnStart()
	{
		Build();

		// Citizen autopath: spawn -> push -> mover -> water -> teleport -> balloons
		AutoPath.Clear();
		AutoPath.Add( SpawnPoint );                                      // start
		AutoPath.Add( new Vector3( -150, 0, 60 ) );                      // mid-runway
		AutoPath.Add( PushTriggerCenter );                               // walk into push
		AutoPath.Add( new Vector3( 100, 0, 60 ) );                       // landing after push
		AutoPath.Add( new Vector3( 190, 0, 60 ) );                       // step onto mover
		AutoPath.Add( new Vector3( 350, 0, 60 ) );                       // step off mover end
		AutoPath.Add( new Vector3( 500, 0, 60 ) );                       // walkway to pool edge
		AutoPath.Add( PoolCenter + new Vector3( 0, 0, 40 ) );            // drop into pool
		AutoPath.Add( new Vector3( 800, 0, 60 ) );                       // climb out, walkway to teleport
		AutoPath.Add( TeleportEntryPad + new Vector3( 0, 0, 30 ) );      // step on teleport
		AutoPath.Add( TeleportExitPad + new Vector3( 60, 0, 30 ) );      // emerge near balloons
		AutoPath.Add( TeleportExitPad + new Vector3( 100, 80, 30 ) );    // wander into balloons
	}

	private void Build()
	{
		// Floor (large)
		AddBox( "Floor", new Vector3( 200, 0, 0 ), new Vector3( 1600, 800, 20 ), new Color( 0.25f, 0.27f, 0.32f ) );

		// Spawn pad
		AddBox( "SpawnPad", new Vector3( -300, 0, 30 ), new Vector3( 160, 160, 20 ), new Color( 0.45f, 0.6f, 0.4f ) );

		// Walk runway from spawn into the push trigger
		AddBox( "Runway", new Vector3( -150, 0, 25 ), new Vector3( 200, 80, 10 ), new Color( 0.35f, 0.35f, 0.38f ) );

		// Walkway after the push, leading toward the mover (so the +X push lands here)
		AddBox( "WalkwayA", new Vector3( 60, 0, 25 ), new Vector3( 200, 80, 10 ), new Color( 0.35f, 0.35f, 0.38f ) );
		// Walkway between mover end and the pool
		AddBox( "WalkwayB", new Vector3( 500, 0, 25 ), new Vector3( 100, 80, 10 ), new Color( 0.35f, 0.35f, 0.38f ) );
		// Walkway after pool to teleport pad
		AddBox( "WalkwayC", new Vector3( 800, 0, 25 ), new Vector3( 100, 80, 10 ), new Color( 0.35f, 0.35f, 0.38f ) );

		// TriggerPush volume — invisible, sits across the runway, pushes +X
		AddPush( "PushTrigger", PushTriggerCenter, new Vector3( 60, 60, 50 ), Vector3.Forward, 350f );

		// FuncMover platform — slides side-to-side at +X end of runway, citizen rides it
		AddMover( "MoverPlatform", RidePlatformLow, new Vector3( 0, 0, 0 ), new Color( 0.55f, 0.35f, 0.2f ) );

		// Pool: water volume + visible blue cube + surrounding ledges
		AddPool( "Pool", PoolCenter );

		// Teleport entry pad (red) and exit pad (blue)
		AddTeleportPair(
			entryPos: TeleportEntryPad,
			exitPos: TeleportExitPad
		);

		// Balloons — three colored, in BalloonArea
		AddBalloon( "BalloonRed",   BalloonArea + new Vector3( -60, -40, 0 ), new Color( 0.9f, 0.2f, 0.2f ) );
		AddBalloon( "BalloonGreen", BalloonArea + new Vector3(   0,  20, 0 ), new Color( 0.2f, 0.9f, 0.3f ) );
		AddBalloon( "BalloonBlue",  BalloonArea + new Vector3(  60, -20, 0 ), new Color( 0.25f, 0.4f, 0.95f ) );

		// Hoverball — hovers above the start area, visible from camera
		AddHoverball( "Hoverball", new Vector3( 100, 250, 160 ) );

		// Hydraulic piston — vertical, near the balloon zone for the finale shot
		AddHydraulic( "Piston", new Vector3( 100, 350, 30 ) );

		// Thruster cube — small physics box near the end of the path, fires up periodically
		AddThruster( "ThrusterCube", new Vector3( -100, 250, 40 ) );
	}

	// ---------- builders ----------

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
		col.Friction = 0.6f;
		return go;
	}

	private void AddPush( string name, Vector3 pos, Vector3 half, Vector3 dir, float power )
	{
		var go = Scene.CreateObject();
		go.Name = name;
		go.Parent = GameObject;
		go.WorldPosition = pos;
		var col = go.Components.Create<BoxCollider>();
		col.IsTrigger = true;
		col.Scale = half * 2f;
		col.Center = Vector3.Zero;
		var push = go.Components.Create<TriggerPush>();
		push.Direction = dir;
		push.Power = power;
		push.Half = half;

		// Visual indicator: thin colored slab on the floor
		var marker = AddBox( name + "_Mark", pos.WithZ( pos.z - 25 ), new Vector3( half.x * 2, half.y * 2, 4 ), new Color( 0.2f, 0.8f, 0.9f ) );
		marker.Components.Get<BoxCollider>().Destroy();
	}

	private void AddMover( string name, Vector3 pos, Vector3 _unused, Color tint )
	{
		var go = Scene.CreateObject();
		go.Name = name;
		go.Parent = GameObject;
		go.WorldPosition = pos;
		go.WorldScale = new Vector3( 120, 120, 12 ) / 50f;
		var mr = go.Components.Create<ModelRenderer>();
		mr.Model = Model.Load( "models/dev/box.vmdl" );
		mr.Tint = tint;
		var col = go.Components.Create<BoxCollider>();
		col.Scale = new Vector3( 50f, 50f, 50f );
		col.Friction = 1.5f;
		var mover = go.Components.Create<FuncMover>();
		// Slide along +X — the citizen's travel direction — so it carries him
		// forward toward the pool while he stands on top.
		mover.LinearDistance = new Vector3( 200, 0, 0 );
		mover.PeriodSeconds = 6f;
		mover.RiderHalfExtents = new Vector3( 80, 80, 50 );
	}

	private void AddPool( string name, Vector3 pos )
	{
		// Ledges around the pool so the citizen has to drop in
		var ledgeCol = new Color( 0.45f, 0.4f, 0.35f );
		AddBox( name + "_LedgeN", pos + new Vector3( 0, 90, 30 ),  new Vector3( 180, 20, 30 ), ledgeCol );
		AddBox( name + "_LedgeS", pos + new Vector3( 0, -90, 30 ), new Vector3( 180, 20, 30 ), ledgeCol );
		AddBox( name + "_LedgeE", pos + new Vector3( 90, 0, 30 ),  new Vector3( 20, 200, 30 ), ledgeCol );
		AddBox( name + "_LedgeW", pos + new Vector3( -90, 0, 30 ), new Vector3( 20, 200, 30 ), ledgeCol );

		// Pool floor (slightly recessed)
		AddBox( name + "_Bottom", pos + new Vector3( 0, 0, -20 ), new Vector3( 180, 200, 10 ), new Color( 0.15f, 0.2f, 0.3f ) );

		// Water visual (translucent-ish blue cube) + WaterVolume trigger
		var water = Scene.CreateObject();
		water.Name = name + "_Water";
		water.Parent = GameObject;
		water.WorldPosition = pos + new Vector3( 0, 0, 15 );
		var halfWorld = new Vector3( 88, 95, 25 );
		// Visual
		var visual = Scene.CreateObject();
		visual.Name = name + "_WaterVisual";
		visual.Parent = water;
		visual.LocalPosition = Vector3.Zero;
		visual.WorldScale = (halfWorld * 2f) / 50f;
		var mr = visual.Components.Create<ModelRenderer>();
		mr.Model = Model.Load( "models/dev/box.vmdl" );
		mr.Tint = new Color( 0.2f, 0.5f, 0.95f, 0.55f );
		// Trigger collider + WaterVolume
		var col = water.Components.Create<BoxCollider>();
		col.IsTrigger = true;
		col.Scale = halfWorld * 2f;
		col.Center = Vector3.Zero;
		var wv = water.Components.Create<WaterVolume>();
		wv.Half = halfWorld;
		Water = wv;
	}

	private void AddTeleportPair( Vector3 entryPos, Vector3 exitPos )
	{
		// Entry pad (red)
		var entry = Scene.CreateObject();
		entry.Name = "TeleportEntry";
		entry.Parent = GameObject;
		entry.WorldPosition = entryPos;

		var entryVis = AddBox( "TeleportEntry_Mark", entryPos.WithZ( entryPos.z + 1 ), new Vector3( 120, 120, 6 ), new Color( 0.95f, 0.15f, 0.15f ) );
		entryVis.Components.Get<BoxCollider>().Destroy();

		var col = entry.Components.Create<BoxCollider>();
		col.IsTrigger = true;
		col.Scale = new Vector3( 100, 100, 80 );
		col.Center = new Vector3( 0, 0, 40 );

		// Exit (blue pad)
		var exitGo = Scene.CreateObject();
		exitGo.Name = "TeleportExit";
		exitGo.Parent = GameObject;
		exitGo.WorldPosition = exitPos;
		var exitVis = AddBox( "TeleportExit_Mark", exitPos.WithZ( exitPos.z + 1 ), new Vector3( 120, 120, 6 ), new Color( 0.15f, 0.4f, 0.95f ) );
		exitVis.Components.Get<BoxCollider>().Destroy();

		var tt = entry.Components.Create<TriggerTeleport>();
		tt.Target = exitGo;
		tt.Half = new Vector3( 50, 50, 40 );
	}

	private void AddBalloon( string name, Vector3 pos, Color tint )
	{
		var go = Scene.CreateObject();
		go.Name = name;
		go.Parent = GameObject;
		go.WorldPosition = pos;
		go.WorldScale = new Vector3( 22, 22, 28 ) / 50f;
		var mr = go.Components.Create<ModelRenderer>();
		mr.Model = Model.Load( "models/dev/sphere.vmdl" );
		mr.Tint = tint;
		var rb = go.Components.Create<Rigidbody>();
		rb.Gravity = false;
		rb.LinearDamping = 1.5f;
		rb.AngularDamping = 2f;
		var col = go.Components.Create<SphereCollider>();
		col.Radius = 25f;
		go.Components.Create<BalloonEntity>();
	}

	private void AddHoverball( string name, Vector3 pos )
	{
		var go = Scene.CreateObject();
		go.Name = name;
		go.Parent = GameObject;
		go.WorldPosition = pos;
		go.WorldScale = new Vector3( 30, 30, 30 ) / 50f;
		var mr = go.Components.Create<ModelRenderer>();
		mr.Model = Model.Load( "models/dev/sphere.vmdl" );
		mr.Tint = new Color( 0.95f, 0.85f, 0.2f );
		var rb = go.Components.Create<Rigidbody>();
		rb.Gravity = false;
		var col = go.Components.Create<SphereCollider>();
		col.Radius = 25f;
		var hb = go.Components.Create<HoverballEntity>();
		hb.TargetZ = pos.z;
		hb.AirResistance = 1.8f;
	}

	private void AddHydraulic( string name, Vector3 pos )
	{
		// Base
		var baseGo = Scene.CreateObject();
		baseGo.Name = name + "_Base";
		baseGo.Parent = GameObject;
		baseGo.WorldPosition = pos;
		baseGo.WorldScale = new Vector3( 60, 60, 30 ) / 50f;
		var bmr = baseGo.Components.Create<ModelRenderer>();
		bmr.Model = Model.Load( "models/dev/box.vmdl" );
		bmr.Tint = new Color( 0.4f, 0.4f, 0.45f );
		var bcol = baseGo.Components.Create<BoxCollider>();
		bcol.Scale = new Vector3( 50, 50, 50 );

		// Pivot (no scale) — so child rod scales independently
		var pivot = Scene.CreateObject();
		pivot.Name = name;
		pivot.Parent = GameObject;
		pivot.WorldPosition = pos + new Vector3( 0, 0, 20 );

		// Rod (child)
		var rod = Scene.CreateObject();
		rod.Name = name + "_Rod";
		rod.Parent = pivot;
		rod.LocalPosition = new Vector3( 0, 0, 40 );
		rod.WorldScale = new Vector3( 20, 20, 70 ) / 50f;
		var rmr = rod.Components.Create<ModelRenderer>();
		rmr.Model = Model.Load( "models/dev/box.vmdl" );
		rmr.Tint = new Color( 0.85f, 0.7f, 0.2f );

		var hyd = pivot.Components.Create<HydraulicEntity>();
		hyd.Rod = rod;
		hyd.MinLength = 40;
		hyd.MaxLength = 110;
		hyd.AnimationSpeed = 1.0f;
		hyd.Axis = Vector3.Up;
	}

	private void AddThruster( string name, Vector3 pos )
	{
		var go = Scene.CreateObject();
		go.Name = name;
		go.Parent = GameObject;
		go.WorldPosition = pos;
		go.WorldScale = new Vector3( 40, 40, 40 ) / 50f;
		var mr = go.Components.Create<ModelRenderer>();
		mr.Model = Model.Load( "models/dev/box.vmdl" );
		mr.Tint = new Color( 0.7f, 0.3f, 0.35f );
		var rb = go.Components.Create<Rigidbody>();
		rb.Gravity = true;
		rb.LinearDamping = 0.5f;
		rb.AngularDamping = 0.5f;
		var col = go.Components.Create<BoxCollider>();
		col.Scale = new Vector3( 50, 50, 50 );

		// Flame visual (child)
		var flame = Scene.CreateObject();
		flame.Name = name + "_Flame";
		flame.Parent = go;
		flame.LocalPosition = new Vector3( 0, 0, -25 );
		flame.WorldScale = new Vector3( 16, 16, 28 ) / 50f;
		var fmr = flame.Components.Create<ModelRenderer>();
		fmr.Model = Model.Load( "models/dev/sphere.vmdl" );
		fmr.Tint = new Color( 1.0f, 0.6f, 0.1f );

		var t = go.Components.Create<ThrusterEntity>();
		t.Axis = Vector3.Up;
		t.Power = 2500f;
		t.OnDuration = 0.6f;
		t.OffDuration = 1.2f;
		t.FlameVisual = flame;
	}
}
