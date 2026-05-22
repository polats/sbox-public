# Components, GameObjects, and the Scene

s&box is a component-based engine: a `Scene` contains `GameObject`s, each with
zero or more `Component`s attached. Code-side behavior lives in Components.

## Required imports

s&box's C# does **not** auto-import `System`. If you use `MathF`, `HashCode`,
`TimeSpan`, etc., you need `using System;` at the top of your `.cs` file —
otherwise you get `The name 'MathF' does not exist in the current context`.
Other commonly-needed imports:

```csharp
using Sandbox;
using System;              // MathF, HashCode, Math, TimeSpan, etc.
using System.Linq;         // FirstOrDefault, Where, Select, etc.
using System.Collections.Generic;  // List<T>, Dictionary<K,V>
```

## Coordinate system & axis convention

- `+X` = forward (default character facing)
- `+Y` = left/right (horizontal)
- `+Z` = up
- 1 unit ≈ 1 inch

Helpers: `Vector3.Forward` is `+X`, `Vector3.Up` is `+Z`, `Vector3.Left` is `+Y`.

## Common Input action names

The default action map used in stock projects:
`Forward`, `Backward`, `Left`, `Right`, `Jump`, `Duck`, `Walk`, `Run`,
`Attack1`, `Attack2`, `Reload`, `Use`, `Score`, `Slot1`–`Slot9`,
`SlotNext`, `SlotPrev`, `View`, `Voice`, `Chat`, `Menu`.

```csharp
if ( Input.Down( "Forward" ) )   // held
if ( Input.Pressed( "Attack1" ) ) // edge: just pressed this frame
if ( Input.Released( "Use" ) )    // edge: just released this frame
```

You can also bind custom action names in `ProjectSettings/InputSettings`.

## The Component skeleton

```csharp
using Sandbox;

namespace MyGame;

public sealed class HealthBar : Component
{
    [Property] public int MaxHealth { get; set; } = 100;
    [Sync] public int Current { get; set; }

    protected override void OnStart()       { base.OnStart(); }      // once, when active
    protected override void OnUpdate()      { /* every frame */ }
    protected override void OnFixedUpdate() { /* physics tick */ }
    protected override void OnDestroy()     { /* cleanup */ }
    protected override void OnEnabled()     { /* enabled/loaded */ }
    protected override void OnDisabled()    { /* hidden/disabled */ }
}
```

To scaffold: copy the template above into `<project-dir>/Code/<Name>.cs`,
set the namespace to match the project (`Org.Ident` from `.sbproj`, or just
`Sandbox` for the stock sandbox project), and adjust the inherited interfaces
and overrides you actually need.

## Reading the Component graph

```csharp
// Sibling component on same GameObject
var rb = Components.Get<Rigidbody>();

// Required (throws if missing)
var rb = Components.GetOrCreate<Rigidbody>();

// Descend into children
var sprite = Components.GetInDescendantsOrSelf<PickupSprite>();

// Walk up to ancestors
var arena = Components.GetInAncestorsOrSelf<Arena>();

// All instances in scene
foreach ( var p in Scene.GetAllComponents<Player>() ) { … }

// Active scene from anywhere
var scene = Game.ActiveScene;
```

## GameObject transforms

```csharp
GameObject.WorldPosition  = new Vector3( 0, 0, 100 );
GameObject.LocalRotation  = Rotation.FromYaw( 90 );
GameObject.Parent         = otherGo;
GameObject.Tags.Add( "enemy" );
GameObject.Destroy();                  // marks for destruction at end of tick
```

## Spawning prefabs

```csharp
var prefab = ResourceLibrary.Get<PrefabFile>( "prefabs/bomb.prefab" );
var go = prefab.Clone( WorldPosition );
go.NetworkSpawn();                     // tell network to replicate (host only)
```

## Networking quick recipe

```csharp
[Sync]    public int Score { get; set; }        // host-authoritative state
[Authority] public void RequestRespawn() { … }  // only owner may call
[Broadcast] public void PlayHitSound() { … }    // called everywhere

if ( IsProxy ) return;     // host-only code below
if ( !Networking.IsHost ) return;
```

`IRestartable.OnRestart` is a Bomb Royale-style interface — your project may
have its own equivalents. Look for interfaces ending in `Listener`.

## Triggers and collisions

```csharp
public sealed class Pickup : Component, Component.ITriggerListener
{
    void Component.ITriggerListener.OnTriggerEnter( Collider other )
    {
        if ( other.GameObject.GetComponent<Player>() is { } p )
            Collect( p );
    }
}
```


## Common pitfalls

- **`Components.Get<T>()` returns null** if no such component on this GameObject.
  Use `GetOrCreate` or check before deref. Mirroring Unity, where the equivalent
  was historically thrown-on-fail.
- **Don't cache `Components.Get<T>()` in a field at construction time.** Components
  attach in any order. Capture in `OnStart` or `OnEnabled` after the graph stabilises.
