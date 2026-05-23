using Sandbox;
using Sandbox.Citizen;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Woid;

/// <summary>
/// Plays a Kimodo SMPL-X motion clip on the citizen by retargeting joint
/// rotations onto citizen bones.
///
/// Clip JSON format (kimodo-animations/<id>.json):
///   { fps, num_frames, bone_names: string[22],
///     global_quats_xyzw: [T][J][4]  — world-space (x,y,z,w),
///     root_positions:    [T][3]     — meters, kimodo (Y-up) frame }
///
/// Retargeting math (from kimodo/src/lib/kimodo/animator.js):
///     Q_target_world(t)  =  Q_kimodo_world(t)  ·  Q_target_rest_world(j)
///     Q_target_local(t)  =  Q_target_parent_world(t).inverse() · Q_target_world(t)
///
/// We drive the citizen by enabling bone GameObjects (CreateBoneObjects=true),
/// flagging each mapped bone GO with `ProceduralBone`, and writing its
/// LocalRotation. The engine's ReadBonesFromGameObjects pipeline reads
/// procedural-bone local transforms each frame and feeds them into the
/// model via SetParentSpaceBone — same path animgraph uses internally,
/// so hierarchy is fully respected and unmapped bones (twists, fingers,
/// helpers) stay at their bind-pose locals.
/// </summary>
public sealed class MocapPlayer : Component
{
	[Property] public SkinnedModelRenderer Target { get; set; }
	[Property] public string DefaultClipPath { get; set; } = "kimodo/wave.json";
	[Property] public bool AutoPlayOnStart { get; set; } = false;
	[Property] public bool LoopPlayback { get; set; } = false;


	/// <summary>Scale factor on root translation. Kimodo gives meters; sbox uses inches (~39.37).
	/// For in-place clips (wave) use 0 so the figure doesn't drift.</summary>
	[Property, Range( 0f, 100f )] public float RootTranslationScale { get; set; } = 0f;

	public bool IsPlaying => _clip != null;
	public float PlaybackTime { get; private set; }

	Clip _clip;
	CitizenAnimationHelper _helper;

	// Per-mapped-bone state:
	readonly Dictionary<string, Rotation> _restWorldRotations = new();      // captured from bind pose
	readonly Dictionary<string, GameObject> _boneObjects = new();            // SMPL-X name → citizen bone GO
	readonly Dictionary<string, Rotation> _frameWorldRotations = new();      // computed per-frame

	// SMPL-X → citizen bone mapping. Citizen actual parent chain matches
	// SMPL-X chain for these 22 (verified via Model.Bones.Parent walk).
	static readonly Dictionary<string, string> CitizenMap = new()
	{
		{ "pelvis",          "pelvis"        },
		{ "left_hip",        "leg_upper_L"   },
		{ "right_hip",       "leg_upper_R"   },
		{ "spine1",          "spine_0"       },
		{ "left_knee",       "leg_lower_L"   },
		{ "right_knee",      "leg_lower_R"   },
		{ "spine2",          "spine_1"       },
		{ "left_ankle",      "ankle_L"       },
		{ "right_ankle",     "ankle_R"       },
		{ "spine3",          "spine_2"       },
		{ "left_foot",       "ball_L"        },
		{ "right_foot",      "ball_R"        },
		{ "neck",            "neck_0"        },
		{ "left_collar",     "clavicle_L"    },
		{ "right_collar",    "clavicle_R"    },
		{ "head",            "head"          },
		{ "left_shoulder",   "arm_upper_L"   },
		{ "right_shoulder",  "arm_upper_R"   },
		{ "left_elbow",      "arm_lower_L"   },
		{ "right_elbow",     "arm_lower_R"   },
		{ "left_wrist",      "hand_L"        },
		{ "right_wrist",     "hand_R"        },
	};

