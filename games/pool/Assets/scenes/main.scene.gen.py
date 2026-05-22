"""Generates Assets/scenes/main.scene for pool/9-ball.

Table is in the XY plane, centered at origin. Camera looks down-and-along
the -X axis from above to give a 3/4 view of the felt.
The GameController builds the table/cushions/pockets/balls in code at OnStart.
"""
import json
import math
from pathlib import Path

G = {
    "scene":       "11111111-0000-0000-0000-000000000001",
    "camera":      "11111111-0000-0000-0000-00000000c00a",
    "camera_cmp":  "22222222-0000-0000-0000-00000000c00a",
    "sun":         "11111111-0000-0000-0000-000000000040",
    "sun_cmp":     "22222222-0000-0000-0000-000000000040",
    "controller":  "11111111-0000-0000-0000-000000000005",
    "ctrl_cmp":    "22222222-0000-0000-0000-000000000005",
    "aimline":     "11111111-0000-0000-0000-000000000007",
    "aimline_cmp": "22222222-0000-0000-0000-000000000007",
    "aimline_mr":  "33333333-0000-0000-0000-000000000007",
    "ground":      "11111111-0000-0000-0000-000000000050",
    "ground_mr":   "22222222-0000-0000-0000-000000000050",
    "hud":         "11111111-0000-0000-0000-000000000030",
    "screenpanel": "22222222-0000-0000-0000-000000000030",
    "hud_panel":   "11111111-0000-0000-0000-000000000031",
    "hud_panel_c": "22222222-0000-0000-0000-000000000031",
}

NULLS = dict(
    OnComponentDestroy=None, OnComponentDisabled=None, OnComponentEnabled=None,
    OnComponentFixedUpdate=None, OnComponentStart=None, OnComponentUpdate=None,
)


def go(guid, name, pos="0,0,0", rot="0,0,0,1", scale="1,1,1", components=None, children=None):
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
    base.update(extras)
    return base


# Camera: position above + behind the cue ball end of the table (cue spot at x = -25).
# Look at table center (0,0,0) from (-90, 0, 80) — angle a bit toward +X and down.
# We aim toward (0,0,0) from (-90,0,80) → forward = (90, 0, -80) normalized.
# Yaw 0 (forward = +X). Pitch arctan(80/90) ~ 41.6° downward.
# Quaternion: yaw 0, pitch P down (rot around Y by +P): (0, sin(P/2), 0, cos(P/2))
pitch = math.degrees(math.atan2(80, 90))
half = math.radians(pitch * 0.5)
camrot = f"0,{math.sin(half):.6f},0,{math.cos(half):.6f}"

camera = go(
    G["camera"], "Camera",
    pos="-90,0,80",
    rot=camrot,
    components=[
        cmp("Sandbox.CameraComponent", G["camera_cmp"], **NULLS,
            BackgroundColor="0.05,0.08,0.05,1",
            ClearFlags="All",
            EnablePostProcessing=False,
            FieldOfView=70, FovAxis="Horizontal",
            IsMainCamera=True,
            Orthographic=False, OrthographicHeight=120,
            PostProcessAnchor=None,
            Priority=1,
            RenderExcludeTags="", RenderTags="",
            RenderTexture=None,
            TargetEye="None",
            Viewport="0,0,1,1",
            ZFar=10000, ZNear=1,
            ),
    ],
)

# Soft warm directional light from above
sun = go(
    G["sun"], "Sun",
    pos="0,0,200", rot="-0.3535534,0.3535534,0.1464466,0.8535534",
    components=[
        cmp("Sandbox.DirectionalLight", G["sun_cmp"], **NULLS,
            FogMode="Disabled", FogStrength=1,
            LightColor="1,0.97,0.9,1.5",
            ShadowBias=0.0005, ShadowCascadeCount=4,
            ShadowCascadeSplitRatio=0.91, ShadowHardness=0,
            Shadows=True,
            SkyColor="0.30,0.32,0.35,1",
            Visualizer={},
            ),
    ],
)

# Ground floor: a dim large box well below the table for visual grounding.
ground = go(
    G["ground"], "Ground", pos="0,0,-40", scale="20,20,0.4",
    components=[
        cmp("Sandbox.ModelRenderer", G["ground_mr"], **NULLS,
            BodyGroups=18446744073709551615,
            CreateAttachments=False,
            LodOverride=None, MaterialGroup=None, MaterialOverride=None, Materials=None,
            Model="models/dev/box.vmdl",
            RenderOptions={"GameLayer": True, "OverlayLayer": False, "BloomLayer": False, "AfterUILayer": False},
            RenderType="On",
            Tint="0.08,0.08,0.10,1",
            ),
    ],
)

controller = go(
    G["controller"], "GameController", pos="0,0,0",
    components=[
        cmp("Local.Pool.GameController", G["ctrl_cmp"], **NULLS),
    ],
)

aimline = go(
    G["aimline"], "AimLine", pos="0,0,0",
    components=[
        cmp("Sandbox.ModelRenderer", G["aimline_mr"], **NULLS,
            BodyGroups=18446744073709551615,
            CreateAttachments=False,
            LodOverride=None, MaterialGroup=None, MaterialOverride=None, Materials=None,
            Model="models/dev/box.vmdl",
            RenderOptions={"GameLayer": True, "OverlayLayer": False, "BloomLayer": False, "AfterUILayer": False},
            RenderType="On",
            Tint="1,1,1,0.7",
            ),
        cmp("Local.Pool.AimLine", G["aimline_cmp"], **NULLS),
    ],
)

hud = go(
    G["hud"], "Hud",
    components=[
        cmp("Sandbox.ScreenPanel", G["screenpanel"], **NULLS,
            AutoScreenScale=True, Opacity=1, Scale=1,
            ScaleStrategy="ConsistentHeight",
            TargetCamera=None, ZIndex=100,
            ),
    ],
    children=[
        go(G["hud_panel"], "HudPanel",
           components=[cmp("Local.Pool.Hud", G["hud_panel_c"], **NULLS)]),
    ],
)

scene = {
    "__guid": G["scene"],
    "GameObjects": [camera, sun, ground, controller, aimline, hud],
    "SceneProperties": {
        "NetworkInterpolation": True,
        "TimeScale": 1,
        "WantsSystemScene": False,
        "Metadata": {},
    },
    "ResourceVersion": 3,
    "Title": "Main",
    "Description": "Pool / 9-ball: aim with mouse, click+drag to charge power, release to shoot.",
    "__references": [],
    "__version": 3,
}

out = Path(__file__).resolve().parent / "main.scene"
out.write_text(json.dumps(scene, indent=2), encoding="utf-8")
print(f"wrote {out} ({out.stat().st_size} bytes)")
