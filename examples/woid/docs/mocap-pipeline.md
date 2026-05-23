# Mocap → s&box Citizen Pipeline

How to take kimodo's SMPL-X mocap clips and play them on the s&box citizen character, end-to-end, self-hosted. No third-party server dependencies.

This doc covers the formats involved, where porting between them breaks, how the s&box import pipeline actually works, what open-source tools we can reuse, and the recommended build plan.

---

## 1. The three formats

### kimodo (SMPL-X mocap, local)

- **Runtime data**: JSON over the wire, shape `{fps, num_frames, bone_names[22], local_quats_wxyz[T][J][4], global_quats_xyzw[T][J][4], root_positions[T][3]}`. See `/home/paul/projects/kimodo/web/src/animator.js:40-59`.
- **Source data**: AMASS-style `.npz` files (motion capture corpus).
- **Viewer mesh**: `.glb` exports of SMPL-X parametric body (`web/public/models/smplx_neutral.glb`).
- **Skeleton**: 22-joint SMPL-X canonical. Bones: `pelvis`, `left_hip/knee/ankle/foot`, `right_hip/knee/ankle/foot`, `spine1/2/3`, `neck`, `head`, `left_collar/shoulder/elbow/wrist`, `right_collar/shoulder/elbow/wrist`. No fingers in this 22-joint subset.
- **Rest pose**: slight A-pose (arms ~10–15° down from horizontal). Confirmed by rest joint positions in `kimodo/web/src/rigs.js:9-32` (e.g. `left_shoulder.y = 0.085`, `left_wrist.y = 0.036`).
- **Coordinate system**: right-handed, Y-up, meters, feet anchored at Y=0.
- **Per-bone local axis convention**: kimodo's exporter constructs the rest such that **rest world orientation per joint is identity**. This is the key reason kimodo's `rest`-mode retargeting works as a simple post-multiply on rigs whose own rest also approximates identity world per joint.
- **Binding model**: Per-frame, per-joint quaternions. Two parallel streams — `local_quats_wxyz` (parent-local, SMPL-X consumers only) and `global_quats_xyzw` (world-space, retarget-ready). Root translation is a separate `root_positions` array.

### Mixamo (Adobe)

- **Format**: FBX 7.4 (ASCII by default from mixamo.com; binary on request). Single `.fbx` per character or per animation.
- **Animation packaging**: Embedded in the same FBX, as `AnimStack` → `AnimLayer` → `AnimCurveNode` per bone. The "Animation only (without skin)" download option is what you use for retargeting onto other rigs.
- **Skeleton**: ~52–65 bones (depends on rig). Prefix `mixamorig:` on every bone. Sample names: `mixamorig:Hips`, `mixamorig:Spine`, `mixamorig:Spine1`, `mixamorig:Spine2`, `mixamorig:LeftShoulder`, `mixamorig:LeftArm`, `mixamorig:LeftForeArm`, `mixamorig:LeftHand`, `mixamorig:LeftUpLeg`, `mixamorig:LeftLeg`, `mixamorig:LeftFoot`, `mixamorig:LeftToeBase`.
- **Rest pose**: strict T-pose (arms exactly horizontal).
- **Bone-local axis convention**: varies per rig, but after Blender's "Automatic Bone Orientation" pass typically Y-down-bone.
- **Coordinate system**: right-handed, Y-up, **centimeters** (Mixamo exports at cm scale).
- **Root motion**: baked into `mixamorig:Hips` translation unless "In Place" is toggled at download time. There is no separate root bone.

### s&box (Source 2 Citizen)

- **Compiled model**: `.vmdl_c` (the runtime asset). Authored in `.vmdl` (KV3 text), a ModelDoc node graph.
- **Compiled animation**: `.vanim_c`, internal-only. You never author this directly.
- **Source model assets**: FBX 7.4 binary. Citizen ships as `addons/citizen/Assets/models/citizen/citizen.fbx` plus LODs.
- **Source animation assets**: FBX (and DMX is accepted via Blender Source Tools). Citizen ships hundreds, e.g. `models/citizen/animations/face/Citizen@Eyes_Blink.fbx`, `models/citizen_human/animations/Human@Run_N_m.fbx`.
- **State machine**: `.vanmgrph` (compiled `.vanmgrph_c`). Optional — a clip can be played without an animgraph via `SceneModel.SetSequence`.

#### Citizen skeleton — ~84 bones

