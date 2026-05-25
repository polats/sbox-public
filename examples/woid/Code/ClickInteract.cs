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

	// The character we last told to "use" (read) a held item. Kept because the
	// takeover disables the Character component, so a Characters.All() lookup may
	// not find it again to toggle reading back off — we hold the reference here.
	Character _reader;

	protected override void OnUpdate()
	{
		// E toggles "use held item" (read the newspaper). Allowed even over UI and
		// regardless of cursor, but not while the fly camera is using E-neighbours;
		// ignore while the fly camera is held so its movement keys don't double up.
		if ( Input.Pressed( "Use" ) && !Input.Down( "CameraFly" ) ) HandleUse();

		if ( IsMouseOverUi() ) return; // UI takes precedence for mouse
		if ( Input.Pressed( "Attack1" ) ) HandleClick();          // left: move / interact
		else if ( Input.Pressed( "Attack2" ) ) HandleThrow();     // right: throw held prop
	}

	/// <summary>E key: toggle "using" the held item. If a character is currently
	/// reading, stop it; otherwise the holder starts reading (if its item is
	/// usable). Stop is driven through the kept reference because the reading
	/// character is disabled mid-takeover.</summary>
	void HandleUse()
	{
		// Currently reading? Toggle off. Prefer the kept reference (survives the
		// component being disabled); fall back to a scan.
		var reader = (_reader.IsValid() && _reader.IsReading)
			? _reader
			: Characters.All().FirstOrDefault( c => c.IsReading );
		if ( reader != null )
		{
			// Sustained uses (reading) toggle off; one-shots (drinking) run to
			// completion on their own, so E is ignored mid-action.
			if ( reader.CurrentUseHolds ) { reader.StopUsingHeld(); _reader = null; }
			return;
		}

		// Not reading: the holder starts using its item (no-op if not usable).
		var holder = Characters.All().FirstOrDefault( c => c.IsHolding );
		if ( holder == null ) return;
		holder.StartUsingHeld();
		if ( holder.IsReading ) _reader = holder;
	}

	/// <summary>Right-click: the holding character throws its prop toward the
	/// point under the cursor (ground or object). Right-mouse is free for this
	/// because the fly camera moved to middle-mouse.</summary>
	void HandleThrow()
	{
		var holder = Characters.All().FirstOrDefault( c => c.IsHolding );
		if ( holder == null ) return;

		var cam = Scene.Camera ?? Scene.GetAllComponents<CameraComponent>().FirstOrDefault();
		if ( cam == null ) return;
		var tr = Scene.Trace.Ray( cam.ScreenPixelToRay( Mouse.Position ), 5000f )
			.IgnoreGameObject( cam.GameObject )
			.WithoutTags( "held" )
			.Run();
		if ( !tr.Hit ) return;

		holder.ThrowHeld( tr.HitPosition );
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
			.WithoutTags( "held" )
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
