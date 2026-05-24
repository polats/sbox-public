# Custom shaders + Materials

Learnings from `examples/material-showcase/` (two real custom `.shader`
files compiled and rendered correctly on Linux/Proton/Wine — hologram
scan lines + dissolve noise). Read this when authoring `.shader` /
`.vmat` files or driving material parameters from C#.

## Custom `.shader` files DO compile on Wine/Linux

This was uncertain going in — the shader compiler runs inside Wine and
might not work. **It does.** Both `hologram.shader` and `dissolve.shader`
produced `.shader_c` files on first load. No fallback was needed.

Standard layout for a `.shader` file (HLSL-ish):

```hlsl
HEADER
{
    Description = "Hologram with scrolling scan lines";
}

FEATURES
{
    #include "common/features.hlsl"
}

COMMON
{
    #include "common/shared.hlsl"
}

struct VertexInput { #include "common/vertexinput.hlsl" };
struct PixelInput { #include "common/pixelinput.hlsl" };

VS
{
    PixelInput MainVs( VertexInput v )
    {
        // ...
    }
}

PS
{
    #include "common/pixel.hlsl"

    float4 MainPs( PixelInput i ) : SV_Target0
    {
        // ...
    }
}
```

Place under `Assets/shaders/<name>.shader`. The editor compiles to
`Assets/shaders/<name>.shader_c` on first asset scan.

For starter material: copy a small existing shader from
`~/.steam/root/steamapps/common/sbox/shaders/` (or `engine/`) and
modify. The simplest reference shaders are unlit-style ones with a
single texture lookup.

## `.vmat` (Material) file format

A Material wraps a shader with concrete parameter values:

```
// hologram.vmat
"Layer0"
{
    "shader" "shaders/hologram.shader"
    "g_flScrollSpeed" "1.5"
    "g_vColor" "[0.5 0.8 1.0 1.0]"
}
```

The editor **re-normalizes** hand-written `.vmat` files on first
compile:
- `shader "complex.vfx"` → `shader "shaders/complex.shader"`
- All keys get quoted
- Floats are rewritten with 6 decimal places

Both pre- and post-normalize forms work — the editor canonicalizes on
load. Don't fight it; either form is fine.

## Loading + applying a material at runtime

```csharp
var mat = Material.Load( "materials/hologram.vmat" );
modelRenderer.MaterialOverride = mat;
```

For multiple materials per model, use `MaterialGroup` (a name string
the editor resolves to a Material) or build a `MaterialOverrideList`.

## CameraComponent.EnablePostProcessing defaults to FALSE — nothing renders until it's true

The single most time-wasting render gotcha: **no post-process effect runs**
(bloom, tonemap, and the `Highlight` outline below) **unless the
`CameraComponent` has `EnablePostProcessing = true`.** It defaults to `false`,
and the failure is *silent* — the effect component is present and enabled, its
shader loads, the targets register, and you still see nothing. Always check the
camera first when a post-process "doesn't show":

```csharp
Scene.Camera.EnablePostProcessing = true;   // or set it in the .scene CameraComponent
```

## Hover/selection outline: HighlightOutline + Highlight (the clean built-in way)

No custom shader needed. Two pieces:
- A `Sandbox.Highlight` post-process **on the camera** (renders the outlines).
- A `Sandbox.HighlightOutline` **on each object** you want outlined (or one with
  `OverrideTargets = true` + `Targets` set to the renderers, the pattern
  sandbox's ContextMenuHost uses). It outlines the object's `Renderer`s.

```csharp
var o = go.Components.GetOrCreate<HighlightOutline>();
o.Color = new Color( 2f, 1.6f, 0.3f );   // HDR-ish reads best through tonemapping
o.Width = 0.6f;
o.ObscuredColor = new Color( 1f, 0.8f, 0.2f ); // shows through occluders
```

For hover: raycast the cursor (`Scene.Camera.ScreenPixelToRay(Mouse.Position)`),
walk up to the interactable, and add/remove the `HighlightOutline` only on the
hovered object. **And — see above — `EnablePostProcessing` must be true or none
of this draws.**

## HDR Tint + bloom for "glowing" without a custom shader

To get a sphere glowing without writing a shader, set the
`ModelRenderer.Tint` to **HDR values > 1**:

```csharp
mr.Tint = new Color( 2.5f, 1.8f, 0.4f );  // bright HDR yellow
```

Add a `PostProcessVolume` Component (`IsGlobal = true`) with a `Bloom`
sub-component, and HDR tints get picked up as bloom.

The standard `complex.vfx` (default PBR) supports:
- `Tint` (Color, HDR for emissive look)
- `MaterialOverride` to swap to a different .vmat
- `MaterialGroup` to pick a named variant
- Various stock material parameters (Metalness, Roughness, AmbientOcclusion)

For ~80% of "make this sphere look interesting" tasks, the stock
shader + Tint + a PostProcessVolume covers it. Custom shaders are for
the 20% that needs effect math (scan lines, dissolve, hologram, etc).

## Editor pre-cache scans description strings as paths

Surprising behavior: when the asset browser caches a project, it
**scans description / display-name strings for asset-path patterns**.
A `[Property] public string Description = "Material with .vmat reference"`
where the value contains `.vmat` triggered a phantom load attempt for
`"Material with .vmat_c"`.

**Avoid `.vmat` / `.vfx` / `.vmdl` substrings in user-visible strings**
(Description, DisplayName) on any Component that the editor scans.
Use other phrasing (`stock complex shader with metalness 0.95` rather
than `complex.vfx with metalness=0.95`).

## `Asset.Compile` requires a `bool full` parameter

```csharp
// ✗ doesn't compile
AssetSystem.FindByPath( "shaders/hologram.shader" )?.Compile();

// ✓
AssetSystem.FindByPath( "shaders/hologram.shader" )?.Compile( true );
```

`Compile(true)` does a full recompile; `Compile(false)` does an
incremental.

## Other gotchas

- **First launch after creating a new shader/material can race the
  asset compile**. Sometimes a `sbox-launch --wait-ready` returns
  before `.shader_c`/`.vmat_c` exist, and the scene loads with
  fallback materials. A second `sbox-launch --kill && sbox-launch
  --wait-ready` picks up the compiled artifacts. If a sphere
  unexpectedly renders pink/checker (the missing-material fallback),
  this is the cause — relaunch once more.
- **`Material.Load` returns null silently** when the path is wrong.
  Always check the return and log if null — saves "why is my sphere
  not changing?" investigations.
- **Non-uniform pedestal scales propagate to children**. If you scale
  a parent box (1.4, 1.4, 2.0) to make a tall plinth and parent a
  sphere underneath, the sphere becomes an ellipsoid. Compensate by
  setting the sphere's local Scale to `(1/1.4, 1/1.4, 1/2.0)`.
