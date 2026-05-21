# Components, GameObjects, and the Scene

s&box is a component-based engine: a `Scene` contains `GameObject`s, each with
zero or more `Component`s attached. Code-side behavior lives in Components.

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
