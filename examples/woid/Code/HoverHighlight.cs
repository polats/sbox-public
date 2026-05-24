using System.Linq;
using Sandbox;

namespace Woid;

/// <summary>
/// Outlines the interactable (WoidObject) currently under the cursor. Uses the
/// engine's HighlightOutline component (added to the hovered object) + a
/// Highlight post-process on the camera, which is the clean built-in way to draw
/// outlines — no custom shaders. We add the HighlightOutline only to the hovered
/// object and remove it when the hover changes, so nothing is outlined otherwise.
///
/// Requires a Sandbox.Highlight component on the active CameraComponent.
/// </summary>
public sealed class HoverHighlight : Component
{
	[Property] public Color Color { get; set; } = new Color( 1f, 0.85f, 0.3f );
	[Property] public float Width { get; set; } = 0.4f;

	GameObject _hovered;
	HighlightOutline _outline;

	protected override void OnUpdate()
	{
		var go = FindHoveredInteractable();
		if ( go == _hovered ) return; // no change

		// Drop the outline from whatever we were hovering.
		if ( _outline.IsValid() ) _outline.Destroy();
		_outline = null;
		_hovered = go;

		if ( go.IsValid() )
		{
			_outline = go.Components.GetOrCreate<HighlightOutline>();
			_outline.Color = Color;
			_outline.Width = Width;
			_outline.ObscuredColor = Color.WithAlpha( 0.35f ); // still hint through occluders
		}
	}

	protected override void OnDisabled()
	{
		base.OnDisabled();
		if ( _outline.IsValid() ) _outline.Destroy();
		_outline = null;
		_hovered = null;
	}

	GameObject FindHoveredInteractable()
	{
		if ( IsMouseOverUi() ) return null;

		var cam = Scene.Camera ?? Scene.GetAllComponents<CameraComponent>().FirstOrDefault();
		if ( cam == null ) return null;

		var tr = Scene.Trace.Ray( cam.ScreenPixelToRay( Mouse.Position ), 5000f )
			.IgnoreGameObject( cam.GameObject )
			.Run();
		if ( !tr.Hit ) return null;

		// Walk up to the GameObject that carries the interactive marker.
		var go = tr.GameObject;
		while ( go != null )
		{
			if ( go.Components.Get<WoidObject>() != null ) return go;
			go = go.Parent;
		}
		return null;
	}

	bool IsMouseOverUi()
	{
		foreach ( var pc in Scene.GetAllComponents<PanelComponent>() )
			if ( pc.Panel?.HasHovered == true ) return true;
		return false;
	}
}
