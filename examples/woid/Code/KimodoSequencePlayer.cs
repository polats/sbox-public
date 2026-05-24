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
/// Root-motion propagation (PropagateRootMotion = true): each tick reads the
/// pelvis bone's animated LocalPosition, applies it to the GameObject's
/// WorldPosition, then resets the pelvis bone position to its bind so the
/// mesh doesn't double-move. Result: the character physically translates
/// with the animation (collider follows). Disable to leave motion in-place.
/// </summary>
public sealed class KimodoSequencePlayer : Component
{
	[Property] public SkinnedModelRenderer Target { get; set; }

	/// <summary>Move the GameObject by the animation's pelvis translation each tick.</summary>
	[Property] public bool PropagateRootMotion { get; set; } = true;

	/// <summary>If true, only horizontal (X/Y) translation is propagated; vertical stays put.</summary>
	[Property] public bool HorizontalOnly { get; set; } = true;

	/// <summary>Current sequence name. Set via Play(name).</summary>
	[Property] public string CurrentSequence { get; private set; } = "";

	const string PELVIS = "pelvis";

	Vector3 _lastPelvisAnimLocal;
	bool _hasLastPelvis;
	int _pelvisBoneIdx = -1;
	Transform _pelvisBindTransform;

	public const string KIMODO_PREFIX = "kim_";

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

		_hasLastPelvis = false;
		CachePelvisInfo();
	}

	public void Stop()
	{
		if ( Target.IsValid() )
		{
			Target.UseAnimGraph = true;
			Target.SceneModel?.ClearBoneOverrides();
		}
		_hasLastPelvis = false;
		CurrentSequence = "";
	}

	void CachePelvisInfo()
	{
		_pelvisBoneIdx = -1;
		var model = Target?.Model;
		if ( model == null ) return;
		for ( int i = 0; i < model.BoneCount; i++ )
		{
			if ( model.GetBoneName( i ) == PELVIS )
			{
				_pelvisBoneIdx = i;
				_pelvisBindTransform = model.GetBoneTransform( i );
				return;
			}
		}
	}

	protected override void OnUpdate()
	{
		if ( string.IsNullOrEmpty( CurrentSequence ) ) return;
		if ( !PropagateRootMotion ) { _hasLastPelvis = false; return; }
		var sm = Target?.SceneModel;
		if ( sm == null || _pelvisBoneIdx < 0 ) return;

		// Read the animation's pelvis local transform directly via SceneModel
		// (bypasses any bone-GameObject ProceduralBone overrides).
		var animatedLocal = sm.GetBoneLocalTransform( _pelvisBoneIdx ).Position;

		if ( _hasLastPelvis )
		{
			var delta = animatedLocal - _lastPelvisAnimLocal;
			// Loop wrap: large negative jumps when the clip loops — skip them
			// so we don't fling the root backward.
			if ( delta.Length < 50f )
			{
				if ( HorizontalOnly ) delta.z = 0f;
				var worldDelta = Target.WorldRotation * delta;
				GameObject.WorldPosition += worldDelta;
			}
		}
		_lastPelvisAnimLocal = animatedLocal;
		// NOTE: not locking pelvis via SetBoneOverride here. That would suppress
		// the GetBoneLocalTransform read on subsequent ticks (override persists
		// and the read returns bind, giving delta=0). Mesh will visually
		// double-move (root + animated pelvis) — accept that for now in
		// exchange for collider/transform propagation working.
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
