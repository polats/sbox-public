using System;
using System.Collections.Generic;
using Sandbox;
using Sandbox.Citizen;

namespace Woid;

/// <summary>
/// A character in the world. Movement via NavMeshAgent, anim drive via
/// CitizenAnimationHelper. Look-at handled here (we drive helper.LookAt
/// directly when the character is mid-conversation).
///
/// The persona/needs/speech fields are still authoritative for the
/// brain-server-sbox contract. The render/movement plumbing is the
/// canonical Facepunch pattern (see examples/npc-patrol).
/// </summary>
public sealed class Character : Component
{
	/// <summary>Woid-side character id. Must match /character POST.</summary>
	[Property] public string CharacterId { get; set; }

	[Property] public SkinnedModelRenderer Model { get; set; }

	[Property] public string PersonaName { get; set; } = "";
	[Property, TextArea] public string PersonaAbout { get; set; } = "";
	[Property] public string PersonaVibe { get; set; } = "";
	[Property] public string BrainProvider { get; set; } = "utility";

	[Property, Range( 0, 100 )] public float InitialEnergy { get; set; } = 100f;
	[Property, Range( 0, 100 )] public float InitialSocial { get; set; } = 60f;
	[Property, Range( 0, 100 )] public float InitialHunger { get; set; } = 80f;

	/// <summary>How long to keep looking at a speech target after the line lands.</summary>
	[Property] public float LookHoldSec { get; set; } = 5f;

	public Dictionary<string, float> Needs { get; } = new();

	public string CurrentAnim => _sitting ? "sit" : (_agent.IsValid() && _agent.IsNavigating ? "walk" : "idle");

	public string LastSpeech { get; private set; } = "";
	public string LastSpeechTo { get; private set; } = "";

	NavMeshAgent _agent;
	CitizenAnimationHelper _anim;
	bool _sitting;
	Chair _currentChair;
	GameObject _lookTarget;
	float _lookExpiresAt;
	Chair _pendingSitChair;
	float _pendingSitTimeoutAt;

	protected override void OnStart()
	{
		base.OnStart();
		Needs["energy"] = InitialEnergy;
		Needs["social"] = InitialSocial;
		Needs["hunger"] = InitialHunger;

		_agent = Components.Get<NavMeshAgent>();
		_anim  = Components.Get<CitizenAnimationHelper>();
		if ( Model == null ) Model = Components.GetInDescendantsOrSelf<SkinnedModelRenderer>();

		if ( _anim.IsValid() && _anim.Target == null && Model.IsValid() ) _anim.Target = Model;
	}

	protected override void OnUpdate()
	{
		// Drive the animgraph from NavMeshAgent velocity each frame.
		if ( _anim.IsValid() && _agent.IsValid() )
		{
			_anim.WithVelocity( _agent.Velocity );
			_anim.WithWishVelocity( _agent.Velocity );
		}

		// Pending-sit: walk-to-chair, snap-sit on arrival or after timeout.
		if ( _pendingSitChair.IsValid() && !_sitting )
		{
			var seat = _pendingSitChair.SeatPosition?.WorldPosition ?? _pendingSitChair.WorldPosition;
			var dist = WorldPosition.Distance( seat );
			var arrived = dist < 28f;
			var timedOut = Time.Now > _pendingSitTimeoutAt;
			if ( arrived || timedOut )
			{
				_pendingSitChair.Sit( this );
				_pendingSitChair = null;
			}
		}

		// Auto-clear stale look targets.
		if ( _lookTarget.IsValid() && Time.Now > _lookExpiresAt )
		{
			_lookTarget = null;
			if ( _anim.IsValid() ) _anim.LookAt = null;
		}
		else if ( _lookTarget.IsValid() && _anim.IsValid() )
		{
			_anim.LookAt = _lookTarget;
		}
	}

