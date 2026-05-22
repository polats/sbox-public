"""Scene builder for fireworks. Run to (re)generate Assets/scenes/main.scene.

Night outdoor scene: huge dark grass ground, fixed pulled-back camera, dim
directional moonlight, a GameManager component to drive launches, a screen
HUD, and an empty PostProcessVolume GameObject so the bloom add-on attaches
to something.
"""
import json
from pathlib import Path

G = {
    "scene":           "11111111-0000-0000-0000-000000000001",
    "camera":          "11111111-0000-0000-0000-000000000010",
    "camera_cmp":      "22222222-0000-0000-0000-000000000010",
    "sun":             "11111111-0000-0000-0000-000000000020",
    "sun_cmp":         "22222222-0000-0000-0000-000000000020",
    "ground":          "11111111-0000-0000-0000-000000000030",
    "ground_model":    "22222222-0000-0000-0000-000000000030",
    "manager":         "11111111-0000-0000-0000-000000000040",
    "manager_cmp":     "22222222-0000-0000-0000-000000000040",
    "ppv":             "11111111-0000-0000-0000-000000000050",
    "ppv_cmp":         "22222222-0000-0000-0000-000000000050",
    "bloom_cmp":       "33333333-0000-0000-0000-000000000050",
    "hud":             "11111111-0000-0000-0000-000000000060",
    "screenpanel":     "22222222-0000-0000-0000-000000000060",
    "hudpanel":        "11111111-0000-0000-0000-000000000061",
    "hudpanel_cmp":    "22222222-0000-0000-0000-000000000061",
}

LIFECYCLE_NULLS = dict(
    OnComponentDestroy=None,
    OnComponentDisabled=None,
    OnComponentEnabled=None,
    OnComponentFixedUpdate=None,
    OnComponentStart=None,
    OnComponentUpdate=None,
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


def model_renderer(guid, model_path, tint):
    return cmp(
        "Sandbox.ModelRenderer", guid, **LIFECYCLE_NULLS,
        BodyGroups=18446744073709551615,
        CreateAttachments=False,
        LodOverride=None,
        MaterialGroup=None,
        MaterialOverride=None,
        Materials=None,
        Model=model_path,
        RenderOptions={"GameLayer": True, "OverlayLayer": False, "BloomLayer": False, "AfterUILayer": False},
        RenderType="On",
        Tint=tint,
    )


# Camera: positioned at (0,-1500,400), looking at origin.
# Quaternion: yaw 90° (Z) then pitch ~15° down (Y) → computed externally.
camera = go(
    G["camera"], "Camera",
    pos="0,-1500,400",
    rot="-0.0918764,0.0918764,0.7011125,0.7011125",
    components=[
        cmp("Sandbox.CameraComponent", G["camera_cmp"], **LIFECYCLE_NULLS,
            BackgroundColor="0.02,0.03,0.08,1",   # near-black dark blue night sky
            ClearFlags="All",
            EnablePostProcessing=True,
            FieldOfView=70,
            FovAxis="Horizontal",
            IsMainCamera=True,
            Orthographic=False,
            OrthographicHeight=1200,
            PostProcessAnchor=None,
            Priority=1,
            RenderExcludeTags="",
            RenderTags="",
            RenderTexture=None,
            TargetEye="None",
            Viewport="0,0,1,1",
            ZFar=20000,
            ZNear=1,
            ),
    ],
)

# Dim moonlight: low intensity, cool color.
sun = go(
    G["sun"], "Moonlight",
    pos="0,0,800", rot="-0.3535534,0.3535534,0.1464466,0.8535534",
    components=[
        cmp("Sandbox.DirectionalLight", G["sun_cmp"], **LIFECYCLE_NULLS,
            FogMode="Enabled",
            FogStrength=1,
            LightColor="0.20,0.22,0.30,1",
            ShadowBias=0.0005,
            ShadowCascadeCount=4,
            ShadowCascadeSplitRatio=0.91,
            ShadowHardness=0,
            Shadows=True,
            SkyColor="0.04,0.05,0.10,1",
            Visualizer={},
            ),
    ],
)

# Ground: dev/box.vmdl is 50×50×50 inch cube. Scale to 2000×2000×40.
ground = go(
    G["ground"], "Ground",
    pos="0,0,-20",
    scale=f"{2000/50},{2000/50},{40/50}",
    components=[
        model_renderer(G["ground_model"], "models/dev/box.vmdl", "0.06,0.10,0.05,1"),
    ],
)

# GameManager — drives launches.
manager = go(
    G["manager"], "GameManager",
    pos="0,0,0",
    components=[
        cmp("Local.Fireworks.GameManager", G["manager_cmp"], **LIFECYCLE_NULLS,
            AutoPlay=False,
            AutoInterval=1.5,
            GroundHalfExtent=900.0,
            SpawnScorchMarks=True,
            ),
    ],
)

# PostProcessVolume + Bloom. The volume box is huge so the camera is always inside.
ppv = go(
    G["ppv"], "PostProcessVolume",
    pos="0,0,500",
    scale="2000,2000,2000",   # not used; volume uses its own size field, but we set scale for safety
    components=[
        cmp("Sandbox.PostProcessVolume", G["ppv_cmp"], **LIFECYCLE_NULLS,
            Size=20000,
            ),
        cmp("Sandbox.Bloom", G["bloom_cmp"], **LIFECYCLE_NULLS,
            Mode="Additive",
            Strength=0.55,
            Threshold=0.5,
            ThresholdWidth=1.0,
            ),
    ],
)

# HUD.
hud = go(
    G["hud"], "Hud",
    components=[
        cmp("Sandbox.ScreenPanel", G["screenpanel"], **LIFECYCLE_NULLS,
            AutoScreenScale=True,
            Opacity=1, Scale=1,
            ScaleStrategy="ConsistentHeight",
            TargetCamera=None,
            ZIndex=100,
            ),
    ],
    children=[
        go(G["hudpanel"], "FireworksHud",
           components=[cmp("Local.Fireworks.FireworksHud", G["hudpanel_cmp"], **LIFECYCLE_NULLS)]),
    ],
)

scene = {
    "__guid": G["scene"],
    "GameObjects": [camera, sun, ground, manager, ppv, hud],
    "SceneProperties": {
        "NetworkInterpolation": True,
        "TimeScale": 1,
        "WantsSystemScene": False,
        "Metadata": {},
    },
    "ResourceVersion": 3,
    "Title": "Fireworks Main",
    "Description": "Night fireworks display: click ground to launch.",
    "__references": [],
    "__version": 3,
}

out = Path(__file__).resolve().parent / "main.scene"
out.write_text(json.dumps(scene, indent=2), encoding="utf-8")
print(f"wrote {out} ({out.stat().st_size} bytes)")