- **`Scene` may be null in editor-only code paths.** Guard with `Scene != null`.
- **Hot reload** preserves component state where possible. After saving a `.cs`
  file the editor recompiles and rebinds. If a field disappears, its value is lost.
- **`Game.ActiveScene` ≠ `Scene` of an editor scene.** In the editor's preview
  panes the active scene flips. Prefer `this.Scene` in component code.

## API gotchas found in the wild

- **`OnTriggerEntered` does not exist.** The interface method is
  `OnTriggerEnter(Collider other)` — past-tense `…ed` looks right and the
  compiler doesn't catch it because `ITriggerListener` is implemented
  explicitly. If your trigger never fires, check the method name first.
- **`Rotation.Inverse` is a property, not a method.** Write
  `rot.Inverse * v`, not `Rotation.Inverse(rot) * v`.
- **`Collision.Other` is a `CollisionSource` struct, not nullable.** Access
  its `.GameObject` / `.Component` directly (those may be null and *are*
  `?.`-able). Don't write `collision.Other?.Whatever`.
- **`CharacterController` is not a `Collider`.** It has its own collision
  system. `ITriggerListener.OnTriggerEnter` will never fire for the
  CharacterController GameObject. Use a per-frame distance check or attach
  a separate `BoxCollider { IsTrigger = true }`.
- **`Collider.Elasticity` and `Collider.Friction` are `float?`, not `Curve`.**
  Code that assumes they're a Curve fails to compile with a non-obvious
  message. See `rules/physics.md`.
- **Game code can't call `Type.GetProperty(string)`.** The compiler
  whitelist blocks reflective property lookup in `local.<project>` Code/.
  Error: `'System.Private.CoreLib/System.Type.GetProperty(System.String)' is not allowed when whitelist is enabled`.
  If you need to probe an API surface for unknown property names, do it
  out-of-band via `sbox-eval` once, then hardcode the result in your
  Component. Wholesale reflection belongs in Editor/ code only, not in
  game code.
- **`Noise.Perlin` does not exist.** No Perlin helper in `Sandbox.Noise`. For procedural noise / shake, use `MathF.Sin/Cos` composed with elapsed time, or write your own value-noise (see Python pattern in `rules/terrain.md`).
- **`Game.ActiveScene` vs `SceneEditorSession.Active.Scene`.** In play mode `Game.ActiveScene` is the active running scene; in edit mode it can be null or different. When `sbox-eval`-debugging a scene, query `SceneEditorSession.Active?.Scene` for edit-mode contents, `Game.ActiveScene` for play-mode contents — they're often different scenes with different GameObject counts.
- **Namespace shadow**: when your project's namespace is e.g. `Local.Achievements` and you also `using Sandbox.Services;`, the compiler resolves bare `Achievements.Unlock(...)` as `Local.Achievements.Unlock(...)` — your own type wins over the using-imported one. Symptom: "cannot resolve Unlock" on a static call that should work. Fix: fully-qualify the engine-side type (`Sandbox.Services.Achievements.Unlock(...)`) when your project's name collides. Same pattern for `Local.Inventory`/`Sandbox.Inventory`, `Local.Voice`/`Sandbox.Voice`, etc.
- **Standard input action names** (confirmed against `sandbox/Code/UI/Inventory/Inventory.razor`): `Slot1`..`Slot9`, `SlotNext`, `SlotPrev`, `Attack1`, `Attack2`, `Reload`, `Use`, `Jump`, `Duck`, `Sprint`, `Forward`, `Backward`, `Left`, `Right`. Use these for `Input.Pressed("…")`/`Input.Down("…")` instead of inventing your own — they're already wired to default keys (1-9, mouse, R, E, Space, etc.).
- **`RenderOptions.BloomLayer` does not exist** in current builds. Bloom
  is configured on a `PostProcessVolume` Component, not as a per-renderer
  flag. The `RenderOptions` struct has `GameLayer`, `OverlayLayer`,
  `AfterUILayer` — `BloomLayer` was removed/never-existed. If you want a
  renderer to glow, give it a bright tint and add a global PostProcessVolume
  with bloom enabled.
- **HTTP in game code is gated.** `System.Net.Http.HttpClient` is whitelist-blocked in `Code/*.cs`. Use `Sandbox.Http.RequestStringAsync`/`RequestAsync` instead. `Sandbox.Http` enforces a hard-coded loopback allowlist: only ports **80/443/8080/8443** for `127.0.0.1`/`localhost`. Other ports denied with `"Access to '<url>' is not allowed"`. `HttpAllowList` field in `.sbproj` Metadata is vestigial — `Sandbox.Http` doesn't read it. Bypass for dev: launch with `-allowlocalhttp` flag (editor only). Editor-side code (`Editor/*.cs`) can use `HttpClient` directly. Default local services to **port 8080** to avoid needing a launch flag.