	/// <summary>Walk to worldPos via NavMeshAgent. Auto-stands if currently sat.</summary>
	public void WalkTo( Vector3 worldPos )
	{
		// Auto-stand: any walk implicitly cancels a sit. Without this the
		// character is stuck on the chair until the LLM picks "stand_up".
		// Find the chair via _currentChair if we have it, else walk up the
		// parent chain for any Chair component (handles stale state).
		if ( _sitting )
		{
			var chair = _currentChair.IsValid()
				? _currentChair
				: GameObject.Parent?.Components?.Get<Chair>( includeDisabled: true );
			if ( chair.IsValid() ) chair.Stand( this );
			else
			{
				// Defensive: no chair to ask, just unparent + clear flags.
				GameObject.SetParent( null, true );
				_sitting = false;
				_currentChair = null;
				if ( Model.IsValid() ) { Model.Set( "sit", 0 ); Model.Set( "b_sit", false ); }
				if ( _agent.IsValid() ) { _agent.UpdatePosition = true; _agent.UpdateRotation = true; }
			}
		}

		if ( !_agent.IsValid() )
		{
			Log.Warning( $"[Character {CharacterId}] no NavMeshAgent component — character won't move" );
			return;
		}
		// Snap the target to the navmesh so we never silently fail when the
		// LLM picks an off-mesh point (e.g. through a chair's center).
		var nm = Scene.NavMesh;
		var snapped = nm?.GetClosestPoint( worldPos, 200f ) ?? worldPos;
		_agent.MoveTo( snapped );
	}

	/// <summary>
	/// Walk toward the chair's SeatPosition, then sit when within range.
	/// Auto-sits after 8s even if NavMesh couldn't reach (so the character
	/// doesn't get stuck "trying to sit").
	/// </summary>
	public void WalkToAndSit( Chair chair )
	{
		if ( chair == null || _sitting ) return;
		var seat = chair.SeatPosition?.WorldPosition ?? chair.WorldPosition;
		WalkTo( seat );
		_pendingSitChair = chair;
		_pendingSitTimeoutAt = Time.Now + 8f;
	}

	public void PlayAnimation( string name, bool loop )
	{
		// With CitizenAnimationHelper driving the graph, most "play X" is
		// implicit (sit pose, holdtype etc set via Chair / verb effects).
		// Keep this method as a no-op breadcrumb for now; verb-specific anim
		// triggers can be added per-case (b_attack, b_jump, etc).
		Log.Info( $"[Character {CharacterId}] anim → {name} (loop={loop})" );
	}

	public void MutateNeed( string axis, string op, float amount )
	{
		if ( !Needs.ContainsKey( axis ) ) Needs[axis] = 0;
		var v = Needs[axis];
		v = op switch
		{
			"+" => v + amount,
			"-" => v - amount,
			"=" => amount,
			_   => v,
		};
		Needs[axis] = Math.Clamp( v, 0f, 100f );
	}

	public void ShowSpeechBubble( string text, string to, long durationMs )
	{
		LastSpeech = text ?? "";
		LastSpeechTo = to ?? "";
		Log.Info( $"[Character {CharacterId}] says{(string.IsNullOrEmpty(to) ? "" : $" to {to}")}: \"{text}\"" );
	}

	/// <summary>Look at this GameObject for LookHoldSec (default 5s). Pass null to clear.</summary>
	public void LookAt( GameObject target )
	{
		_lookTarget = target;
		_lookExpiresAt = Time.Now + LookHoldSec;
		if ( _anim.IsValid() )
		{
			_anim.LookAt = target;
			if ( target != null )
			{
				_anim.EyesWeight = 1.0f;
				_anim.HeadWeight = 1.0f;
				_anim.BodyWeight = 0.3f;
			}
		}
	}

	/// <summary>Set by Chair.Sit/Stand so other systems (animation, walk) honor sit state.</summary>
	public void SetSitting( bool sitting, Chair chair = null )
	{
		_sitting = sitting;
		_currentChair = sitting ? chair : null;
	}
	public bool IsSitting => _sitting;
}
