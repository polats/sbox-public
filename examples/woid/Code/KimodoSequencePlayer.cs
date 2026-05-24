using System;
using System.Collections.Generic;
using System.Linq;
using Sandbox;

namespace Woid;

/// <summary>
/// Plays kimodo-baked animation sequences on a SkinnedModelRenderer whose
/// Model is a kimodo wrapper vmdl (base_model = citizen, AnimationList of
/// AnimFile entries). Drives playback via Sequence.Name with UseAnimGraph
/// disabled — our wrapper vmdl has no animgraph.
///
/// Root-motion propagation (PropagateRootMotion = true): the wrapper vmdl's
/// AnimFile entries carry an ExtractMotion node (root_bone_name = pelvis,
/// horizontal only), so the engine pulls the pelvis's ground-plane translation
/// out of the bone and exposes it as SkinnedModelRenderer.RootMotion — a
/// per-frame Transform delta in the model's local space. Each tick we rotate
/// that delta into world space and feed it through CharacterController.Move()
/// so the capsule sweeps against geometry and stops on collision. Vertical bob
/// is left in the mesh (extract_tz = false), so jumps still bob in place.
/// </summary>
public sealed class KimodoSequencePlayer : Component
{
	[Property] public SkinnedModelRenderer Target { get; set; }

	/// <summary>Move the GameObject by the animation's extracted root motion each tick.</summary>
	[Property] public bool PropagateRootMotion { get; set; } = true;

	/// <summary>If true, zero any vertical component of the root-motion delta.</summary>
	[Property] public bool HorizontalOnly { get; set; } = true;

	/// <summary>
	/// Optional CharacterController on the same GameObject. If present, root-motion
	/// goes through CharacterController.Move() so the character sweeps against world
	/// geometry and stops on collision. Without it we fall back to writing
	/// WorldPosition directly (no collision).
	/// </summary>
	[Property] public CharacterController Controller { get; set; }

	/// <summary>
	/// Reject (skip) any frame whose implied ground speed exceeds this (cm/s).
	/// This is a garbage filter ONLY — the first frame after a sequence switch
	/// reports a bogus ~6000 cm/s delta. We must never *clamp* (rescale) real
	/// motion: the root translation is calibrated to the foot animation, so
	/// scaling it down makes the body advance slower than the legs sweep and
	/// the planted foot skates backward — a moonwalk. So the threshold sits well
	/// above any real locomotion speed and we drop, not rescale, outliers.
	/// </summary>
	[Property] public float RejectSpeedAbove { get; set; } = 1500f;

	/// <summary>Current sequence name. Set via Play(name).</summary>
	[Property] public string CurrentSequence { get; private set; } = "";

	public const string KIMODO_PREFIX = "kim_";

	// Skip root-motion propagation for one tick after Play(): the first
	// RootMotion read straddles the sequence switch and reports garbage.
	bool _warmup;

	// A kimodo clip is a full-body takeover — exactly like the standalone player
	// and ModelDoc, where the sequence drives every bone and the extracted root
	// translation drives the transform. In the live scene two components fight
	// that: the NavMeshAgent (owns position) and Character (owns facing + feeds
	// the animgraph). Either one reverses/moonwalks the clip's motion — which is
	// why ModelDoc (having neither) plays it right but the live character didn't.
	// Rather than gate each one per-frame (fragile — ordering races), we SUSPEND
	// both for the clip's duration. Disabled components don't run, so nothing but
	// us can touch the transform: one mechanism, no per-frame gates, no rotation
	// locks. We remember each one's prior enabled state so we only restore what
	// we actually turned off.
	NavMeshAgent _agent;
	Character _character;
	bool _agentWasEnabled, _characterWasEnabled;

	/// <summary>True while a kimodo clip is taking over the character.</summary>
	public bool IsPlaying => !string.IsNullOrEmpty( CurrentSequence );

