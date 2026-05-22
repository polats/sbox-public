"""Generates Assets/scenes/main.scene for character-customizer.

Scene: orbit Camera, 3 spotlights, a Citizen (SkinnedModelRenderer) on a platform,
CustomizerManager with refs into camera/citizen/body, plus a ScreenPanel HUD.
"""
import json
from pathlib import Path

G = {
    "scene":       "11111111-0000-0000-0000-000000000001",
    "camera":      "11111111-0000-0000-0000-00000000c00a",
    "camera_cmp":  "22222222-0000-0000-0000-00000000c00a",
    "sun":         "11111111-0000-0000-0000-000000000040",
    "sun_cmp":     "22222222-0000-0000-0000-000000000040",
    "spot_a":      "11111111-0000-0000-0000-000000000041",
    "spot_a_cmp":  "22222222-0000-0000-0000-000000000041",
    "spot_b":      "11111111-0000-0000-0000-000000000042",
    "spot_b_cmp":  "22222222-0000-0000-0000-000000000042",
    "spot_c":      "11111111-0000-0000-0000-000000000043",
    "spot_c_cmp":  "22222222-0000-0000-0000-000000000043",
    "platform":    "11111111-0000-0000-0000-000000000020",
    "platform_mr": "22222222-0000-0000-0000-000000000020",
    "citizen":     "11111111-0000-0000-0000-000000000010",
    "citizen_smr": "22222222-0000-0000-0000-000000000010",
    "mgr":         "11111111-0000-0000-0000-000000000050",
    "mgr_cmp":     "22222222-0000-0000-0000-000000000050",
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


camera = go(
    G["camera"], "Camera",
    pos="-220,0,140",
    rot="0,0,0,1",
    components=[
        cmp("Sandbox.CameraComponent", G["camera_cmp"], **NULLS,
            BackgroundColor="0.04,0.04,0.07,1",
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
            ZFar=20000, ZNear=1,
            ),
    ],
)

sun = go(
    G["sun"], "AmbientSun",
    pos="0,0,400", rot="-0.3535534,0.3535534,0.1464466,0.8535534",
    components=[
        cmp("Sandbox.DirectionalLight", G["sun_cmp"], **NULLS,
            FogMode="Disabled", FogStrength=1,
            LightColor="0.25,0.25,0.32,1",
            ShadowBias=0.0005, ShadowCascadeCount=2,
            ShadowCascadeSplitRatio=0.91, ShadowHardness=0,
            Shadows=False,
            SkyColor="0.05,0.06,0.10,1",
            Visualizer={},
            ),
    ],
)


def spot(guid, cmp_guid, name, pos, rot, color):
    return go(
        guid, name, pos=pos, rot=rot,
        components=[
            cmp("Sandbox.SpotLight", cmp_guid, **NULLS,
                LightColor=color,
                Radius=900,
                ConeInner=35,
                ConeOuter=55,
                Shadows=True,
                Attenuation=1,
                FogMode="Disabled",
                FogStrength=1,
                ),
        ],
    )


# Three spotlights aimed at center (~0,0,80). Pre-computed quaternions:
# - Front: from (250,0,200) looking at (0,0,80) -> roughly downwards & towards origin
# - Right: from (0,250,200) similarly
# - Back: from (-200,0,180) similarly
# We'll use LookAt orientations approximated; sbox will normalize them.
# Front-key spotlight (warm)
spot_a = spot(G["spot_a"], G["spot_a_cmp"], "KeyLight",
              pos="250,80,220", rot="-0.1830,0.1830,0.6830,0.6830", color="1,0.92,0.78,12")
# Rim/side spot (cool blue)
spot_b = spot(G["spot_b"], G["spot_b_cmp"], "RimLight",
              pos="-180,200,210", rot="0,0,0,1", color="0.55,0.7,1.0,9")
# Back-fill (magenta tint)
spot_c = spot(G["spot_c"], G["spot_c_cmp"], "BackFill",
              pos="-220,-150,200", rot="0,0,0,1", color="1.0,0.55,0.85,7")

platform = go(
    G["platform"], "Platform",
    pos="0,0,-2",
    scale="2.4,2.4,0.06",
    components=[
        cmp("Sandbox.ModelRenderer", G["platform_mr"], **NULLS,
            BodyGroups=18446744073709551615,
            LodOverride=None, MaterialGroup=None,
            MaterialOverride=None, Materials=None,
            Model="models/dev/box.vmdl",
            RenderOptions={"GameLayer": True, "OverlayLayer": False, "AfterUILayer": False},
            RenderType="On",
            Tint="0.10,0.10,0.13,1",
            ),
    ],
)

citizen = go(
    G["citizen"], "Citizen", pos="0,0,4",
    components=[
        cmp("Sandbox.SkinnedModelRenderer", G["citizen_smr"], **NULLS,
            BodyGroups=18446744073709551615,
            CreateAttachments=True,
            CreateBoneObjects=False,
            LodOverride=None, MaterialGroup=None,
            MaterialOverride=None, Materials=None,
            Model="models/citizen_human/citizen_human_male.vmdl",
            RenderOptions={"GameLayer": True, "OverlayLayer": False, "AfterUILayer": False},
            RenderType="On",
            Tint="1,1,1,1",
            UseAnimGraph=True,
            ),
    ],
)

mgr = go(
    G["mgr"], "CustomizerManager",
    components=[
        cmp("Local.CharacterCustomizer.CustomizerManager", G["mgr_cmp"], **NULLS,
            AutoPlay=False,  # video uses true; ship value is false
            AutoInterval=2.5,
            BodyRenderer={"_type": "component", "component_id": G["citizen_smr"], "go": G["citizen"], "component_type": "SkinnedModelRenderer"},
            OrbitCamera={"_type": "component", "component_id": G["camera_cmp"], "go": G["camera"], "component_type": "CameraComponent"},
            CitizenRoot={"_type": "gameobject", "go": G["citizen"]},
            OrbitRadius=220.0,
            OrbitHeight=80.0,
            OrbitSpeed=18.0,
            ),
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
           components=[cmp("Local.CharacterCustomizer.CustomizerHud", G["hud_panel_c"], **NULLS)]),
    ],
)

scene = {
    "__guid": G["scene"],
    "GameObjects": [camera, sun, spot_a, spot_b, spot_c, platform, citizen, mgr, hud],
    "SceneProperties": {
        "NetworkInterpolation": True,
        "TimeScale": 1,
        "WantsSystemScene": False,
        "Metadata": {},
    },
    "ResourceVersion": 3,
    "Title": "Character Customizer",
    "Description": "Citizen turntable + clothing swap demo.",
    "__references": [],
    "__version": 3,
}

out = Path(__file__).resolve().parent / "main.scene"
out.write_text(json.dumps(scene, indent=2), encoding="utf-8")
print(f"wrote {out} ({out.stat().st_size} bytes)")
