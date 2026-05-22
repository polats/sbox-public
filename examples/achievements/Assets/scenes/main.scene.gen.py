"""Scene generator for the achievements puzzler.

Layout:
- Flat ground (plane_large) at z=0.
- Citizen player at ( 0, -260, 0 ), spawn safely south of the room.
- Central pedestal at origin (crate prop) — players touching it disqualify
  the pacifist achievement.
- Five colored floor buttons (dev/box scaled flat) on a ~150-inch radius
  circle around the pedestal, in rainbow order going CCW from angle 90°:
    red, yellow, green, blue, purple
- Directional sun + ambient sky.
- 3rd-person camera follows the player.
- HUD ScreenPanel hosting the AchievementPanel + ToastHud Razor panels.
- One GameManager-like GameObject hosting the AchievementManager.

AutoPlay defaults to false; flip the scene value while recording the video.
"""
import json
import math
from pathlib import Path

G = {
    "scene":         "11111111-0000-0000-0000-000000000001",
    "ground":        "11111111-0000-0000-0000-000000000002",
    "ground_model":  "22222222-0000-0000-0000-000000000002",
    "sun":           "11111111-0000-0000-0000-000000000003",
    "sun_cmp":       "22222222-0000-0000-0000-000000000003",
    "camera":        "11111111-0000-0000-0000-000000000004",
    "camera_cmp":    "22222222-0000-0000-0000-000000000004",
    "camera_follow": "33333333-0000-0000-0000-000000000004",
    "manager_go":    "11111111-0000-0000-0000-000000000005",
    "manager_cmp":   "22222222-0000-0000-0000-000000000005",
    "pedestal":      "11111111-0000-0000-0000-000000000006",
    "pedestal_mdl":  "22222222-0000-0000-0000-000000000006",
    "pedestal_cmp":  "33333333-0000-0000-0000-000000000006",
    "player":        "11111111-0000-0000-0000-000000000010",
    "player_smr":    "22222222-0000-0000-0000-000000000010",
    "player_cmp":    "33333333-0000-0000-0000-000000000010",
    "hud":           "11111111-0000-0000-0000-000000000030",
    "screenpanel":   "22222222-0000-0000-0000-000000000030",
    "ach_panel_go":  "11111111-0000-0000-0000-000000000031",
    "ach_panel_cmp": "22222222-0000-0000-0000-000000000031",
    "toast_go":      "11111111-0000-0000-0000-000000000032",
    "toast_cmp":     "22222222-0000-0000-0000-000000000032",
}

# Button data: (color_id, tint, hex_idx)
BUTTONS = [
    ("red",    "1.0,0.18,0.18,1"),
    ("yellow", "1.0,0.92,0.20,1"),
    ("green",  "0.20,0.95,0.30,1"),
    ("blue",   "0.20,0.45,1.0,1"),
    ("purple", "0.75,0.30,1.0,1"),
]

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
        RenderOptions={"GameLayer": True, "OverlayLayer": False, "AfterUILayer": False},
        RenderType="On",
        Tint=tint,
    )


def skinned_model_renderer(guid, model_path):
    return cmp(
        "Sandbox.SkinnedModelRenderer", guid, **LIFECYCLE_NULLS,
        BodyGroups=18446744073709551615,
        CreateAttachments=True,
        LodOverride=None,
        MaterialGroup=None,
        MaterialOverride=None,
        Materials=None,
        Model=model_path,
        RenderOptions={"GameLayer": True, "OverlayLayer": False, "AfterUILayer": False},
        RenderType="On",
        Tint="1,1,1,1",
        UseAnimGraph=True,
    )


# Ground: plane_large is 1024x1024. Place at z=-0.5 so buttons sit on top.
ground = go(
    G["ground"], "Ground", pos="0,0,0", scale="2,2,1",
    components=[ model_renderer(G["ground_model"], "models/dev/plane_large.vmdl", "0.20,0.22,0.28,1") ],
)

sun = go(
    G["sun"], "Sun",
    pos="0,0,800", rot="-0.3535534,0.3535534,0.1464466,0.8535534",
    components=[
        cmp("Sandbox.DirectionalLight", G["sun_cmp"], **LIFECYCLE_NULLS,
            FogMode="Enabled", FogStrength=0.4,
            LightColor="1,0.97,0.92,1",
            ShadowBias=0.0005,
            ShadowCascadeCount=4,
            ShadowCascadeSplitRatio=0.91,
            ShadowHardness=0,
            Shadows=True,
            SkyColor="0.28,0.34,0.45,1",
            Visualizer={},
            ),
    ],
)

