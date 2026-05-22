"""Generates Assets/scenes/main.scene for flappy-bird.

Side view: camera is at +Y looking toward -Y. The play axis is X (pipes scroll
from +X to -X). Bird sits at x=0 and moves only on Z.

Models:
  Bird   - models/citizen_props/balloonregular01.vmdl   (20 x 20 x 28")
  Pipes  - models/sbox_props/gutters/gutter_wall_pipe_b.vmdl (96 x 6 x 6")
           spawned in code by PipeSpawner. Not present in the scene.
  Ground - models/dev/box.vmdl flattened wide
  Sky    - subtle background-colored box high up (cosmetic)
"""
import json
from pathlib import Path

G = {
    "scene":         "11111111-0000-0000-0000-000000000001",
    "camera":        "11111111-0000-0000-0000-00000000c00a",
    "camera_cmp":    "22222222-0000-0000-0000-00000000c00a",
    "sun":           "11111111-0000-0000-0000-000000000040",
    "sun_cmp":       "22222222-0000-0000-0000-000000000040",
    "manager":       "11111111-0000-0000-0000-000000000005",
    "manager_cmp":   "22222222-0000-0000-0000-000000000005",
    "spawner_cmp":   "33333333-0000-0000-0000-000000000005",
    "bird":          "11111111-0000-0000-0000-000000000010",
    "bird_model":    "22222222-0000-0000-0000-000000000010",
    "bird_cmp":      "33333333-0000-0000-0000-000000000010",
    "ground":        "11111111-0000-0000-0000-000000000020",
    "ground_model":  "22222222-0000-0000-0000-000000000020",
    "ceiling":       "11111111-0000-0000-0000-000000000021",
    "ceiling_model": "22222222-0000-0000-0000-000000000021",
    "backdrop":      "11111111-0000-0000-0000-000000000022",
    "backdrop_model":"22222222-0000-0000-0000-000000000022",
    "hud":           "11111111-0000-0000-0000-000000000030",
    "screenpanel":   "22222222-0000-0000-0000-000000000030",
    "hud_panel":     "11111111-0000-0000-0000-000000000031",
    "hud_panel_cmp": "22222222-0000-0000-0000-000000000031",
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


def model_renderer(guid, model_path, tint):
    return cmp(
        "Sandbox.ModelRenderer", guid, **NULLS,
        BodyGroups=18446744073709551615,
        CreateAttachments=False,
        LodOverride=None, MaterialGroup=None, MaterialOverride=None, Materials=None,
        Model=model_path,
        RenderOptions={"GameLayer": True, "OverlayLayer": False, "BloomLayer": False, "AfterUILayer": False},
        RenderType="On",
        Tint=tint,
    )


# Camera: at +Y looking toward -Y. Rotation that maps +X (default forward) to
# -Y is yaw -90° (rotate around +Z by -90°): quaternion (0, 0, -0.7071, 0.7071).
# Centered on the bird (x=0..200 play range), at z=320 (middle of play area).
camera = go(
    G["camera"], "Camera",
    pos="200,-1000,320",
    rot="0,0,0.7071068,0.7071068",
    components=[
        cmp("Sandbox.CameraComponent", G["camera_cmp"], **NULLS,
            BackgroundColor="0.45,0.75,0.95,1",
            ClearFlags="All",
            EnablePostProcessing=False,
            FieldOfView=70, FovAxis="Horizontal",
            IsMainCamera=True,
            Orthographic=True, OrthographicHeight=800,
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

sun = go(
    G["sun"], "Sun",
    pos="0,0,800", rot="-0.3535534,0.3535534,0.1464466,0.8535534",
    components=[
        cmp("Sandbox.DirectionalLight", G["sun_cmp"], **NULLS,
            FogMode="Disabled", FogStrength=1,
            LightColor="1,0.97,0.9,1",
            ShadowBias=0.0005, ShadowCascadeCount=4,
            ShadowCascadeSplitRatio=0.91, ShadowHardness=0,
            Shadows=True,
            SkyColor="0.45,0.65,0.85,1",
            Visualizer={},
            ),
    ],
)

# Bird: balloonregular01 verified = 20 × 20.5 × 28.4 in. Already bird-sized,
# scale 1. Tinted yellow for that classic Flappy Bird palette.
bird = go(
    G["bird"], "Bird", pos="-150,0,300", scale="3,3,3",
    components=[
        model_renderer(G["bird_model"], "models/citizen_props/balloonregular01.vmdl", "1,0.85,0.15,1"),
        cmp("Local.FlappyBird.Bird", G["bird_cmp"], **NULLS,
            Gravity=900.0, FlapVelocity=380.0, MaxFallSpeed=700.0,
            StartPosition="-150,0,320",
            CollisionRadius=42.0,
            GroundZ=60.0,
            CeilingZ=620.0,
            ),
    ],
)

# Ground: dev box scaled long and flat. Box is 50x50x50, so scale 40,4,0.4
# = 2000 x 200 x 20 in. Sits at z=0 so its top is at z=10.
ground = go(
    G["ground"], "Ground", pos="0,0,0", scale="40,4,0.6",
    components=[
        model_renderer(G["ground_model"], "models/dev/box.vmdl", "0.85,0.65,0.35,1"),
    ],
)

# Backdrop: a wide tall flat box far behind the play plane to fake a sky color.
backdrop = go(
    G["backdrop"], "Backdrop", pos="200,200,400", scale="60,0.2,16",
    components=[
        model_renderer(G["backdrop_model"], "models/dev/box.vmdl", "0.55,0.78,0.95,1"),
    ],
)

# GameManager + spawner sit on a top-level GameObject.
manager = go(
    G["manager"], "GameManager", pos="0,0,0",
    components=[
        cmp("Local.FlappyBird.GameManager", G["manager_cmp"], **NULLS),
        cmp("Local.FlappyBird.PipeSpawner", G["spawner_cmp"], **NULLS,
            SpawnInterval=1.8, SpawnX=600.0,
            GapSize=180.0, GapMinCenter=220.0, GapMaxCenter=440.0,
            ScrollSpeed=220.0,
            PipeModel="models/sbox_props/gutters/gutter_wall_pipe_b.vmdl",
            PipeScale="8,8,8",
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
           components=[cmp("Local.FlappyBird.Hud", G["hud_panel_cmp"], **NULLS)]),
    ],
)

scene = {
    "__guid": G["scene"],
    "GameObjects": [camera, sun, backdrop, ground, bird, manager, hud],
    "SceneProperties": {
        "NetworkInterpolation": True,
        "TimeScale": 1,
        "WantsSystemScene": False,
        "Metadata": {},
    },
    "ResourceVersion": 3,
    "Title": "Main",
    "Description": "Flappy Bird — side scroller with gravity, pipes, score, gameover.",
    "__references": [],
    "__version": 3,
}

out = Path(__file__).resolve().parent / "main.scene"
out.write_text(json.dumps(scene, indent=2), encoding="utf-8")
print(f"wrote {out} ({out.stat().st_size} bytes)")
