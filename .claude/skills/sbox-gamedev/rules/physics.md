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
