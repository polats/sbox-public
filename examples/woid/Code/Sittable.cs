using System;
using System.Linq;
using Sandbox;

namespace Woid;

/// <summary>
/// Generic sittable surface — chair, bench, ledge, stool, the ground. The
/// "seat target" (where the butt rests + which way to face) is resolved
/// systematically, not hand-tuned:
///
///   • SeatMarker set  → use it (chairs: a marker at the seat surface, because
///     a chair's collider box is taller than the seat so a downward trace would
///     hit the backrest top, not the seat).
///   • otherwise        → trace straight down onto this object's own collider;
///     the top surface is the seat (works for flat things: benches, ledges,
///     tables, ground).
///
/// The character then aligns its POSED pelvis to that surface every frame
/// (Character.AlignToSeat) and foot-IKs to the floor, so any model/height sits
/// cleanly with zero per-object numeric tuning.
/// </summary>
public sealed class Sittable : Component
{
	/// <summary>citizen animgraph sit enum: 1 Chair, 2 ChairForward, 3 ChairCrossed,
	/// 4 KneelingOpen, 5 Kneeling, 6 Ground, 7 GroundCrossed.</summary>
	[Property] public int SitPose { get; set; } = 1;

	/// <summary>Optional seat-surface marker (overrides auto-trace). Position =
	/// seat surface; forward = the direction the seated character faces.</summary>
	[Property] public GameObject SeatMarker { get; set; }

	/// <summary>How far in front of the seat the character stops before sitting,
	/// in inches. Kept small so they walk right up to the object; the navmesh
	/// snaps it just outside the collider, so it can't be unreachable.</summary>
	[Property] public float ApproachDistance { get; set; } = 30f;

	/// <summary>Height of the pelvis bone above the seat surface when seated —
	/// a human constant (~butt-to-hip), NOT per-object. Tune once if needed.</summary>
	[Property] public float PelvisAboveSeat { get; set; } = 6f;

	public string Occupant { get; private set; }
	public bool IsOccupied => !string.IsNullOrEmpty( Occupant );

	public struct SeatTarget
	{
		public Vector3 Surface;   // world point the butt rests on
		public Rotation Facing;   // direction the seated character faces
		public float PelvisOffset; // PelvisAboveSeat, carried for the aligner
	}

	/// <summary>Resolve the seat surface + facing (marker, else downward trace).</summary>
	public SeatTarget GetSeatTarget()
	{
		var facing = SeatMarker.IsValid() ? SeatMarker.WorldRotation : WorldRotation;
		Vector3 surface;

		if ( SeatMarker.IsValid() )
		{
			surface = SeatMarker.WorldPosition;
		}
		else
		{
			// Auto-detect from the model's world bounds: top surface, placed near
			// the FRONT edge (along the facing direction) so the legs hang off the
			// surface instead of sinking into a solid box. A chair has open space
			// in front (hence its marker); a bench/ledge/crate doesn't.
			var rend = Components.Get<ModelRenderer>();
			var b = rend.IsValid() ? rend.Bounds : BBox.FromPositionAndSize( WorldPosition, 32f );
			var fwd = facing.Forward.WithZ( 0f ).Normal;
			var halfDepth = MathF.Abs( fwd.x ) * b.Size.x * 0.5f + MathF.Abs( fwd.y ) * b.Size.y * 0.5f;
			surface = b.Center.WithZ( b.Maxs.z ) + fwd * MathF.Max( 0f, halfDepth - 10f );
		}

		return new SeatTarget { Surface = surface, Facing = facing, PelvisOffset = PelvisAboveSeat };
	}

	/// <summary>Floor point in front of the seat where the character approaches /
	/// is ejected to. Derived from the seat facing — no separate anchor needed.</summary>
	public Vector3 ApproachPoint()
	{
		var t = GetSeatTarget();
		var p = t.Surface + t.Facing.Forward * ApproachDistance;
		// Drop to the floor under that point so the navmesh can reach it.
		var tr = Scene.Trace.Ray( p + Vector3.Up * 128f, p - Vector3.Up * 256f )
			.WithoutTags( "player", "kimodo" ).Run();
		return tr.Hit ? tr.HitPosition : p.WithZ( WorldPosition.z );
	}

	public bool Sit( Character c )
	{
		if ( c == null || IsOccupied ) return false;

		// Freeze the navmesh agent so it can't drag the character off the seat.
		var agent = c.Components.Get<NavMeshAgent>();
		if ( agent.IsValid() ) { agent.Stop(); agent.UpdatePosition = false; agent.UpdateRotation = false; }

		c.GameObject.SetParent( GameObject, true );
		Occupant = c.CharacterId;
		c.BeginSeated( this, SitPose );
		return true;
	}

	public void Stand( Character c )
	{
		if ( c == null ) return;
		if ( IsOccupied && Occupant != c.CharacterId ) return;
		c.StandUp(); // eased rise in place; Character clears the seat + restores the agent
	}

	/// <summary>Clear the occupant (called by Character.StandUp).</summary>
	public void Vacate( Character c )
	{
		if ( c != null && Occupant == c.CharacterId ) Occupant = null;
	}
}
