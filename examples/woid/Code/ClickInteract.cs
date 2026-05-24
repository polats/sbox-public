using System.Linq;
using Sandbox;

namespace Woid;

/// <summary>
/// Left-click on a Chair / Bed / Mug → nearest non-busy character walks
/// over and does the matching action. Useful for testing animation paths
/// without waiting for the LLM to pick the action.
///
/// Right-click on the ground → clears selection / makes nearest character
/// walk to clicked point.
/// </summary>
public sealed class ClickInteract : Component
{
	[Property] public CharacterRegistry Characters { get; set; }

	protected override void OnUpdate()
	{
		if ( !Input.Pressed( "Attack1" ) ) return; // left mouse
		if ( IsMouseOverUi() ) return; // UI takes precedence
		HandleClick();
	}

	bool IsMouseOverUi()
	{
		// If any PanelComponent's root panel is hovered, the click belongs to UI.
		foreach ( var pc in Scene.GetAllComponents<PanelComponent>() )
		{
			if ( pc.Panel?.HasHovered == true ) return true;
		}
		return false;
	}

	void HandleClick()
	{
		var cam = Scene.Camera ?? Scene.GetAllComponents<CameraComponent>().FirstOrDefault();
		if ( cam == null ) return;

		var ray = cam.ScreenPixelToRay( Mouse.Position );
		var tr = Scene.Trace.Ray( ray, 5000f )
			.IgnoreGameObject( cam.GameObject )
			.Run();

		if ( !tr.Hit ) return;

		var go = tr.GameObject;
		// Walk up parent chain to find a WoidObject (the prop's interactive marker).
		Woid.WoidObject obj = null;
		while ( go != null )
		{
			obj = go.Components.Get<WoidObject>();
			if ( obj != null ) break;
			go = go.Parent;
		}

		if ( obj == null )
		{
			// Clicked the floor: send the nearest character there — INCLUDING a
			// seated one. WalkTo() auto-stands first, so clicking the floor while
			// Bob is sitting makes him get up and walk to the new spot.
			var c = NearestCharacter( tr.HitPosition, idleOnly: false );
			if ( c != null ) c.WalkTo( tr.HitPosition );
			return;
		}

		var character = NearestCharacter( obj.GameObject.WorldPosition, idleOnly: true );
		if ( character == null ) { Log.Info( "[ClickInteract] no idle character available" ); return; }

		// Dispatch by type
		if ( go.Components.Get<Sittable>() is { } seat )
		{
			Log.Info( $"[ClickInteract] {character.CharacterId} → sit on {obj.ObjectId}" );
			character.WalkToAndSit( seat );
		}
		else if ( go.Components.Get<Bed>() is { } bed )
		{
			Log.Info( $"[ClickInteract] {character.CharacterId} → sleep on {obj.ObjectId}" );
			character.WalkToAndSleep( bed );
		}
		else if ( go.Components.Get<HoldableProp>() is { } prop )
		{
			Log.Info( $"[ClickInteract] {character.CharacterId} → pick up {obj.ObjectId}" );
			character.WalkToAndHold( prop );
		}
		else
		{
			// Object exists but no interactive component — just walk to it
			character.WalkTo( obj.GameObject.WorldPosition );
		}
	}

	Character NearestCharacter( Vector3 to, bool idleOnly )
	{
		Character best = null;
		var bestDist = float.MaxValue;
		foreach ( var c in Characters.All() )
		{
			if ( idleOnly && c.IsSitting ) continue; // don't yank a seated one into an interaction
			var d = c.WorldPosition.Distance( to );
			if ( d < bestDist ) { bestDist = d; best = c; }
		}
		return best;
	}
}
