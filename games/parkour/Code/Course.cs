using Sandbox;
using System;
using System.Collections.Generic;

namespace Local.Parkour;

/// <summary>
/// Builds the parkour course in code on OnStart: zigzagging platforms with
/// gaps, narrow walkways, ramps, and a goal trigger at the end.
/// 1 unit = 1 inch. Citizen is 69" tall.
/// </summary>
public sealed class Course : Component
{
	public Vector3 StartSpawn { get; private set; } = new Vector3( 0, 0, 60 );
	public Vector3 GoalPosition { get; private set; }

	protected override void OnStart()
	{
		base.OnStart();
		Build();
	}

	private void Build()
	{
		var brown = new Color( 0.45f, 0.32f, 0.20f );
		var stone = new Color( 0.55f, 0.55f, 0.60f );
		var wood  = new Color( 0.60f, 0.40f, 0.22f );
		var redzone = new Color( 0.85f, 0.25f, 0.20f );
		var greenzone = new Color( 0.20f, 0.75f, 0.30f );
		var rampCol = new Color( 0.70f, 0.55f, 0.30f );

		// START PLATFORM (wide)
		AddPlatform( "Start", new Vector3( 0, 0, 0 ), new Vector3( 240, 240, 20 ), redzone );

		// Narrow walkway forward (+X) — small gap from start
		AddPlatform( "Walk1", new Vector3( 240, 0, 0 ), new Vector3( 160, 80, 20 ), brown );

		// Gap, then a small platform requiring a jump
		AddPlatform( "Jump1", new Vector3( 440, 0, 0 ), new Vector3( 100, 100, 20 ), stone );

		// Bigger gap to another stepping platform (offset Y to add curve)
		AddPlatform( "Jump2", new Vector3( 620, -30, 0 ), new Vector3( 100, 100, 20 ), stone );

		// Slightly elevated platform via a ramp
		AddPlatform( "Mid", new Vector3( 820, -60, 30 ), new Vector3( 180, 140, 20 ), brown );

		// Zigzag: turn to +Y (left)
		AddPlatform( "Zig1", new Vector3( 820, 120, 30 ), new Vector3( 100, 100, 20 ), wood );
		AddPlatform( "Zig2", new Vector3( 820, 280, 30 ), new Vector3( 100, 100, 20 ), wood );

		// A higher jump platform
		AddPlatform( "High", new Vector3( 820, 440, 50 ), new Vector3( 100, 100, 20 ), stone );

		// Down to final stretch (+X direction)
		AddPlatform( "Final1", new Vector3( 1020, 440, 30 ), new Vector3( 140, 100, 20 ), brown );

		// Gap to final platform
		AddPlatform( "Final2", new Vector3( 1220, 440, 30 ), new Vector3( 120, 120, 20 ), brown );

		// GOAL platform (wide)
		var goalPos = new Vector3( 1420, 440, 30 );
		AddPlatform( "Goal", goalPos, new Vector3( 240, 240, 20 ), greenzone );
		GoalPosition = goalPos + new Vector3( 0, 0, 30 );

		// Goal trigger
		var goalGo = Scene.CreateObject();
		goalGo.Name = "GoalTrigger";
		goalGo.Parent = GameObject;
		goalGo.WorldPosition = GoalPosition;
		var col = goalGo.Components.Create<BoxCollider>();
		col.IsTrigger = true;
		col.Scale = new Vector3( 180, 180, 100 );
		goalGo.Components.Create<GoalTrigger>();

		// A bright marker pole at the goal (tall thin box)
		AddPlatform( "GoalMarker", goalPos + new Vector3( 0, 0, 120 ), new Vector3( 12, 12, 200 ), greenzone );

		// Ground floor far below as visual safety net
		AddPlatform( "Ground", new Vector3( 700, 200, -400 ), new Vector3( 3000, 3000, 20 ), new Color( 0.08f, 0.10f, 0.14f ) );
	}

	private void AddPlatform( string name, Vector3 pos, Vector3 size, Color tint )
	{
		var go = Scene.CreateObject();
		go.Name = "Platform_" + name;
		go.Parent = GameObject;
		go.WorldPosition = pos;
		go.WorldScale = size / 50f; // box.vmdl is 50"
		var mr = go.Components.Create<ModelRenderer>();
		mr.Model = Model.Load( "models/dev/box.vmdl" );
		mr.Tint = tint;
		var col = go.Components.Create<BoxCollider>();
		col.Scale = new Vector3( 50f, 50f, 50f );
		col.Center = Vector3.Zero;
		col.Friction = 0.6f;
	}

	private void AddRamp( string name, Vector3 pos, Vector3 size, float pitchDeg, Color tint )
	{
		var go = Scene.CreateObject();
		go.Name = "Ramp_" + name;
		go.Parent = GameObject;
		go.WorldPosition = pos;
		go.WorldRotation = Rotation.From( pitchDeg, 0, 0 );
		go.WorldScale = size / 50f;
		var mr = go.Components.Create<ModelRenderer>();
		mr.Model = Model.Load( "models/dev/box.vmdl" );
		mr.Tint = tint;
		var col = go.Components.Create<BoxCollider>();
		col.Scale = new Vector3( 50f, 50f, 50f );
		col.Center = Vector3.Zero;
		col.Friction = 0.6f;
	}
}
