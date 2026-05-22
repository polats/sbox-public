#!/usr/bin/env python3
"""Generate a procedural heightmap terrain resource (.terrain).

The s&box .terrain file is JSON; HeightMap is a Deflate-compressed
ushort[] (R16) base64 string, SplatMap is a Deflate-compressed uint[]
base64 string. Each pixel of the splatmap is a CompactTerrainMaterial:
  bits 0-4   : base texture id (we use 0)
  bits 5-9   : overlay id
  bits 10-17 : blend
  bit 18     : hole
"""
import base64, json, math, os, random, struct, zlib

RES = 256                 # 256x256 heightmap
SIZE = 2000.0             # 2000 world-units wide (small-ish for fast load)
HEIGHT = 500.0            # max terrain height (z)

random.seed(42)

# --- value-noise: random lattice of values, smoothstep-interp -----------
LATTICE = 8  # number of cells across
lattice_vals = [[random.random() for _ in range(LATTICE + 1)] for _ in range(LATTICE + 1)]

def smoothstep(t):
    return t * t * (3 - 2 * t)

def value_noise(u, v):
    # u,v in [0,1]
    fx = u * LATTICE
    fy = v * LATTICE
    ix = int(fx)
    iy = int(fy)
    tx = smoothstep(fx - ix)
    ty = smoothstep(fy - iy)
    ix = min(ix, LATTICE - 1)
    iy = min(iy, LATTICE - 1)
    a = lattice_vals[iy][ix]
    b = lattice_vals[iy][ix + 1]
    c = lattice_vals[iy + 1][ix]
    d = lattice_vals[iy + 1][ix + 1]
    return (a * (1 - tx) + b * tx) * (1 - ty) + (c * (1 - tx) + d * tx) * ty

def height_at(u, v):
    # fractal: 3 octaves of value noise
    h = 0.0
    amp = 1.0
    freq = 1.0
    total_amp = 0.0
    for _ in range(3):
        h += value_noise(u * freq % 1.0, v * freq % 1.0) * amp
        total_amp += amp
        amp *= 0.5
        freq *= 2.0
    h /= total_amp

    # Carve a flatter "valley" near the center spawn so the player has a flat start
    du = u - 0.5
    dv = v - 0.5
    d = math.sqrt(du * du + dv * dv)
    flat_factor = math.exp(-d * d * 10.0)  # gaussian
    h = h * (1.0 - flat_factor * 0.7) + 0.25 * flat_factor

    return max(0.0, min(1.0, h))

# --- fill heightmap ----------------------------------------------------
heightmap = bytearray(RES * RES * 2)
for y in range(RES):
    for x in range(RES):
        u = x / (RES - 1)
        v = y / (RES - 1)
        h = height_at(u, v)
        # h is 0..1, scale to ushort
        val = int(h * 65535)
        idx = (y * RES + x) * 2
        heightmap[idx] = val & 0xff
        heightmap[idx + 1] = (val >> 8) & 0xff

# --- fill splatmap (all material 0) -------------------------------------
# CompactTerrainMaterial packed = base_id=0, overlay=0, blend=0 → 0
splatmap = bytearray(RES * RES * 4)  # all zeros

# --- compress (raw deflate, NOT zlib-with-header) -----------------------
def deflate(data: bytes) -> bytes:
    # .NET DeflateStream produces raw deflate (no zlib header)
    co = zlib.compressobj(level=6, wbits=-15)
    return co.compress(data) + co.flush()

hm_b64 = base64.b64encode(deflate(bytes(heightmap))).decode("ascii")
sm_b64 = base64.b64encode(deflate(bytes(splatmap))).decode("ascii")

doc = {
    "Maps": {
        "heightmap": hm_b64,
        "splatmap": sm_b64,
    },
    "Resolution": RES,
    "TerrainSize": SIZE,
    "TerrainHeight": HEIGHT,
    "Materials": [
        "terrains/grass_simple.tmat"
    ],
    "MaterialSettings": {
        "HeightBlendEnabled": True,
        "HeightBlendSharpness": 0.87,
    },
    "ResourceVersion": 1,
    "__references": [],
    "__version": 1,
}

out_path = os.path.join(os.path.dirname(__file__), "world.terrain")
with open(out_path, "w") as f:
    json.dump(doc, f, indent=2)
print(f"wrote {out_path}  res={RES}  size={SIZE}  height={HEIGHT}")
