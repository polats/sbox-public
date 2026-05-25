using Sandbox;
using System.Collections.Generic;
using System.Linq;

namespace Woid;

/// <summary>
/// Masked upper-body overlay of a kimodo clip. Drives ONLY the upper-body bones
/// — spine_2 and everything above it (neck, head, clavicles, arms, hands) —
/// while the animgraph keeps driving the pelvis, legs and root. That is how the
/// citizen "holds an object and still walks/crouches": an upper-body layer over
/// locomotion. Here the layer's source is a kimodo reading clip.
///
/// Source of truth = the BAKED sequence, sampled via a hidden proxy renderer.
/// We play kim_read_newspaper on an off-screen SkinnedModelRenderer (UseAnimGraph
/// = false) so the engine poses it with the SAME correct retargeting the
/// full-body takeover used, then copy each upper-body bone's LOCAL rotation onto
/// the live character's procedural bones. We deliberately do NOT re-retarget the
/// raw kimodo JSON at runtime — that re-derivation got limb roll wrong (elbows
/// twisting inside out). Local rotations from the proxy are already correct and,
/// being parent-relative, compose with the live (facing-correct) torso for free.
///
/// Mechanism: flag each driven bone GameObject ProceduralBone and write its
/// LocalRotation; the engine reads those through the same pipeline the animgraph
/// uses, so unflagged bones keep animating. A 0..1 weight ramps the override in
/// and out — at weight 0 the written local equals the animgraph's own local, so
/// start/stop can't pop. UseAnimGraph stays TRUE and the Character/agent stay
/// enabled, so reading composes with walking and sitting.
/// </summary>
public sealed class ReadingLayer : Component
{
	[Property] public SkinnedModelRenderer Target { get; set; }
	[Property] public float BlendInTime { get; set; } = 0.25f;
	[Property] public float BlendOutTime { get; set; } = 0.3f;

	// Upper-body citizen bones to override (spine_2 up). Order is irrelevant —
	// we copy each bone's LOCAL rotation, already relative to its own parent.
	// Mask is built from these per use: torso+head always, plus one or both arms.
	static readonly string[] TorsoHead = { "spine_2", "neck_0", "head" };
	static readonly string[] ArmR = { "clavicle_R", "arm_upper_R", "arm_lower_R", "hand_R" };
	static readonly string[] ArmL = { "clavicle_L", "arm_upper_L", "arm_lower_L", "hand_L" };

	public bool IsActive => _active;

	class Driven
	{
		public string Name;
		public GameObject Go;
		public Sandbox.BoneCollection.Bone Bone;    // for clean animgraph reads
		public Sandbox.BoneCollection.Bone Parent;  // ditto (for the local blend)
	}

	bool _active;
	bool _stopping;
	bool _hold;           // true = sustained (hold last frame until End); false = one-shot (auto-end at clip end)
	float _weight;
	readonly List<Driven> _driven = new();
	SkinnedModelRenderer _proxy;

	/// <summary>Start the overlay from a baked sequence name.
	/// <paramref name="hold"/>: true holds the last frame until End() (newspaper);
	/// false plays once then auto-blends back to the animgraph (drink).
	/// <paramref name="bothArms"/>: drive both arms, else only the
	/// <paramref name="handedness"/> arm (1=right, 2=left) — torso+head always.
	/// Idempotent while already active.</summary>
	public void Begin( string sequenceName, bool hold = true, bool bothArms = true, int handedness = 1 )
	{
		if ( !Target.IsValid() ) { Log.Warning( "[ReadingLayer] no Target" ); return; }
		if ( _active && !_stopping ) return;

		Target.CreateBoneObjects = true;
		EnsureProxy();
		_proxy.Sequence.Name = sequenceName;
		_proxy.Sequence.Time = 0f;
		_proxy.Sequence.Looping = false;
		_proxy.Sequence.PlaybackRate = 1f;
		_proxy.GameObject.Enabled = true;

		BindBones( bothArms, handedness );
		_hold = hold;
		_active = true;
		_stopping = false;
		// keep any current _weight so a re-begin mid blend-out ramps from there
	}

