# Scenes: file format, conventions, gotchas

A scene in s&box is JSON at `<project>/Assets/scenes/<name>.scene`. It defines
the GameObject tree, their Components, and scene-level metadata. Components
referenced here must be classes that exist in the project's compiled code or
in `Sandbox.*`.

The editor remembers the most recently-open scene tab in `.sbox/project.json`
and restores it on next launch. To open a specific scene immediately after
creating a project, prime it with `tools/sbox-set-startup-scene <project> <scene>`.

## Coordinate system

- **+X = forward** (the way characters face by default)
- **+Y = left/right** (depends on handedness convention; positive Y is typically left)
- **+Z = up**
- **1 unit = 1 inch** (real-world scale).

## Scaling rule: keep models at scale 1, fit the camera to the world

s&box models are authored at real-world inch scale. Hand-tweaking `Scale` to
make things "fit" usually compounds — a player at `0.5`, a coin at `0.3` and a
tree at `0.3` all look reasonable individually but produce a broken-feeling
world when next to each other.

Better: **keep `Scale: "1,1,1"` for all models**, lay them out at realistic
distances, and position the camera to frame the playfield.

### Reference real-world sizes (verified via tools/sbox-model-info)

`tools/sbox-model-info <model-path...>` returns ground-truth dimensions by
loading the model in a probe project and reading `Model.Bounds`. Use it whenever
scale matters. The numbers below were captured this way — **do not guess**.

| Model | Real size (X × Y × Z, inches) | Suggested scale for human-sized world |
|---|---|---|
| `models/citizen_human/citizen_human_male.vmdl` | 12 × 48 × **69**  | 1.0  |
| `models/citizen_props/coin01.vmdl`              | 27 × 27 × 4.5     | 1.0  (already prop-sized, ~knee-high) |
| `models/citizen_props/crate01.vmdl`             | 37 × 36 × 39      | 1.0  |
| `models/sbox_props/trees/oak/tree_oak_big_a.vmdl` | 1125 × 1161 × **1029** | **0.07–0.15** (a real Source 2 oak is ~85 ft tall) |
| `models/dev/box.vmdl`                           | 50 × 50 × 50      | 1.0  |
| `models/dev/sphere.vmdl`                        | 32 × 32 × 32 (approx) | 1.0  |

The pattern: **don't trust a model's name to imply its size.** `coin01` sounds
small but is a 27" Mario-style prop coin; `tree_oak_big_a` is an 85 ft hero
tree, not a backyard sapling. Always probe before placing.

### Framing a scene at human scale

Rule of thumb for a 3/4 view with a citizen in frame and ~6 humans of breathing room around them:

- **Look-at point**: roughly head height of the player, `(0, 0, 70)`.
- **Camera distance**: 500–700 units from look-at.
- **Camera pitch**: 25–35° downward.
- **FOV**: 70° horizontal (default).

Position formula (pitch P, distance D, look-at `(lx, ly, lz)`):
```
forward = (cos(P), 0, -sin(P))
cam_pos = look_at - forward * D
```

Quaternion for pitching N° down around +Y: `(0, sin(N/2°), 0, cos(N/2°))`.
Common values:

| Pitch | Quaternion |
|---|---|
| 20° | `0, 0.1736, 0, 0.9848` |
| 30° | `0, 0.2588, 0, 0.9659` |
| 45° | `0, 0.3827, 0, 0.9239` |
| 60° | `0, 0.5,    0, 0.8660` |
| 90° (top-down) | `0, 0.7071, 0, 0.7071` |

Worked example (`coin-rush`):
- Look-at `(0, 0, 70)`, 30° pitch, distance 600
- Camera position: `(0,0,70) - (cos30°, 0, -sin30°)*600 = (-520, 0, 370)`
- Coins placed at radius ~150 from player
- Trees placed at radius ~300 (just behind the playfield, framing it)

## Quaternion rotation cheatsheet

s&box stores rotation as `"Rotation": "x,y,z,w"` quaternion.

| View                          | Rotation              |
|-------------------------------|-----------------------|
| Identity (forward = +X)       | `0,0,0,1`             |
| 90° yaw left (Z axis)         | `0,0,0.7071,0.7071`   |
| 90° yaw right (Z axis)        | `0,0,-0.7071,0.7071`  |
| Pitch 45° down (around Y)     | `0,0.3827,0,0.9239`   |
| Pitch 90° down (look at -Z)   | `0,0.7071,0,0.7071`   |
| Pitch 45° up (around Y)       | `0,-0.3827,0,0.9239`  |

