using Sandbox;
using System;

namespace Local.ToyBox;

/// <summary>
/// Local equivalent of sandbox/Code/Weapons/ToolGun/Modes/Hydraulic/HydraulicEntity.cs.
/// Upstream uses a SliderJoint between two physics bodies; the joint's min/max
/// length is animated. We don't have a SliderJoint authored in code easily, so
/// we drive a simple piston: a child rod GameObject's LocalPosition.z
/// oscillates between MinLength and MaxLength. The visual+gameplay result is
/// the same (extend/retract on a timer), without the rigidbody coupling.
///
/// Optional: if Animated=true, oscillates with ease curves like the canonical
/// version's Animated mode.
/// </summary>
[Icon( "swap_vert" )]
public sealed class HydraulicEntity : Component
{
	[Property] public GameObject Rod { get; set; }
	[Property] public float MinLength { get; set; } = 20f;
	[Property] public float MaxLength { get; set; } = 90f;
	[Property] public float AnimationSpeed { get; set; } = 1.0f;
	[Property] public Vector3 Axis { get; set; } = Vector3.Up;

	protected override void OnUpdate()
	{
		if ( !Rod.IsValid() ) return;
		var t = (MathF.Sin( Time.Now * AnimationSpeed * 2f ) + 1f) * 0.5f;
		// ease-in-out
		t = t * t * (3f - 2f * t);
		var len = MinLength + t * (MaxLength - MinLength);
		Rod.LocalPosition = Axis.Normal * len;
	}

	protected override void DrawGizmos()
	{
		Gizmo.Draw.Color = Color.Yellow;
		Gizmo.Draw.Line( Vector3.Zero, Axis.Normal * MinLength );
		Gizmo.Draw.Color = Color.Orange;
		Gizmo.Draw.Line( Axis.Normal * MinLength, Axis.Normal * MaxLength );
	}
}