camera = go(
    G["camera"], "Camera",
    pos="-260,0,180",
    rot="0,0.2588,0,0.9659",
    components=[
        cmp("Sandbox.CameraComponent", G["camera_cmp"], **LIFECYCLE_NULLS,
            BackgroundColor="0.08,0.10,0.16,1",
            ClearFlags="All",
            EnablePostProcessing=False,
            FieldOfView=75,
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
            ZFar=10000,
            ZNear=1,
        ),
        cmp("Local.Achievements.FollowCamera", G["camera_follow"], **LIFECYCLE_NULLS,
            Offset="-260,0,180",
            LookAtOffset="0,0,55",
            Smoothing=4.0,
        ),
    ],
)

# Central pedestal (crate prop, ~37x36x39 inches)
pedestal = go(
    G["pedestal"], "Pedestal", pos="0,0,0", scale="1,1,1",
    components=[
        model_renderer(G["pedestal_mdl"], "models/citizen_props/crate01.vmdl", "0.6,0.5,0.4,1"),
        cmp("Local.Achievements.Pedestal", G["pedestal_cmp"], **LIFECYCLE_NULLS),
    ],
)

# Five colored buttons on a circle of radius 150, starting at angle 90° and
# going counter-clockwise so rainbow order reads naturally around the room.
buttons = []
RADIUS = 150.0
START_ANGLE_DEG = 90.0
ANGLE_STEP_DEG = 360.0 / len(BUTTONS)
for i, (cid, tint) in enumerate(BUTTONS):
    ang = math.radians(START_ANGLE_DEG + i * ANGLE_STEP_DEG)
    x = math.cos(ang) * RADIUS
    y = math.sin(ang) * RADIUS
    go_id = f"11111111-0000-0000-0000-000000000{100 + i:03d}"
    mdl_id = f"22222222-0000-0000-0000-000000000{100 + i:03d}"
    cmp_id = f"33333333-0000-0000-0000-000000000{100 + i:03d}"
    buttons.append(go(
        go_id, f"Button_{cid}", pos=f"{x:.2f},{y:.2f},2", scale="0.6,0.6,0.08",
        components=[
            model_renderer(mdl_id, "models/dev/box.vmdl", tint),
            cmp("Local.Achievements.ColorButton", cmp_id, **LIFECYCLE_NULLS,
                ColorId=cid, Tint=tint),
        ],
    ))

# Player — citizen at z=0 (CharacterController offsets itself).
player = go(
    G["player"], "Player", pos="0,-260,0",
    components=[
        skinned_model_renderer(G["player_smr"], "models/citizen_human/citizen_human_male.vmdl"),
        cmp("Local.Achievements.PuzzlePlayer", G["player_cmp"], **LIFECYCLE_NULLS,
            MoveSpeed=220.0,
            AutoPlay=False,
        ),
    ],
)

# GameManager-equivalent host
manager = go(
    G["manager_go"], "GameManager",
    components=[
        cmp("Local.Achievements.AchievementManager", G["manager_cmp"], **LIFECYCLE_NULLS,
            ResetOnStart=False,
        ),
    ],
)

# HUD
hud = go(
    G["hud"], "Hud",
    components=[
        cmp("Sandbox.ScreenPanel", G["screenpanel"], **LIFECYCLE_NULLS,
            AutoScreenScale=True, Opacity=1, Scale=1,
            ScaleStrategy="ConsistentHeight",
            TargetCamera=None, ZIndex=100,
        ),
    ],
    children=[
        go(G["ach_panel_go"], "AchievementPanel",
           components=[cmp("Local.Achievements.AchievementPanel", G["ach_panel_cmp"], **LIFECYCLE_NULLS)]),
        go(G["toast_go"], "ToastHud",
           components=[cmp("Local.Achievements.ToastHud", G["toast_cmp"], **LIFECYCLE_NULLS)]),
    ],
)

scene = {
    "__guid": G["scene"],
    "GameObjects": [ground, sun, camera, manager, pedestal, *buttons, player, hud],
    "SceneProperties": {
        "NetworkInterpolation": True,
        "TimeScale": 1,
        "WantsSystemScene": False,
        "Metadata": {},
    },
    "ResourceVersion": 3,
    "Title": "Main",
    "Description": "Achievement-gated puzzler: press buttons in the room to unlock 5 achievements.",
    "__references": [],
    "__version": 3,
}

out = Path(__file__).resolve().parent / "main.scene"
out.write_text(json.dumps(scene, indent=2), encoding="utf-8")
print(f"wrote {out} ({out.stat().st_size} bytes)")
