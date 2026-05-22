using Sandbox;
using System;
using System.Collections.Generic;

namespace Local.ToyBox;

/// <summary>
/// Local equivalent of sandbox/Code/GameLoop/GameManager.Water.cs.
/// Box-volume "water" — applies buoyancy to any Rigidbody whose center is below
/// the surface plane (extents.z above the volume center). Slows CharacterControllers
/// while inside (horizontal damping). On enter/exit, raises swim flag on the
/// player so the rest of the game can react.
/// </summary>
[Icon( "water" )]
public sealed class WaterVolume : Component, Component.ITriggerListener
{
	[Property] public Vector3 Half { get; set; } = new Vector3( 100, 100, 40 );
	[Property] public float BuoyancyStrength { get; set; } = 1200f;
	[Property] public float SwimSpeedMult { get; set; } = 0.45f;

	private readonly HashSet<Rigidbody> _bodies = new();

	public bool IsPointInside( Vector3 p )
	{
		var d = p - WorldPosition;
		return MathF.Abs( d.x ) <= Half.x * WorldScale.x
			&& MathF.Abs( d.y ) <= Half.y * WorldScale.y
			&& MathF.Abs( d.z ) <= Half.z * WorldScale.z;
	}

	public float WaterSurfaceZ => WorldPosition.z + Half.z * WorldScale.z;

	void ITriggerListener.OnTriggerEnter( Collider other )
	{
		var rb = other.Rigidbody;
		if ( rb.IsValid() ) _bodies.Add( rb );
	}

	void ITriggerListener.OnTriggerExit( Collider other )
	{
		var rb = other.Rigidbody;
		if ( rb.IsValid() ) _bodies.Remove( rb );
	}

	protected override void OnFixedUpdate()
	{
		var surfaceZ = WaterSurfaceZ;

		// Buoyancy + damping for tracked rigidbodies
		var removeList = new List<Rigidbody>();
		foreach ( var rb in _bodies )
		{
			if ( !rb.IsValid() ) { removeList.Add( rb ); continue; }
			var p = rb.WorldPosition;
			var depth = surfaceZ - p.z;
			if ( depth <= 0f ) continue;

			// Scale buoyancy by depth (clamped), so things bob near surface.
			var t = MathF.Min( depth / (Half.z * 2f), 1f );
			rb.ApplyForce( Vector3.Up * BuoyancyStrength * t * Time.Delta * 60f );

			// Damping
			rb.Velocity *= (1f - MathF.Min( 0.5f, 2.5f * Time.Delta ));
			rb.AngularVelocity *= (1f - MathF.Min( 0.5f, 2.5f * Time.Delta ));
		}
		foreach ( var rb in removeList ) _bodies.Remove( rb );
	}

	protected override void DrawGizmos()
	{
		Gizmo.Draw.Color = new Color( 0.2f, 0.5f, 0.9f, 0.4f );
		Gizmo.Draw.LineBBox( new BBox( -Half, Half ) );
	}
}
