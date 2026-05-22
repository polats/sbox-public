# s&box physics

Learnings from building pool/9-ball with `Rigidbody` + `SphereCollider` +
`BoxCollider` + trigger pockets. Read this before writing any physics-driven
game (ball games, vehicles, projectiles, ragdolls).

## Units & scale (don't forget)

1 s&box unit = 1 inch. Real-world spec → divide by inches.

- Pool ball: 2.25" diameter → sphere model scale = `2.25 / 64 = 0.0352`
  (the `models/dev/sphere.vmdl` is 64×64×64 cube of inches).
- Pool table: regulation 112" × 56" × 34".
- Citizen height: 69".

Pick a real-world reference and stick to it. Mixing scales (this thing in
meters, that one in feet) gives mysteriously-floaty physics.

## Rigidbody basics

```csharp
[Component] Rigidbody    // adds mass + integration
[Component] SphereCollider | BoxCollider | MeshCollider | CapsuleCollider
```

Tuning starting points (tune from here, don't trust as defaults):

```
LinearDamping  = 0.5     // air drag; raise for rolling surfaces, lower for free-fall
AngularDamping = 0.5     // rotational drag
Mass           = 1       // affects impulse response; leave at 1 unless multi-body interaction matters
EnhancedCcd    = true    // CRITICAL for fast-moving small bodies — without it they tunnel through thin walls
```

`EnhancedCcd` (continuous collision detection) is the difference between "ball
hits cushion" and "ball passes through cushion". Always enable for projectiles
and fast moving things.

## Collider properties — types matter

`Collider.Elasticity` and `Collider.Friction` are `float?`, NOT `Curve`. If
you see code passing a `Curve` to them it will fail to compile.

```csharp
collider.Elasticity = 0.95f;     // 0 = inelastic, 1 = perfect bounce
collider.Friction = 0.1f;        // 0 = ice, 1 = sticky
```

For `IsTrigger = true`, the collider doesn't push back but still fires
trigger callbacks. Use for pickup volumes, pockets, win zones, kill zones.

## Applying forces

```csharp
rigidbody.ApplyImpulse(direction * power);   // instantaneous velocity change
rigidbody.ApplyForce(direction * force);     // per-second force; call from FixedUpdate
rigidbody.Velocity = newVel;                 // overrides directly
rigidbody.AngularVelocity = newAngVel;
```

Impulses are the everyday tool for "hit this thing". Reasonable magnitudes:

- Light tap (pool break): 350-500
- Footstep / weak nudge: 50-150
- Cannon shot: 2000-5000
- Explosion: 5000-15000

If you cap impulses (e.g. `power = MathF.Min(rawPower, 350f)`) you usually
sidestep tunneling and exploding-physics issues at the cost of feel.

## "Park" a body without destroying it

Common pattern when an object needs to be hidden/respawnable (cue ball after
a scratch, projectile after firing):

```csharp
rigidbody.MotionEnabled = false;     // freezes; gravity stops applying
go.WorldPosition = parkSpot;
// ...later, on respawn...
go.WorldPosition = startSpot;
rigidbody.Velocity = Vector3.Zero;
rigidbody.AngularVelocity = Vector3.Zero;
rigidbody.MotionEnabled = true;
```

**Don't** just teleport a body with `Gravity = true` to `(0, 0, -500)` — it
just keeps falling. Either set `MotionEnabled = false` (above) or put it
somewhere with a floor under it.

## Collision events

There are two listener interfaces; pick by which you need:

```csharp
class Ball : Component, Component.ICollisionListener
{
    void Component.ICollisionListener.OnCollisionStart( Collision collision )
    {
        // collision.Other is a CollisionSource STRUCT (no ?. operator).
        // Its .GameObject and .Component may be null — check explicitly.
        var otherGo = collision.Other.GameObject;
        if ( otherGo != null && otherGo.Name == "CueBall" )
            Sound.Play( clickEvent, WorldPosition );
    }
}

class Pocket : Component, Component.ITriggerListener
{
    void Component.ITriggerListener.OnTriggerEntered( Collider other )
    {
        var ball = other.GameObject.Components.Get<PoolBall>();
        if ( ball != null )
            controller.OnBallPocketed( ball );
    }
}
```

`Collision.Other` is the gotcha: it's a value-typed `CollisionSource` struct,
**not nullable**. You access `.GameObject` / `.Component` on it directly,
those may be null — and only those properties are `?.`-able.

## Sound on physics events

The engine ships hundreds of `SoundEvent` resources. Find them with:

```csharp
sbox-eval 'ResourceLibrary.GetAll<SoundEvent>().Select(s => s.ResourcePath).Where(p => p.Contains("impact")).Take(20).ToList()'
```

For pool we used:

- `sounds/impacts/melee/impact-melee-wood.sound` — cue strike
- `sounds/kenney/ui/ui.button.press.sound` — ball click
- `sounds/impacts/melee/impact-melee-cloth.sound` — pocket drop
- `sounds/editor/success.sound` — win

Verify a path is real before relying on it:

```csharp
sbox-eval 'ResourceLibrary.Get<SoundEvent>("sounds/impacts/melee/impact-melee-wood.sound") != null'
```

Sound.Play has a few overloads; the position-bearing one gives 3D falloff:

```csharp
Sound.Play( soundEvent, worldPosition );
```

## Hitscan and `Scene.Trace.Ray`

The everyday pattern for "did I shoot something":

```csharp
var camPos = camera.WorldPosition;
var camFwd = camera.WorldRotation.Forward;
var tr = Scene.Trace.Ray( camPos, camPos + camFwd * 10000f ).Run();
if ( !tr.Hit ) return;

// CRITICAL: tr.GameObject is the renderer's GameObject, not necessarily
// the one carrying your Target Component. The hit lands on whichever
// GameObject hosts the actual Collider — usually a child of the logical
// "Target" object. Walk up the Parent chain to find it.
var go = tr.GameObject;
while ( go != null && go.Components.Get<Target>() == null )
    go = go.Parent;
go?.Components.Get<Target>()?.OnShot( tr.HitPosition, tr.Normal );
```

Without the walk-up, half your hits land on a child mesh and silently do
nothing.

## Sound resource gotcha

`ResourceLibrary.Get<SoundEvent>("path/to.sound")` returning non-null does
NOT guarantee `Sound.Play` actually emits. Some `.sound` files reference
audio that wasn't shipped on Linux. Verify with an actual play call before
relying on a path. Known-missing in current engine builds:

- `sounds/editor/success.sound` — referenced in older examples but doesn't
  resolve. Use `sounds/kenney/ui/ui.favourite.sound` for game-win fanfare.

When picking sounds for a new game, do a one-time discovery pass:

```bash
sbox-eval '
  var names = new[] { "shoot", "gun", "impact", "click", "footstep" };
  return names.ToDictionary(n => n, n =>
    ResourceLibrary.GetAll<SoundEvent>()
      .Select(s => s.ResourcePath)
      .Where(p => p.Contains(n))
      .Take(5).ToList());
'
```

…then verify each chosen path with `Sound.Play(event)` in a sandboxed
`sbox-eval` call.

## Ragdolls (`ModelPhysics` Component)

The engine ragdoll is `ModelPhysics`, NOT a `SetRagdoll(bool)` call. The
working setup (verified in examples/ragdoll-cannon — citizen tumbling
through pendulum obstacles into a goal pit):

```csharp
var smr = go.Components.Create<SkinnedModelRenderer>();
smr.Model = Model.Load( "models/citizen_human/citizen_human_male.vmdl" );
smr.CreateBoneObjects = true;
smr.UseAnimGraph = false;            // animation fights ragdoll physics

var mp = go.Components.Create<ModelPhysics>();
mp.Renderer = smr;
mp.Model = smr.Model;
mp.IgnoreRoot = true;                // CRITICAL — else the root pin keeps the ragdoll glued
mp.MotionEnabled = true;
```

`SkinnedModelRenderer.SetRagdoll(bool)` does **not** exist — only
`ClearPhysicsBones()`. The engine pattern is the `ModelPhysics`
Component above. The citizen model ships with 16 physics parts + 15
joints (verified with `sbox-eval`).

### Launching a ragdoll (apply impulse to all bodies)

`ModelPhysics.Bodies` is a `List<ModelPhysics+Body>` (a struct with
`.Component` → `Rigidbody`, `.Bone`, `.LocalTransform`). It is **not**
a list of `PhysicsBody`. To launch:

```csharp
foreach ( var b in mp.Bodies )
{
    var rb = b.Component;            // Rigidbody, not PhysicsBody
    rb.LinearDamping = 0.05f;        // reset — defaults can halve flight range
    rb.ApplyImpulse( direction * speed * rb.Mass );

    // For tumble (limbs flailing), add a small angular impulse per body.
    // Rigidbody has no ApplyAngularImpulse — route through PhysicsBody:
    var spin = new Vector3(
        Random.Shared.Float( -60f, 60f ),
        Random.Shared.Float( -60f, 60f ),
        Random.Shared.Float( -60f, 60f ) ) * rb.Mass;
    rb.PhysicsBody.ApplyAngularImpulse( spin );
}
```

Symmetric impulses across all bodies make the ragdoll fly as a T-pose
plank — add a per-body random angular kick to break symmetry into a
proper tumble.

`mp.PhysicsGroup` returns null on freshly-spawned ragdolls even when
`mp.Bodies` is populated. Iterate `Bodies` directly; don't rely on
`PhysicsGroup`.

## Joints (Hinge, Spring, Fixed, Slider, Ball, Upright, Filter, Control)

### Anchor body must be a `Rigidbody`

A joint to a GameObject that has **no** `Rigidbody` silently fails to
constrain — the engine has no anchor to attach to. For static anchors:

```csharp
var anchor = Scene.CreateObject();
var arb = anchor.Components.Create<Rigidbody>();
arb.MotionEnabled = false;            // pinned in space
arb.Gravity = false;
```

…then place the dynamic body and add the joint between them.

### HingeJoint setup

```csharp
var joint = parent.Components.Create<HingeJoint>();
joint.Body = bobRigidbody;
joint.AnchorBody = anchorRigidbody;
joint.Attachment = Joint.AttachmentMode.Auto;  // derives LocalFrames from current poses
joint.EnableCollision = false;
```

`HingeJoint.Axis` is `[JsonIgnore]` and **computed at runtime from
LocalFrame1/2**. Setting it directly has no persistent effect. Place
the bodies at their desired relative orientation, then set
`Attachment = Auto` — the engine reads the orientation and infers the
hinge axis.

To start the pendulum swinging, apply a one-time impulse on the bob's
`PhysicsBody` after the joint is wired.

### SpringJoint setup (trampoline pattern)

```csharp
var spring = pad.Components.Create<SpringJoint>();
spring.Body = padRigidbody;
spring.AnchorBody = anchorBelowGround;
spring.Frequency = 8f;
spring.Damping = 0.5f;
spring.MinLength = 50f;
spring.RestLength = 75f;
spring.MaxLength = 90f;
```

Combine with `Collider.Elasticity = 1.2` on the pad's collider to get
genuine bounce.

### `RigidbodyFlags.None` does not exist

The `RigidbodyFlags` enum has no `None` member. To start with no flags,
use `default(RigidbodyFlags)` or just don't set the property — leave it
at the default.

## Physics debugging via sbox-eval

`sbox-eval` shines here. A live readout of all rigidbodies:

```bash
sbox-eval '
  Game.ActiveScene.GetAllComponents<Rigidbody>()
    .Select(r => $"{r.GameObject.Name}: vel={r.Velocity.Length:F1} pos={r.GameObject.WorldPosition}")
    .ToList()
'
```

When "the ball isn't moving" or "everything is exploding" or "two objects
keep oscillating", this is the first thing to check.
