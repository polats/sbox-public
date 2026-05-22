using Sandbox;
using System;
using System.Collections.Generic;

namespace Local.ToyBox;

/// <summary>
/// Local equivalent of sandbox/Code/Map/TriggerTeleport.cs.
/// On trigger-enter, teleports the root GameObject to Target.WorldPosition.
/// Polls CharacterController positions each fixed-update since CC isn't a Collider.
/// </summary>
[Icon( "swap_calls" )]
public sealed class TriggerTeleport : Component, Component.ITriggerListener
{
	[Property] public GameObject Target { get; set; }
	[Property] public Vector3 Half { get; set; } = new Vector3( 50, 50, 50 );

	// Avoid bouncing back-and-forth if both pads point at each other or pad overlaps target
	private float _cooldownUntil;

	void ITriggerListener.OnTriggerEnter( Collider other )
	{
		Teleport( other.GameObject?.Root );
	}

	protected override void OnFixedUpdate()
	{
		if ( !Target.IsValid() ) return;

		var center = WorldPosition;
		var half = Half * WorldScale;
		foreach ( var cc in Scene.GetAllComponents<CharacterController>() )
		{
			var p = cc.WorldPosition;
			var d = p - center;
			if ( MathF.Abs( d.x ) > half.x ) continue;
			if ( MathF.Abs( d.y ) > half.y ) continue;
			if ( MathF.Abs( d.z ) > half.z ) continue;
			Teleport( cc.GameObject );
		}
	}

	private void Teleport( GameObject go )
	{
		if ( !Target.IsValid() || go is null ) return;
		if ( Time.Now < _cooldownUntil ) return;
		_cooldownUntil = Time.Now + 1.0f;
		go.WorldPosition = Target.WorldPosition + Vector3.Up * 4f;
		go.Transform.ClearInterpolation();
		var cc = go.Components.Get<CharacterController>();
		if ( cc.IsValid() ) cc.Velocity = Vector3.Zero;
	}

	protected override void DrawGizmos()
	{
		Gizmo.Draw.Color = Color.Magenta;
		Gizmo.Draw.LineBBox( new BBox( -Half, Half ) );
		if ( Target.IsValid() )
			Gizmo.Draw.Arrow( Vector3.Zero, WorldTransform.PointToLocal( Target.WorldPosition ) );
	}
}