**Sign trap:** the Y component is *positive* to rotate the camera downward
(pitch the forward vector toward -Z). Negative Y rotates it upward.

## `.sbproj` Ident dictates the C# assembly name

The `.sbproj` file has an `Ident` field. The editor auto-generates the
Code csproj as `<Ident>.csproj` and the assembly name as
`package.local.<Ident>`. If you ship a manually-named `arena-themes.csproj`
(with a hyphen) but the .sbproj says `"Ident": "arena_themes"`, the
editor silently creates `arena_themes.csproj` next to it and ignores
your hyphen-named one. Your code in the hyphenated csproj never
compiles.

Rules:

- **`Ident` must be snake_case** — C# identifier rules, no hyphens.
- **Let the editor own the Code csproj filename**. Use `<Ident>.csproj`
  to match what the editor auto-generates.
- **Project directory** can still be hyphenated (`examples/arena-themes/`)
  — that's a folder, not a C# identifier. The mapping is
  `dir-name` → `Ident: dir_name` → `dir_name.csproj` →
  `package.local.dir_name` assembly.

Reflection from `sbox-eval` against your project's types must use that
`package.local.<ident>` assembly name — see `rules/resources.md`.

## `__guid` must be a valid GUID

Every GameObject and Component needs a unique `__guid`. The format is
`XXXXXXXX-XXXX-XXXX-XXXX-XXXXXXXXXXXX` (8-4-4-4-12 hex). Anything else fails
JSON deserialization with `InvalidOperationException: An element of type
'String' cannot be converted to a 'System.Guid'`.