	void SetTakeover( bool active )
	{
		_agent     ??= Components.Get<NavMeshAgent>();
		_character ??= Components.Get<Character>();

		if ( active )
		{
			if ( _agent.IsValid() && _agent.Enabled )
			{
				_agentWasEnabled = true;
				_agent.Stop();
				_agent.Enabled = false;
			}
			if ( _character.IsValid() && _character.Enabled )
			{
				_characterWasEnabled = true;
				_character.Enabled = false;
			}
		}
		else
		{
			if ( _agent.IsValid() && _agentWasEnabled )
			{
				_agent.Enabled = true;
				_agent.SetAgentPosition( GameObject.WorldPosition );  // pick up where the clip left us
				_agentWasEnabled = false;
			}
			if ( _character.IsValid() && _characterWasEnabled )
			{
				_character.Enabled = true;
				_characterWasEnabled = false;
			}
		}
	}

	/// <summary>Our kimodo-baked clips (prefixed kim_).</summary>
	public IEnumerable<string> ListKimodoSequences() => ListSequences( true );

	/// <summary>Inherited base_model clips (everything not prefixed kim_).</summary>
	public IEnumerable<string> ListOtherSequences() => ListSequences( false );

	IEnumerable<string> ListSequences( bool kimodoOnly )
	{
		if ( !Target.IsValid() ) yield break;
		foreach ( var name in Target.Sequence.SequenceNames )
		{
			if ( string.IsNullOrEmpty( name ) ) continue;
			var isKim = name.StartsWith( KIMODO_PREFIX );
			if ( kimodoOnly == isKim ) yield return name;
		}
	}

	public void Play( string sequenceName )
	{
		if ( !Target.IsValid() )
		{
			Log.Warning( "[KimodoSequencePlayer] no Target SkinnedModelRenderer" );
			return;
		}
		Target.UseAnimGraph = false;
		Target.Sequence.Name = sequenceName;
		Target.Sequence.Time = 0f;
		Target.Sequence.Looping = true;
		Target.Sequence.PlaybackRate = 1f;
		CurrentSequence = sequenceName;
		_warmup = true;
		SetTakeover( true );
	}

	public void Stop()
	{
		if ( Target.IsValid() )
			Target.UseAnimGraph = true;
		CurrentSequence = "";
		SetTakeover( false );
	}

	protected override void OnUpdate()
	{
		if ( string.IsNullOrEmpty( CurrentSequence ) || !PropagateRootMotion || !Target.IsValid() )
			return;

		// Engine-extracted root motion: per-frame translation delta in the
		// model's local space (horizontal only — vertical was left in the mesh
		// by the ExtractMotion node's extract_tz = false).
		var localDelta = Target.RootMotion.Position;
		if ( HorizontalOnly ) localDelta.z = 0f;
		if ( localDelta.IsNearZeroLength ) return;

		// Drop the first tick after Play() (sequence-switch garbage delta).
		if ( _warmup ) { _warmup = false; return; }

		// Drop frames whose implied speed is absurd (sequence-switch garbage).
		// We DROP, never rescale — rescaling desyncs the body from the foot
		// animation and produces a moonwalk. Real locomotion passes untouched.
		if ( Time.Delta > 0f && localDelta.Length / Time.Delta > RejectSpeedAbove )
			return;

		// Local (model-space) → world using the character's current facing.
		var worldDelta = Target.WorldRotation * localDelta;

		if ( Controller.IsValid() )
		{
			// MoveTo sweeps from current position to target and slides/stops on
			// collision. useStep:false — no step-up, so the character can't climb
			// or punch through obstacles it's running into (it just stops). The
			// run animation keeps cycling in place against the wall, which reads
			// fine. Velocity is informational for downstream code.
			var before = GameObject.WorldPosition;
			Controller.MoveTo( before + worldDelta, false );
			Controller.Velocity = Time.Delta > 0f ? (GameObject.WorldPosition - before) / Time.Delta : Vector3.Zero;
		}
		else
		{
			GameObject.WorldPosition += worldDelta;
		}
	}

	[ConCmd( "kimodo_play_seq", Help = "Play a kimodo sequence by name on every KimodoSequencePlayer" )]
	public static void Cmd_Play( string sequenceName )
	{
		var scene = Game.ActiveScene;
		if ( scene == null ) return;
		foreach ( var p in scene.GetAllComponents<KimodoSequencePlayer>() ) p.Play( sequenceName );
	}

	[ConCmd( "kimodo_stop_seq" )]
	public static void Cmd_Stop()
	{
		var scene = Game.ActiveScene;
		if ( scene == null ) return;
		foreach ( var p in scene.GetAllComponents<KimodoSequencePlayer>() ) p.Stop();
	}
}
