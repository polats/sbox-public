"""Generates Assets/scenes/main.scene for material-showcase.

Layout:
- Floor 1500 x 1500 (box scaled), 4 walls, ceiling — small interior.
- 6 pedestals in a row along the Y axis at X = 0.
- Camera at -X side looking at +X (down the row of pedestals).
- DirectionalLight providing even lighting.
- PostProcessVolume w/ Bloom for the glow demo.
- GameManager (hover detect + bloom toggle + autoplay orbit).
- ScreenPanel + ShowcaseHud razor panel.

Generates 6 distinct pedestals; the Pedestal.cs component reads its Style
property and applies the corresponding material at runtime.
"""

import json
import uuid
from pathlib import Path

NULLS = dict(
    OnComponentDestroy=None, OnComponentDisabled=None, OnComponentEnabled=None,
    OnComponentFixedUpdate=None, OnComponentStart=None, OnComponentUpdate=None,
)


def go(guid, name, pos="0,0,0", rot="0,0,0,1", scale="1,1,1",
       components=None, children=None):
    return {
        "__guid": guid, "__version": 2, "Flags": 0,
        "Name": name, "Position": pos, "Rotation": rot, "Scale": scale,
        "Tags": "", "Enabled": True,
        "NetworkMode": 2, "NetworkFlags": 0, "NetworkOrphaned": 0,
        "NetworkTransmit": True, "OwnerTransfer": 1,
        "Components": components or [], "Children": children or [],
    }


def cmp(t, guid, **extras):
    base = {"__type": t, "__guid": guid, "__enabled": True, "Flags": 0}
    base.update(NULLS)
    base.update(extras)
    return base


def mr(guid, model="models/dev/box.vmdl", tint="1,1,1,1"):
    return cmp(
        "Sandbox.ModelRenderer", guid,
        BodyGroups=18446744073709551615,
        CreateAttachments=False,
        LodOverride=None, MaterialGroup=None,
        MaterialOverride=None, Materials=None,
        Model=model,
        RenderOptions={"GameLayer": True, "OverlayLayer": False, "AfterUILayer": False},
        RenderType="On",
        Tint=tint,
    )


def box_collider(guid, scale="50,50,50", center="0,0,0"):
    return cmp(
        "Sandbox.BoxCollider", guid,
        IsTrigger=False,
        Static=True,
        Scale=scale,
        Center=center,
    )


# --- Room ---
# Floor 1500 x 1500; box.vmdl is 50x50x50 so we use Scale=30,30,0.5 (1500x1500x25).
FLOOR_HALF = 750
WALL_H = 400
WALL_T = 20

floor = go(
    "11111111-0000-0000-0000-000000000001", "Floor",
    pos="0,0,0", scale="30,30,0.5",
    components=[
        mr("22222222-0000-0000-0000-000000000001",
           model="models/dev/box.vmdl", tint="0.32,0.34,0.38,1"),
    ],
)

# Walls: 4 of them around the room.
# Each wall is a box positioned at the edge.
wall_n = go(
    "11111111-0000-0000-0000-000000000002", "Wall_North",
    pos=f"{FLOOR_HALF},0,{WALL_H//2}",
    scale=f"{WALL_T/50},{(FLOOR_HALF*2)/50},{WALL_H/50}",
    components=[mr("22222222-0000-0000-0000-000000000002",
                   model="models/dev/box.vmdl", tint="0.18,0.20,0.26,1")],
)
wall_s = go(
    "11111111-0000-0000-0000-000000000003", "Wall_South",
    pos=f"{-FLOOR_HALF},0,{WALL_H//2}",
    scale=f"{WALL_T/50},{(FLOOR_HALF*2)/50},{WALL_H/50}",
    components=[mr("22222222-0000-0000-0000-000000000003",
                   model="models/dev/box.vmdl", tint="0.18,0.20,0.26,1")],
)
wall_e = go(
    "11111111-0000-0000-0000-000000000004", "Wall_East",
    pos=f"0,{FLOOR_HALF},{WALL_H//2}",
    scale=f"{(FLOOR_HALF*2)/50},{WALL_T/50},{WALL_H/50}",
    components=[mr("22222222-0000-0000-0000-000000000004",
                   model="models/dev/box.vmdl", tint="0.18,0.20,0.26,1")],
)
wall_w = go(
    "11111111-0000-0000-0000-000000000005", "Wall_West",
    pos=f"0,{-FLOOR_HALF},{WALL_H//2}",
    scale=f"{(FLOOR_HALF*2)/50},{WALL_T/50},{WALL_H/50}",
    components=[mr("22222222-0000-0000-0000-000000000005",
                   model="models/dev/box.vmdl", tint="0.18,0.20,0.26,1")],
)
ceiling = go(
    "11111111-0000-0000-0000-000000000006", "Ceiling",
    pos=f"0,0,{WALL_H}", scale="30,30,0.5",
    components=[mr("22222222-0000-0000-0000-000000000006",
                   model="models/dev/box.vmdl", tint="0.10,0.10,0.14,1")],
)