Main deform chain (already mapped in `kimodo/web/src/rigs.js:citizenMapping()`):
`pelvis`, `spine_0/1/2`, `neck_0`, `head`, `clavicle_L/R`, `arm_upper_L/R`, `arm_lower_L/R`, `hand_L/R`, `leg_upper_L/R`, `leg_lower_L/R`, `ankle_L/R`, `ball_L/R`.

Plus:

- **Twist bones (16)**: `arm_upper_{L,R}_twist{0,1}`, `arm_lower_{L,R}_twist{0,1}`, `leg_upper_{L,R}_twist{0,1}`, `leg_lower_{L,R}_twist{0,1}`. Children of the main limb bones.
- **IK targets**: `foot_{L,R}_IK_target`, `hand_{L,R}_IK_target`, `root_IK`, `hand_R_to_L_ikrule`, `hand_L_to_R_ikrule`.
- **Helpers**: `arm_elbow_helper_{L,R}`, `leg_knee_helper_{L,R}`, `leg_glute_helper_{L,R}`, `hold_{L,R}`, `neck_clothing`.
- **Face**: `eye_{L,R}`, `face_lid_*`, `ear_{L,R}`.
- **Fingers**: thumb 0–2, index/middle/ring meta/0/1/2 per hand. No pinky.

#### Citizen rest pose

A-pose, arms ~30° down from horizontal. Confirmed by the CARL `citizen_t_pose_calibrated_v1` preset, which rotates `arm_upper_L` by `+29.25°` on local Y to reach T-pose.

#### Units

Source assets are authored in **centimeters**. The vmdl applies `ModelModifier_ScaleAndMirror` at `scale=0.3937` to convert to engine inches (1 / 2.54).

#### Twist bone mechanism — critical

Twist bones are **not animated in the FBX**. They're driven at runtime by `AnimConstraintTiltTwist` / `AnimConstraintParent` nodes in `citizen_animconstraintlist.vmdl_prefab`. Example: `arm_upper_R_twist0` is the slave of `arm_upper_R` with weight `-0.65` on X; `arm_upper_R_twist1` is driven by an attachment `driver_arm_upper_R_twist1` at weight `1.75`.

**Implication for our pipeline**: emit animation FBX with twist bones at bind pose. The engine's constraint evaluation will fill them in. Manually animating them (as we currently do in the kimodo viewer) is a *runtime-viewer-only* workaround, not what you want in the baked FBX.

---

## 2. Cross-pipeline porting friction

### SMPL-X → Mixamo (kimodo already does this)

Working today. Implementation in `kimodo/web/src/animator.js`. Math (line 51):

```
Q_target_world(t) = Q_kimodo_world(t) · R_align[j]
Q_target_local(t) = Q_target_parent_world(t)⁻¹ · Q_target_world(t)
```

`R_align[j]` is precomputed per bone from the rest poses, with four modes (`rest`, `frame`, `direction`, `none`). For a skinned target, `drivable = target.skeleton.bones`. The `frame` mode (lines 243-251) builds an orthonormal basis from primary axis (bone → child) and a twist reference bone, constraining both direction and twist. Pelvis position is scaled by character height ratio and offset by `groundOffsetY`.

Solved gotchas: rest pose mismatch (SMPL-X A-pose vs Mixamo T-pose), three.js GLTFLoader name sanitization (`:` → `_`), parent-world inversion order, root translation offset.

### Mixamo → s&box Citizen ("known-working via sboxcool")

Dominated by **two rest-pose deltas plus naming**:

1. **Bone renaming.** `mixamorig:Hips` → `pelvis`, `mixamorig:LeftArm` → `arm_upper_L`, etc. CARL's authoritative table lives in `Assets/tools/citizen_retarget/backend/tools/blender/config/source_profiles/mixamo_humanoid.json`.

2. **T-pose → A-pose compensation.** CARL stores this as Euler-XYZ deltas in `Editor/CitizenRetarget/Data/citizen_arm_rest_compensation.json`:
   - `clavicle_L.Y = -72°`, `arm_upper_L.Y = -60°`, `arm_upper_L.Z = -21°`
   - `arm_lower_L.Y = -48°`, `arm_lower_L.Z = +9°`
   - `hand_L.Y = -46.5°`, `hand_L.Z = +277.5°`
   - Right side mirrored, except hand: `hand_R.Y = -81°`, `hand_R.Z = +9°`.

3. **Unit scale.** Export FBX in cm — the citizen vmdl handles the cm→inch conversion at compile.

4. **Twist bones.** Leave at bind.

