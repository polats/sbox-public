using Sandbox;
using System;
using System.Collections.Generic;

namespace Local.ToyBox;

/// <summary>
/// Local equivalent of sandbox/Code/Map/TriggerPush.cs. The canonical sandbox
/// type lives in a Type=game project and isn't reachable from external projects,
/// so we mirror it here.
///
/// Box volume that pushes anything with a Rigidbody or CharacterController in a
/// fixed world-space direction. CharacterController is NOT a Collider, so we
/// also poll every frame for nearby CharacterControllers (matches the parkour
/// pattern — see rules/animations.md).
/// </summary>
[Icon( "compare_arrows" )]
public sealed class TriggerPush : Component, Component.ITriggerListener
{
	[Property] public Vector3 Direction { get; set; } = Vector3.Forward;
	[Property] public float Power { get; set; } = 350f;
	[Property] public Vector3 Half { get; set; } = new Vector3( 60, 60, 60 );

	private readonly HashSet<GameObject> _bodies = new();

	void ITriggerListener.OnTriggerEnter( Collider other )
	{
		var root = other.GameObject.Root;
		if ( root is null ) return;
		_bodies.Add( root );
	}

	void ITriggerListener.OnTriggerExit( Collider other )
	{
		var root = other.GameObject.Root;
		if ( root is null ) return;
		_bodies.Remove( root );
	}

	protected override void OnFixedUpdate()
	{
		var dir = Direction.Normal;

		// Rigidbody-based pushes
		foreach ( var go in _bodies )
		{
			if ( !go.IsValid() ) continue;
			var rb = go.Components.Get<Rigidbody>( FindMode.EverythingInSelfAndDescendants );
			if ( rb.IsValid() )
			{
				rb.Velocity += dir * Power * Time.Delta * 3f;
			}
		}

		// CharacterController poll (parkour Player has a CC, not a Collider)
		var center = WorldPosition;
		var half = Half * WorldScale;
		foreach ( var cc in Scene.GetAllComponents<CharacterController>() )
		{
			var p = cc.WorldPosition;
			var d = p - center;
			if ( MathF.Abs( d.x ) > half.x ) continue;
			if ( MathF.Abs( d.y ) > half.y ) continue;
			if ( MathF.Abs( d.z ) > half.z ) continue;
			cc.Velocity += dir * Power * Time.Delta * 5f;
		}
	}

	protected override void DrawGizmos()
	{
		Gizmo.Draw.Color = Color.Cyan;
		Gizmo.Draw.LineBBox( new BBox( -Half, Half ) );
		Gizmo.Draw.Arrow( Vector3.Zero, Direction.Normal * 100f );
	}
}
