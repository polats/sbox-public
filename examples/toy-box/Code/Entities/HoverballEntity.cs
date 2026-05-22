using Sandbox;
using System;

namespace Local.ToyBox;

/// <summary>
/// Local equivalent of sandbox/Code/Weapons/ToolGun/Modes/Hoverball/HoverballEntity.cs.
/// The canonical version drives velocity directly (target velZ clamped from
/// distance, lerped toward by stiffness * (AirResistance+1)). We mirror that
/// algorithm — it's a velocity-controller, not a force PID. Pull the same
/// numbers as upstream.
/// </summary>
[Icon( "bubble_chart" )]
public sealed class HoverballEntity : Component
{
	[Property] public float TargetZ { get; set; }
	[Property] public float AirResistance { get; set; } = 1.5f;
	[Property] public bool LockToStartZ { get; set; } = true;

	protected override void OnStart()
	{
		if ( LockToStartZ ) TargetZ = WorldPosition.z;
		var rb = GetComponent<Rigidbody>();
		if ( rb.IsValid() ) rb.Gravity = false;
	}

	protected override void OnFixedUpdate()
	{
		var rb = GetComponent<Rigidbody>();
		if ( !rb.IsValid() ) return;

		var pos = WorldPosition;
		var vel = rb.Velocity;
		var distance = TargetZ - pos.z;

		// Drive Z velocity toward target proportional to distance.
		var targetVelZ = Math.Clamp( distance * 20f, -400f, 400f );
		var lerp = MathF.Min( Time.Delta * 15f * (AirResistance + 1f), 1f );
		var newVelZ = vel.z + (targetVelZ - vel.z) * lerp;
		var newVel = vel.WithZ( newVelZ );

		// Horizontal drag
		if ( AirResistance > 0f )
		{
			var drag = MathF.Min( AirResistance * Time.Delta * 5f, 1f );
			newVel = newVel.WithX( vel.x * (1f - drag) ).WithY( vel.y * (1f - drag) );
		}

		rb.Velocity = newVel;
	}

	protected override void DrawGizmos()
	{
		var local = new Vector3( 0, 0, TargetZ - WorldPosition.z );
		Gizmo.Draw.Color = new Color( 0.9f, 0.6f, 0.1f, 1 );
		Gizmo.Draw.Line( Vector3.Zero, local );
		Gizmo.Draw.LineSphere( local, 5 );
	}
}
