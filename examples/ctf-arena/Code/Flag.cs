using Sandbox;
using System;
using System.Linq;

namespace Local.CtfArena;

/// <summary>
/// A team flag. Lives on its base stand by default; when an enemy player
/// touches it, they "pick it up" (the flag becomes parented to that player's
/// model, and the GameManager records the carrier Guid in a [Sync] field).
///
/// Authority model: the host owns flag pickup/score logic. On a client this
/// component reads the synced GameManager state and snaps the visual.
/// We don't [Sync] on Flag directly — the carrier state lives in GameManager
/// (RedFlagCarrier / BlueFlagCarrier [Sync] Guids) so all flag-related state
/// is in one place.
/// </summary>
public sealed class Flag : Component
{
	[Property] public Team Team { get; set; } = Team.Red;
	[Property] public Vector3 HomePosition { get; set; }

	/// <summary>Pickup radius when a player walks over the flag at its home stand.</summary>
	[Property] public float PickupRadius { get; set; } = 60f;

	private ModelRenderer _renderer;

	protected override void OnStart()
	{
		base.OnStart();
		_renderer = Components.GetInDescendantsOrSelf<ModelRenderer>();
		if ( _renderer != null )
			_renderer.Tint = Team.ToColor();

		// Snapshot the home position from the scene transform at start, so the
		// scene file's placement is authoritative.
		if ( HomePosition == Vector3.Zero )
			HomePosition = WorldPosition;
	}

	/// <summary>Move the flag back to its base stand. Host-side only.</summary>
	public void ReturnToBase()
	{
		GameObject.Parent = null;
		GameObject.WorldPosition = HomePosition;
		GameObject.WorldRotation = Rotation.Identity;
	}

	/// <summary>Attach to a carrier so the flag visually rides on their back.</summary>
	public void AttachTo( GameObject carrier )
	{
		GameObject.Parent = carrier;
		GameObject.LocalPosition = new Vector3( -10f, 0, 60f );
		GameObject.LocalRotation = Rotation.FromYaw( 90f );
	}
}
