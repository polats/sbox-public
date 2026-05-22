# Layered NPC framework

Learnings from `examples/npc-patrol/` — a guard that patrols waypoints,
detects the player via a sight cone, transitions Patrol→Alert→Chase→Investigate
→Patrol. Read this when building any complex AI character (guards,
enemies, companions, civilians).

## `Sandbox.Npcs.Npc` is unreachable from external projects

The sandbox addon ships a full layered NPC system under `Sandbox.Npcs.*`
(Npc, BaseNpcLayer, SensesLayer, NavigationLayer, AnimationLayer,
SpeechLayer, ScheduleBase, TaskBase, TaskStatus). It would be ideal to
just inherit from it.

**But you can't.** Sandbox's `.sbproj` has `Type = "game"`, not a
library type. Game packages can't be referenced from another project.
Verified: enumerating `AppDomain.CurrentDomain.GetAssemblies()` from an
example project only shows `package.base` + `package.local.<ident>` —
no `package.sandbox`.

**The workable path: copy the pattern, not the code.** Author your own
`BaseNpcLayer`/`SensesLayer`/etc. in `Local.YourProject.Npcs.*`. The
shapes from `sandbox/Code/Npcs/` give you a known-good design template
to mirror.

## Architecture (mirror this)

```
Npc (abstract)                              ← composes layers, runs current schedule
├─ SensesLayer                              ← sight cone + LOS raycast + hearing
├─ NavigationLayer                          ← wraps NavMeshAgent
├─ AnimationLayer                           ← drives SkinnedModelRenderer animgraph + look-at
└─ SpeechLayer (optional)                   ← chat bubble / subtitle

ScheduleBase                                ← sequence of tasks
├─ Patrol      = MoveToTask → WaitTask → repeat
├─ Alert       = StopTask → LookAtTask → SayTask
├─ Chase       = MoveToTask(target, reeval) → LookAtTask(target)
└─ Investigate = MoveToTask(lastSeenPos) → SayTask → WaitTask

TaskBase                                    ← single behavior-tree leaf
├─ MoveToTask
├─ WaitTask
├─ LookAtTask
└─ SayTask
```

The Npc's per-tick logic:

1. Update senses (visible/audible targets).
2. `GetSchedule()` selector decides which schedule should be active
   (Chase wins over Alert wins over Investigate wins over Patrol).
3. Run the current schedule's tasks until one returns
   `TaskStatus.Success` or the schedule's `ShouldCancel()` fires.
4. AnimationLayer continuously drives the renderer's animgraph from
   NavigationLayer.Velocity.

## Sight cone + LOS

```csharp
bool CanSee( GameObject target )
{
    var toTarget = (target.WorldPosition - WorldPosition).Normal;
    var forward = WorldRotation.Forward;
    if ( Vector3.Dot( forward, toTarget ) < MathF.Cos( SightConeDegrees.DegreeToRadian() * 0.5f ) )
        return false;                       // outside cone

    var distance = Vector3.DistanceBetween( WorldPosition, target.WorldPosition );
    if ( distance > SightRange )
        return false;                       // out of range

    // Line-of-sight raycast — does anything block?
    var tr = Scene.Trace.Ray( WorldPosition + Vector3.Up * 60, target.WorldPosition + Vector3.Up * 30 )
        .IgnoreGameObjectHierarchy( GameObject )
        .Run();
    return !tr.Hit || tr.GameObject == target;
}
```

## Gotchas (CRITICAL)

### `Scene.FindInPhysics` misses `CharacterController`

The CharacterController is NOT a `Collider`, so physics-volume queries
miss it. If your SensesLayer (or any AI system) uses
`Scene.FindInPhysics(center, radius)` to enumerate targets, the
**player will never be found** — they have a CharacterController, not
a Collider.

Workaround: enumerate by tag instead.

```csharp
// ✗ misses CharacterControllers
var hits = Scene.FindInPhysics( WorldPosition, SightRange );

// ✓ works for tagged players
var candidates = Scene.GetAllObjects( true )
    .Where( go => go.Tags.HasAny( TargetTags ) );
foreach ( var go in candidates )
    if ( CanSee( go ) ) … ;
```

### Tag inheritance: child GameObjects inherit parent tags

When you tag a Player GameObject `"player"`, all its children (e.g.
the 38 citizen-skeleton bones from `CreateBoneObjects = true`) also
report `Tags.HasAny("player")` for queries. Your sight scan picks up
every bone individually and the "nearest visible" target flips
between them.

```csharp
// ✓ filter to root-only when scanning for actors
.Where( go => go.Tags.HasAny( TargetTags ) && go.Root == go );
```

`go.Root == go` is the cheapest way to require "this is the top-level
GameObject, not a child".

### `sbox-eval` chokes on multi-statement lambdas / inner double quotes

The eval wraps your snippet in `return (...);` for expressions. Any
internal `;` or `"` confuses the wrapping. Use simple expressions only
for `sbox-eval`; for complex inspection, write a tiny helper static
method into your Editor code and call THAT from eval.

### `using System;` is NOT implicit

The project-level global usings cover `Sandbox.*` and a few
namespaces, but `System` is NOT auto-imported. Every framework file
that uses `MathF`, `Type`, `Math`, etc. needs an explicit
`using System;`. The error cascade ("MathF not found", "GameObject
not found") is misleading — the actual missing using is `System`.

## Schedule transitions: log on change only

State-transition logs are noisy if they fire on every tick. Track
last-state and log only on change:

```csharp
private GuardState _lastLoggedState;
private bool _loggedInitial;

void LogTransitionIfChanged()
{
    if ( State == _lastLoggedState && _loggedInitial ) return;
    Log.Info( $"[GuardNpc] {_lastLoggedState} → {State}" );
    _lastLoggedState = State;
    _loggedInitial = true;
}
```

The `_loggedInitial` flag prevents the very first patrol-tick from
being suppressed (otherwise it looks like nothing's running).

## Scene-file cross-Component references need all 4 fields

A Component on GameObject A referencing a Component on GameObject B
in scene JSON needs:

```json
{
  "_type": "component",
  "component_id": "<guid of the target component>",
  "go":           "<guid of the GameObject hosting that component>",
  "component_type": "Sandbox.NavMeshAgent"
}
```

Just `component_id` looks fine to a human, but the engine needs
`go` + `component_type` too. Common case: NavMeshAgent on parent GO
+ SkinnedModelRenderer on a child GO; the Npc Component referencing
both needs both refs fully populated.
