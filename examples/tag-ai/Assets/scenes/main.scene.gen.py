"""Generates Assets/scenes/main.scene for tag-ai.

Scene contents:
  - Camera, Sun
  - Arena (Floor + 4 perimeter walls + 6 scattered crate obstacles, each
    with a BoxCollider so the NavMesh has something to bake around)
  - Player (citizen, CharacterController, Player component)
  - GameManager (spawns NPCs at runtime; bakes navmesh at runtime)
  - HUD (ScreenPanel + TagHud razor)
  - SceneProperties.NavMesh.Enabled = true (so Scene.NavMesh.Generate works)
"""
import json
import math
import random
import uuid
from pathlib import Path

random.seed(7)


def gid(seed):
    """Deterministic UUID from a seed string."""
    return str(uuid.UUID(bytes=uuid.uuid5(uuid.NAMESPACE_OID, seed).bytes))


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


NULLS = dict(
    OnComponentDestroy=None, OnComponentDisabled=None, OnComponentEnabled=None,
    OnComponentFixedUpdate=None, OnComponentStart=None, OnComponentUpdate=None,
)


def cmp(t, guid, **extras):
    base = {"__type": t, "__guid": guid, "__enabled": True, "Flags": 0}
    base.update(extras)
    return base


def box_renderer(guid, model="models/dev/box.vmdl", tint="1,1,1,1"):
    return cmp("Sandbox.ModelRenderer", guid, **NULLS,
               BodyGroups=18446744073709551615,
               CreateAttachments=False,
               LodOverride=None, MaterialGroup=None,
               MaterialOverride=None, Materials=None,
               Model=model,
               RenderOptions={"GameLayer": True, "OverlayLayer": False, "AfterUILayer": False},
               RenderType="On",
               Tint=tint)


def box_collider(guid, scale="50,50,50", center="0,0,0", is_trigger=False):
    return cmp("Sandbox.BoxCollider", guid, **NULLS,
               IsTrigger=is_trigger,
               Scale=scale, Center=center,
               Static=True)


# ---- Camera ----
camera = go(
    gid("camera"), "Camera",
    pos="-300,0,180", rot="0,0.1736,0,0.9848",
    components=[
        cmp("Sandbox.CameraComponent", gid("camera.cmp"), **NULLS,
            BackgroundColor="0.45,0.62,0.85,1",
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
            ZFar=20000, ZNear=1),
    ],
)

# ---- Sun ----
sun = go(
    gid("sun"), "Sun",
    pos="0,0,400", rot="-0.3535534,0.3535534,0.1464466,0.8535534",
    components=[
        cmp("Sandbox.DirectionalLight", gid("sun.cmp"), **NULLS,
            FogMode="Disabled", FogStrength=1,
            LightColor="1,0.97,0.9,1.6",
            ShadowBias=0.0005, ShadowCascadeCount=4,
            ShadowCascadeSplitRatio=0.91, ShadowHardness=0,
            Shadows=True,
            SkyColor="0.45,0.6,0.85,1",
            Visualizer={}),
    ],
)

# ---- Arena ----
HALF = 750.0  # arena half-extent (so total 1500x1500)
WALL_H = 80.0
WALL_T = 20.0

# Floor: dev box is 50x50x50, we scale to (HALF*2)/50 = 30 in X/Y, and 1 in Z (becomes 50 tall, sunk down)
arena_children = []

floor = go(
    gid("floor"), "Floor",
    pos=f"0,0,-25",  # top of 50-tall floor sits at z=0
    scale=f"{HALF*2/50},{HALF*2/50},1",
    components=[
        box_renderer(gid("floor.r"), tint="0.35,0.5,0.32,1"),
        # BoxCollider Scale field is the half-extents-ish in engine units; dev box is 50.
        # Use Scale matching renderer scale by reading collider as a separate component on same GO.
        box_collider(gid("floor.c"), scale="50,50,50", center="0,0,0"),
    ],
)
arena_children.append(floor)

