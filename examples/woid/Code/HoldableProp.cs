using System;
using Sandbox;

namespace Woid;

/// <summary>
/// A small prop a character can pick up and hold (mug, coin, etc).
///
/// Canonical pattern from sandbox/Code/Npcs/Layers/AnimationLayer.Hold.cs:
///   - One-handed: parent prop to renderer.GetBoneObject("hold_R")
///   - Set animgraph: holdtype = HoldItem, holdtype_handedness = Right,
///     holdtype_pose = 2.0 (one-handed grip), holdtype_pose_hand = 0.005
///
/// Two-handed support not yet wired (would parent to "spine_2" + dual hand IK).
/// </summary>
public sealed class HoldableProp : Component
{
	/// <summary>1=Right, 2=Left, 0=Both. From CitizenAnimationHelper.Hand.</summary>
	[Property] public int Handedness { get; set; } = 1;

	/// <summary>0..5: 0 narrow grip, 5 wide. 2.0 is comfortable one-handed.</summary>
	[Property] public float HoldtypePose { get; set; } = 2.0f;

	/// <summary>Grip tightness param. ~0.005 is sandbox default.</summary>
	[Property] public float HoldtypePoseHand { get; set; } = 0.005f;

	public string Holder { get; private set; }

	public bool Hold( Character c )
	{
		if ( c == null || !string.IsNullOrEmpty( Holder ) ) return false;
		if ( !c.Model.IsValid() ) { Log.Warning( $"[HoldableProp] {c.CharacterId} has no model" ); return false; }

		var boneName = Handedness == 2 ? "hold_L" : "hold_R";
		var handBone = c.Model.GetBoneObject( boneName );
		if ( handBone == null ) { Log.Warning( $"[HoldableProp] bone {boneName} not found on {c.CharacterId}" ); return false; }

		SetPhysics( false ); // held props are kinematic — hand-attached, no collision
		GameObject.SetParent( handBone, false );
		GameObject.LocalPosition = Vector3.Zero;
		GameObject.LocalRotation = Rotation.Identity;
		GameObject.Tags.Add( "held" ); // excluded from interaction raycasts while carried

		c.Model.Set( "holdtype", 4 );           // 4 = HoldItem
		c.Model.Set( "holdtype_handedness", Handedness );
		c.Model.Set( "holdtype_pose", HoldtypePose );
		c.Model.Set( "holdtype_pose_hand", HoldtypePoseHand );

		Holder = c.CharacterId;
		c.SetHolding( this );
		return true;
	}

	public void Drop( Character c )
	{
		if ( c == null ) return;
		if ( !string.IsNullOrEmpty( Holder ) && Holder != c.CharacterId ) return;

		Release( c );
		// Re-enabling physics with no extra velocity lets it fall in place.
	}

	/// <summary>Throw the held prop in a ballistic arc toward a world point.</summary>
	public void Throw( Character c, Vector3 target )
	{
		if ( c == null ) return;
		if ( !string.IsNullOrEmpty( Holder ) && Holder != c.CharacterId ) return;

		var start = WorldPosition; // the hand
		Release( c );

		if ( Components.Get<Rigidbody>( true ) is { } rb )
		{
			// Solve a simple ballistic launch: pick a flight time from distance,
			// then the velocity that lands at `target` under gravity.
			var to = target - start;
			var horiz = to.WithZ( 0f );
			var dist = horiz.Length;
			var g = MathF.Abs( Scene.PhysicsWorld.Gravity.z ); if ( g < 1f ) g = 800f;
			var t = MathX.Clamp( dist / 320f, 0.45f, 1.3f );
			var vUp = (to.z + 0.5f * g * t * t) / t;
			var v = (dist > 0.01f ? horiz.Normal * (dist / t) : Vector3.Zero) + Vector3.Up * vUp;
			rb.Velocity = v;
			rb.AngularVelocity = Vector3.Random * 4f; // a little tumble
		}
	}

	/// <summary>Common release: unparent, restore physics, clear the carry pose.</summary>
	void Release( Character c )
	{
		GameObject.SetParent( null, true );
		GameObject.Tags.Remove( "held" );
		SetPhysics( true );

		if ( c.Model.IsValid() )
		{
			c.Model.Set( "holdtype", 0 );          // None
			c.Model.Set( "holdtype_pose", 0f );
		}

		Holder = null;
		c.SetHolding( null );
	}

	void SetPhysics( bool on )
	{
		if ( Components.Get<Rigidbody>( true ) is { } rb ) rb.Enabled = on;
		if ( Components.Get<Collider>( true ) is { } col ) col.Enabled = on;
	}

	/// <summary>Pulse b_attack to play the holdtype's "use" animation (e.g. raise mug to lips).</summary>
	public void PulseUse( Character c )
	{
		if ( c?.Model.IsValid() != true ) return;
		c.Model.Set( "b_attack", true );
		// Caller is responsible for clearing on next tick. We don't keep a coroutine here.
	}
}
