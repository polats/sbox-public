using Sandbox;

namespace Woid;

/// <summary>
/// Hold-to-fly camera: while the RIGHT mouse button is held, the cursor hides
/// and the camera does mouse-look + WASD movement (Space/Ctrl for up/down,
/// Shift to go faster). Release to return to normal cursor + click-to-direct.
///
/// There's no separate "fly mode" toggle — this component stays enabled and
/// only takes over the camera while RMB is down. Cursor visibility is only
/// changed on the press/release transitions (never every frame), so it doesn't
/// capture ESC and block the editor from leaving play mode.
///
/// Add this to the Camera GameObject (alongside CameraComponent / CameraZoom).
/// </summary>
public sealed class FlyCamera : Component
{
	[Property] public float MoveSpeed { get; set; } = 300f;
	[Property] public float SprintMultiplier { get; set; } = 3f;
	[Property] public float MouseSensitivity { get; set; } = 0.15f;

	Angles _angles;
	bool _active;

	protected override void OnUpdate()
	{
		var held = Input.Down( "Attack2" ); // right mouse

		if ( held && !_active )
		{
			// Begin: capture current orientation so look continues smoothly,
			// and hide the cursor so Mouse.Delta drives the look.
			_angles = WorldRotation.Angles();
			SetCursor( visible: false );
			_active = true;
		}
		else if ( !held && _active )
		{
			SetCursor( visible: true );
			_active = false;
		}

		if ( !_active ) return;

		// Look
		_angles.pitch = (_angles.pitch + Mouse.Delta.y * MouseSensitivity).Clamp( -89f, 89f );
		_angles.yaw  -= Mouse.Delta.x * MouseSensitivity;
		WorldRotation = _angles.ToRotation();

		// Move (WASD + Space/Ctrl up-down, Shift sprint)
		var speed = MoveSpeed * Time.Delta;
		if ( Input.Down( "Run" ) ) speed *= SprintMultiplier;

		var rot = WorldRotation;
		var delta = Vector3.Zero;
		if ( Input.Down( "Forward"  ) ) delta += rot.Forward * speed;
		if ( Input.Down( "Backward" ) ) delta -= rot.Forward * speed;
		if ( Input.Down( "Left"     ) ) delta -= rot.Right   * speed;
		if ( Input.Down( "Right"    ) ) delta += rot.Right   * speed;
		if ( Input.Down( "Jump"     ) ) delta += Vector3.Up  * speed;
		if ( Input.Down( "Duck"     ) ) delta -= Vector3.Up  * speed;
		WorldPosition += delta;
	}

	protected override void OnDisabled()
	{
		base.OnDisabled();
		if ( _active ) { SetCursor( visible: true ); _active = false; }
	}

	static void SetCursor( bool visible )
	{
		Mouse.Visible = visible;
		Mouse.Visibility = visible ? MouseVisibility.Visible : MouseVisibility.Hidden;
	}
}
