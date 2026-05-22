#!/usr/bin/env python3
"""Generate the main.scene file for hammer-level.

The "BSP" brushes are GameObjects with BoxCollider + ModelRenderer pairs,
each one analogous to a CMapMesh in a real binary .vmap. See
Assets/maps/lobby_handauthored.vmap.txt for the schematic and why we did
not author the binary DMX directly.
"""

import json, os, uuid, sys

OUT = os.path.join(os.path.dirname(__file__), "Assets/scenes/main.scene")

def gid(seed: int) -> str:
    return f"aaaaaaaa-0000-0000-0000-{seed:012x}"

def cid(seed: int) -> str:
    return f"bbbbbbbb-0000-0000-0000-{seed:012x}"

_counter = [1000]
def nxt():
    _counter[0] += 1
    return _counter[0]

def go(name, pos=(0,0,0), rot=(0,0,0,1), scale=(1,1,1), components=None, children=None, tags=""):
    sid = nxt()
    return {
        "__guid": gid(sid),
        "__version": 2,
        "Flags": 0,
        "Name": name,
        "Position": f"{pos[0]},{pos[1]},{pos[2]}",
        "Rotation": f"{rot[0]},{rot[1]},{rot[2]},{rot[3]}",
        "Scale": f"{scale[0]},{scale[1]},{scale[2]}",
        "Tags": tags,
        "Enabled": True,
        "NetworkMode": 2,
        "NetworkFlags": 0,
        "NetworkOrphaned": 0,
        "NetworkTransmit": True,
        "OwnerTransfer": 1,
        "Components": components or [],
        "Children": children or [],
    }

def brush(name, pos, size, tint="0.7,0.7,0.75,1", tag="brush"):
    """A 'BSP brush' = static collider + scaled dev/box renderer.

    models/dev/box.vmdl is a 50-unit cube, so scale = size/50.
    """
    sx, sy, sz = size[0]/50.0, size[1]/50.0, size[2]/50.0
    comps = [
        {
            "__type": "Sandbox.ModelRenderer",
            "__guid": cid(nxt()),
            "__enabled": True,
            "Flags": 0,
            "BodyGroups": 18446744073709551615,
            "MaterialOverride": None,
            "Model": "models/dev/box.vmdl",
            "RenderOptions": {"GameLayer": True, "OverlayLayer": False, "BloomLayer": False, "AfterUILayer": False},
            "RenderType": "On",
            "Tint": tint,
        },
        {
            "__type": "Sandbox.BoxCollider",
            "__guid": cid(nxt()),
            "__enabled": True,
            "Flags": 0,
            "Center": "0,0,0",
            "Scale": "50,50,50",
            "Static": True,
            "IsTrigger": False,
            "Friction": None,
            "Surface": None,
            "SurfaceVelocity": "0,0,0",
        },
    ]
    return go(name, pos=pos, scale=(sx,sy,sz), components=comps, tags=tag)

# ---- "Brushes" (BSP analog) ----
# Lobby (Room A), 480 x 480 inches, centered at x=-360
brushes = []
# Lobby
brushes.append(brush("floor_lobby",     (-360,    0,   -8), (480, 480,  16)))
brushes.append(brush("ceiling_lobby",   (-360,    0,  264), (480, 480,  16), tint="0.45,0.45,0.50,1"))
brushes.append(brush("wall_lobby_W",    (-600,    0,  128), ( 16, 480, 256), tint="0.55,0.40,0.35,1"))
brushes.append(brush("wall_lobby_N",    (-360,  240,  128), (480,  16, 256), tint="0.55,0.40,0.35,1"))
brushes.append(brush("wall_lobby_S",    (-360, -240,  128), (480,  16, 256), tint="0.55,0.40,0.35,1"))
brushes.append(brush("wall_lobby_E_a",  (-120,  140,  128), ( 16, 200, 256), tint="0.55,0.40,0.35,1"))
brushes.append(brush("wall_lobby_E_b",  (-120, -140,  128), ( 16, 200, 256), tint="0.55,0.40,0.35,1"))

# Corridor (between rooms)
brushes.append(brush("floor_corr",      (   0,    0,   -8), (240,  80,  16)))
brushes.append(brush("ceiling_corr",    (   0,    0,  264), (240,  80,  16), tint="0.45,0.45,0.50,1"))
brushes.append(brush("wall_corr_N",     (   0,   40,  128), (240,  16, 256), tint="0.55,0.40,0.35,1"))
brushes.append(brush("wall_corr_S",     (   0,  -40,  128), (240,  16, 256), tint="0.55,0.40,0.35,1"))