5. **Root motion.** Either download Mixamo "In Place" or pass hips translation as pelvis translation, with `root_IK` left at origin.

### SMPL-X → Citizen directly (what we actually want)

This is harder than going through Mixamo only because there's less prior art — the math is simpler:

- Bone count: 22 SMPL-X joints → 22 main citizen bones. 1:1 via existing `citizenMapping()`.
- Spine joints align: SMPL-X `spine1/2/3` ↔ citizen `spine_0/1/2`.
- **Rest pose math.** SMPL-X rest is *already close to* citizen's A-pose, both have arms angled downward. The CARL Euler deltas don't apply directly — they bake in Mixamo's T-pose as source. Instead compute per bone: `R_correction = R_citizen_rest · R_smplx_rest⁻¹`. This is what `animator.js`'s `frame` mode does, runtime.
- Twist/helper/IK bones: emit at bind, let engine constraints drive.
- Fingers, face, IK targets: SMPL-X has none. Citizen requires they exist at bind (the constraint system reads them).

### The bone-axis problem we hit in the viewer

Kimodo's `rest`-mode formula `Q_target = Q_kimodo · R_rest` works when `R_rest ≈ identity` (which is true for SMPL-X and approximately true for Mixamo Adam). It produces visually-wrong motion when target rest deviates from identity *and* differs in bone-local axis convention — citizen uses local +X down the bone where SMPL-X uses local +Z, so even with correct world rotation the bone's visible direction interprets differently. The `frame` mode resolves this with explicit basis change; kimodo's author rejected it in our earlier runtime experiments but the math it does is exactly what's needed for SMPL-X → citizen.

---

## 3. s&box specifics

### ModelDoc / FBX import

ModelDoc is the editor for `.vmdl`. KV3 text under the hood:

