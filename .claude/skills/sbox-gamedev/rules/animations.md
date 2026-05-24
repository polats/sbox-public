# Citizen + animgraph

Learnings from building the parkour runner. Read this before doing any
character-animation work.

## The citizen model

`models/citizen_human/citizen_human_male.vmdl` is 12×48×69 inches
(probed with `sbox-model-info`). It's the default human rig and comes
with a full animgraph and **75 animgraph parameters** you can drive from
a `SkinnedModelRenderer`.

Place it with a SkinnedModelRenderer (not ModelRenderer — the latter
won't play animations):

```csharp
var smr = playerGo.Components.Create<SkinnedModelRenderer>();
smr.Model = Model.Load("models/citizen_human/citizen_human_male.vmdl");
smr.UseAnimGraph = true;   // verify this is on by default; flip if T-posed
```

## Discovering the parameters

`Model.AnimGraphParameters` gives you the full list:

```bash
sbox-eval '
  var smr = SceneEditorSession.Active.Scene.GetAllComponents<SkinnedModelRenderer>().First();
  return smr.Model.AnimGraphParameters?.Select(p => $"{p.Name} ({p.Type})").ToList();
'
```

## Parameters you actually drive

You don't need to set all 75. Drive these and you have a working
locomotion citizen:

| Parameter | Type | Drive with |
|---|---|---|
| `move_groundspeed` | float | current horizontal speed (length of horizontal velocity) |
| `move_speed`       | float | same — sometimes wired separately by the animgraph |
| `move_direction`   | float | degrees (0..360) of horizontal velocity relative to **local citizen forward** |
| `move_x`           | float | local-space velocity X (forward component) |
| `move_y`           | float | local-space velocity Y (sideways component) |
| `move_z`           | float | vertical velocity (use for falling poses) |
| `wish_direction`   | float | same as move_direction but for **intended** direction (input pre-friction) — drives lean/anticipation |
| `wish_speed`       | float | intended speed |
| `wish_groundspeed` | float | usually equal to move_groundspeed |
| `wish_x` / `wish_y` / `wish_z` | float | local-space wish velocity |
| `b_grounded`       | bool  | true when on ground |
| `b_jump`           | bool  | **edge-triggered** — set true for ONE frame on jump, then false |
| `b_swim`           | bool  | false unless swimming |
| `b_climbing`       | bool  | false unless mantling |
| `b_noclip`         | bool  | false |
| `duck`             | float | 0..1 crouch blend |
| `move_rotationspeed` | float | degrees/sec for turn-in-place |

`move_direction` and `move_x/y` together drive the 8-way locomotion blend.
Compute local-space velocity correctly:

```csharp
var rot = GameObject.WorldRotation;
var vel = controller.Velocity;
var localFwd = rot.Forward.Dot( vel );   // signed
var localRight = rot.Right.Dot( vel );

smr.Set( "move_groundspeed", new Vector3(vel.x, vel.y, 0).Length );
smr.Set( "move_x", localFwd );
smr.Set( "move_y", localRight );
smr.Set( "move_z", vel.z );
smr.Set( "move_direction", MathF.Atan2( localRight, localFwd ).RadianToDegree() );
smr.Set( "b_grounded", controller.IsOnGround );
```

Mirror those into `wish_*` if you want crisper anticipation animations.

## Citizen face emotions: one enum, not per-emotion floats

The citizen animgraph exposes face emotion as a **single enum** param,
not as a bag of per-expression floats. Setting `Model.Set("smile", 0.8f)`
silently no-ops because no such param exists.

```csharp
// correct — enum is set by INT INDEX, not name:
Model.Set( "face_override", 1 );  // 1 = smile
// clear:
Model.Set( "face_override", 0 );  // 0 = NO_OVERRIDE
```

**`SkinnedModelRenderer.Set` has no string overload.** The overloads are
`Vector3 / int / float / bool / Rotation` (see
`engine/Sandbox.Engine/Scene/Components/Render/SkinnedModelRenderer.Parameters.cs`).
The commented-out `Set(string, Enum)` was disabled too. For enum
`CEnumAnimParameter`s, look up the index in the `.vanmgrph` (values listed
in declaration order, starting at 0) and pass it as an int.

`face_override` indices (from `citizen.vanmgrph`): `0 NO_OVERRIDE,
1 smile, 2 frown, 3 surprise, 4 sad, 5 angry, 6 eyes_closed`. Same
ordering on the sausage `citizen.vmdl` and human `citizen_human_*.vmdl`.

The graph is on/off — there's no strength axis. If you need partial
smiles or composite expressions, you'd drive the underlying FACS morphs
via `SkinnedModelRenderer.Morphs.Set("lip_corner_puller_l", v)` etc.
(see the `FacePoseEditor` in sandbox/Code/UI/FacePoser for the full
morph list). For agent dialogue sentiment, `face_override` is enough.

To discover similar enum params in any model, grep its `.vanmgrph` for
`CEnumAnimParameter` — the value list is right under each one.

## The jump trigger pattern

`b_jump` is edge-triggered. The animgraph fires the jump animation on the
rising edge. So set it true for one frame, then back to false:

```csharp
if ( Input.Pressed( "Jump" ) && controller.IsOnGround )
{
    controller.Velocity = controller.Velocity.WithZ( 350f );
    smr.Set( "b_jump", true );
    _resetJumpNextFrame = true;
}
else if ( _resetJumpNextFrame )
{
    smr.Set( "b_jump", false );
    _resetJumpNextFrame = false;
}
```

Don't leave it true permanently or the animgraph loops the jump.

## Sitting: the param set (and why it "squats")

To seat the citizen, match the engine's `BaseChair.UpdatePlayerAnimator` set —
not just `sit`:

```csharp
smr.Set( "sit", (int)pose );          // 1 Chair, 2 ChairForward, 6 Ground, ...
smr.Set( "sit_offset_height", h*12f ); // h in -1..1
smr.Set( "b_grounded", true );         // REQUIRED — without it the pose blends
smr.Set( "b_climbing", false );        // with an airborne stance and the
smr.Set( "b_swim", false );            // character "squats" above the seat
smr.Set( "duck", false );
```

- **There is no `b_sit` parameter** — setting it is a silent no-op.
- The engine pattern parents the character's *origin* to a seat node; the sit
  pose then places the body. To make it land on **any** seat with no per-chair
  tuning, read the posed `pelvis` bone (`SceneModel.GetBoneWorldTransform`) and
  rigid-shift the root so the pelvis meets the seat surface — self-correcting.
- **Don't foot-IK the seated legs by tracing the IK-solved foot each frame** —
  reading the solved foot and re-targeting it feeds back into a leg spasm.

## Root motion from a sequence (ExtractMotion)

`SkinnedModelRenderer.RootMotion` (a per-frame local-space `Transform` delta) is
**zero unless the animation has an `ExtractMotion` node** (in the AnimFile, or
the citizen `*_ani_process_*` prefabs). It works with `UseAnimGraph = false` +
`CurrentSequence` too. Consume it to move a character:

```csharp
var worldDelta = renderer.WorldRotation * renderer.RootMotion.Position;
controller.MoveTo( WorldPosition + worldDelta, false ); // or WorldPosition += ...
```

**Never rescale/clamp the delta** — it's calibrated to the foot animation, so
shrinking it makes the body lag the leg sweep and the planted foot skates
backward (a moonwalk). Drop garbage frames (e.g. the first frame after a
sequence switch reports a huge bogus delta), don't scale real ones. And if the
same GameObject has a `NavMeshAgent` / other transform-driver, **suspend them**
while root motion owns the transform, or they fight and reverse the motion.

## CharacterController patterns

`CharacterController` is the engine's "move with sliding against walls,
respect step height, capsule collision" helper. **It is NOT a `Collider`**
— it has its own collision system. Implications:

- `ITriggerListener.OnTriggerEnter` does **not** fire when a CharacterController
  enters a trigger. The CC isn't seen by the trigger's collision system.
- Detect "player reached goal" with a **per-frame distance check** to the
  goal's position, not a trigger listener:

  ```csharp
  if ( Vector3.DistanceBetween( GameObject.WorldPosition, goalPos ) < 50f )
      OnGoalReached();
  ```

- Or attach a separate `BoxCollider { IsTrigger = true }` parented to the
  same GameObject as the CharacterController and listen on that — but the
  per-frame distance check is simpler.

## Scene-file gotchas for CharacterController

`CharacterController` in the scene JSON does **not** like an empty
`IgnoreLayers` field. Setting `"IgnoreLayers": {}` makes the scene fail
to load (TagSet deserialization). Either omit the field or write
`"IgnoreLayers": ""` (empty string).

## Footsteps without animation events

If you don't want to author animation-event triggers for footstep sounds,
just time them in your Player.OnUpdate:

```csharp
private TimeSince _sinceLastStep = 0;
…
var stride = isSprinting ? 0.28f : 0.42f;
if ( controller.IsOnGround && controller.Velocity.LengthSquared > 100f
     && _sinceLastStep >= stride )
{
    Sound.Play( footstepEvent, GameObject.WorldPosition );
    _sinceLastStep = 0;
}
```

Tune `stride` to look right against the actual run animation cycle.

## First-person camera + mouse look

For an FPS, split yaw and pitch differently:
- **Yaw goes on the player body's WorldRotation** (so movement is relative
  to where you're looking).
- **Pitch goes only on the camera child's WorldRotation** (so the body
  doesn't lean).

```csharp
private Angles _eyeAngles;

protected override void OnUpdate()
{
    _eyeAngles.yaw   -= Input.MouseDelta.x * 0.04f;
    _eyeAngles.pitch =  Math.Clamp( _eyeAngles.pitch + Input.MouseDelta.y * 0.04f, -89f, 89f );

    // Body yaw — keeps strafe/forward axis consistent with view
    GameObject.WorldRotation = Rotation.FromYaw( _eyeAngles.yaw );

    // Camera pitch only — body stays upright
    camera.WorldRotation = Rotation.From( _eyeAngles.pitch, _eyeAngles.yaw, 0 );

    Mouse.Visible = false;   // capture cursor
}
```

`Input.MouseDelta` is already-DPI-adjusted; tune the sensitivity (`0.04f`)
to taste. The sign on yaw may need flipping based on user expectation —
flip if "drag right turns left" feels backwards.

Re-enable `Mouse.Visible = true` when entering menus or on game end so
the cursor returns to the player.

## 3rd-person camera follow

Standard pattern (the parkour runner uses this):

```csharp
[Property] public GameObject Target { get; set; }
[Property] public Vector3 Offset { get; set; } = new( -200, 0, 100 );
[Property] public float Smoothing { get; set; } = 5f;

protected override void OnUpdate()
{
    if ( Target == null ) return;

    // Mouse look (pitch + yaw) — update yaw from mouse X, pitch from mouse Y
    _yaw -= Input.MouseDelta.x * 0.2f;
    _pitch = MathF.Max( -60f, MathF.Min( 60f, _pitch - Input.MouseDelta.y * 0.2f ) );

    var camRot = Rotation.FromYaw( _yaw ) * Rotation.FromPitch( _pitch );
    var desired = Target.WorldPosition + camRot * Offset;

    WorldPosition = Vector3.Lerp( WorldPosition, desired, Time.Delta * Smoothing );
    WorldRotation = Rotation.LookAt( Target.WorldPosition + Vector3.Up * 50 - WorldPosition );
}
```

The lerp gives spring-arm-style smoothing without an actual spring-arm.

## Things to skip on a first pass

- IK (foot placement, hand placement) — looks great but not needed for
  movement to read correctly.
- Animation events for footsteps — timer-based is fine.
- Procedural lean / look-at — covered by `wish_*` parameters if you set them.
- Ragdoll on death — different system (`SetRagdoll`), do later.