# Room B
brushes.append(brush("floor_roomB",     ( 360,    0,   -8), (480, 480,  16), tint="0.5,0.6,0.5,1"))
brushes.append(brush("ceiling_roomB",   ( 360,    0,  264), (480, 480,  16), tint="0.45,0.50,0.45,1"))
brushes.append(brush("wall_roomB_E",    ( 600,    0,  128), ( 16, 480, 256), tint="0.4,0.5,0.45,1"))
brushes.append(brush("wall_roomB_N",    ( 360,  240,  128), (480,  16, 256), tint="0.4,0.5,0.45,1"))
brushes.append(brush("wall_roomB_S",    ( 360, -240,  128), (480,  16, 256), tint="0.4,0.5,0.45,1"))
brushes.append(brush("wall_roomB_W_a",  ( 120,  140,  128), ( 16, 200, 256), tint="0.4,0.5,0.45,1"))
brushes.append(brush("wall_roomB_W_b",  ( 120, -140,  128), ( 16, 200, 256), tint="0.4,0.5,0.45,1"))

# World root grouping the brushes
world_go = go("World (BSP)", components=[], children=brushes)

# ---- Lights ----
sun = go(
    "Sun",
    pos=(200, 100, 500),
    rot=(-0.3535534, 0.3535534, 0.1464466, 0.8535534),
    components=[{
        "__type": "Sandbox.DirectionalLight",
        "__guid": cid(nxt()),
        "__enabled": True,
        "Flags": 0,
        "FogMode": "Disabled",
        "FogStrength": 1,
        "LightColor": "1,0.95,0.85,1.4",
        "ShadowBias": 0.0005,
        "ShadowCascadeCount": 4,
        "ShadowCascadeSplitRatio": 0.91,
        "ShadowHardness": 0,
        "Shadows": True,
        "SkyColor": "0.45,0.50,0.60,1",
        "Visualizer": {}
    }],
)

def pointlight(name, pos, color, radius):
    return go(name, pos=pos, components=[{
        "__type": "Sandbox.PointLight",
        "__guid": cid(nxt()),
        "__enabled": True,
        "Flags": 0,
        "Attenuation": 1,
        "FogMode": "Disabled",
        "FogStrength": 1,
        "LightColor": color,
        "Radius": radius,
        "ShadowMode": "On",
        "ShadowHardness": 0,
    }])

light_lobby = pointlight("light_lobby", (-360, 0, 230), "1,0.85,0.6,40", 600)
light_corr  = pointlight("light_corr",  (   0, 0, 230), "0.9,0.95,1.0,30", 300)
light_roomB = pointlight("light_roomB", ( 360, 0, 230), "0.7,1.0,0.8,40", 600)

# ---- GameController ----
game_controller = go("GameController", components=[{
    "__type": "Local.HammerLevel.GameController",
    "__guid": cid(nxt()),
    "__enabled": True,
    "Flags": 0,
    "ImpactSound": None,
}])

# ---- MapInstance (demonstrates the Component, points at shipped vmap) ----
# This is the actual MapInstance Component, wired to the shipped empty_box.vmap.
# In Edit/Play it will TRY to load that map; on Linux without an asset compiler
# we expect it to log "Couldn't find map physics" or similar and gracefully no-op,
# but the wiring is correct and documented.
map_instance_go = go("MapInstance (demo)", pos=(0,0,-2000), components=[{
    "__type": "Sandbox.MapInstance",
    "__guid": cid(nxt()),
    "__enabled": False,  # disabled by default to avoid load errors on Linux
    "Flags": 0,
    "MapName": "maps/templates/empty_box.vmap",
    "UseMapFromLaunch": False,
    "EnableCollision": True,
    "OnMapLoaded": None,
    "OnMapUnloaded": None,
    "NoOrigin": False,
}])

