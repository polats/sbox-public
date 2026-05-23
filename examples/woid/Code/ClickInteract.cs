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
			// Clicked the floor (or something un-tagged). Send nearest character there.
			var c = NearestIdleCharacter( tr.HitPosition );
			if ( c != null ) c.WalkTo( tr.HitPosition );
			return;
		}

		var character = NearestIdleCharacter( obj.GameObject.WorldPosition );
		if ( character == null ) { Log.Info( "[ClickInteract] no idle character available" ); return; }

		// Dispatch by type
		if ( go.Components.Get<Chair>() is { } chair )
		{
			Log.Info( $"[ClickInteract] {character.CharacterId} → sit on {obj.ObjectId}" );
			character.WalkToAndSit( chair );
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

	Character NearestIdleCharacter( Vector3 to )
	{
		Character best = null;
		var bestDist = float.MaxValue;
		foreach ( var c in Characters.All() )
		{
			if ( c.IsSitting ) continue; // skip busy ones
			var d = c.WorldPosition.Distance( to );
			if ( d < bestDist ) { bestDist = d; best = c; }
		}
		return best;
	}
}
