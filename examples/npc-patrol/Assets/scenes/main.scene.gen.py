"""Generates Assets/scenes/main.scene for npc-patrol.

Scene layout:
  - Camera, Sun
  - Compound: floor + 4 perimeter walls + 4 interior walls forming corridors
    (each with BoxCollider so navmesh has obstacles to bake around)
  - 4 Waypoints (empty GameObjects at known positions inside the compound)
  - Player (citizen + CharacterController + Player.cs)
  - Guard: parent GO + body child (citizen + Npc layers) + state-indicator sphere child
  - GameManager
  - HUD (ScreenPanel + PatrolHud.razor)
  - SceneProperties.NavMesh.Enabled = true
"""
import json
import math
import uuid
from pathlib import Path


def gid(seed):
    return str(uuid.UUID(bytes=uuid.uuid5(uuid.NAMESPACE_OID, seed).bytes))


def go(guid, name, pos="0,0,0", rot="0,0,0,1", scale="1,1,1",
       tags="", components=None, children=None):
    return {
        "__guid": guid, "__version": 2, "Flags": 0,
        "Name": name, "Position": pos, "Rotation": rot, "Scale": scale,
        "Tags": tags, "Enabled": True,
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


# === Camera ===
camera = go(
    gid("camera"), "Camera",
    pos="-300,0,180", rot="0,0.1736,0,0.9848",
    components=[
        cmp("Sandbox.CameraComponent", gid("camera.cmp"), **NULLS,
            BackgroundColor="0.32,0.36,0.45,1",
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

# === Sun ===
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

# === Compound (floor + perimeter + interior walls) ===
HALF = 800.0   # arena half-extent (1600x1600 total)
WALL_H = 90.0
WALL_T = 24.0

compound_children = []

# Floor (top at z=0)
compound_children.append(go(
    gid("floor"), "Floor",
    pos="0,0,-25",
    scale=f"{HALF*2/50},{HALF*2/50},1",
    components=[
        box_renderer(gid("floor.r"), tint="0.32,0.36,0.4,1"),
        box_collider(gid("floor.c"), scale="50,50,50"),
    ],
))

# 4 perimeter walls
for (name, pos, scl) in [
    ("WallN", f"0,{HALF},{WALL_H/2}", f"{HALF*2/50},{WALL_T/50},{WALL_H/50}"),
    ("WallS", f"0,{-HALF},{WALL_H/2}", f"{HALF*2/50},{WALL_T/50},{WALL_H/50}"),
    ("WallE", f"{HALF},0,{WALL_H/2}", f"{WALL_T/50},{HALF*2/50},{WALL_H/50}"),
    ("WallW", f"{-HALF},0,{WALL_H/2}", f"{WALL_T/50},{HALF*2/50},{WALL_H/50}"),
]:
    compound_children.append(go(
        gid(f"wall.{name}"), name, pos=pos, scale=scl,
        components=[
            box_renderer(gid(f"wall.{name}.r"), tint="0.55,0.5,0.42,1"),
            box_collider(gid(f"wall.{name}.c"), scale="50,50,50"),
        ],
    ))

# Interior walls forming corridors (2 long L-shapes carving the open space)
# Two interior cover walls — short, off-center, so the central area
# is mostly open (lets the guard patrol see the player crossing)
# while still giving the player a hiding spot to lose line of sight.
interior_walls = [
    # (name, x, y, scale-x, scale-y)
    ("CoverN", 350, 250, 6, WALL_T/50),    # 300x24 wall in north-east area
    ("CoverS", -350, -250, 6, WALL_T/50),  # 300x24 wall in south-west area (player can hide behind)
]
for (name, x, y, sx, sy) in interior_walls:
    compound_children.append(go(
        gid(f"iwall.{name}"), name,
        pos=f"{x},{y},{WALL_H/2}",
        scale=f"{sx},{sy},{WALL_H/50}",
        components=[
            box_renderer(gid(f"iwall.{name}.r"), tint="0.62,0.55,0.45,1"),
            box_collider(gid(f"iwall.{name}.c"), scale="50,50,50"),
        ],
    ))

compound = go(gid("compound"), "Compound", children=compound_children)

# === Waypoints (4 corners-ish around the interior obstacles) ===
waypoint_positions = [
    ("Waypoint_0", 500, 500),
    ("Waypoint_1", 500, -500),
    ("Waypoint_2", -500, -500),
    ("Waypoint_3", -500, 500),
]
waypoint_gos = []
for (wname, wx, wy) in waypoint_positions:
    wp_marker = cmp("Sandbox.ModelRenderer", gid(f"wp.{wname}.r"), **NULLS,
                    BodyGroups=18446744073709551615,
                    CreateAttachments=False,
                    LodOverride=None, MaterialGroup=None,
                    MaterialOverride=None, Materials=None,
                    Model="models/dev/sphere.vmdl",
                    RenderOptions={"GameLayer": True, "OverlayLayer": False, "AfterUILayer": False},
                    RenderType="On",
                    Tint="0.4,0.7,1.0,1")
    waypoint_gos.append(go(
        gid(f"wp.{wname}"), wname,
        pos=f"{wx},{wy},10",
        scale="0.25,0.25,0.25",
        components=[wp_marker],
    ))

# === Player ===
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
    Tint="0.7,0.85,1.0,1",
    UseAnimGraph=True,
)
player_child = go(gid("player.body"), "Body", components=[player_smr])

# AutoplayPath as serialized list of Vector3 (s&box JSON list-of-strings)
# Path designed to cross the guard's sight cone twice:
#   start far away (-600,-350) → walk into the open central area (0,0)
#   where the guard patrols → flee south (0,-600) behind cover →
#   come back into the open (0,0) → flee again.
auto_path = [
    "-600,-100,40",  # spawn west, behind the south cover wall start
    "0,0,40",        # cross into the central open area → guard sees player → ALERT/CHASE
    "-600,-450,40",  # flee south-west behind CoverS → guard loses sight (LoseSightDelay=3)
    "-700,-450,40",  # wait behind cover so guard transitions back to Patrolling
    "0,200,40",      # re-emerge into central area → ALERT/CHASE again
]

player = go(
    gid("player"), "Player",
    pos="-600,-100,40", tags="player",
    components=[
        cmp("Sandbox.CharacterController", gid("player.cc"), **NULLS,
            Radius=16, Height=64, StepHeight=18, GroundAngle=50,
            Acceleration=12, Bounciness=0.3,
            UseCollisionRules=False),
        cmp("Local.NpcPatrol.Player", gid("player.cmp"), **NULLS,
            WalkSpeed=200, RunSpeed=400,
            Gravity=900, RotationSpeed=10,
            AutoPlay=False,
            AutoplayPath=auto_path),
    ],
    children=[player_child],
)

# === Guard NPC ===
# Structure: Guard GO contains the Npc/layers/agent; child Body has the SkinnedModelRenderer;
# child StateIndicator has a colored sphere.
guard_smr = cmp(
    "Sandbox.SkinnedModelRenderer", gid("guard.smr"), **NULLS,
    BodyGroups=18446744073709551615,
    CreateAttachments=True,
    CreateBoneObjects=False,
    LodOverride=None, MaterialGroup=None,
    MaterialOverride=None, Materials=None,
    Model="models/citizen_human/citizen_human_male.vmdl",
    RenderOptions={"GameLayer": True, "OverlayLayer": False, "AfterUILayer": False},
    RenderType="On",
    Tint="1,0.85,0.7,1",
    UseAnimGraph=True,
)
guard_body = go(gid("guard.body"), "Body", components=[guard_smr])

state_indicator_renderer = cmp(
    "Sandbox.ModelRenderer", gid("guard.state.r"), **NULLS,
    BodyGroups=18446744073709551615,
    CreateAttachments=False,
    LodOverride=None, MaterialGroup=None,
    MaterialOverride=None, Materials=None,
    Model="models/dev/sphere.vmdl",
    RenderOptions={"GameLayer": True, "OverlayLayer": False, "AfterUILayer": False},
    RenderType="On",
    Tint="0.4,1.0,0.4,1",
)
state_indicator = go(
    gid("guard.state"), "StateIndicator",
    pos="0,0,100", scale="0.4,0.4,0.4",
    components=[
        state_indicator_renderer,
        cmp("Local.NpcPatrol.GuardStateIndicator", gid("guard.state.cmp"), **NULLS,
            Guard={"_type": "component", "component_id": gid("guard.npc"), "go": gid("guard"), "component_type": "GuardNpc"},
            Renderer={"_type": "component", "component_id": gid("guard.state.r"), "go": gid("guard.state"), "component_type": "ModelRenderer"}),
    ],
)

# Waypoints reference list — s&box scene JSON serializes GameObject refs as { "_type": "gameobject", "go": "<guid>" }
waypoint_refs = [
    {"_type": "gameobject", "go": gid(f"wp.{name}")}
    for (name, _, _) in waypoint_positions
]

guard = go(
    gid("guard"), "Guard",
    pos="-100,-100,5", rot="0,0,0,1",
    components=[
        # NavMeshAgent on the guard
        cmp("Sandbox.NavMeshAgent", gid("guard.agent"), **NULLS,
            MaxSpeed=110, Radius=16, Height=64,
            Acceleration=600, Separation=0.5,
            UpdatePosition=True, UpdateRotation=True,
            AllowedAreas=[], ForbiddenAreas=[],
            AllowDefaultArea=True, AutoTraverseLinks=True),
        # Npc layers
        cmp("Local.NpcPatrol.Npcs.Layers.SensesLayer", gid("guard.senses"), **NULLS,
            SightRange=700,
            HearingRange=300,
            SightConeDegrees=140,
            ScanTags="player",
            TargetTags="player"),
        cmp("Local.NpcPatrol.Npcs.Layers.NavigationLayer", gid("guard.nav"), **NULLS,
            StopDistance=16),
        cmp("Local.NpcPatrol.Npcs.Layers.AnimationLayer", gid("guard.anim"), **NULLS,
            LookSpeed=4, MaxHeadAngle=45, AimStrength=1),
        cmp("Local.NpcPatrol.Npcs.Layers.SpeechLayer", gid("guard.speech"), **NULLS,
            Cooldown=4),
        # The Npc itself (GuardNpc)
        cmp("Local.NpcPatrol.GuardNpc", gid("guard.npc"), **NULLS,
            ShowDebugOverlay=False,
            DisplayName="Guard",
            Renderer={"_type": "component", "component_id": gid("guard.smr"), "go": gid("guard.body"), "component_type": "SkinnedModelRenderer"},
            Waypoints=waypoint_refs,
            LoseSightDelay=3,
            ChaseSpeed=220),
    ],
    children=[guard_body, state_indicator],
)

# === GameManager ===
gm = go(
    gid("gm"), "GameManager",
    components=[
        cmp("Local.NpcPatrol.GameManager", gid("gm.cmp"), **NULLS,
            BakeAtRuntime=True),
    ],
)

# === HUD ===
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
           components=[cmp("Local.NpcPatrol.PatrolHud", gid("hud.panel.c"), **NULLS)]),
    ],
)

scene = {
    "__guid": gid("scene"),
    "GameObjects": [camera, sun, compound] + waypoint_gos + [player, guard, gm, hud],
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
            "CustomBounds": False,
        },
    },
    "ResourceVersion": 3,
    "Title": "Layered NPC Patrol",
    "Description": "A guard citizen patrols a compound, alerts/chases on player sight, and returns to patrol on lost sight.",
    "__references": [],
    "__version": 3,
}

out = Path(__file__).resolve().parent / "main.scene"
out.write_text(json.dumps(scene, indent=2), encoding="utf-8")
print(f"wrote {out} ({out.stat().st_size} bytes)")
