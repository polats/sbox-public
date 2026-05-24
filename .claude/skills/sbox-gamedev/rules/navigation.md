# Navigation: NavMesh + NavMeshAgent

Learnings from `examples/tag-ai/` (5 NPCs pathfinding to the player
through an arena of obstacles). Read this when you need agent
movement, pathfinding, or any AI that has to get from A to B around
obstacles.

## The type name is `Sandbox.Navigation.NavMesh`, not `Sandbox.NavMesh`

This burns hours if you don't know it.

- ✗ `Sandbox.NavMesh` — does NOT exist
- ✓ `Sandbox.Navigation.NavMesh` — the actual type, in an inner namespace
- ✓ `Sandbox.NavMeshAgent` — the Component (this one IS in `Sandbox.`)
- ✓ `Sandbox.NavMeshArea` — Component for areas
- ✓ `Sandbox.NavMeshLink` — Component for off-mesh links

The Scene exposes the instance directly: `Scene.NavMesh.Generate(...)`,
`Scene.NavMesh.GetClosestPoint(...)`, etc.

## Bake the navmesh at runtime

For autonomous scenes (no human clicking Scene → Bake NavMesh in the
editor), generate from `OnStart`:

```csharp
protected override async Task OnLoad()
{
    await Scene.NavMesh.Generate( Scene.PhysicsWorld );
}
```

(or call `.Generate(...)` from `OnStart` without `await` and check
`Scene.NavMesh.IsEnabled` in `OnUpdate`.)

**`Scene.NavMesh.IsEnabled` reports false during edit mode** even when
`SceneProperties.NavMesh.Enabled = true` in the .scene file. The mesh
only activates in play mode. Don't rely on baking-time-of-scene-author
— bake at runtime so it works regardless of how the scene was saved.

`Sandbox.Navigation.NavMesh.BakeNavMesh()` (static) also exists, but
`await Scene.NavMesh.Generate(physicsWorld)` is the cleaner runtime
path.

## Scene-file NavMesh config

NavMesh config in the .scene file lives under `SceneProperties.NavMesh`,
NOT as a Component:

```json
"SceneProperties": {
  "NavMesh": {
    "Enabled": true,
    "EditorAutoUpdate": false,
    "AgentHeight": 64,
    "AgentRadius": 16,
    "StepSize": 18,
    "MaxSlope": 45,
    "IncludeStaticBodies": true,
    "IncludeKeyframedBodies": false,
    "IncludeMaps": true
  },
  ...
}
```

The field is `Enabled` (not `IsEnabled` — that's the runtime property,
different name).

Grep `sbox-bombroyale/Assets/scenes/arena.scene` for a real example.

## `NavMeshAgent` Component setup

```csharp
var agent = npcGo.Components.Create<NavMeshAgent>();
agent.MaxSpeed = 80;          // tune for the game — fast = harder to see chase
agent.Radius = 16;
agent.Height = 64;
agent.Acceleration = 600;
agent.UpdatePosition = true;  // agent moves the GameObject
agent.UpdateRotation = true;  // agent rotates the GameObject to face velocity
```

Scene-file shape (`Sandbox.NavMeshAgent` JSON):

```json
{
  "__type": "Sandbox.NavMeshAgent",
  "MaxSpeed": 80,
  "Radius": 16,
  "Height": 64,
  "Acceleration": 600,
  "Separation": 0.5,
  "UpdatePosition": true,
  "UpdateRotation": true,
  "AllowedAreas": [],
  "ForbiddenAreas": [],
  "AllowDefaultArea": true,
  "AutoTraverseLinks": true
}
```

(Crib from `sandbox/Assets/entities/sents/npc/combat_npc.prefab` for
the canonical field list.)

### `UpdateRotation = true` aims at a path look-ahead, not the actual velocity

The agent's built-in rotation faces a point *ahead on the path* (`GetLookAhead`),
which has two bad tells: it **spins the character toward an unreachable target**
when the path is blocked (the agent stalls but keeps re-aiming), and it **snaps
the facing ~180° at arrival** when it slightly overshoots the last corner (the
residual look-ahead points back). If either bites, set `UpdateRotation = false`
and face the real travel direction yourself, only while actually moving:

```csharp
agent.UpdateRotation = false;
// each frame:
var v = agent.Velocity.WithZ( 0f );
if ( v.Length > 10f )
    WorldRotation = Rotation.Slerp( WorldRotation, Rotation.LookAt( v.Normal ), Time.Delta * 8f );
```

Keeping it velocity-based also means the character holds its last facing when it
stops, instead of pivoting.

## Moving an agent

```csharp
// Each frame the target moves (or every 0.25s if you want to dial it back):
agent.MoveTo( player.WorldPosition );
```

`MoveTo` is idempotent — calling it every frame with a slightly-moved
target just updates the destination, doesn't re-plan from scratch.

To stop:
```csharp
agent.Stop();              // freezes immediately
agent.MaxSpeed = 0;        // belt-and-suspenders for forever-stop
agent.UpdatePosition = false;
```

## Sampling random / closest points on the mesh

`Scene.NavMesh.GetRandomPoint(position, radius)` and
`Scene.NavMesh.GetClosestPoint(position, radius)` both return
`Vector3?` (nullable). They return null if no navmesh point exists
within `radius`.

Use `GetClosestPoint` to snap spawn candidates onto the mesh:

```csharp
var candidate = new Vector3(
    Random.Shared.Float( -700, 700 ),
    Random.Shared.Float( -700, 700 ),
    0 );
var snapped = Scene.NavMesh.GetClosestPoint( candidate, 200 );
if ( snapped.HasValue )
    SpawnNpc( snapped.Value );
```

## Animgraph integration for NPC walk cycle

Drive the NPC's `SkinnedModelRenderer` from the agent's velocity (same
pattern as parkour's player, but the agent moves automatically). See
`rules/animations.md` "Parameters you actually drive" section. Quick
version:

```csharp
var smr = Components.Get<SkinnedModelRenderer>();
var vel = agent.Velocity;
var localFwd   = WorldRotation.Forward.Dot( vel );
var localRight = WorldRotation.Right.Dot( vel );
smr.Set( "move_groundspeed", new Vector3(vel.x, vel.y, 0).Length );
smr.Set( "move_x", localFwd );
smr.Set( "move_y", localRight );
smr.Set( "b_grounded", true );
```

The agent's `UpdateRotation = true` keeps the GameObject's rotation
aligned with velocity, so `WorldRotation.Forward.Dot(vel)` is basically
`vel.Length` — but the math holds either way.

## Common API holes you may hit

- **`Color.FromHsv` doesn't exist**. Hardcode a palette or convert HSV
  manually.
- **`Component.IsActive` doesn't exist**. Use the component reference
  directly — `Components.Get<T>() != null` for "is it there".
- **NavMesh API has both instance methods (on `Scene.NavMesh`) and
  statics on `Sandbox.Navigation.NavMesh`**. Prefer instance.
