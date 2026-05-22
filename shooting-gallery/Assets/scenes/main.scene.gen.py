"""Generates Assets/scenes/main.scene for shooting-gallery.

Scene contents:
- Camera at the player's eye (child of Player), looks down +X.
- Sun.
- GameController (builds room + targets in code at OnStart).
- Player at (-200, 0, 0) with Camera child at (0,0,64) eye height.
- HUD (ScreenPanel + HudPanel).
"""
import json
from pathlib import Path

G = {
    "scene":       "11111111-0000-0000-0000-000000000001",
    "sun":         "11111111-0000-0000-0000-000000000040",
    "sun_cmp":     "22222222-0000-0000-0000-000000000040",
    "controller":  "11111111-0000-0000-0000-000000000005",
    "ctrl_cmp":    "22222222-0000-0000-0000-000000000005",
    "player":      "11111111-0000-0000-0000-000000000010",
    "player_cmp":  "22222222-0000-0000-0000-000000000010",
    "camera":      "11111111-0000-0000-0000-00000000c00a",
    "camera_cmp":  "22222222-0000-0000-0000-00000000c00a",
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


# Camera is a child of Player. Player code rotates it each frame.
camera_child = go(
    G["camera"], "Camera",
    pos="0,0,64",       # eye height above player root
    rot="0,0,0,1",
    components=[
        cmp("Sandbox.CameraComponent", G["camera_cmp"], **NULLS,
            BackgroundColor="0.10,0.12,0.16,1",
            ClearFlags="All",
            EnablePostProcessing=False,
            FieldOfView=80, FovAxis="Horizontal",
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
    pos="200,100,500", rot="-0.3535534,0.3535534,0.1464466,0.8535534",
    components=[
        cmp("Sandbox.DirectionalLight", G["sun_cmp"], **NULLS,
            FogMode="Disabled", FogStrength=1,
            LightColor="1,0.95,0.85,1.6",
            ShadowBias=0.0005, ShadowCascadeCount=4,
            ShadowCascadeSplitRatio=0.91, ShadowHardness=0,
            Shadows=True,
            SkyColor="0.45,0.50,0.60,1",
            Visualizer={},
            ),
    ],
)

controller = go(
    G["controller"], "GameController", pos="0,0,0",
    components=[
        cmp("Local.ShootingGallery.GameController", G["ctrl_cmp"], **NULLS,
            GameDuration=60),
    ],
)

# Player rooted at the shooting position; camera is child for first-person view.
player = go(
    G["player"], "Player",
    pos="-200,0,0", rot="0,0,0,1",
    components=[
        cmp("Local.ShootingGallery.Player", G["player_cmp"], **NULLS,
            MouseSensitivity=0.08, MaxTraceDistance=12000,
            AutoPlay=True, AutoFireInterval=0.55,
            ),
    ],
    children=[camera_child],
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
           components=[cmp("Local.ShootingGallery.Hud", G["hud_panel_c"], **NULLS)]),
    ],
)

scene = {
    "__guid": G["scene"],
    "GameObjects": [sun, controller, player, hud],
    "SceneProperties": {
        "NetworkInterpolation": True,
        "TimeScale": 1,
        "WantsSystemScene": False,
        "Metadata": {},
    },
    "ResourceVersion": 3,
    "Title": "Main",
    "Description": "Shooting Gallery: first-person, mouse-look, hitscan, ragdoll targets.",
    "__references": [],
    "__version": 3,
}

out = Path(__file__).resolve().parent / "main.scene"
out.write_text(json.dumps(scene, indent=2), encoding="utf-8")
print(f"wrote {out} ({out.stat().st_size} bytes)")