	// SMPL-X parent table (kimodo/src/lib/kimodo/rigs.js). null = root.
	static readonly Dictionary<string, string> SmplxParent = new()
	{
		{ "pelvis",         null         },
		{ "left_hip",       "pelvis"     }, { "right_hip",      "pelvis"        },
		{ "spine1",         "pelvis"     },
		{ "left_knee",      "left_hip"   }, { "right_knee",     "right_hip"     },
		{ "spine2",         "spine1"     },
		{ "left_ankle",     "left_knee"  }, { "right_ankle",    "right_knee"    },
		{ "spine3",         "spine2"     },
		{ "left_foot",      "left_ankle" }, { "right_foot",     "right_ankle"   },
		{ "neck",           "spine3"     },
		{ "left_collar",    "spine3"     }, { "right_collar",   "spine3"        },
		{ "head",           "neck"       },
		{ "left_shoulder",  "left_collar"  }, { "right_shoulder", "right_collar" },
		{ "left_elbow",     "left_shoulder"}, { "right_elbow",    "right_shoulder"},
		{ "left_wrist",     "left_elbow"   }, { "right_wrist",    "right_elbow"  },
	};

	// Iteration order: parents always come before children.
	static readonly string[] SmplxOrder = new[]
	{
		"pelvis",
		"left_hip", "right_hip", "spine1",
		"left_knee", "right_knee", "spine2",
		"left_ankle", "right_ankle", "spine3",
		"left_foot", "right_foot",
		"neck", "left_collar", "right_collar",
		"head", "left_shoulder", "right_shoulder",
		"left_elbow", "right_elbow",
		"left_wrist", "right_wrist",
	};

	protected override void OnStart()
	{
		base.OnStart();
		_helper = Components.Get<CitizenAnimationHelper>();
		if ( AutoPlayOnStart ) Play();
	}

	public void Play( string clipPath = null )
	{
		clipPath ??= DefaultClipPath;
		if ( !Target.IsValid() ) { Log.Warning( "[MocapPlayer] no Target" ); return; }

		string json;
		try { json = FileSystem.Mounted.ReadAllText( clipPath ); }
		catch ( Exception e ) { Log.Warning( $"[MocapPlayer] read {clipPath}: {e.Message}" ); return; }

		try { _clip = JsonSerializer.Deserialize<Clip>( json ); }
		catch ( Exception e ) { Log.Warning( $"[MocapPlayer] parse {clipPath}: {e.Message}" ); return; }

		if ( _clip?.global_quats_xyzw == null || _clip.bone_names == null )
		{
			Log.Warning( "[MocapPlayer] clip missing motion data" );
			_clip = null;
			return;
		}

		// Stop animgraph so it doesn't fight us. Disabling the helper alone is
		// NOT enough — the animgraph itself keeps running with default params
		// (which is an idle/breathing animation, not bind pose), and that
		// would overlay onto our procedural-bone overrides for unmapped bones
		// (twists, fingers), producing a contorted pose. UseAnimGraph=false
		// freezes the model at bind pose so our procedural bones are the
		// only active driver.
		if ( _helper.IsValid() ) _helper.Enabled = false;
		Target.UseAnimGraph = false;

		// Spawn bone GameObjects (no-op if already created).
		Target.CreateBoneObjects = true;

		// Reset bones to bind pose so rest captures are accurate.
		Target.SceneModel?.UpdateToBindPose();

		// Capture rest world rotation for each mapped bone, and pin down
		// the bone GameObject + flag it as procedural so the engine will
		// read its LocalRotation each frame.
		_restWorldRotations.Clear();
		_boneObjects.Clear();
		foreach ( var (smplx, citizen) in CitizenMap )
		{
			var go = Target.GetBoneObject( citizen );
			if ( !go.IsValid() ) continue;

			go.Flags |= GameObjectFlags.ProceduralBone;
			_boneObjects[smplx] = go;
			_restWorldRotations[smplx] = go.WorldRotation;
		}

		PlaybackTime = 0;
		Log.Info( $"[MocapPlayer] {clipPath} → {_clip.num_frames}f @ {_clip.fps:F1}fps " +
				  $"({_clip.num_frames / _clip.fps:F1}s), bound {_boneObjects.Count}/22 bones" );
	}

