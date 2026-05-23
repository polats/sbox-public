using Sandbox;

namespace Woid;

/// <summary>
/// WASD + mouse-look fly camera for the play-mode camera. Disabled by
/// default — toggled on/off by the DebugBar button. When enabled, locks
/// the mouse cursor so look works. Hold Shift to fly faster.
///
/// Add this component to the Camera GameObject (or its parent).
/// </summary>
public sealed class FlyCamera : Component
{
	[Property] public float MoveSpeed { get; set; } = 300f;
	[Property] public float SprintMultiplier { get; set; } = 3f;
	[Property] public float MouseSensitivity { get; set; } = 0.15f;

	Angles _angles;

	protected override void OnEnabled()
	{
		base.OnEnabled();
		_angles = WorldRotation.Angles();
		Mouse.Visible = false;
		Mouse.Visibility = MouseVisibility.Hidden;
	}

	protected override void OnDisabled()
	{
		base.OnDisabled();
		Mouse.Visible = true;
		Mouse.Visibility = MouseVisibility.Visible;
	}

	protected override void OnUpdate()
	{
		// Tab toggle is now handled in DebugBar.OnUpdate so the same key
		// can both enable and disable from a single place. FlyCam itself
		// no longer self-disables.

		// Look
		_angles.pitch += Mouse.Delta.y * MouseSensitivity;
		_angles.yaw   -= Mouse.Delta.x * MouseSensitivity;
		_angles.pitch = _angles.pitch.Clamp( -89f, 89f );
		WorldRotation = _angles.ToRotation();

		// Move
		var speed = MoveSpeed * Time.Delta;
		if ( Input.Down( "Run" ) ) speed *= SprintMultiplier;

		var forward = WorldRotation.Forward;
		var right   = WorldRotation.Right;
		var up      = Vector3.Up;

		var delta = Vector3.Zero;
		if ( Input.Down( "Forward"  ) ) delta += forward * speed;
		if ( Input.Down( "Backward" ) ) delta -= forward * speed;
		if ( Input.Down( "Left"     ) ) delta -= right   * speed;
		if ( Input.Down( "Right"    ) ) delta += right   * speed;
		if ( Input.Down( "Jump"     ) ) delta += up      * speed;
		if ( Input.Down( "Duck"     ) ) delta -= up      * speed;
		WorldPosition += delta;
	}
}
