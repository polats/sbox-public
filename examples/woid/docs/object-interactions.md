# Object interactions

How a character interacts with world objects (sit, hold, sleep) in woid, the
reusable pattern, and how to author new interactables. Read this before adding
or tuning an interactable.

## Architecture

- **`WoidObject`** (in `ObjectRegistry.cs`) — the marker every interactable
  carries: `ObjectId` (string id) + `Type` (e.g. "mug", "seat"). It's what
  click/hover walk up the hierarchy to find.
- **Interaction components** sit alongside it on the same GameObject:
  `Sittable`, `HoldableProp`, `Bed`. Each owns its *act* logic.
- **`ClickInteract`** (on WoidRoot) — left-click raycasts, walks up to the
  `WoidObject`, and dispatches by component type to a `Character.WalkToAndX`
  method. Floor clicks just `WalkTo` the nearest character (which auto-stands).
- **`HoverHighlight`** (on Hud) — cursor raycast → outlines the hovered
  `WoidObject`. **Requires `CameraComponent.EnablePostProcessing = true` + a
  `Sandbox.Highlight` on the camera**, or nothing draws (silently).
- **`EffectInterpreter`** drives the *same* `Character` methods from the brain,
  so anything wired for click works for the LLM too. `ObjectRegistry` /
  `CharacterRegistry` map ids → components.

## The interaction pattern (use this for EVERY interactable)

1. **Approach** — walk to a point *near, not into,* the object (its collider
   blocks walking in). Derive it from the object, snap to the navmesh.
2. **Arrive** — only act once actually at the approach point (or stopped right
   by it), not while still walking in.
3. **Act** — suspend anything else that drives the transform (NavMeshAgent,
   kimodo takeover), then run an **eased** transition (never teleport/snap).
4. **Exit** — eased reverse (rise / release) *then* hand control back; don't
   warp-and-go.

`Sittable` is the reference implementation of all four.

## Sittable (reference)

Generic sit surface — chairs, benches, ledges, the ground. Two ways the seat
target (butt point + facing) is resolved, no numeric tuning either way:

- **`SeatMarker` set** → use it. **Chairs need this** because their collider box
  is taller than the seat, so a downward trace would hit the backrest top. Place
  a child GameObject at the seat surface, forward = the way the seated character
  faces (out of the chair).
- **No marker** → trace down onto the object's own collider and sit at the
  **front edge** along the facing direction, so legs hang off a solid box
  instead of sinking in. Good for benches / ledges / crates / ground.

Flow: walk to the front approach point → **turn in place** (idle) to face the
seat → blend the sit pose and **ease** the root so the *posed pelvis* lands on
the seat surface (read the pelvis bone each frame, rigid-shift — self-correcting
for any model/height). Standing reverses it: eased rise to the front, *then*
walk.

### Settings

| Setting | Where | Default | What it does |
|---|---|---|---|
| `SitPose` | Sittable | 1 | citizen sit enum (1 Chair, 2 ChairForward, 6 Ground, …) |
| `SeatMarker` | Sittable | none | seat-surface override; **required for chairs**, omit for flat surfaces (auto-trace) |
| `ApproachDistance` | Sittable | 30 | how far in front to stop before sitting (navmesh-snapped, so can't be unreachable) |
| `PelvisAboveSeat` | Sittable | 6 | pelvis height above the seat surface — a human constant, NOT per-object |
| `SeatTurnRate` | Character const | 9 | slerp speed turning to face the seat |
| `SeatSettleRate` | Character const | 7 | ease speed lowering in / rising out |
| `RiseDuration` | Character const | 0.55s | how long the stand-up rise takes before walking |

### Making a new sittable

- **Chair-like (backrest, solid seat):** add `WoidObject` + `Sittable`, add a
  child GameObject at the seat surface (forward = facing out), set it as
  `SeatMarker`. Done — no heights to tune.
- **Flat surface (bench / ledge / crate / ground):** add `WoidObject` +
  `Sittable`, leave `SeatMarker` empty. Auto-trace finds the top + front edge.
  Pick `SitPose` (e.g. Ground for the floor).
- The object needs a collider (so the approach is reachable / the trace hits).
  The collider should match the real shape — **don't oversize it** (an oversized
  box swallows the seat and the navmesh routes too wide).

## Key gotchas

- Sitting **teleport-parents** onto the seat, so the seat never needs to be
  navmesh-reachable — only the approach point does. Keep the approach *outside*
  the collider.
- **Suspend transform-drivers during an interaction** or they fight it: sitting
  freezes the NavMeshAgent; a kimodo clip disables the agent + Character
  (`SetTakeover`). The same will apply to any new interaction that moves the body.
- Citizen sit params: **`b_grounded = true` is required** (or it "squats"), and
  there is **no `b_sit`** parameter. See `rules/animations.md` in the skill.
- Hover outline: `EnablePostProcessing` + camera `Highlight` (see above).

## Holdables (HoldableProp) — current state + NEXT

`HoldableProp.Hold(c)` parents the prop to the `hold_R`/`hold_L` bone object and
sets the citizen `holdtype` params (`holdtype=4 HoldItem`, `holdtype_handedness`,
`holdtype_pose`, `holdtype_pose_hand`). `Drop` unparents + resets. Settings:
`Handedness` (1 R / 2 L / 0 both), `HoldtypePose` (0..5 grip width),
`HoldtypePoseHand` (grip tightness). The mug / hotdog / newspaper carry it.

**It has the rough edges sitting had before this pass:** `WalkToAndHold` walks
*into* the prop (`dist < 40`) and then snaps the grab — there's no approach-near,
no reach-down, and the prop teleports to the hand. The props also rest on the
**table at z≈30**, so the pickup should reach to that height, not the floor.

**Next session — apply the interaction pattern to pickup:**
1. Approach a point *beside* the prop (not into it), face it.
2. On arrival, play a reach/grab transition (the citizen has `holdtype` but no
   "reach down and grab" — likely needs a short kimodo grab clip, or at minimum
   ease the prop from its resting spot into the hand rather than snapping).
3. Attach to the hand bone (as today), keep `holdtype` for the carry pose.
4. Dropping should *place* the prop (on a surface / ease down) rather than
   teleport it to the floor.
Consider a generic `Interactable` base or shared approach/arrival helper on
`Character` so Sittable + HoldableProp + Bed don't each reimplement it.