- `RenderMeshList` wraps geometry from an FBX (binary preferred — Blender's exporter writes binary cleanly).
- `AnimationList` contains animation entries — `AnimFile` (single clip), `1DBlend`/`2DBlend` (parametric blend), `AnimSubtract` (additive delta), `Folder` (organization). `AnimFile` props: `source_filename`, `start_frame`, `end_frame`, `framerate`, `take`, `looping`, `delta`, `worldSpace`.
- `BoneMarkupList` tags bones for IK / LOD preservation / etc.
- `AnimConstraintList` defines procedural constraints (the twist driver system).
- `ModelModifierList` applies per-asset transforms.

### The "Base Model" pattern — our cleanest target

A vmdl can declare `base_model = "models/citizen/citizen.vmdl"` and add only an `AnimationList`. The compiled model inherits mesh, skeleton, constraints, attachments, bone markup — you ship only animations.

This is what our pipeline should produce: a `kimodo_anims.vmdl` with `base_model = citizen.vmdl` and an `AnimationList` of `AnimFile` references to per-clip FBXs. No mesh, no constraint duplication, automatically picks up future citizen updates.

### Do we need an animgraph?

No, not strictly. Animgraph is the state machine — common for player workflows but optional. For "play a kimodo clip on a citizen and call it a day", a Base Model vmdl + `SceneModel.SetSequence("clip_name")` is enough.

### Workshop / community models

Distributed as packaged sbox assets, typically a vmdl + skeleton + animgraph either using citizen's skeleton via `base_model` or shipping their own. Examples: Facepunch's "Citizen Assets" on sbox.game, community playermodel guides.

**Should we use a non-citizen base for mocap-friendly rest?** Probably not. The twist-bone constraint system and all the hand/face setup is what makes citizen look right; switching bases means redoing all of that. The CARL approach (keep citizen, fix the rest math at bake time) is more pragmatic.

### Why the twist setup is the way it is

Citizen's twist bones expect to be driven by `AnimConstraintTiltTwist` nodes after sampling. Writing to `*_twist*` in an FBX clip is overwritten by constraint evaluation at runtime. Same applies to `*_helper_*` and IK targets unless authoring IK explicitly.

---

## 4. Open-source tools to leverage

| Tool | URL | License | What it gives us |
|---|---|---|---|
| **CARL** (Citizen Animation Retargeting Library) | https://github.com/pumped-bit/citizen-animation-retargeting-library | MIT | **Highest-value reference.** Vendor the JSON mapping + arm rest compensation tables verbatim. Reuse `tools/blender_export_citizen_dmx.py` as Blender backend starting point. Windows PowerShell wrapper, but Python is portable. |
| **Blender Source Tools** (`io_scene_valvesource`) | http://steamreview.org/BlenderSourceTools/ | GPL | Required if exporting DMX. Headless Blender + this addon = safest Source 2-friendly DMX writer. |
| **Rokoko Studio Live for Blender** | https://github.com/Rokoko/rokoko-studio-live-blender | LGPL | CARL lists it as a dependency, but the actual retargeting script does its own thing via JSON. **Optional** for us. |
| **Meshcapade SMPL_blender_addon** | https://github.com/Meshcapade/SMPL_blender_addon | (their license) | Loads SMPL-H/X/SUPR `.npz` into Blender as rigged armature. Useful entry point on the kimodo side if we ever go via Blender for the source mesh. |
| **smplx_blender_addon** (Tübingen) | https://gitlab.tuebingen.mpg.de/jtesch/smplx_blender_addon | MPI research | Original SMPL-X Blender importer. Check license for commercial use. |
| **softcat477/SMPL-to-FBX** | https://github.com/softcat477/SMPL-to-FBX | Check repo | Pure-Python SMPL → FBX baker via Autodesk FBX SDK. Skips Blender if licensing works. |
| **BEDLAM 2.0 retargeting** | https://github.com/PerceivingSystems/bedlam2_retargeting | Research | SMPL-X → arbitrary humanoid via UE5 IK Retargeter. Reference only — UE5 dep is heavy. |
| **GMR** (General Motion Retargeting) | https://github.com/YanjieZe/GMR | Research | SMPL-X (AMASS/OMOMO) + FBX for humanoid robots. The SMPL-X → FBX path is reusable. |
| **EmptyBlueBox/BVH2SMPL** | https://github.com/EmptyBlueBox/BVH2SMPL | MIT | BVH ↔ SMPL conversion. Useful if we want a BVH intermediate. |
| **mwni/blender-animation-retargeting** | https://github.com/Mwni/blender-animation-retargeting | GPL-likely | Solid free generic Blender retarget addon (Rokoko alternative). |
| **igelbox/blender-retarget** | https://github.com/igelbox/blender-retarget | GPL | Smaller, simpler Blender retarget addon. |
| **Auto-Rig Pro Remap** | https://superhivemarket.com/products/auto-rig-pro/ | Commercial (€40) | Industry-standard. See `Shimingyi/ARP-Batch-Retargeting` for headless batch use. |
| **enziop/mixamo_converter** | https://github.com/enziop/mixamo_converter | Blender addon | Reference for Mixamo root-motion split idiom. |
| **ufbx** | https://github.com/ufbx/ufbx | MIT | Single-file C FBX **reader**. CARL bundles it. Use to inspect FBX without Autodesk SDK. Read-only. |
| **FBX2glTF** (godotengine fork) | https://github.com/godotengine/FBX2glTF | Apache 2.0 | FBX → glTF. Useful for the kimodo viewer side. |
| **pyfbx** (nannafudge) | https://github.com/nannafudge/pyfbx | MIT-ish | Pure-Python FBX parser. Read-mostly; writing FBX 7.4 binary by hand is risky. |
| **Blender's `io_scene_fbx`** | bundled with Blender | GPL | Most battle-tested FBX 7.4 writer. **Use this via headless Blender — don't write your own FBX.** |

---

## 5. Recommended approach

**Headless Blender pipeline targeting a Base-Model vmdl.** Pictorially:

```
kimodo JSON  →  Python writes a per-clip action plan
              →  Blender (headless, bpy)
                    Load citizen_REF.fbx as armature
                    Apply per-frame quaternions to mapped bones (citizenMapping())
                    Apply rest-pose compensation (computed, not CARL's Mixamo offsets)
                    Leave twist/helper/IK bones at bind
                    Bake action, export as FBX 7.4 binary
              →  Drop FBX into project/Assets/models/kimodo_clips/<name>.fbx
              →  Generate kimodo_anims.vmdl with base_model = "models/citizen/citizen.vmdl"
                  and one AnimFile node per clip
              →  s&box compiler turns it into .vmdl_c + .vanim_c on next addon reload
```

### Option matrix

| Option | Complexity | Runtime cost | Flexibility | Breaks at… |
|---|---|---|---|---|
| **A. Headless Blender bake → FBX per clip** | Medium (Blender install + ~200-300 LOC Python) | Free at game runtime (compiled vanim) | High — root motion, additive deltas, looping markers | Blender process management; FBX exporter axis quirks. **Best ROI.** |
| **B. Pure JS runtime retarget (current kimodo viewer)** | Low — already working | Per-frame in browser | Can't be used inside s&box runtime without porting math to C# | Twist bones (worked around), IK, fingers; no animgraph integration. |
| **C. C#-only in-engine retarget** | High — reimplement frame alignment in s&box runtime, manage bone arrays at 60Hz | ~22 quats/frame, negligible | High — kimodo can stream live | Bypasses animgraph blending; clothing constraints may mis-evaluate. |
| **D. Pre-bake fixed library** | Low | Free | Zero — fixed set | Loses kimodo's dynamic motion advantage. |
| **E. Dynamic per-clip bake on request** | Medium-high — Blender as a service (queue + worker) | 5-30s wall time per bake | High | Latency, infra. Justified only if catalog explodes. |

### v1 plan

1. **Vendor CARL data.** Pull `citizen.json` target profile and `citizen_arm_rest_compensation.json` into our Python package. These took CARL weeks to calibrate — don't redo.
2. **Skip Rokoko.** Build the Blender script as a single headless `bpy` module that mirrors CARL's `blender_export_citizen_dmx.py`: load citizen reference FBX, set up target armature, apply source action via JSON mapping + Euler compensation, export FBX. Read CARL's `retarget_clip_to_armature()` for exact transform composition order.
3. **kimodo → Blender bridge.** kimodo emits global quaternions per joint. Convert to bone-local in Python (`parent_world.invert() · world`, same math as `animator.js:334-340`), keyframe into Blender action, run the export. Avoid the intermediate "fake Mixamo FBX" step.
4. **Pose harness verification.** Write a kimodo test clip whose first frame is SMPL-X T-pose. Compile + confirm citizen stands in T-pose in editor preview. If yes, rest math is correct.
5. **Output layout.** One FBX 7.4 binary per clip into `addons/<addon>/Assets/models/kimodo/<clip_id>.fbx`, plus a generated `kimodo_anims.vmdl` with `base_model = "models/citizen/citizen.vmdl"` and one `AnimFile` per clip. Editor compiler does the rest.

### Open questions / uncertainties

- **DMX vs FBX for the bake.** CARL uses DMX (Blender Source Tools). Citizen's own animations are FBX. Both work in ModelDoc — pick FBX unless we hit a problem (one less Blender addon dep).
- **Linux headless Blender FBX round-trip into ModelDoc**: not yet verified. Run step 4 before scaling.
- CARL is Windows-only by installer, but its Python is portable. The `ual2_ufbx_helper.dll` is x64 Windows native — replaceable with `ufbx` compiled as a Linux `.so` if we ever need its FBX inspection.

---

## 6. Non-citizen models in s&box

Citizen is one model among many. Anything you bring through ModelDoc is on equal footing — citizen just happens to ship with the editor and have a finely-tuned animgraph + constraint set built around it. Here's what s&box gives you for arbitrary skeletons.

### The vmdl fields that matter

Every vmdl has three top-level "what kind of thing is this" fields (visible on every example we sampled):

- `model_archetype` — usually `""`. Used for special cases (cloth, particles); blank = standard model.
- `base_model_name` — if set, this vmdl *inherits* the referenced vmdl's mesh, skeleton, attachments, constraints, bone markup. The citizen ecosystem could use this to share the skeleton across staging/sfm variants, but in practice the shipped citizen variants leave it `""` and replicate the full graph. The hook exists.
- `anim_graph_name` — path to a `.vanmgrph`. Optional. Citizen sets this to `models/citizen/citizen.vanmgrph`. The default vmdl template (`templates/default.vmdl`) leaves it `""`.

A custom-imported skinned model is typically one of two shapes:

1. **Mesh-only vmdl (no skeleton, no anims).** Static prop. `RenderMeshList` only. Used for everything from `block_desert.vmdl` to `bomb.vmdl`. No animation pipeline involved.
2. **Skinned vmdl with its own skeleton + animations.** Whatever bone hierarchy your FBX has becomes the model's skeleton. Animations are referenced by `AnimationList` → `AnimFile` entries pointing at FBX clips. With or without an animgraph.

### How to play an animation on an arbitrary skinned model

Three patterns, in increasing order of complexity:

#### 1. Direct sequence playback (no animgraph)

`SkinnedModelRenderer.Sequence.Name = "clip_name"`. Set `UseAnimGraph = false` first. The clip name is whatever the vmdl's `AnimationList` entry named it. Confirmed in the engine source:

- `addons/tools/Code/Editor/VisemeEditor/Preview.cs:53` — `SceneObject.UseAnimGraph = false;` then plays a viseme directly.
- `addons/tools/Code/Inspectors/ModelInspector.cs:76` — `smr.UseAnimGraph = string.IsNullOrWhiteSpace( name );` — toggling animgraph on/off based on whether a sequence is being driven.

This is the simplest path. Works for any skinned model, citizen or not. **This is what our kimodo player already does** (`Code/MocapPlayer.cs` sets `UseAnimGraph = false` before driving bones).

#### 2. Animgraph state machine

Set `anim_graph_name` in the vmdl. Build a `.vanmgrph` in the editor. C# code sets parameters via `SkinnedModelRenderer.SetAnimParameter`. The graph blends/transitions between clips based on those parameters.

This is what player character workflows use (movement blend, idle/run/jump transitions). It's optional for one-off clip playback.

#### 3. Manual bone driving (ProceduralBone)

Mark a bone GameObject with `ProceduralBone`, set its `LocalRotation` each frame in code, and `SkinnedModelRenderer` reads those rotations after animgraph evaluates. Useful for runtime IK, look-at, mocap streaming. Combine with `UseAnimGraph = false` if you want full control without animgraph fighting you on unmapped bones.

This is the runtime path that would let kimodo stream clips into s&box without any FBX baking — but you lose blending and pay per-frame quaternion work in C#.

### Constraint and IK behavior

`AnimConstraintList` lives in the vmdl. The engine evaluates it *after* sampling animation, *before* rendering. So:

- Constraints run regardless of whether the source motion came from animgraph, sequence playback, or procedural bones.
- If your model has no twist bones / IK / helpers, you don't need any `AnimConstraintList` — just `AnimationList` and you're done.
- If you reuse citizen's skeleton (via `base_model_name = "models/citizen/citizen.vmdl"` or by replicating the prefab), you inherit citizen's constraint setup automatically.

### Practical implications for our problem

We have three live options for non-citizen targets:

- **Use citizen, bake against it.** What the doc above recommends. Inherits all the constraint goodness.
- **Use a custom skeleton that matches SMPL-X structure.** No twist bones, A-pose at rest, local axes match kimodo's convention. Then `Q · rest` retargeting works directly. The character would have to be rigged that way at source — Mixamo's Adam is close (no twist bones, rest is T-pose). A community workshop model rigged for SMPL-X would be ideal but those are rare.
- **Use a stripped-down citizen variant.** Make a new vmdl with `base_model_name = "models/citizen/citizen.vmdl"` but override the constraint list to be empty, then bake animations that *do* drive twist bones (as our viewer currently does). Loses the citizen-author-intended look but bypasses the rest-pose math.

The first option remains best for production; the second is useful if we eventually want a "pure" mocap test character that we control end-to-end.

### Where workshop / community models fit

Sbox.game distributes packaged addons. A community character is typically a vmdl + skeleton + animgraph + textures, packaged as an addon you mount. If the model uses citizen's skeleton (via `base_model_name`), all citizen animations work on it for free. If it uses its own skeleton, you ship animations specific to it. The publishing flow is the standard addon upload — no special "model registry" beyond sbox.game's asset browser.

For our pipeline: not directly useful unless someone has already published an SMPL-X-rigged character (we haven't found one in our research). For a long-term content strategy it's a viable distribution channel.

---

## Local ground-truth file references

- kimodo retargeter: `/home/paul/projects/kimodo/web/src/animator.js`, `/home/paul/projects/kimodo/web/src/rigs.js`
- Citizen vmdl + prefabs: `/home/paul/projects/sbox-public/game/addons/citizen/Assets/models/citizen/citizen.vmdl`, `…/prefabs/citizen_animationlist.vmdl_prefab`, `…/prefabs/citizen_animconstraintlist.vmdl_prefab`, `…/prefabs/citizen_bonemarkuplist.vmdl_prefab`
- Citizen FBX source: `/home/paul/.local/share/Steam/steamapps/common/sbox/addons/citizen/Assets/models/citizen/citizen.fbx` (binary 7.4)
- Reference animation FBX: `/home/paul/.local/share/Steam/steamapps/common/sbox/addons/citizen/Assets/models/citizen_human/animations/Human@Run_N_m.fbx` (ASCII 7.4)
- CARL upstream: https://github.com/pumped-bit/citizen-animation-retargeting-library