	public void Stop()
	{
		if ( _clip == null ) return;
		_clip = null;

		// Unflag bones so the engine stops reading our locals; reset to bind.
		foreach ( var go in _boneObjects.Values )
		{
			if ( go.IsValid() ) go.Flags &= ~GameObjectFlags.ProceduralBone;
		}
		_boneObjects.Clear();

		// Restore animgraph + helper so the citizen returns to normal idle/locomotion.
		if ( Target.IsValid() ) Target.UseAnimGraph = true;
		Target?.SceneModel?.UpdateToBindPose();
		if ( _helper.IsValid() ) _helper.Enabled = true;
	}

	protected override void OnUpdate()
	{

		if ( _clip == null ) return;
		if ( !Target.IsValid() ) { _clip = null; return; }

		PlaybackTime += Time.Delta;
		var frame = (int)Math.Floor( PlaybackTime * _clip.fps );

		if ( frame >= _clip.num_frames )
		{
			if ( LoopPlayback ) { PlaybackTime = 0; frame = 0; }
			else { Stop(); return; }
		}

		var clipBones = _clip.bone_names;
		var quats = _clip.global_quats_xyzw[frame];

		// Build SMPL-X name → quat index for this clip.
		// (Clip bone_names happens to match SmplxOrder, but we don't assume.)
		var quatByName = new Dictionary<string, float[]>( clipBones.Length );
		for ( int j = 0; j < clipBones.Length; j++ )
			quatByName[clipBones[j]] = quats[j];

		// Basis-change quaternion R: kimodo (three.js, character faces +Z toward
		// camera) → sbox (+X forward, +Y left, +Z up). With character facing the
		// camera in three.js standard convention:
		//   kimodo +X (character left)    → sbox +Y (left)
		//   kimodo +Y (up)                → sbox +Z (up)
		//   kimodo +Z (character forward) → sbox +X (forward)
		// Matrix M (columns = kimodo basis in sbox coords) has trace 0
		// → 120° rotation around axis (+1,+1,+1)/√3
		// Quaternion: (sin(60°)·axis_xyz, cos(60°)) = (0.5, 0.5, 0.5, 0.5).
		// (Verified at runtime: produced arm-going-DOWN with the previous sign;
		// the wave was actually pushing the arm toward the floor, not up.)
		// Try the original axis swap with proper conjugation. The previous
		// false starts were dominated by the animgraph noise; now that
		// UseAnimGraph is off, we can iterate on R cleanly.
		var R = new Rotation( 0.5f, -0.5f, -0.5f, 0.5f );
		var Rinv = R.Inverse;

		_frameWorldRotations.Clear();

		// Walk in parent-first order so child can read parent's new world.
		foreach ( var smplx in SmplxOrder )
		{
			if ( !_boneObjects.TryGetValue( smplx, out var go ) ) continue;
			if ( !_restWorldRotations.TryGetValue( smplx, out var restWorld ) ) continue;
			if ( !quatByName.TryGetValue( smplx, out var q ) ) continue;

			// Pelvis: skip the kimodo world rotation so the character keeps
			// facing wherever the GameObject is pointing. Wave/dance/etc. clips
			// often have slight pelvis drift that would otherwise turn the body.
			if ( smplx == "pelvis" )
			{
				_frameWorldRotations[smplx] = restWorld;  // identity-to-rest, no kimodo
				go.LocalRotation = Target.WorldRotation.Inverse * restWorld;
				continue;
			}

			// Kimodo quat as-is (no axis component swap — the conjugation by R
			// handles the basis change properly):
			var kimodoQ = new Rotation( q[0], q[1], q[2], q[3] );

			// Express kimodo's world rotation in sbox's coordinate system via
			// basis-change conjugation:  Q_sbox = R · Q_kimodo · R⁻¹
			var kimodoInSbox = R * kimodoQ * Rinv;

			// SMPL-X retargeting: Q_world = Q_kimodo_in_sbox · Q_target_rest_world
			// (Kimodo's canonical rest is identity per joint, so the rest pose
			// of the target rig comes from the citizen bind pose we captured.)
			var newWorld = kimodoInSbox * restWorld;
			_frameWorldRotations[smplx] = newWorld;

			// Find parent's new world rotation; if no mapped parent, use model root.
			var parentName = SmplxParent.TryGetValue( smplx, out var p ) ? p : null;
			Rotation parentWorld;
			if ( parentName != null && _frameWorldRotations.TryGetValue( parentName, out var pw ) )
				parentWorld = pw;
			else
				parentWorld = Target.WorldRotation;

			go.LocalRotation = parentWorld.Inverse * newWorld;

			// Pelvis root translation (kimodo Y-up meters → sbox Z-up inches).
			if ( smplx == "pelvis" && RootTranslationScale > 0f && _clip.root_positions != null )
			{
				var rp = _clip.root_positions[frame];
				var rootOffset = new Vector3( rp[2], -rp[0], rp[1] ) * RootTranslationScale;
				go.LocalPosition = go.LocalPosition + rootOffset;
			}
		}
	}

