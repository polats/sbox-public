"""Generates Assets/scenes/main.scene for ctf-arena.

Scene layout (symmetric, X is the long axis):
  -1000 ............ 0 ............. +1000
  RED BASE         midfield         BLUE BASE
  red flag stand                    blue flag stand
  red player spawn                  blue player spawn

Components:
  - Camera (overhead-angled, watches the whole arena)
  - Sun
  - Arena (Floor, 4 perimeter walls, a few cover crates near midfield)
  - RedBase + RedFlag (with Flag component, Team=Red)
  - BlueBase + BlueFlag (Flag component, Team=Blue)
  - RedPlayer (CharacterController + Citizen + CtfPlayer Team=Red, AutoPlay)
  - BluePlayer (same, Team=Blue)
  - GameManager (with AutoPlay flag)
  - HUD (ScreenPanel + CtfHud razor)
"""
import json
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


# ---- Camera (overhead angled, looking down-forward) ----
# Pitch -45 around Y: q=(0, sin(-22.5°)=-0.3827, 0, cos(22.5°)=0.9239)
camera = go(
    gid("camera"), "Camera",
    pos="0,-200,900", rot="-0.27,-0.27,-0.072,0.92",
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
HALF_X = 1200.0  # along play axis
HALF_Y = 600.0   # perpendicular
WALL_H = 80.0
WALL_T = 20.0
arena_children = []

# Floor: dev box is 50, scale to cover.
floor = go(
    gid("floor"), "Floor",
    pos="0,0,-25",
    scale=f"{HALF_X*2/50},{HALF_Y*2/50},1",
    components=[
        box_renderer(gid("floor.r"), tint="0.30,0.40,0.28,1"),
        box_collider(gid("floor.c"), scale="50,50,50", center="0,0,0"),
    ],
)
arena_children.append(floor)

# Red half tint (left)
red_half = go(
    gid("redhalf"), "RedTint",
    pos=f"{-HALF_X/2},0,0.5",
    scale=f"{HALF_X/50},{HALF_Y*2/50},0.05",
    components=[box_renderer(gid("redhalf.r"), tint="0.50,0.28,0.28,1")],
)
arena_children.append(red_half)

blue_half = go(
    gid("bluehalf"), "BlueTint",
    pos=f"{HALF_X/2},0,0.5",
    scale=f"{HALF_X/50},{HALF_Y*2/50},0.05",
    components=[box_renderer(gid("bluehalf.r"), tint="0.28,0.36,0.55,1")],
)
arena_children.append(blue_half)

# Mid-line stripe
midline = go(
    gid("midline"), "MidLine",
    pos="0,0,0.8",
    scale=f"0.2,{HALF_Y*2/50},0.05",
    components=[box_renderer(gid("midline.r"), tint="0.95,0.95,0.95,1")],
)
arena_children.append(midline)

# 4 perimeter walls
for (name, pos, scale) in [
    ("WallN", f"0,{HALF_Y},{WALL_H/2}", f"{HALF_X*2/50},{WALL_T/50},{WALL_H/50}"),
    ("WallS", f"0,{-HALF_Y},{WALL_H/2}", f"{HALF_X*2/50},{WALL_T/50},{WALL_H/50}"),
    ("WallE", f"{HALF_X},0,{WALL_H/2}", f"{WALL_T/50},{HALF_Y*2/50},{WALL_H/50}"),
    ("WallW", f"{-HALF_X},0,{WALL_H/2}", f"{WALL_T/50},{HALF_Y*2/50},{WALL_H/50}"),
]:
    arena_children.append(go(
        gid(f"wall.{name}"), name,
        pos=pos, scale=scale,
        components=[
            box_renderer(gid(f"wall.{name}.r"), tint="0.55,0.50,0.40,1"),
            box_collider(gid(f"wall.{name}.c"), scale="50,50,50", center="0,0,0"),
        ],
    ))

# Cover crates near midfield (symmetric)
for i, (x, y) in enumerate([
    (-200, 280), (-200, -280),
    (200, 280), (200, -280),
    (0, 380), (0, -380),
]):
    sz = 1.8
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


# ---- Base / Flag factories ----
def make_base(team_key, color_tint, base_x):
    """A base = visible platform + a flag (with Flag component) parented on top.

    The flag is a tall colored prism — a slightly elongated dev box.
    """
    # Platform (visual + collider)
    platform = go(
        gid(f"{team_key}.base.platform"), "Platform",
        pos="0,0,20",
        scale=f"{160/50},{160/50},{40/50}",
        components=[
            box_renderer(gid(f"{team_key}.base.platform.r"), tint=color_tint),
            box_collider(gid(f"{team_key}.base.platform.c"), scale="50,50,50", center="0,0,0"),
        ],
    )
    # Flag pole - a separate, taller sliver
    pole = go(
        gid(f"{team_key}.flag.pole"), "Pole",
        pos="0,0,80",  # local to base (parent at base_x)
        scale=f"{8/50},{8/50},{120/50}",
        components=[
            box_renderer(gid(f"{team_key}.flag.pole.r"), tint="0.9,0.85,0.7,1"),
        ],
    )
    # Flag itself (with Flag component + a visible "cloth" box on top)
    flag_visual = go(
        gid(f"{team_key}.flag.cloth"), "FlagCloth",
        pos="22,0,55",  # local to flag GO
        scale=f"{30/50},{6/50},{30/50}",
        components=[
            box_renderer(gid(f"{team_key}.flag.cloth.r"), tint=color_tint),
        ],
    )

    team_enum = "Red" if team_key == "red" else "Blue"
    # The Flag GameObject itself sits at the top of the pole. Its WorldPosition
    # is what HomePosition snapshots in OnStart.
    flag_go = go(
        gid(f"{team_key}.flag"), f"{team_enum}Flag",
        pos=f"{base_x},0,140",
        components=[
            cmp("Local.CtfArena.Flag", gid(f"{team_key}.flag.cmp"), **NULLS,
                Team=team_enum,
                HomePosition=f"{base_x},0,140",
                PickupRadius=150.0),
        ],
        children=[flag_visual],
    )

    base_go = go(
        gid(f"{team_key}.base"), f"{team_enum}Base",
        pos=f"{base_x},0,0",
        children=[platform, pole],
    )
    return base_go, flag_go


red_base, red_flag = make_base("red", "0.95,0.25,0.25,1", -1000)
blue_base, blue_flag = make_base("blue", "0.25,0.5,0.95,1", 1000)


# ---- Citizen player factory ----
def make_player(player_key, team_name, spawn_x, is_local):
    player_smr = cmp(
        "Sandbox.SkinnedModelRenderer", gid(f"{player_key}.smr"), **NULLS,
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
    body = go(
        gid(f"{player_key}.body"), "Body",
        components=[player_smr],
    )

    # Face roughly toward midfield so the bot heading is natural.
    # Red (left) faces +X (yaw 0); Blue (right) faces -X (yaw 180).
    if team_name == "Red":
        rot = "0,0,0,1"
    else:
        rot = "0,0,1,0"  # yaw 180

    return go(
        gid(player_key), f"{team_name}Player",
        pos=f"{spawn_x},0,40",
        rot=rot,
        components=[
            cmp("Sandbox.CharacterController", gid(f"{player_key}.cc"), **NULLS,
                Radius=16, Height=64, StepHeight=18, GroundAngle=50,
                Acceleration=12, Bounciness=0.3,
                UseCollisionRules=False),
            cmp("Local.CtfArena.CtfPlayer", gid(f"{player_key}.cmp"), **NULLS,
                Team=team_name,
                WalkSpeed=180, SprintSpeed=360, CrouchSpeed=90,
                Gravity=900, RotationSpeed=10, JumpSpeed=280,
                AutoPlay=False, IsLocalPlayer=is_local),
        ],
        children=[body],
    )


red_player = make_player("redplayer", "Red", -850, is_local=True)
blue_player = make_player("blueplayer", "Blue", 850, is_local=False)

# ---- GameManager ----
gm = go(
    gid("gm"), "GameManager",
    components=[
        cmp("Local.CtfArena.GameManager", gid("gm.cmp"), **NULLS,
            AutoPlay=False),  # flip to True when recording autonomous demo video
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
           components=[cmp("Local.CtfArena.CtfHud", gid("hud.panel.c"), **NULLS)]),
    ],
)

scene = {
    "__guid": gid("scene"),
    "GameObjects": [
        camera, sun, arena,
        red_base, red_flag,
        blue_base, blue_flag,
        red_player, blue_player,
        gm, hud,
    ],
    "SceneProperties": {
        "NetworkInterpolation": True,
        "TimeScale": 1,
        "WantsSystemScene": False,
        "Metadata": {},
        "NavMesh": {
            "Enabled": False,
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
    "Title": "CTF Arena",
    "Description": "2-team capture the flag — first to 3.",
    "__references": [],
    "__version": 3,
}

out = Path(__file__).resolve().parent / "main.scene"
out.write_text(json.dumps(scene, indent=2), encoding="utf-8")
print(f"wrote {out} ({out.stat().st_size} bytes)")
