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
- Units are roughly 1 unit ≈ 1 inch.

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
`bullet-hell/Assets/scenes/main.scene.gen.py` for a worked example. The
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