	protected override void OnDisabled()
	{
		base.OnDisabled();
		if ( _clip != null ) Stop();
	}

	// Console commands so we can drive playback via the SkillTrigger 'cmd'
	// trigger from outside the running game (skills don't have easy access
	// to game-scene components from editor context).
	[ConCmd( "kimodo_play", Help = "Start playing the default kimodo clip on every MocapPlayer in the scene" )]
	public static void CmdPlay()
	{
		var scene = Game.ActiveScene;
		if ( scene == null ) return;
		foreach ( var mp in scene.GetAllComponents<MocapPlayer>() ) mp.Play();
		Log.Info( "[MocapPlayer] kimodo_play triggered" );
	}

	[ConCmd( "kimodo_stop", Help = "Stop all kimodo playback" )]
	public static void CmdStop()
	{
		var scene = Game.ActiveScene;
		if ( scene == null ) return;
		foreach ( var mp in scene.GetAllComponents<MocapPlayer>() ) mp.Stop();
	}

	[ConCmd( "woid_shot", Help = "Render current scene to PNG via game-context camera. Hides Hud UI during capture so the character isn't blocked. Saves to sbox-data/<project>/captures/." )]
	public static void CmdShot( int width = 800, int height = 800 )
	{
		var scene = Game.ActiveScene;
		if ( scene == null ) { Log.Warning( "[woid_shot] no Game.ActiveScene" ); return; }
		var cam = scene.Camera;
		if ( !cam.IsValid() ) { Log.Warning( "[woid_shot] no scene camera" ); return; }

		// Hide every ScreenPanel (UI) so the camera render doesn't include
		// the AnimationPreview / BrainInspector / DebugBar panels in front of
		// the character. Restore enabled state after the capture.
		var screenPanels = scene.GetAllComponents<Sandbox.ScreenPanel>().ToList();
		var wasEnabled = screenPanels.Select( p => p.Enabled ).ToList();
		foreach ( var p in screenPanels ) p.Enabled = false;

		var rt = RenderTarget.GetTemporary( width, height );
		try
		{
			var prevSize = cam.CustomSize;
			cam.CustomSize = new Vector2( width, height );
			var rendered = cam.RenderToTexture( rt.ColorTarget );
			cam.CustomSize = prevSize;
			if ( !rendered ) { Log.Warning( "[woid_shot] render failed" ); return; }
			var bmp = rt.ColorTarget.GetBitmap( 0 );
			if ( bmp == null ) { Log.Warning( "[woid_shot] bitmap null" ); return; }
			var name = $"shot-{System.DateTime.Now:HHmmss-ffff}.png";
			FileSystem.Data.CreateDirectory( "captures" );
			using ( var s = FileSystem.Data.OpenWrite( $"captures/{name}" ) )
			{
				var png = bmp.ToPng();
				s.Write( png, 0, png.Length );
			}
			Log.Info( $"[woid_shot] saved data/captures/{name}" );
		}
		finally
		{
			rt?.Dispose();
			for ( int i = 0; i < screenPanels.Count; i++ ) screenPanels[i].Enabled = wasEnabled[i];
		}
	}

	class Clip
	{
		[JsonPropertyName( "fps" )]               public float fps { get; set; }
		[JsonPropertyName( "num_frames" )]        public int num_frames { get; set; }
		[JsonPropertyName( "bone_names" )]        public string[] bone_names { get; set; }
		[JsonPropertyName( "global_quats_xyzw" )] public float[][][] global_quats_xyzw { get; set; }
		[JsonPropertyName( "root_positions" )]    public float[][] root_positions { get; set; }
	}
}
