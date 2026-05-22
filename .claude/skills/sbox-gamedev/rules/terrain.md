# Terrain (heightmap-based ground)

Learnings from `examples/open-world-walk/` — a real procedural terrain
authored from Python, compiled, and rendered correctly on Linux/Proton.
Read this when you need rolling outdoor ground rather than a flat box.

## Architecture: `Terrain` Component + `TerrainStorage` GameResource

Two engine types you'll touch:

- **`Sandbox.Terrain`** — a Component (in `engine/Sandbox.Engine/Scene/Components/Terrain/`)
  that you add to a GameObject. **It inherits `Collider`**, so
  `Scene.Trace.Ray` raycasts hit it for free.
- **`Sandbox.TerrainStorage`** — a `GameResource` (`.terrain` extension)
  with the actual heightmap and splatmap data. The Component's
  `Storage` field references it by path string.

## `.terrain` file format

```json
{
  "Resolution": 256,
  "TerrainSize": 2000.0,
  "TerrainHeight": 500.0,
  "Materials": [
    { "Material": "terrains/grass_simple.tmat" }
  ],
  "Maps": {
    "heightmap": "<base64 raw-deflate-compressed ushort[256*256]>",
    "splatmap":  "<base64 raw-deflate-compressed uint[256*256]>"
  },
  "__references": [],
  "__version": 0
}
```

Critical encoding details:

- **Heightmap**: `ushort[Resolution*Resolution]` (R16, **little-endian**),
  one row at a time. Values 0..65535 map to 0..`TerrainHeight` units.
- **Splatmap**: `uint[Resolution*Resolution]` of `CompactTerrainMaterial.Packed`.
  Layout: 5 bits base material ID + 5 bits overlay material ID + 8 bits
  blend + 1 bit hole + padding. All zeros = base material 0 everywhere
  (simplest).
- **Compression**: **raw Deflate (wbits=-15), NOT zlib-wrapped.** Python's
  default `zlib.compress` adds a 2-byte header that .NET's `DeflateStream`
  rejects. Use:
  ```python
  import zlib, base64
  c = zlib.compressobj(level=9, wbits=-15)  # raw, no zlib header
  data = c.compress(heightmap_bytes) + c.flush()
  encoded = base64.b64encode(data).decode()
  ```

## Generating a terrain procedurally (Python)

```python
import math, struct, zlib, base64, json, hashlib

RES = 256
SIZE = 2000.0
HEIGHT = 500.0

def value_noise(x, y, seed):
    # cheap 3-octave value noise — replace with whatever
    h = (hashlib.md5(f"{int(x)},{int(y)},{seed}".encode()).digest()[0] / 255.0)
    return h

heights = bytearray(RES * RES * 2)
for j in range(RES):
    for i in range(RES):
        nx, ny = i / RES * 4, j / RES * 4
        h = value_noise(nx, ny, 0)
        v = int(h * 65535)
        struct.pack_into("<H", heights, (j * RES + i) * 2, v)

splats = bytes(RES * RES * 4)  # all zeros = base material 0

def compress_raw(b):
    c = zlib.compressobj(level=9, wbits=-15)
    return base64.b64encode(c.compress(b) + c.flush()).decode()

doc = {
    "Resolution": RES,
    "TerrainSize": SIZE,
    "TerrainHeight": HEIGHT,
    "Materials": [{"Material": "terrains/grass_simple.tmat"}],
    "Maps": {
        "heightmap": compress_raw(bytes(heights)),
        "splatmap": compress_raw(splats),
    },
    "__references": [],
    "__version": 0,
}

with open("Assets/terrains/world.terrain", "w") as f:
    json.dump(doc, f, indent=2)
```

Generate once at scene-author time; the engine's asset compiler bakes
it into `.terrain_c` on next editor launch.

## Critical gotcha: `TerrainMaterial` must be authored in-project

The engine ships `terrains/materials/grass.tmat_c` compiled, but the
sidecar BCR/NHO `.generated.vtex_c` textures aren't always findable
from external content mounts. Symptom: terrain renders as a
**magenta-checker missing-texture pattern**.

Fix: author your own `.tmat` in your project (`Assets/terrains/grass_simple.tmat`)
referencing engine texture sources:

```
Layer0
{
    "shader" "terrain.shader"
    "TextureColor" "materials/default/default_color.tga"
    "TextureNormal" "materials/default/default_normal.tga"
}
```

The asset compiler then bakes `grass_simple_tmat_bcr.generated.vtex_c`
and `grass_simple_tmat_nho.generated.vtex_c` into YOUR project, where
the engine loads them. After regenerating, terrain renders correctly.

## Scene-file shape for the Terrain Component

```json
{
  "__type": "Sandbox.Terrain",
  "__guid": "...",
  "Storage": "terrains/world.terrain",
  "IsTrigger": false
}
```

Place it on a GameObject at world origin (the terrain extends from
its position).

## Ground-height sampling for the player

Because `Terrain` inherits `Collider`, the standard `Scene.Trace.Ray`
hits it:

```csharp
var traceStart = playerPos + Vector3.Up * 2000f;
var traceEnd = playerPos - Vector3.Up * 1000f;
var tr = Scene.Trace.Ray( traceStart, traceEnd ).Run();
if ( tr.Hit )
    playerGo.WorldPosition = playerGo.WorldPosition.WithZ( tr.HitPosition.z );
```

No special "terrain raycast" API needed at the gameplay level. (For
editor-grade precision there's `Terrain.RayIntersects(...)` if you
need it.)

## Clutter scattering

There IS a `Sandbox.TerrainScatterer` Component, but in current builds
it's **editor-tool-only** — it doesn't scatter at runtime in play
mode. For runtime clutter (grass, rocks, trees), do the scatter
manually in `OnStart`:

```csharp
for ( int i = 0; i < 50; i++ )
{
    var pos = RandomGroundPoint();
    var go = Scene.CreateObject();
    go.Name = $"Grass_{i}";
    go.WorldPosition = pos;
    go.WorldRotation = Rotation.FromYaw( Random.Shared.Float( 0, 360 ) );
    go.WorldScale = Vector3.One * Random.Shared.Float( 0.8f, 1.3f );
    var mr = go.Components.Create<ModelRenderer>();
    mr.Model = Model.Load( "models/sbox_props/grass/grass_clump_a.vmdl" );
    go.SetParent( scatterContainer );
}
```

Use `Scene.Trace.Ray` straight down at each candidate position to
find the ground Z and snap the clutter to the terrain.

## VolumetricFog

`Sandbox.VolumetricFogVolume` Component. Fields:

- `Bounds` (BBox — JSON shape `{"Mins":"x,y,z","Maxs":"x,y,z"}`)
- `Strength` (0..1)
- `FalloffExponent`

Don't forget to set the DirectionalLight's `FogMode = "Enabled"` so
the sun contributes to fog scattering (otherwise the fog is uniform
gray without any lit volumes).

## Soundscapes — author or fake

`.sndscape` resources author ambient zones, but `SoundscapeTrigger.Playing`
is `internal set` and only `SceneSoundscapeSystem` flips it — making
manual runtime activation awkward without a fully authored
`.sndscape` asset.

Pragmatic alternative: just fire `Sound.Play(event, position)` from
a timer in your Player Component, randomized for ambient feel. Works
for demos; not a true positional zoning system.
