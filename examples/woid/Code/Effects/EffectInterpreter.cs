using System.Collections.Generic;
using Sandbox;

namespace Woid;

/// <summary>
/// Applies declarative Effects to the live scene. One method per Effect kind.
/// Adding a new Effect kind in woid → add one Apply overload here.
/// </summary>
public sealed class EffectInterpreter : Component
{
	[Property] public CharacterRegistry Characters { get; set; }
	[Property] public ObjectRegistry    Objects    { get; set; }

	public void Apply( IEnumerable<Effect> effects )
	{
		foreach ( var e in effects ) ApplyOne( e );
	}

	public void ApplyOne( Effect e )
	{
		switch ( e )
		{
			case SetPos sp:        Apply( sp ); break;
			case PlayAnim pa:      Apply( pa ); break;
			case Occupy oc:        Apply( oc ); break;
			case Need n:           Apply( n );  break;
			case Moodlet m:        Apply( m );  break;
			case AdvanceSim a:     Apply( a );  break;
			case SpeechBubble sb:  Apply( sb ); break;
			case Perceive p:       Apply( p );  break;
			case SitOnChair soc:   Apply( soc ); break;
			case StandUp su:       Apply( su ); break;
			case SleepInBed sib:   Apply( sib ); break;
			case DrinkHoldable dh: Apply( dh ); break;
			default:
				Log.Info( $"[EffectInterpreter] unhandled effect type: {e?.GetType().Name}" );
				break;
		}
	}

	void Apply( SetPos e )
	{
		var c = Characters?.Get( e.Actor );
		if ( c == null ) { Log.Warning( $"[set_pos] unknown actor: {e.Actor}" ); return; }
		c.WalkTo( new Vector3( e.X, e.Y, e.Z ) );
	}

	void Apply( PlayAnim e )
	{
		var c = Characters?.Get( e.Actor );
		if ( c == null ) { Log.Warning( $"[play_anim] unknown actor: {e.Actor}" ); return; }
		c.PlayAnimation( e.Name, e.Loop );
	}

	void Apply( Occupy e )
	{
		var obj = Objects?.Get( e.ObjectId );
		if ( obj == null ) { Log.Warning( $"[occupy] unknown object: {e.ObjectId}" ); return; }
		obj.Occupant = e.Release ? null : e.Actor;
	}

	void Apply( Need e )
	{
		var c = Characters?.Get( e.Actor );
		if ( c == null ) { Log.Warning( $"[need] unknown actor: {e.Actor}" ); return; }
		c.MutateNeed( e.Axis, e.Op, e.Amount );
	}

	void Apply( Moodlet e )
	{
		// First slice: log only. UI rendering comes with the inspector phase.
		Log.Info( $"[moodlet] {e.Actor} += {e.Id} weight={e.Weight} for {e.DurationMs}ms" );
	}

	void Apply( AdvanceSim e )
	{
		// First slice: log only. Sim-clock is woid-side; sbox just renders.
		Log.Info( $"[advance_sim] +{e.Minutes} minutes (no-op sbox-side)" );
	}

	void Apply( SpeechBubble e )
	{
		var c = Characters?.Get( e.Actor );
		if ( c == null ) { Log.Warning( $"[speech_bubble] unknown actor: {e.Actor}" ); return; }
		c.ShowSpeechBubble( e.Text, e.To, e.DurationMs );
	}

	void Apply( Perceive e )
	{
		// Sbox doesn't act on perceive effects directly — these are events
		// going BACK to woid via /perception/event. WoidClient handles them.
		// Logging only here.
		Log.Info( $"[perceive] target={e.Target} event={e.Event}" );
	}

	void Apply( SitOnChair e )
	{
		var c = Characters?.Get( e.Actor );
		if ( c == null ) { Log.Warning( $"[sit_on_chair] unknown actor: {e.Actor}" ); return; }
		var obj = Objects?.Get( e.ObjectId );
		if ( obj == null ) { Log.Warning( $"[sit_on_chair] unknown object: {e.ObjectId}" ); return; }
		var seat = obj.Components.Get<Sittable>();
		if ( seat == null ) { Log.Warning( $"[sit_on_chair] object {e.ObjectId} has no Sittable component" ); return; }
		c.WalkToAndSit( seat );
	}

	void Apply( StandUp e )
	{
		var c = Characters?.Get( e.Actor );
		if ( c == null ) { Log.Warning( $"[stand_up] unknown actor: {e.Actor}" ); return; }
		var obj = Objects?.Get( e.ObjectId );
		var seat = obj?.Components.Get<Sittable>();
		if ( seat != null ) seat.Stand( c );
		else Log.Warning( $"[stand_up] no Sittable for object {e.ObjectId}" );
	}

	void Apply( SleepInBed e )
	{
		var c = Characters?.Get( e.Actor );
		if ( c == null ) { Log.Warning( $"[sleep_in_bed] unknown actor: {e.Actor}" ); return; }
		var obj = Objects?.Get( e.ObjectId );
		if ( obj == null ) { Log.Warning( $"[sleep_in_bed] unknown object: {e.ObjectId}" ); return; }
		var bed = obj.Components.Get<Bed>();
		if ( bed == null ) { Log.Warning( $"[sleep_in_bed] object {e.ObjectId} has no Bed component" ); return; }
		c.WalkToAndSleep( bed );
	}

	void Apply( DrinkHoldable e )
	{
		var c = Characters?.Get( e.Actor );
		if ( c == null ) { Log.Warning( $"[drink_holdable] unknown actor: {e.Actor}" ); return; }
		var obj = Objects?.Get( e.ObjectId );
		if ( obj == null ) { Log.Warning( $"[drink_holdable] unknown object: {e.ObjectId}" ); return; }
		var prop = obj.Components.Get<HoldableProp>();
		if ( prop == null ) { Log.Warning( $"[drink_holdable] object {e.ObjectId} has no HoldableProp" ); return; }
		c.WalkToAndHold( prop );
	}
}
