# Woid devlog

## 2026-05-24 — root motion, one character, sittables, camera + outline

### What shipped
- **Kimodo root motion → real movement + collision.** AnimFile `ExtractMotion`
  node in the wrapper vmdl exposes pelvis translation as
  `SkinnedModelRenderer.RootMotion`; the runtime consumes it to drive the
  transform (and a CharacterController for collision).
- **Consolidated to a single character, "Bob."** kimodo model + citizen
  animgraph for navmesh locomotion, kimodo clips as triggerable actions; brain,
  click-to-move and the button panel all target it.
- **Generic `Sittable` system.** Any surface is sittable with zero per-seat
  tuning: auto-trace (or a marker) finds the seat, and the character aligns its
  *posed pelvis* to the surface each frame. Works on chairs and a demo ledge.
- **Hold-RMB camera** (replaced the separate fly-mode toggle), **hover outline**
  on interactables, **seated → click-floor stands and walks.**

### Lessons (the expensive ones)

**1. Root motion is an engine feature you must opt into.** `RootMotion` is zero
unless the AnimFile has an `ExtractMotion` node. And you must never *rescale*
the extracted delta — it's calibrated to the foot animation, so clamping it made
the body advance slower than the legs swept and the planted foot skated backward
(a moonwalk). Drop garbage frames; don't scale real ones.

**2. "Backward kimodo" was integration, not the bake.** I burned a lot of time
re-checking the bake while the user kept saying it worked before. The decisive
clue was theirs: *it's correct in ModelDoc, wrong in the live scene.* That means
a live component is fighting it — here the `NavMeshAgent` (owns position) and the
velocity-facing I'd added (owns rotation) were both touching the transform during
playback. Fix: a kimodo clip is a **full takeover** — `SetTakeover` disables the
agent + Character for the clip's duration. One mechanism replaced three
overlapping guards (agent-suspend + an `IsPlaying` gate + a per-frame rotation
freeze). **Trust the user's eyes over your own measurements**; when they conflict,
your measurement is testing the wrong thing.

**3. NavMeshAgent.UpdateRotation aims at a path look-ahead**, so it spins the
character toward unreachable targets and snaps it around at arrival. Turn it off
and face the actual velocity yourself.

**4. Sitting, made systematic.** The squat was two things: the citizen sit pose
needs `b_grounded=true` (without it the animgraph blends in an airborne stance),
and `b_sit` isn't a real parameter. Beyond that, hand-tuning `SeatPosition`/
`SitHeight` per chair is the thing to eliminate — so instead we read the *posed
pelvis bone* and rigid-shift the root so the butt lands on the seat surface,
which self-corrects for any model/height. On solid boxes, place the seat at the
**front edge** so legs hang off instead of sinking in. Foot-IK was a trap:
reading the IK-solved foot each frame and re-targeting it fed back into a leg
spasm — removed it; the pose places the legs.

**5. Post-processing was silently off.** The hover outline (the engine's clean
`HighlightOutline` + camera `Highlight`) rendered *nothing* despite a correct
setup and a compiled shader — because the `CameraComponent` had
`EnablePostProcessing = false`. No post-process (outline, bloom, tonemap) runs
until that's true. Hours of "is the shader loading / is the outline registered"
when the whole stack was disabled at the camera.

### Process note
The fastest debugging move all session was **bisection**: disable everything,
confirm the core works, re-enable one component at a time. That's how we proved
the kimodo motion was fine in isolation and the regression lived in the
Character/agent integration.
