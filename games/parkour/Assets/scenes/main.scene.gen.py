"""Generates Assets/scenes/main.scene for parkour-runner.

Scene is minimal: Camera, Sun, Course (builds platforms in code on OnStart),
Player (a GameObject with the citizen SkinnedModelRenderer + Player component +
a CharacterController), and a ScreenPanel + HudPanel for the HUD.
"""
import json
from pathlib import Path

G = {
    "scene":       "11111111-0000-0000-0000-000000000001",
    "camera":      "11111111-0000-0000-0000-00000000c00a",
    "camera_cmp":  "22222222-0000-0000-0000-00000000c00a",
    "sun":         "11111111-0000-0000-0000-000000000040",
    "sun_cmp":     "22222222-0000-0000-0000-000000000040",
    "course":      "11111111-0000-0000-0000-000000000005",
    "course_cmp":  "22222222-0000-0000-0000-000000000005",
    "player":      "11111111-0000-0000-0000-000000000010",
    "player_cmp":  "22222222-0000-0000-0000-000000000010",
    "player_cc":   "33333333-0000-0000-0000-000000000010",
    "player_smr":  "44444444-0000-0000-0000-000000000010",
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


# Camera: starts behind+above the player start (0,0,60). Player code will then drive it each frame.
camera = go(
    G["camera"], "Camera",
    pos="-180,0,140",
    rot="0,0.1736,0,0.9848",  # 20° down
    components=[
        cmp("Sandbox.CameraComponent", G["camera_cmp"], **NULLS,
            BackgroundColor="0.50,0.65,0.82,1",
            ClearFlags="All",
            EnablePostProcessing=False,
            FieldOfView=75, FovAxis="Horizontal",
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
    G["sun"], "Sun",
    pos="0,0,400", rot="-0.3535534,0.3535534,0.1464466,0.8535534",
    components=[
        cmp("Sandbox.DirectionalLight", G["sun_cmp"], **NULLS,
            FogMode="Disabled", FogStrength=1,
            LightColor="1,0.97,0.9,1.6",
            ShadowBias=0.0005, ShadowCascadeCount=4,
            ShadowCascadeSplitRatio=0.91, ShadowHardness=0,
            Shadows=True,
            SkyColor="0.45,0.55,0.68,1",
            Visualizer={},
            ),
    ],
)

course = go(
    G["course"], "Course", pos="0,0,0",
    components=[cmp("Local.Parkour.Course", G["course_cmp"], **NULLS)],
)

# Player GameObject. Citizen skinned model is a CHILD so we can rotate the
# parent (and capsule) independently of the model offset.
player_model_child = {
    "__guid": "11111111-0000-0000-0000-000000000011",
    "__version": 2, "Flags": 0,
    "Name": "CitizenModel",
    "Position": "0,0,0", "Rotation": "0,0,0,1", "Scale": "1,1,1",
    "Tags": "", "Enabled": True,
    "NetworkMode": 2, "NetworkFlags": 0, "NetworkOrphaned": 0,
    "NetworkTransmit": True, "OwnerTransfer": 1,
    "Components": [
        cmp("Sandbox.SkinnedModelRenderer", G["player_smr"], **NULLS,
            BodyGroups=18446744073709551615,
            CreateAttachments=True,
            CreateBoneObjects=False,
            LodOverride=None, MaterialGroup=None,
            MaterialOverride=None, Materials=None,
            Model="models/citizen_human/citizen_human_male.vmdl",
            RenderOptions={"GameLayer": True, "OverlayLayer": False, "BloomLayer": False, "AfterUILayer": False},
            RenderType="On",
            Tint="1,1,1,1",
            UseAnimGraph=True,
            ),
    ],
    "Children": [],
}

player = go(
    G["player"], "Player", pos="0,0,60",
    components=[
        cmp("Sandbox.CharacterController", G["player_cc"], **NULLS,
            Radius=16, Height=64, StepHeight=18, GroundAngle=50,
            Acceleration=12, Bounciness=0.3,
            UseCollisionRules=False,
            ),
        cmp("Local.Parkour.Player", G["player_cmp"], **NULLS,
            WalkSpeed=200, RunSpeed=400, JumpStrength=350,
            Gravity=900, RotationSpeed=10,
            AutoPlay=True,
            ),
    ],
    children=[player_model_child],
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
           components=[cmp("Local.Parkour.Hud", G["hud_panel_c"], **NULLS)]),
    ],
)

scene = {
    "__guid": G["scene"],
    "GameObjects": [camera, sun, course, player, hud],
    "SceneProperties": {
        "NetworkInterpolation": True,
        "TimeScale": 1,
        "WantsSystemScene": False,
        "Metadata": {},
    },
    "ResourceVersion": 3,
    "Title": "Main",
    "Description": "Parkour Runner: citizen runs an obstacle course of platforms with jumps and ramps.",
    "__references": [],
    "__version": 3,
}

out = Path(__file__).resolve().parent / "main.scene"
out.write_text(json.dumps(scene, indent=2), encoding="utf-8")
print(f"wrote {out} ({out.stat().st_size} bytes)")