# --- Sun (DirectionalLight) ---
sun = go(
    "11111111-0000-0000-0000-000000000010", "Sun",
    pos="0,0,300", rot="-0.3535534,0.3535534,0.1464466,0.8535534",
    components=[
        cmp("Sandbox.DirectionalLight",
            "22222222-0000-0000-0000-000000000010",
            FogMode="Disabled", FogStrength=1,
            LightColor="1,0.98,0.92,1.4",
            ShadowBias=0.0005, ShadowCascadeCount=4,
            ShadowCascadeSplitRatio=0.91, ShadowHardness=0,
            Shadows=True,
            SkyColor="0.45,0.55,0.68,1",
            Visualizer={}),
    ],
)

# --- Pedestals ---
# 6 pedestals laid out along the Y axis at intervals of ~180 units.
# Each pedestal is a tall box (height 100). Sphere sits on top.
# Sphere.vmdl is ~32 units; we scale to 1.0 (32" sphere).
PEDESTAL_HEIGHT = 100
PEDESTAL_SCALE_XY = 1.4   # 50 * 1.4 = 70" wide / deep
SPHERE_RADIUS = 16        # sphere.vmdl is 32 diameter
SPHERE_Z = PEDESTAL_HEIGHT + SPHERE_RADIUS + 4

PEDESTAL_DEFS = [
    dict(
        display="Default PBR",
        style="DefaultPbr",
        desc="The stock complex.vfx shader with no overrides — baseline.",
        sphere_tint="1,1,1,1",
        pedestal_tint="0.55,0.55,0.60,1",
    ),
    dict(
        display="Bright Tint",
        style="BrightTint",
        desc="Same stock shader, saturated red Tint property applied via ModelRenderer.",
        sphere_tint="1.0,0.15,0.20,1",
        pedestal_tint="0.55,0.20,0.20,1",
    ),
    dict(
        display="Glow / Bloom",
        style="GlowEmissive",
        desc="HDR Tint (>1.0 RGB) lights up PostProcessVolume.Bloom — press 1 to toggle.",
        sphere_tint="2.5,1.8,0.4,1",   # HDR yellow
        pedestal_tint="0.35,0.30,0.10,1",
    ),
    dict(
        display="Hologram (custom .shader)",
        style="CustomHologram",
        desc="Authored shader: scrolling scan lines + Fresnel rim, semi-transparent.",
        sphere_tint="1,1,1,1",
        pedestal_tint="0.10,0.30,0.50,1",
    ),
    dict(
        display="Dissolve (custom .shader)",
        style="CustomDissolve",
        desc="Authored shader: procedural 3D noise discard + emissive burn edge.",
        sphere_tint="1,1,1,1",
        pedestal_tint="0.50,0.20,0.10,1",
    ),
    dict(
        display="Metallic Material",
        style="MetallicVmat",
        desc="Hand-authored Material file (complex.vfx with metalness=0.95).",
        sphere_tint="1,1,1,1",
        pedestal_tint="0.25,0.45,0.30,1",
    ),
]

N = len(PEDESTAL_DEFS)
SPACING = 200
START_Y = -((N - 1) / 2.0) * SPACING

pedestal_gos = []
sphere_gos = []
for i, defn in enumerate(PEDESTAL_DEFS):
    y = START_Y + i * SPACING
    pedestal_guid = f"11111111-0000-0000-0000-0000000001{i:02d}"
    pedestal_mr_guid = f"22222222-0000-0000-0000-0000000001{i:02d}"
    pedestal_pedestal_guid = f"33333333-0000-0000-0000-0000000001{i:02d}"
    pedestal_collider_guid = f"44444444-0000-0000-0000-0000000001{i:02d}"
    sphere_guid = f"55555555-0000-0000-0000-0000000001{i:02d}"
    sphere_mr_guid = f"66666666-0000-0000-0000-0000000001{i:02d}"
    sphere_spinner_guid = f"77777777-0000-0000-0000-0000000001{i:02d}"
    sphere_collider_guid = f"88888888-0000-0000-0000-0000000001{i:02d}"

    # Sphere as a TOP-LEVEL sibling — avoids inheriting the pedestal's non-uniform scale.
    # Pedestal.cs walks GameObject.Children to find a child named "Sphere", so instead
    # we put the sphere as a child of the pedestal but at scale that undoes the parent.
    # Simpler: name the sphere with the parent's expected child, parented directly,
    # and undo parent scale via local Scale on the sphere itself.
    inv_x = 1.0 / PEDESTAL_SCALE_XY
    inv_y = 1.0 / PEDESTAL_SCALE_XY
    pedestal_scale_z = PEDESTAL_HEIGHT / 50
    inv_z = 1.0 / pedestal_scale_z

    sphere = go(
        sphere_guid, "Sphere",
        # local Z above pedestal top: pedestal occupies local Z in (-25, +25) at scale 1,
        # we want world Z = PEDESTAL_HEIGHT (top of pedestal) + SPHERE_RADIUS + 6.
        # Pedestal center is at world Z = PEDESTAL_HEIGHT/2.
        # Sphere local Z = (target_world_z - pedestal_center_world_z) / pedestal_scale_z
        pos=f"0,0,{(PEDESTAL_HEIGHT + SPHERE_RADIUS + 6 - PEDESTAL_HEIGHT/2) / pedestal_scale_z}",
        scale=f"{inv_x},{inv_y},{inv_z}",
        components=[
            mr(sphere_mr_guid, model="models/dev/sphere.vmdl", tint=defn["sphere_tint"]),
            cmp("Local.MaterialShowcase.Spinner", sphere_spinner_guid,
                DegreesPerSecond=35.0),
            box_collider(sphere_collider_guid, scale="32,32,32"),
        ],
    )

    pedestal = go(
        pedestal_guid, f"Pedestal_{i}_{defn['style']}",
        pos=f"0,{y},{PEDESTAL_HEIGHT/2}",
        scale=f"{PEDESTAL_SCALE_XY},{PEDESTAL_SCALE_XY},{pedestal_scale_z}",
        components=[
            mr(pedestal_mr_guid, model="models/dev/box.vmdl",
               tint=defn["pedestal_tint"]),
            box_collider(pedestal_collider_guid, scale="50,50,50"),
            cmp("Local.MaterialShowcase.Pedestal", pedestal_pedestal_guid,
                DisplayName=defn["display"],
                Description=defn["desc"],
                Style=defn["style"],
                SphereTint=defn["sphere_tint"],
                PedestalTint=defn["pedestal_tint"]),
        ],
        children=[sphere],
    )
    pedestal_gos.append(pedestal)

