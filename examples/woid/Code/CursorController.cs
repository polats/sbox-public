using Sandbox;

namespace Woid;

/// <summary>
/// Shows the mouse cursor on play start so HUD buttons are clickable.
/// CRITICAL: only sets once in OnStart — forcing Mouse.Visible every frame
/// captures keyboard input including ESC, blocking the editor from exiting
/// play mode.
/// </summary>
public sealed class CursorController : Component
{
	protected override void OnStart()
	{
		base.OnStart();
		Mouse.Visible = true;
		Mouse.Visibility = MouseVisibility.Visible;
	}
}
