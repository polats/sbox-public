"""Generates Assets/scenes/main.scene for bodygroup-morph showcase.

Scene: 3 pedestals in a row along +Y axis, each with a citizen SkinnedModelRenderer.
Camera orbits around the center pedestal. Spotlights + dark backdrop. HUD ScreenPanel.
"""
import json
from pathlib import Path

G = {
    "scene":         "11111111-0000-0000-0000-000000000001",
    "camera":        "11111111-0000-0000-0000-00000000c00a",
    "camera_cmp":    "22222222-0000-0000-0000-00000000c00a",
    "sun":           "11111111-0000-0000-0000-000000000040",
    "sun_cmp":       "22222222-0000-0000-0000-000000000040",
    "spot_key":      "11111111-0000-0000-0000-000000000041",
    "spot_key_cmp":  "22222222-0000-0000-0000-000000000041",
    "spot_fill":     "11111111-0000-0000-0000-000000000042",
    "spot_fill_cmp": "22222222-0000-0000-0000-000000000042",
    "spot_rim":      "11111111-0000-0000-0000-000000000043",
    "spot_rim_cmp":  "22222222-0000-0000-0000-000000000043",
    "backdrop":      "11111111-0000-0000-0000-000000000060",
    "backdrop_mr":   "22222222-0000-0000-0000-000000000060",
    "floor":         "11111111-0000-0000-0000-000000000061",
    "floor_mr":      "22222222-0000-0000-0000-000000000061",
    "center":        "11111111-0000-0000-0000-000000000070",
    "ped1":          "11111111-0000-0000-0000-000000000021",
    "ped1_mr":       "22222222-0000-0000-0000-000000000021",
    "ped2":          "11111111-0000-0000-0000-000000000022",
    "ped2_mr":       "22222222-0000-0000-0000-000000000022",
    "ped3":          "11111111-0000-0000-0000-000000000023",
    "ped3_mr":       "22222222-0000-0000-0000-000000000023",
    "citizen1":      "11111111-0000-0000-0000-000000000011",
    "citizen1_smr":  "22222222-0000-0000-0000-000000000011",
    "citizen2":      "11111111-0000-0000-0000-000000000012",
    "citizen2_smr":  "22222222-0000-0000-0000-000000000012",
    "citizen3":      "11111111-0000-0000-0000-000000000013",
    "citizen3_smr":  "22222222-0000-0000-0000-000000000013",
    "mgr":           "11111111-0000-0000-0000-000000000050",
    "mgr_cmp":       "22222222-0000-0000-0000-000000000050",
    "hud":           "11111111-0000-0000-0000-000000000030",
    "screenpanel":   "22222222-0000-0000-0000-000000000030",
    "hud_panel":     "11111111-0000-0000-0000-000000000031",
    "hud_panel_c":   "22222222-0000-0000-0000-000000000031",
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


# Pedestals at y=-220, 0, 220 (along Y axis = left/right in s&box)
PED_Y = [-220, 0, 220]


camera = go(
    G["camera"], "Camera",
    pos="-500,0,170",
    rot="0,0,0,1",
    components=[
        cmp("Sandbox.CameraComponent", G["camera_cmp"], **NULLS,
            BackgroundColor="0.03,0.03,0.05,1",
            ClearFlags="All",
            EnablePostProcessing=True,
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
            LightColor="0.85,0.88,1.0,1",
            ShadowBias=0.0005, ShadowCascadeCount=2,
            ShadowCascadeSplitRatio=0.91, ShadowHardness=0,
            Shadows=False,
            SkyColor="0.20,0.22,0.30,1",
            Visualizer={},
            ),
    ],
)


def spot(guid, cmp_guid, name, pos, rot, color, radius=1200):
    return go(
        guid, name, pos=pos, rot=rot,
        components=[
            cmp("Sandbox.SpotLight", cmp_guid, **NULLS,
                LightColor=color,
                Radius=radius,
                ConeInner=40,
                ConeOuter=70,
                Shadows=True,
                Attenuation=1,
                FogMode="Disabled",
                FogStrength=1,
                ),
        ],
    )


# Three spotlights illuminating the row of pedestals from various angles
# Spotlights aimed downward at pedestal row from above-and-around.
# Rotations roughly point down toward origin; engine normalizes.
spot_key = spot(G["spot_key"], G["spot_key_cmp"], "KeyLight",
                pos="-200,0,300", rot="0,0.3826834,0,0.9238795", color="1,0.92,0.78,40", radius=1500)
spot_fill = spot(G["spot_fill"], G["spot_fill_cmp"], "FillLight",
                 pos="0,400,280", rot="0,0.3826834,0,0.9238795", color="0.55,0.7,1.0,25", radius=1500)
spot_rim = spot(G["spot_rim"], G["spot_rim_cmp"], "RimLight",
                pos="200,-200,280", rot="0,0.3826834,0,0.9238795", color="1.0,0.55,0.85,25", radius=1500)


# Floor (big dark plane)
floor = go(
    G["floor"], "Floor",
    pos="0,0,-50",
    scale="12,12,1",
    components=[
        cmp("Sandbox.ModelRenderer", G["floor_mr"], **NULLS,
            BodyGroups=18446744073709551615,
            LodOverride=None, MaterialGroup=None,
            MaterialOverride=None, Materials=None,
            Model="models/dev/box.vmdl",
            RenderOptions={"GameLayer": True, "OverlayLayer": False, "AfterUILayer": False},
            RenderType="On",
            Tint="0.06,0.06,0.08,1",
            ),
    ],
)

# Backdrop wall behind the citizens (in +X direction since camera is at -X)
backdrop = go(
    G["backdrop"], "Backdrop",
    pos="200,0,150",
    scale="0.3,10,3",
    components=[
        cmp("Sandbox.ModelRenderer", G["backdrop_mr"], **NULLS,
            BodyGroups=18446744073709551615,
            LodOverride=None, MaterialGroup=None,
            MaterialOverride=None, Materials=None,
            Model="models/dev/box.vmdl",
            RenderOptions={"GameLayer": True, "OverlayLayer": False, "AfterUILayer": False},
            RenderType="On",
            Tint="0.08,0.09,0.13,1",
            ),
    ],
)


def pedestal(guid, mr_guid, name, y):
    return go(
        guid, name, pos=f"0,{y},-4",
        scale="1.4,1.4,0.6",
        components=[
            cmp("Sandbox.ModelRenderer", mr_guid, **NULLS,
                BodyGroups=18446744073709551615,
                LodOverride=None, MaterialGroup=None,
                MaterialOverride=None, Materials=None,
                Model="models/dev/box.vmdl",
                RenderOptions={"GameLayer": True, "OverlayLayer": False, "AfterUILayer": False},
                RenderType="On",
                Tint="0.13,0.13,0.17,1",
                ),
        ],
    )


def citizen(guid, smr_guid, name, y):
    # Rotation 180° around Z: quaternion (0,0,1,0) → citizen faces -X (toward camera at -X)
    return go(
        guid, name, pos=f"0,{y},25",
        rot="0,0,1,0",
        components=[
            cmp("Sandbox.SkinnedModelRenderer", smr_guid, **NULLS,
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


pedestals = [pedestal(G[f"ped{i+1}"], G[f"ped{i+1}_mr"], f"Pedestal{i+1}", y) for i, y in enumerate(PED_Y)]
citizens  = [citizen(G[f"citizen{i+1}"], G[f"citizen{i+1}_smr"], f"Citizen{i+1}", y) for i, y in enumerate(PED_Y)]

center = go(G["center"], "Center", pos="0,0,40")

mgr = go(
    G["mgr"], "ShowcaseManager",
    components=[
        cmp("Local.BodygroupMorph.ShowcaseManager", G["mgr_cmp"], **NULLS,
            AutoPlay=False,
            Citizen1={"_type": "component", "component_id": G["citizen1_smr"], "go": G["citizen1"], "component_type": "SkinnedModelRenderer"},
            Citizen2={"_type": "component", "component_id": G["citizen2_smr"], "go": G["citizen2"], "component_type": "SkinnedModelRenderer"},
            Citizen3={"_type": "component", "component_id": G["citizen3_smr"], "go": G["citizen3"], "component_type": "SkinnedModelRenderer"},
            OrbitCamera={"_type": "component", "component_id": G["camera_cmp"], "go": G["camera"], "component_type": "CameraComponent"},
            CenterTarget={"_type": "gameobject", "go": G["center"]},
            OrbitRadius=480.0,
            OrbitHeight=110.0,
            OrbitSpeed=36.0,
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
           components=[cmp("Local.BodygroupMorph.ShowcaseHud", G["hud_panel_c"], **NULLS)]),
    ],
)

scene = {
    "__guid": G["scene"],
    "GameObjects": [camera, sun, spot_key, spot_fill, spot_rim, floor, backdrop,
                    *pedestals, *citizens, center, mgr, hud],
    "SceneProperties": {
        "NetworkInterpolation": True,
        "TimeScale": 1,
        "WantsSystemScene": False,
        "Metadata": {},
    },
    "ResourceVersion": 3,
    "Title": "Bodygroup + Morph Showcase",
    "Description": "Three citizens demonstrating bodygroups, morphs/tints, and combined effects.",
    "__references": [],
    "__version": 3,
}

out = Path(__file__).resolve().parent / "main.scene"
out.write_text(json.dumps(scene, indent=2), encoding="utf-8")
print(f"wrote {out} ({out.stat().st_size} bytes)")
