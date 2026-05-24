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
	KimodoSequencePlayer _kimodo;
	bool _sitting;
	Sittable _currentSeat;
	Bed _currentBed;
	HoldableProp _holding;
	GameObject _lookTarget;
	float _lookExpiresAt;
	Sittable _pendingSit;
	Bed _pendingSleepBed;
	float _pendingSitTimeoutAt;

	// Seated procedural-alignment state (see BeginSeated / AlignToSeat).
	int _pelvisBone = -1;
	float _b_attack_clear_at;
	float _faceExpireAt;
	string _activeFaceParam;
	float _b_attack_next_pulse_at;

	protected override void OnStart()
	{
		base.OnStart();
		Needs["energy"] = InitialEnergy;
		Needs["social"] = InitialSocial;
		Needs["hunger"] = InitialHunger;

		_agent = Components.Get<NavMeshAgent>();
		_anim  = Components.Get<CitizenAnimationHelper>();
		_kimodo = Components.Get<KimodoSequencePlayer>();
		if ( Model == null ) Model = Components.GetInDescendantsOrSelf<SkinnedModelRenderer>();

		// We face the direction of travel ourselves (see OnUpdate). The agent's
		// own UpdateRotation aims at a look-ahead point on the path, which spins
		// the character toward unreachable targets when blocked and snaps it
		// around at arrival when it slightly overshoots the last corner.
		if ( _agent.IsValid() ) _agent.UpdateRotation = false;

		if ( _anim.IsValid() && _anim.Target == null && Model.IsValid() ) _anim.Target = Model;

		// Required for GetBoneObject() to return non-null — needed for hand-bone
		// parenting (HoldableProp.Hold uses hold_R / hold_L).
		if ( Model.IsValid() ) Model.CreateBoneObjects = true;
	}

	protected override void OnUpdate()
	{
		// Note: while a kimodo clip plays, KimodoSequencePlayer disables this
		// component entirely (full takeover), so this method doesn't run then —
		// no per-frame "is a clip playing?" gate is needed here.

		// While seated, the seat owns the body: align the posed pelvis to the
		// seat surface + foot-IK to the floor, and skip locomotion entirely.
		if ( _sitting && _currentSeat.IsValid() )
		{
			AlignToSeat();
			TickInteractions();
			return;
		}

		// Drive the animgraph from NavMeshAgent velocity each frame.
		if ( _anim.IsValid() && _agent.IsValid() )
		{
			_anim.WithVelocity( _agent.Velocity );
			_anim.WithWishVelocity( _agent.Velocity );
		}

		// Face the direction of actual travel, but only while genuinely moving —
		// this avoids the agent's look-ahead spin at arrival / when blocked.
		if ( _agent.IsValid() && !_sitting )
		{
			var v = _agent.Velocity.WithZ( 0f );
			if ( v.Length > 10f )
			{
				var aim = Rotation.LookAt( v.Normal, Vector3.Up );
				WorldRotation = Rotation.Slerp( WorldRotation, aim, Time.Delta * 8f );
			}
		}

		// Per-interaction state machines (stretch/dance/greet pulses).
		TickInteractions();

		// Pending-sit: walk to the seat's front approach point, then Sit()
		// parents + begins the procedural seated alignment.
		if ( _pendingSit.IsValid() && !_sitting )
		{
			var dist = WorldPosition.Distance( _pendingSit.ApproachPoint() );
			var arrived = dist < 30f;
			var timedOut = Time.Now > _pendingSitTimeoutAt;
			if ( arrived || timedOut )
			{
				_pendingSit.Sit( this );
				_pendingSit = null;
			}
		}
		// Pending-sleep: walk-to-bed, snap-sleep
		if ( _pendingSleepBed.IsValid() && !_sitting )
		{
			var rest = _pendingSleepBed.RestPosition?.WorldPosition ?? _pendingSleepBed.WorldPosition;
			var dist = WorldPosition.Distance( rest );
			if ( dist < 40f || Time.Now > _pendingSitTimeoutAt )
			{
				_pendingSleepBed.Sleep( this );
				_pendingSleepBed = null;
			}
		}

		// Edge-triggered animgraph bools need clearing on the next frame.
		if ( _b_attack_clear_at > 0 && Time.Now > _b_attack_clear_at && _anim.IsValid() )
		{
			Model?.Set( "b_attack", false );
			_b_attack_clear_at = 0;
		}

		// Face emotion expire — clear the face_override enum back to NO_OVERRIDE (0).
		if ( _faceExpireAt > 0 && Time.Now > _faceExpireAt && _activeFaceParam != null && Model.IsValid() )
		{
			Model.Set( "face_override", 0 );
			_activeFaceParam = null;
			_faceExpireAt = 0;
		}

		// While holding, periodically pulse b_attack so we "use" the item (sip mug, etc).
		if ( _holding.IsValid() && _b_attack_next_pulse_at > 0 && Time.Now > _b_attack_next_pulse_at && Model.IsValid() )
		{
			Model.Set( "b_attack", true );
			_b_attack_clear_at = Time.Now + 0.05f;
			_b_attack_next_pulse_at = Time.Now + 3.5f;
		}

		// Pending-pickup: walk-to-prop, hold on arrival
		if ( _pendingHoldProp.IsValid() && _holding == null )
		{
			var dist = WorldPosition.Distance( _pendingHoldProp.WorldPosition );
			if ( dist < 40f || Time.Now > _pendingSitTimeoutAt )
			{
				_pendingHoldProp.Hold( this );
				_pendingHoldProp = null;
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
		// Commanding a move interrupts any kimodo clip taking over the body —
		// Stop() restores UseAnimGraph (so locomotion animates) and re-enables
		// the agent (which Play() had disabled for the takeover).
		if ( _kimodo.IsValid() && _kimodo.IsPlaying ) _kimodo.Stop();

		// Auto-stand from a seat OR wake from bed.
		if ( _sitting )
		{
			if ( _currentBed.IsValid() ) { _currentBed.Wake( this ); }
			else
			{
				var seat = _currentSeat.IsValid()
					? _currentSeat
					: GameObject.Parent?.Components?.Get<Sittable>( includeDisabled: true );
				if ( seat.IsValid() ) seat.Stand( this );
				else
				{
					GameObject.SetParent( null, true );
					EndSeated();
					if ( _agent.IsValid() ) { _agent.UpdatePosition = true; }
				}
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
	/// Walk to the seat's front approach point, then Sit() on arrival parents +
	/// begins the procedural seated alignment. Short fallback timeout.
	/// </summary>
	public void WalkToAndSit( Sittable seat )
	{
		if ( seat == null || _sitting ) return;
		WalkTo( seat.ApproachPoint() );
		_pendingSit = seat;
		_pendingSitTimeoutAt = Time.Now + 6f;
	}

	public void WalkToAndSleep( Bed bed )
	{
		if ( bed == null || _sitting ) return;
		var rest = bed.RestPosition?.WorldPosition ?? bed.WorldPosition;
		WalkTo( rest );
		_pendingSleepBed = bed;
		_pendingSitTimeoutAt = Time.Now + 8f;
	}

	/// <summary>Walk to a holdable, pick it up. Returns true if pickup will happen on arrival.</summary>
	public bool WalkToAndHold( HoldableProp prop )
	{
		if ( prop == null ) return false;
		WalkTo( prop.WorldPosition );
		_pendingHoldProp = prop;
		_pendingSitTimeoutAt = Time.Now + 8f;
		return true;
	}
	HoldableProp _pendingHoldProp;

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

		// Crude sentiment to face emotion. The LLM's vocabulary is consistent
		// enough that lexical hints work; this is placeholder for proper NLP.
		var lower = (text ?? "").ToLowerInvariant();
		if ( lower.Contains( "lovely" ) || lower.Contains( "delight" ) || lower.Contains( "beautiful" ) || lower.Contains( "wonderful" ) || lower.Contains( "magic" ) )
			ShowFaceEmotion( "smile", 0.8f, 4f );
		else if ( lower.Contains( "!" ) || lower.Contains( "oh," ) || lower.Contains( "ah," ) || lower.Contains( "ah!" ) )
			ShowFaceEmotion( "surprise", 0.6f, 3f );
		else if ( lower.Contains( "sad" ) || lower.Contains( "alas" ) || lower.Contains( "sigh" ) )
			ShowFaceEmotion( "sad", 0.6f, 3f );
		else if ( lower.Contains( "secret" ) || lower.Contains( "whisper" ) )
			ShowFaceEmotion( "smile", 0.4f, 3f );
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

	/// <summary>Set by Bed.Sleep/Wake (chair == null) for the lie-down pose, which
	/// manages its own placement. Sittable uses BeginSeated/EndSeated instead.</summary>
	public void SetSitting( bool sitting, Sittable seat = null )
	{
		_sitting = sitting;
		_currentSeat = sitting ? seat : null;
	}
	public bool IsSitting => _sitting;

	// ─── Seated: procedural alignment to any Sittable surface ───────────
	// Sittable.Sit/Stand call these. The pose is mapped onto the seat by reading
	// the POSED pelvis and shifting the root so the butt meets the surface, then
	// foot-IK plants the feet on the floor — no per-seat numeric tuning.

	/// <summary>Begin sitting on a Sittable: apply the canonical sit params and
	/// start the per-frame alignment (Character.OnUpdate → AlignToSeat).</summary>
	public void BeginSeated( Sittable seat, int sitPose )
	{
		_sitting = true;
		_currentSeat = seat;

		_pelvisBone = FindBone( "pelvis" );

		if ( Model.IsValid() )
		{
			Model.LocalRotation = Rotation.Identity;
			// Canonical citizen sit params (see engine BaseChair). b_grounded MUST
			// be true or the pose blends with an airborne stance (the "squat").
			Model.Set( "sit", sitPose );
			Model.Set( "b_grounded", true );
			Model.Set( "b_climbing", false );
			Model.Set( "b_swim", false );
			Model.Set( "duck", false );
		}
	}

	/// <summary>Stop sitting: clear foot-IK and the sit pose.</summary>
	public void EndSeated()
	{
		if ( Model.IsValid() )
			Model.Set( "sit", 0 );
		_sitting = false;
		_currentSeat = null;
	}

	/// <summary>Each frame while seated: face the seat and rigid-shift the root so
	/// the POSED pelvis lands on the seat surface. Self-correcting, so any
	/// model/height sits cleanly with no per-seat tuning. (The sit pose places
	/// the legs/feet; we deliberately don't foot-IK them — tracing the IK-solved
	/// feet each frame fed back into a leg spasm.)</summary>
	void AlignToSeat()
	{
		if ( !Model.IsValid() ) return;
		var sm = Model.SceneModel;
		if ( sm == null || _pelvisBone < 0 ) return;

		var t = _currentSeat.GetSeatTarget();
		WorldRotation = t.Facing;

		var pelvis = sm.GetBoneWorldTransform( _pelvisBone ).Position;
		var targetPelvis = t.Surface + Vector3.Up * t.PelvisOffset;
		WorldPosition += targetPelvis - pelvis;
	}

	int FindBone( string name )
	{
		var m = Model?.Model;
		if ( m == null ) return -1;
		for ( int i = 0; i < m.BoneCount; i++ )
			if ( m.GetBoneName( i ) == name ) return i;
		return -1;
	}

	/// <summary>Set by Bed.Sleep/Wake.</summary>
	public void SetOccupiedBed( Bed bed )
	{
		_currentBed = bed;
		_sitting = bed != null;
	}

	/// <summary>Set by HoldableProp.Hold/Drop.</summary>
	public void SetHolding( HoldableProp prop )
	{
		_holding = prop;
		_b_attack_next_pulse_at = prop != null ? Time.Now + 2.5f : 0;
	}

	/// <summary>Briefly show a citizen face emotion. The citizen animgraph
	/// exposes <c>face_override</c> as a CEnumAnimParameter; SkinnedModelRenderer.Set
	/// has NO string overload, so enum params must be set by their int index.
	/// Indices (from citizen.vanmgrph): 0 NO_OVERRIDE, 1 smile, 2 frown,
	/// 3 surprise, 4 sad, 5 angry, 6 eyes_closed.</summary>
	public void ShowFaceEmotion( string emotion, float strength = 1f, float durationSec = 3f )
	{
		if ( Model == null ) return;
		var idx = FaceEmotionIndex( emotion );
		if ( idx < 0 ) return;
		Model.Set( "face_override", idx );
		_activeFaceParam = emotion;
		_faceExpireAt = Time.Now + durationSec;
	}

	static int FaceEmotionIndex( string name ) => name switch
	{
		"NO_OVERRIDE" => 0,
		"smile"       => 1,
		"frown"       => 2,
		"surprise"    => 3,
		"sad"         => 4,
		"angry"       => 5,
		"eyes_closed" => 6,
		_             => -1,
	};

	// ─── New interactions (task #22) ────────────────────────────────
	// Lightweight "verbs" the agent can perform. Each starts a state machine
	// and clears it after a duration. Designed to compose with the animgraph
	// primitives we have today (face_override, holdtype, b_attack, sit).
	// Where applicable, these will look better once kimodo retargeting
	// (task #20) is correct — currently they use animgraph-only fallbacks.

	// Nap: in-place eyes-closed; ~6s by default.
	public void Nap( float durationSec = 6f )
	{
		if ( Model == null ) return;
		Log.Info( $"[Character {CharacterId}] nap ({durationSec:F1}s)" );
		ShowFaceEmotion( "eyes_closed", 1f, durationSec );
	}

	// Stretch: short arms-up holdtype cycle.
	public void Stretch( float durationSec = 2.5f )
	{
		if ( Model == null ) return;
		Log.Info( $"[Character {CharacterId}] stretch ({durationSec:F1}s)" );
		// Pose 5 = punch — closest "arms up" pose without kimodo.
		Model.Set( "holdtype", 5 );
		_stretchClearAt = Time.Now + durationSec;
	}
	float _stretchClearAt;

	// Dance: smile + occasional b_jump pulses.
	public void Dance( float durationSec = 5f )
	{
		Log.Info( $"[Character {CharacterId}] dance ({durationSec:F1}s)" );
		ShowFaceEmotion( "smile", 1f, durationSec );
		_danceEndAt = Time.Now + durationSec;
		_danceNextHopAt = Time.Now + 0.4f;
	}
	float _danceEndAt;
	float _danceNextHopAt;

	// Greet: walk near another character, face them, brief smile.
	public void Greet( Character target )
	{
		if ( target == null || !target.IsValid() ) return;
		Log.Info( $"[Character {CharacterId}] greet → {target.CharacterId}" );
		var toward = (target.WorldPosition - WorldPosition).Normal;
		WalkTo( target.WorldPosition - toward * 80f );
		LookAt( target.GameObject );
		ShowFaceEmotion( "smile", 1f, 4f );
		// b_attack pulse on arrival mimics a wave gesture (kimodo wave fixes this later).
		_greetPulseAt = Time.Now + 1.5f;
	}
	float _greetPulseAt;

	// Hug: walk close, face each other, big smile.
	public void Hug( Character target )
	{
		if ( target == null || !target.IsValid() ) return;
		Log.Info( $"[Character {CharacterId}] hug → {target.CharacterId}" );
		var toward = (target.WorldPosition - WorldPosition).Normal;
		WalkTo( target.WorldPosition - toward * 45f );
		LookAt( target.GameObject );
		ShowFaceEmotion( "smile", 1f, 5f );
	}

	// Talk: face target + one speech bubble.
	public void Talk( Character target, string line )
	{
		if ( target == null || !target.IsValid() ) return;
		LookAt( target.GameObject );
		ShowSpeechBubble( line, target.CharacterId, 3500 );
	}

	// Override OnUpdate-style ticks for stretch + dance happen via these:
	internal void TickInteractions()
	{
		if ( _stretchClearAt > 0 && Time.Now > _stretchClearAt && Model.IsValid() )
		{
			Model.Set( "holdtype", 0 );
			_stretchClearAt = 0;
		}
		if ( _danceEndAt > 0 )
		{
			if ( Time.Now > _danceEndAt )
			{
				_danceEndAt = 0;
				_danceNextHopAt = 0;
			}
			else if ( Time.Now > _danceNextHopAt && Model.IsValid() )
			{
				Model.Set( "b_jump", true );
				_danceNextHopAt = Time.Now + 0.7f;
			}
		}
		if ( _greetPulseAt > 0 && Time.Now > _greetPulseAt && Model.IsValid() )
		{
			Model.Set( "b_attack", true );
			_b_attack_clear_at = Time.Now + 0.05f;
			_greetPulseAt = 0;
		}
	}
}