# 4 perimeter walls
wall_specs = [
    # (name, pos, scale_xyz)
    ("WallN", f"0,{HALF},{WALL_H/2}", f"{HALF*2/50},{WALL_T/50},{WALL_H/50}"),
    ("WallS", f"0,{-HALF},{WALL_H/2}", f"{HALF*2/50},{WALL_T/50},{WALL_H/50}"),
    ("WallE", f"{HALF},0,{WALL_H/2}", f"{WALL_T/50},{HALF*2/50},{WALL_H/50}"),
    ("WallW", f"{-HALF},0,{WALL_H/2}", f"{WALL_T/50},{HALF*2/50},{WALL_H/50}"),
]
for (name, pos, scale) in wall_specs:
    wall = go(
        gid(f"wall.{name}"), name,
        pos=pos, scale=scale,
        components=[
            box_renderer(gid(f"wall.{name}.r"), tint="0.7,0.65,0.55,1"),
            box_collider(gid(f"wall.{name}.c"), scale="50,50,50", center="0,0,0"),
        ],
    )
    arena_children.append(wall)

# Scattered cover crates (dev box, smaller scale)
for i in range(8):
    ang = random.random() * math.tau
    r = 200 + random.random() * 450
    x = math.cos(ang) * r
    y = math.sin(ang) * r
    sz = random.uniform(1.4, 2.2)  # 70-110 units cube
    crate = go(
        gid(f"crate.{i}"), f"Crate_{i}",
        pos=f"{x:.1f},{y:.1f},{(50*sz)/2:.1f}",
        scale=f"{sz},{sz},{sz}",
        components=[
            box_renderer(gid(f"crate.{i}.r"), tint="0.55,0.42,0.28,1"),
            box_collider(gid(f"crate.{i}.c"), scale="50,50,50", center="0,0,0"),
        ],
    )
    arena_children.append(crate)

arena = go(gid("arena"), "Arena", children=arena_children)

# ---- Player ----
player_smr = cmp(
    "Sandbox.SkinnedModelRenderer", gid("player.smr"), **NULLS,
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
)
player_child = go(
    gid("player.body"), "Body",
    components=[player_smr],
)
player = go(
    gid("player"), "Player",
    pos="0,0,40",
    components=[
        cmp("Sandbox.CharacterController", gid("player.cc"), **NULLS,
            Radius=16, Height=64, StepHeight=18, GroundAngle=50,
            Acceleration=12, Bounciness=0.3,
            UseCollisionRules=False),
        cmp("Local.TagAi.Player", gid("player.cmp"), **NULLS,
            WalkSpeed=200, RunSpeed=400,
            Gravity=900, RotationSpeed=10,
            TagRange=80,
            AutoPlay=False),  # set true in scene file to record demo video
    ],
    children=[player_child],
)

# ---- GameManager ----
gm = go(
    gid("gm"), "GameManager",
    components=[
        cmp("Local.TagAi.GameManager", gid("gm.cmp"), **NULLS,
            NpcCount=5,
            SpawnRadiusMin=500.0, SpawnRadiusMax=700.0,
            BakeAtRuntime=True),
    ],
)

# ---- HUD ----
hud = go(
    gid("hud"), "Hud",
    components=[
        cmp("Sandbox.ScreenPanel", gid("hud.sp"), **NULLS,
            AutoScreenScale=True, Opacity=1, Scale=1,
            ScaleStrategy="ConsistentHeight",
            TargetCamera=None, ZIndex=100),
    ],
    children=[
        go(gid("hud.panel"), "HudPanel",
           components=[cmp("Local.TagAi.TagHud", gid("hud.panel.c"), **NULLS)]),
    ],
)

scene = {
    "__guid": gid("scene"),
    "GameObjects": [camera, sun, arena, player, gm, hud],
    "SceneProperties": {
        "NetworkInterpolation": True,
        "TimeScale": 1,
        "WantsSystemScene": False,
        "Metadata": {},
        "NavMesh": {
            "Enabled": True,
            "IncludeStaticBodies": True,
            "IncludeKeyframedBodies": True,
            "EditorAutoUpdate": False,
            "AgentHeight": 64,
            "AgentRadius": 16,
            "AgentStepSize": 18,
            "AgentMaxSlope": 40,
            "ExcludedBodies": "",
            "IncludedBodies": "",
            "DeferGeneration": False,
            "CustomBounds": False
        },
    },
    "ResourceVersion": 3,
    "Title": "Tag with AI",
    "Description": "Pathfinding NPCs chase the player; tag them to freeze. Freeze all to win.",
    "__references": [],
    "__version": 3,
}

out = Path(__file__).resolve().parent / "main.scene"
out.write_text(json.dumps(scene, indent=2), encoding="utf-8")
print(f"wrote {out} ({out.stat().st_size} bytes)")