# --- Camera ---
camera = go(
    "11111111-0000-0000-0000-000000000020", "Camera",
    # Sit at -X side, looking +X across the row of pedestals. Pitch slightly
    # down (Y of quat ≈ +0.087 = 10°).
    pos="-560,0,200", rot="0,0.0871557,0,0.9961947",
    components=[
        cmp("Sandbox.CameraComponent",
            "22222222-0000-0000-0000-000000000020",
            BackgroundColor="0.05,0.06,0.10,1",
            ClearFlags="All",
            EnablePostProcessing=True,
            FieldOfView=75, FovAxis="Horizontal",
            IsMainCamera=True,
            Orthographic=False, OrthographicHeight=120,
            PostProcessAnchor=None,
            Priority=1,
            RenderExcludeTags="", RenderTags="",
            RenderTexture=None,
            TargetEye="None",
            Viewport="0,0,1,1",
            ZFar=20000, ZNear=1),
    ],
)

# --- GameManager ---
gm = go(
    "11111111-0000-0000-0000-000000000030", "GameManager",
    components=[
        cmp("Local.MaterialShowcase.GameManager",
            "22222222-0000-0000-0000-000000000030",
            AutoPlay=False,
            SecondsPerPedestal=2.0,
            OrbitRadius=380.0,
            OrbitHeight=110.0),
    ],
)

# --- PostProcessVolume + Bloom ---
ppv = go(
    "11111111-0000-0000-0000-000000000040", "PostProcessVolume",
    pos="0,0,200", scale="2000,2000,2000",
    components=[
        cmp("Sandbox.PostProcessVolume",
            "22222222-0000-0000-0000-000000000040",
            Size=20000),
        cmp("Sandbox.Bloom",
            "33333333-0000-0000-0000-000000000040",
            Mode="Additive",
            Strength=0.45,
            Threshold=0.85,
            ThresholdWidth=1.0),
    ],
)

# --- HUD ---
hud = go(
    "11111111-0000-0000-0000-000000000050", "Hud",
    components=[
        cmp("Sandbox.ScreenPanel",
            "22222222-0000-0000-0000-000000000050",
            AutoScreenScale=True, Opacity=1, Scale=1,
            ScaleStrategy="ConsistentHeight",
            TargetCamera=None, ZIndex=100),
    ],
    children=[
        go("33333333-0000-0000-0000-000000000050", "HudPanel",
           components=[cmp("Local.MaterialShowcase.ShowcaseHud",
                           "44444444-0000-0000-0000-000000000050")]),
    ],
)

scene = {
    "__guid": "11111111-0000-0000-0000-000000000000",
    "GameObjects": [
        camera, sun,
        floor, wall_n, wall_s, wall_e, wall_w, ceiling,
        *pedestal_gos,
        gm, ppv, hud,
    ],
    "SceneProperties": {
        "NetworkInterpolation": True,
        "TimeScale": 1,
        "WantsSystemScene": False,
        "Metadata": {},
    },
    "ResourceVersion": 3,
    "Title": "Material Showcase",
    "Description": "Gallery of 6 spheres demoing material variants (stock + 2 custom .shader files).",
    "__references": [],
    "__version": 3,
}

out = Path(__file__).resolve().parent / "main.scene"
out.write_text(json.dumps(scene, indent=2), encoding="utf-8")
print(f"wrote {out} ({out.stat().st_size} bytes)")
