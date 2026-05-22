"""Generates Assets/scenes/main.scene for tower-defense.

Scene contents:
  - Camera (top-down-ish overhead view of the path)
  - Sun
  - Ground (1500x1500 floor with collider)
  - Path (a visible wider strip down the middle, y in [-40, 40])
  - SpawnZone (visual marker on the left)
  - Base (visual + BaseObject component on the right)
  - 4 perimeter walls + a few decoration crates flanking the path
  - GameManager (handles state, saves, navmesh bake)
  - HUD (ScreenPanel + TdHud razor)
  - SceneProperties.NavMesh.Enabled = true
"""
import json
import math
import uuid
from pathlib import Path


def gid(seed):
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


# ---- Camera (overhead-angled) ----
# Pitch about 55 deg looking down-forward.
# Quaternion for pitch -55 deg around Y axis: (x=0, y=sin(-27.5°)=-0.462, z=0, w=cos(27.5°)=0.887)
camera = go(
    gid("camera"), "Camera",
    pos="0,0,1200", rot="0,-0.462,0,0.887",
    components=[
        cmp("Sandbox.CameraComponent", gid("camera.cmp"), **NULLS,
            BackgroundColor="0.35,0.5,0.7,1",
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
            LightColor="1,0.97,0.9,1.8",
            ShadowBias=0.0005, ShadowCascadeCount=4,
            ShadowCascadeSplitRatio=0.91, ShadowHardness=0,
            Shadows=True,
            SkyColor="0.45,0.6,0.85,1",
            Visualizer={}),
    ],
)

# ---- Arena ----
HALF = 750.0
WALL_H = 80.0
WALL_T = 20.0
arena_children = []

# Floor: dev box is 50x50x50, scale 30x30x1 → 1500x1500x50.
floor = go(
    gid("floor"), "Floor",
    pos="0,0,-25",
    scale=f"{HALF*2/50},{HALF*2/50},1",
    components=[
        box_renderer(gid("floor.r"), tint="0.30,0.40,0.28,1"),
        box_collider(gid("floor.c"), scale="50,50,50", center="0,0,0"),
    ],
)
arena_children.append(floor)

# Path: a brighter strip down the middle, 80 wide, slightly above ground.
path = go(
    gid("path"), "Path",
    pos="0,0,0.5",
    scale=f"{HALF*2/50},{80/50},0.05",
    components=[
        box_renderer(gid("path.r"), tint="0.55,0.50,0.32,1"),
    ],
)
arena_children.append(path)

# 4 perimeter walls
for (name, pos, scale) in [
    ("WallN", f"0,{HALF},{WALL_H/2}", f"{HALF*2/50},{WALL_T/50},{WALL_H/50}"),
    ("WallS", f"0,{-HALF},{WALL_H/2}", f"{HALF*2/50},{WALL_T/50},{WALL_H/50}"),
    ("WallE", f"{HALF},0,{WALL_H/2}", f"{WALL_T/50},{HALF*2/50},{WALL_H/50}"),
    ("WallW", f"{-HALF},0,{WALL_H/2}", f"{WALL_T/50},{HALF*2/50},{WALL_H/50}"),
]:
    arena_children.append(go(
        gid(f"wall.{name}"), name,
        pos=pos, scale=scale,
        components=[
            box_renderer(gid(f"wall.{name}.r"), tint="0.55,0.50,0.40,1"),
            box_collider(gid(f"wall.{name}.c"), scale="50,50,50", center="0,0,0"),
        ],
    ))

# Decorative crates along the path edges to break it up visually.
crate_positions = [
    (-450, 200), (-450, -200), (-150, 250), (-150, -250),
    (150, 200), (150, -200), (450, 250), (450, -250),
    (-300, 350), (300, -350), (0, 400), (0, -400),
]
for i, (x, y) in enumerate(crate_positions):
    sz = 1.6
    arena_children.append(go(
        gid(f"crate.{i}"), f"Crate_{i}",
        pos=f"{x},{y},{(50*sz)/2}",
        scale=f"{sz},{sz},{sz}",
        components=[
            box_renderer(gid(f"crate.{i}.r"), tint="0.45,0.36,0.22,1"),
            box_collider(gid(f"crate.{i}.c"), scale="50,50,50", center="0,0,0"),
        ],
    ))

arena = go(gid("arena"), "Arena", children=arena_children)

# ---- SpawnZone (visual marker, no logic — GameManager spawns at SpawnPoint) ----
spawn_zone = go(
    gid("spawn"), "SpawnZone",
    pos="-600,0,5",
    scale=f"{100/50},{100/50},{10/50}",
    components=[
        box_renderer(gid("spawn.r"), tint="1.0,0.3,0.3,1"),
    ],
)

# ---- Base (with BaseObject component) ----
base_visual = go(
    gid("base.v"), "BaseVisual",
    pos="0,0,40",
    scale=f"{120/50},{120/50},{80/50}",
    components=[
        box_renderer(gid("base.v.r"), tint="0.2,0.7,1.0,1"),
        box_collider(gid("base.v.c"), scale="50,50,50", center="0,0,0"),
    ],
)
base_obj = go(
    gid("base"), "Base",
    pos="600,0,0",
    components=[
        cmp("Local.TowerDefense.BaseObject", gid("base.cmp"), **NULLS,
            MaxHpStat=100.0,
            CurrentHp=100.0),
    ],
    children=[base_visual],
)

# ---- GameManager ----
gm = go(
    gid("gm"), "GameManager",
    components=[
        cmp("Local.TowerDefense.GameManager", gid("gm.cmp"), **NULLS,
            AutoPlay=False,  # flip to True when recording the video
            SpawnPoint="-600,0,0",
            BasePoint="600,0,0"),
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
           components=[cmp("Local.TowerDefense.TdHud", gid("hud.panel.c"), **NULLS)]),
    ],
)

scene = {
    "__guid": gid("scene"),
    "GameObjects": [camera, sun, arena, spawn_zone, base_obj, gm, hud],
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
    "Title": "Tower Defense",
    "Description": "Place towers, defend the base, survive 5 waves of creeps.",
    "__references": [],
    "__version": 3,
}

out = Path(__file__).resolve().parent / "main.scene"
out.write_text(json.dumps(scene, indent=2), encoding="utf-8")
print(f"wrote {out} ({out.stat().st_size} bytes)")