	/// <summary>Stop the overlay (ramps out, then releases the bones + proxy).</summary>
	public void End()
	{
		if ( _active ) _stopping = true;
	}

	void EnsureProxy()
	{
		if ( _proxy.IsValid() ) return;
		var go = new GameObject( true, "read_proxy" );
		go.Parent = GameObject;
		var pr = go.Components.Create<SkinnedModelRenderer>();
		pr.Model = Target.Model;
		pr.UseAnimGraph = false;                                 // play the raw sequence
		pr.RenderType = ModelRenderer.ShadowRenderType.Off;      // no shadow
		HideProxy( pr );                                         // invisible, but still ticks + poses
		_proxy = pr;
	}

	// The proxy must keep ticking (to advance + pose the sequence) but never draw.
	// SceneObject.RenderingEnabled = false hides the model while it still updates.
	static void HideProxy( SkinnedModelRenderer pr )
	{
		if ( pr.SceneObject != null ) pr.SceneObject.RenderingEnabled = false;
	}

	void BindBones( bool bothArms, int handedness )
	{
		if ( _driven.Count > 0 ) return;
		var names = new List<string>( TorsoHead );
		if ( bothArms ) { names.AddRange( ArmR ); names.AddRange( ArmL ); }
		else names.AddRange( handedness == 2 ? ArmL : ArmR ); // one-handed: drive the holding arm only

		var bones = Target.Model.Bones;
		foreach ( var name in names )
		{
			var go = Target.GetBoneObject( name );
			if ( !go.IsValid() ) { Log.Warning( $"[ReadingLayer] bone {name} missing" ); continue; }
			var b = bones.GetBone( name );
			go.Flags |= GameObjectFlags.ProceduralBone;
			_driven.Add( new Driven { Name = name, Go = go, Bone = b, Parent = b.Parent } );
		}
	}

	void ReleaseBones()
	{
		foreach ( var d in _driven )
			if ( d.Go.IsValid() ) d.Go.Flags &= ~GameObjectFlags.ProceduralBone;
		_driven.Clear();
	}

	void Teardown()
	{
		ReleaseBones();
		if ( _proxy.IsValid() ) _proxy.GameObject.Destroy();
		_proxy = null;
		_active = false;
		_stopping = false;
	}

	protected override void OnUpdate()
	{
		if ( !_active || !_proxy.IsValid() ) return;

		// SceneObject can be null the frame the proxy is created; ensure it's hidden.
		if ( _proxy.SceneObject is { RenderingEnabled: true } ) HideProxy( _proxy );

		// One-shot (drink): once the clip has played through, blend back to the
		// animgraph. Sustained (newspaper) holds the last frame until End().
		if ( !_hold && !_stopping && _proxy.Sequence.IsFinished ) _stopping = true;

		var rate = _stopping
			? (BlendOutTime > 0f ? Time.Delta / BlendOutTime : 1f)
			: (BlendInTime  > 0f ? Time.Delta / BlendInTime  : 1f);
		_weight = _weight.Approach( _stopping ? 0f : 1f, rate );

		if ( _stopping && _weight <= 0f ) { Teardown(); return; }

		foreach ( var d in _driven )
		{
			if ( !d.Go.IsValid() ) continue;

			// Engine-retargeted reading pose for this bone (parent-relative).
			if ( !_proxy.TryGetBoneTransformLocal( d.Name, out var proxyTx ) ) continue;
			var readingLocal = proxyTx.Rotation;

			// The animgraph's own local for this bone, derived from clean animgraph
			// world reads (independent of our override). At weight 0 the written
			// local equals this, so the blend can't pop.
			Rotation animLocal = readingLocal;
			if ( Target.TryGetBoneTransformAnimation( ref d.Bone, out var bw )
			  && Target.TryGetBoneTransformAnimation( ref d.Parent, out var pw ) )
				animLocal = pw.Rotation.Inverse * bw.Rotation;

			d.Go.LocalRotation = Rotation.Slerp( animLocal, readingLocal, _weight );
		}
	}

	protected override void OnDisabled()
	{
		base.OnDisabled();
		if ( _active ) Teardown();
	}
}
