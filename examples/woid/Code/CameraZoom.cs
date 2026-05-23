using Sandbox;

namespace Woid;

/// <summary>
/// Mouse-wheel zoom for the play-mode camera. Moves the camera along its
/// own forward axis so it works whether FlyCamera is on or off. Add this
/// alongside the CameraComponent.
/// </summary>
public sealed class CameraZoom : Component
{
	[Property] public float StepUnits { get; set; } = 40f;
	[Property] public float SprintMultiplier { get; set; } = 3f;

	protected override void OnUpdate()
	{
		var wheel = Input.MouseWheel.y;
		if ( wheel == 0 ) return;
		var step = StepUnits * wheel;
		if ( Input.Down( "Run" ) ) step *= SprintMultiplier;
		WorldPosition += WorldRotation.Forward * step;
	}
}