When generating scenes from a Python script, **always** `str(uuid.uuid4())`,
never hand-roll. A trick like `"...007" + "a"` to get a second GUID will
silently corrupt the scene and the editor will refuse to load it (no
useful error in the dock — the scene just doesn't appear).

## Minimum playable scene

```json
{
  "__guid": "11111111-0000-0000-0000-000000000001",
  "GameObjects": [
    {
      "__guid": "11111111-0000-0000-0000-00000000c00a",
      "__version": 2, "Flags": 0,
      "Name": "Camera",
      "Position": "300,0,1000",
      "Rotation": "0,0.7071068,0,0.7071068",
      "Scale": "1,1,1",
      "Tags": "", "Enabled": true,
      "NetworkMode": 2, "NetworkFlags": 0, "NetworkOrphaned": 0,
      "NetworkTransmit": true, "OwnerTransfer": 1,
      "Components": [
        { "__type": "Sandbox.CameraComponent",
          "__guid": "22222222-0000-0000-0000-00000000c00a",
          "__enabled": true, "Flags": 0,
          "BackgroundColor": "0.05,0.06,0.10,1",
          "ClearFlags": "All",
          "FieldOfView": 70, "FovAxis": "Horizontal",
          "IsMainCamera": true,
          "Orthographic": false, "OrthographicHeight": 1200,
          "Priority": 1,
          "RenderExcludeTags": "", "RenderTags": "",
          "TargetEye": "None", "Viewport": "0,0,1,1",
          "ZFar": 10000, "ZNear": 1 }
      ],
      "Children": []
    }
  ],
  "SceneProperties": {
    "NetworkInterpolation": true,
    "TimeScale": 1,
    "WantsSystemScene": false,
    "Metadata": {}
  },
  "ResourceVersion": 3,
  "Title": "Main",
  "Description": null,
  "__references": [],
  "__version": 3
}
```

## Component field shapes (the values that bite)

### `Sandbox.CameraComponent`

The two most common deserialize-error sources:

- `"RenderTags": ""` and `"RenderExcludeTags": ""` — must be **strings, not objects**. `{}` produces a `TagSet+JsonConvert read too much or not enough` error.
- `"ClearFlags": "All"` — **string**, not int. Other valid values: `"None"`, `"Color"`, `"Depth"`, `"Color | Depth"`.

Required fields:
```
BackgroundColor, ClearFlags, FieldOfView, FovAxis ("Horizontal"|"Vertical"),
IsMainCamera, Orthographic, OrthographicHeight, Priority,
RenderTags, RenderExcludeTags, TargetEye, Viewport,
ZNear, ZFar
```

### `Sandbox.ModelRenderer`

```json
{
  "__type": "Sandbox.ModelRenderer",
  "__guid": "...", "__enabled": true, "Flags": 0,
  "BodyGroups": 18446744073709551615,
  "CreateAttachments": false,
  "LodOverride": null, "MaterialGroup": null,
  "MaterialOverride": null, "Materials": null,
  "Model": "models/dev/box.vmdl",
  "RenderOptions": {"GameLayer": true, "OverlayLayer": false, "BloomLayer": false, "AfterUILayer": false},
  "RenderType": "On",
  "Tint": "1,1,1,1"
}
```

Built-in primitive models always available:
- `models/dev/box.vmdl`
- `models/dev/sphere.vmdl`
- `models/dev/plane.vmdl`
- `models/dev/plane_large.vmdl`

Reference the **source path** (`.vmdl`), not the compiled `.vmdl_c`.

### Real models (props, characters, weapons, vehicles, …)

The s&box install knows about ~2800 models, but they're not all equally
available to every project. There are three scopes:

| Scope | Location on disk | Available to any project? |
|---|---|---|
| **core**  | `core/models/dev/...`             | Yes — engine primitives |
| **addon** | `addons/citizen/`, `addons/menu/` | Yes — bundled with the engine |
| **cloud** | `download/assets/models/...`      | **No** — requires the project to have downloaded that cloud package |

If you reference a `cloud`-scope model from a new project that hasn't
downloaded it, the engine logs `ERROR_FILEOPEN: models/<name>.vmdl_c` and
renders the **purple ERROR placeholder** in its place. This is the
single most common cause of "I see an ERROR model in my scene" — fix is
to pick a model from `core` or `addon` scope, or set up cloud access.

`tools/sbox-models` defaults to `core + addon` only so you can't pick
something you can't load. Pass `--cloud` to widen the search.

```bash
sbox-models                              list always-available categories
sbox-models chest                        no matches (chest is cloud only)
sbox-models chest --cloud                shows cloud chests + a (cloud) tag
sbox-models --category citizen_human     list character models (always available)
sbox-models coin                         models/citizen_props/coin01.vmdl  ←
sbox-models ball                         beachball, balloons, baseball caps...
sbox-models --update                     rebuild cache (after fresh install)
```

#### Reliable picks (verified to load in any project)

- **Characters**: `models/citizen_human/citizen_human_male.vmdl`,
  `models/citizen_human/citizen_human_female.vmdl`
- **Pickups / coins**: `models/citizen_props/coin01.vmdl`,
  `models/citizen_props/sodacan01.vmdl`, `models/citizen_props/coffeemug01.vmdl`
- **Big objects**: `models/citizen_props/crate01.vmdl`,
  `models/citizen_props/recyclingbin01.vmdl`,
  `models/citizen_props/balloonregular01.vmdl`
- **Trees**: `models/sbox_props/trees/oak/tree_oak_big_a.vmdl` (from menu addon)
- **Tools / weapons-ish**: `models/citizen_props/crowbar01.vmdl`,
  `models/citizen_props/broom01.vmdl`

### Caveat for standalone Steam export

The exporter (`engine/Sandbox.Tools/Utility/Standalone/StandaloneExporter.cs`)
bundles a fixed whitelist of core assets plus your project's local assets
and the `base` addon — **not** the citizen addon, the menu addon, or any
cloud-downloaded models. If you reference `models/citizen_props/...` or
`models/sbox_props/...` in a scene and try to ship a standalone build,
those assets won't be in the package. For editor playthrough they work fine.

If you need a model in the standalone build, copy/reauthor it under your
project's `Assets/models/` so it's part of `CopyProjectAssets`.

### `Sandbox.BoxCollider`

```json
{ "__type": "Sandbox.BoxCollider", "__guid": "...", "__enabled": true, "Flags": 0,
  "IsTrigger": true,
  "Scale": "50,50,50",
  "Center": "0,0,0" }
```

If you're not sure about field shapes, **create the collider in code** in your
component's `OnStart()` instead — `Components.Create<BoxCollider>()` avoids any
scene-JSON shape gotchas. See [component-lifecycle.md](component-lifecycle.md).

### `Sandbox.ScreenPanel` (for UI)

```json
{ "__type": "Sandbox.ScreenPanel", "__guid": "...", "__enabled": true, "Flags": 0,
  "AutoScreenScale": true,
  "Opacity": 1, "Scale": 1,
  "ScaleStrategy": "ConsistentHeight",
  "TargetCamera": null, "ZIndex": 100 }
```

Razor panels attach as Components on **children** of the `ScreenPanel` GameObject.
The `__type` is `<Namespace>.<RazorClassName>`, e.g. `Local.BulletHell.ScoreHud`
for `Code/UI/ScoreHud.razor` with `@namespace Local.BulletHell`.

**Important**: the Razor file must use `@inherits PanelComponent`, NOT
`@inherits Panel`. The `Panel` base class is a UI element, not a Component;
the scene loader can only attach `PanelComponent` subclasses. If the editor
log says `Missing Component: couldn't find Component type ...ScoreHud` and
the class clearly exists in your project, this is almost certainly the cause.
See [ui-razor-scss.md](ui-razor-scss.md#inherits--panel-vs-panelcomponent).

### `Sandbox.DirectionalLight`

```json
{ "__type": "Sandbox.DirectionalLight", "__guid": "...", "__enabled": true, "Flags": 0,
  "FogMode": "Enabled", "FogStrength": 1,
  "LightColor": "1,1,1,1", "SkyColor": "0.3,0.4,0.5,1",
  "ShadowBias": 0.0005, "ShadowCascadeCount": 4,
  "ShadowCascadeSplitRatio": 0.91, "ShadowHardness": 0,
  "Shadows": true,
  "Visualizer": {} }
```

## Lifecycle null hooks

Every Component object in a scene needs these six fields, all `null`:

```json
"OnComponentDestroy": null,
"OnComponentDisabled": null,
"OnComponentEnabled": null,
"OnComponentFixedUpdate": null,
"OnComponentStart": null,
"OnComponentUpdate": null
```

Omitting them sometimes works, sometimes not. Always include for safety.

## GUIDs

Every GameObject and every Component needs a unique `__guid` (uuid4-format).
If you generate scenes from a script, use deterministic fixed GUIDs (e.g.
`"11111111-0000-0000-0000-000000000010"`) so re-running the generator
produces stable diffs.

## Generating scenes from Python

For non-trivial scenes, write a generator script. See
`examples/bullet-hell/Assets/scenes/main.scene.gen.py` for a worked example. The
pattern:

```python
import json
from pathlib import Path

def go(guid, name, pos="0,0,0", rot="0,0,0,1", scale="1,1,1",
       components=None, children=None):
    return {"__guid": guid, "__version": 2, "Flags": 0, "Name": name,
            "Position": pos, "Rotation": rot, "Scale": scale, "Tags": "",
            "Enabled": True, "NetworkMode": 2, "NetworkFlags": 0,
            "NetworkOrphaned": 0, "NetworkTransmit": True, "OwnerTransfer": 1,
            "Components": components or [], "Children": children or []}

def cmp(t, guid, **extras):
    base = {"__type": t, "__guid": guid, "__enabled": True, "Flags": 0,
            "OnComponentDestroy": None, "OnComponentDisabled": None,
            "OnComponentEnabled": None, "OnComponentFixedUpdate": None,
            "OnComponentStart": None, "OnComponentUpdate": None}
    base.update(extras); return base
```

## Auto-opening a scene after creation

After writing the scene file, run:

```bash
tools/sbox-set-startup-scene <project-dir> scenes/<name>.scene
```

This primes `.sbox/project.json` so `sbox-launch <project-dir>` opens directly
into that scene tab. **The editor must be closed when this runs** — it
clobbers the file on graceful shutdown.

## Never `EditorScene.OpenScene` via eval after launch — it resets the user's layout

The editor restores the **last-open scene AND the dock/viewport layout**
natively on launch, from `.sbox/project.json` (keys like
`Window.SboxSceneEditor.Dock`, `scenes/<name>.scene.Viewport0`, and the
`SceneDock:scenes/<name>.scene` entry). So after `sbox-launch` the scene is
**already open** — confirm with:

```bash
tools/sbox-eval 'SceneEditorSession.Active?.Scene?.Source?.ResourceName'   # → "main"
```

Do **not** then call `EditorScene.OpenScene(asset.LoadResource<SceneFile>())`
in an eval to "make sure" it's open. That spins up a *fresh* editor session
and throws away the restored viewport + dock arrangement — the user sees their
panel layout and camera reset on every launch. If the scene reliably isn't
restored, fix it with `sbox-set-startup-scene` (above), not with a runtime
open.

**To change scene contents without disrupting layout, edit live instead of
file-edit + relaunch.** Toggling a component, moving an object, etc. on the
*edit* scene via eval is reflected immediately and survives into the next play,
with zero layout cost:

```csharp
var scene = SceneEditorSession.Active.Scene;
var go = scene.GetAllObjects( true ).First( o => o.Name == "Bob" );
foreach ( var c in go.Components.GetAll() )
    if ( c.GetType().Name == "Character" ) c.Enabled = true;   // live toggle
```

(If you want the change to persist across a *relaunch*, also patch the `.scene`
file — but the live toggle is what the user tests now.) And remember
`sbox-launch` **hard-kills** the editor, so any layout changes the user made
since the last graceful exit are lost — prefer `sbox-recompile` (code) and live
eval edits (scene) over relaunching whenever possible.

## Wine file-watcher caveat

If the editor is already running, changes to scene files on disk are usually
**not picked up** (Wine's inotify under pressure-vessel misses external
edits). After regenerating a scene file:

1. Close the editor (`tools/sbox-launch --kill`).
2. Relaunch with `tools/sbox-launch <project-dir>`.
3. Verify in `tools/sbox-logs` that the scene loaded without errors.

Edits made from *inside* the editor (the in-app Save) work fine — they're
just Linux-side file writes from outside that get missed.

## Common scene-load errors

| Log message | Cause |
|---|---|
| `Error when deserializing Sandbox.CameraComponent.RenderTags` | RenderTags / RenderExcludeTags is `{}` — must be `""` |
| `Missing Component: couldn't find Component type X.Y.Z` | Project code didn't compile, OR `__type` in scene doesn't match the C# fully-qualified name |
| `View "RenderToSwapChain" ... non-scratch render target ...` | Cascading error from a broken Camera |
| Empty viewport in Play mode, but objects in Hierarchy | Camera rotation is wrong (see quaternion cheatsheet) |

## Cross-component property references in .scene

A `[Property] public Component X` field in a Component, wired in a .scene to point at another component (on the same or different GameObject), needs the **four-field reference shape**:

```json
"X": {
  "_type": "component",
  "component_id": "<__guid of the target component>",
  "go": "<__guid of the GameObject hosting it>",
  "component_type": "<class name, e.g. CharacterRegistry>"
}
```

The shorter `{"_guid": "...", "_type": "component"}` form looks plausible (sbox uses `__guid` everywhere else) but **silently resolves to null** at runtime. No editor error. The property just reads null, OnStart sees null, and the component appears broken.

For `[Property] public GameObject X` (object reference, not component), the shape is different again: `{"_type": "gameobject", "go": "<guid>"}` — single field.

This is the most common reason a hand-authored scene "compiles fine, runs fine, but the property is null". Always check the reference shape first.

## Non-uniform-scale roots stretch parented characters

`GameObject.SetParent(parent, false)` inherits the parent's WorldScale. If
the parent root has non-uniform scale (e.g. a bed shaped via `Scale:
"2.5,1,0.5"`), any character parented to it (BaseChair-style sit pattern)
gets stretched in the same proportions.

**Pattern to follow** whenever a prop's root will host attachment points
(SeatPosition, RestPosition, UsePosition):
- Keep prop root at `Scale: "1,1,1"`.
- Put the scaled visual on a child GameObject (`PropVisual`) with the
  ModelRenderer + the non-uniform Scale.
- Put attachment points (RestPosition etc) as siblings of the visual,
  not children of it.

The attachment points then inherit only the root's identity scale, so
SetParent + LocalTransform.Zero gives an un-stretched character.

Same applies to BoxColliders for navmesh — when the root is unscaled,
the collider's `Scale` field is the actual world-unit size of the
collision box, not a multiplier. Match it to the visual's world bounds.

## Model imports: probe size, don't eyeball

Source 2 world units = 1 inch. The stock citizen is ~72u tall (~6 ft).
Imported `.vmdl`s land at their FBX-authored size with no auto-fit — many
stock assets are wildly out of scale for human characters (the
`coffeemug01.vmdl` ships at 10.6u tall ≈ a half-foot stein).

**Never pick a `Scale` by eyeballing the viewport.** Use the probe tool:

```
.claude/skills/sbox-gamedev/tools/sbox-model-info <path.vmdl> [<path2>...]
```

It opens the editor's probe scene, measures the model's render bounds,
and prints `size x×y×z (inches)` plus a suggested human-relative scale.
Compute the right scale from the printed dimensions and the prop's
intended real-world size, then commit a single accurate number.

Reference sizes (inches): coffee mug ≈ 4 tall, dinner plate ≈ 10 wide,
chair seat ≈ 18 high, table ≈ 30 high, doorway ≈ 80 tall, citizen ≈ 72
tall.

**Holdable props are especially prone to this:** the citizen
`hold_R`/`hold_L` bones are sized for citizen hands, so an oversized prop
becomes a stein-sized weapon once bone-parented. Probe + scale before
hooking it up; don't trust the standalone placement.

Past incidents: the mug shipped at Scale 1 (10.6u tall, fixed to 0.4
after probing); the bed needed structural decomposition for unrelated
parent-scale reasons (see rule above). Probe-first prevents both.