# ---- Player ----
camera = go("Camera",
    pos=(0,0,64),
    components=[{
        "__type": "Sandbox.CameraComponent",
        "__guid": cid(nxt()),
        "__enabled": True,
        "Flags": 0,
        "BackgroundColor": "0.10,0.12,0.16,1",
        "ClearFlags": "All",
        "EnablePostProcessing": False,
        "FieldOfView": 80,
        "FovAxis": "Horizontal",
        "IsMainCamera": True,
        "Orthographic": False,
        "OrthographicHeight": 120,
        "PostProcessAnchor": None,
        "Priority": 1,
        "RenderExcludeTags": "",
        "RenderTags": "",
        "RenderTexture": None,
        "TargetEye": "None",
        "Viewport": "0,0,1,1",
        "ZFar": 20000,
        "ZNear": 1,
    }])

player = go(
    "Player",
    pos=(-400, 0, 40),
    components=[
        {
            "__type": "Local.HammerLevel.Player",
            "__guid": cid(nxt()),
            "__enabled": True,
            "Flags": 0,
            "MouseSensitivity": 0.1,
            "MaxTraceDistance": 12000,
            "WalkSpeed": 180,
            "AutoPlay": False,
            "AutoFireInterval": 0.5,
        },
        {
            "__type": "Sandbox.CharacterController",
            "__guid": cid(nxt()),
            "__enabled": True,
            "Flags": 0,
            "Radius": 16,
            "Height": 64,
            "StepHeight": 18,
            "GroundAngle": 50,
            "Acceleration": 12,
            "Bounciness": 0.3,
            "UseCollisionRules": False,
        },
    ],
    children=[camera],
)

# ---- Targets ----
def target(name, pos):
    return go(name, pos=pos, components=[
        {
            "__type": "Sandbox.ModelRenderer",
            "__guid": cid(nxt()),
            "__enabled": True,
            "Flags": 0,
            "BodyGroups": 18446744073709551615,
            "MaterialOverride": None,
            "Model": "models/citizen/citizen.vmdl",
            "RenderOptions": {"GameLayer": True, "OverlayLayer": False, "BloomLayer": False, "AfterUILayer": False},
            "RenderType": "On",
            "Tint": "1,1,1,1",
        },
        {
            "__type": "Sandbox.CapsuleCollider",
            "__guid": cid(nxt()),
            "__enabled": True,
            "Flags": 0,
            "Start": "0,0,8",
            "End": "0,0,64",
            "Radius": 16,
            "Static": False,
            "IsTrigger": False,
            "Friction": None,
            "Surface": None,
            "SurfaceVelocity": "0,0,0",
        },
        {
            "__type": "Local.HammerLevel.Target",
            "__guid": cid(nxt()),
            "__enabled": True,
            "Flags": 0,
            "RespawnDelay": 5,
            "HitImpulse": 250,
        },
    ])

targets = [
    target("Target_Lobby_1", (-300,  120, 0)),
    target("Target_Lobby_2", (-300, -120, 0)),
    target("Target_RoomB_1", ( 360,  140, 0)),
    target("Target_RoomB_2", ( 480,    0, 0)),
    target("Target_RoomB_3", ( 360, -140, 0)),
]

# ---- HUD ----
hud_panel = go(
    "HudPanel",
    components=[{
        "__type": "Local.HammerLevel.Hud",
        "__guid": cid(nxt()),
        "__enabled": True,
        "Flags": 0,
    }]
)
hud = go(
    "Hud",
    components=[{
        "__type": "Sandbox.ScreenPanel",
        "__guid": cid(nxt()),
        "__enabled": True,
        "Flags": 0,
        "AutoScreenScale": True,
        "Opacity": 1,
        "Scale": 1,
        "ScaleStrategy": "ConsistentHeight",
        "TargetCamera": None,
        "ZIndex": 100,
    }],
    children=[hud_panel],
)

scene = {
    "__guid": "aaaaaaaa-0000-0000-0000-000000000001",
    "GameObjects": [
        sun,
        light_lobby, light_corr, light_roomB,
        game_controller,
        map_instance_go,
        world_go,
        player,
        *targets,
        hud,
    ],
    "SceneProperties": {
        "NetworkInterpolation": True,
        "TimeScale": 1,
        "WantsSystemScene": False,
        "Metadata": {},
    },
    "ResourceVersion": 3,
    "Title": "Hammer Level",
    "Description": "Hammer-style indoor level (BSP brushes via BoxColliders + dev/box).",
    "__references": [],
    "__version": 3,
}

os.makedirs(os.path.dirname(OUT), exist_ok=True)
with open(OUT, "w") as f:
    json.dump(scene, f, indent=2)
print(f"wrote {OUT}")
