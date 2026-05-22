#!/usr/bin/env python3
"""Generate main.scene for the ragdoll-cannon project."""
import json
from pathlib import Path

def go(guid, name, pos="0,0,0", rot="0,0,0,1", scale="1,1,1", components=None, children=None):
    return {"__guid": guid, "__version": 2, "Flags": 0, "Name": name,
            "Position": pos, "Rotation": rot, "Scale": scale, "Tags": "",
            "Enabled": True, "NetworkMode": 2, "NetworkFlags": 0,
            "NetworkOrphaned": 0, "NetworkTransmit": True, "OwnerTransfer": 1,
            "Components": components or [], "Children": children or []}

def cmp(t, guid, **extras):
    base = {"__type": t, "__guid": guid, "__enabled": True, "Flags": 0,
            "OnComponentDestroy": None, "OnComponentDisabled": None,
            "OnComponentEnabled": None, "OnComponentFixedUpdate": None,
            "OnComponentStart": None, "OnComponentUpdate": None}
    base.update(extras)
    return base

CAM_ROT = "-0.072274566,0.07227456,0.7034035,0.7034034"

scene = {
    "__guid": "11111111-1111-1111-1111-000000000001",
    "GameObjects": [
        go("22222222-2222-2222-2222-000000000010", "Camera",
           pos="0,-1300,350", rot=CAM_ROT,
           components=[cmp("Sandbox.CameraComponent",
                           "33333333-3333-3333-3333-000000000010",
                           BackgroundColor="0.06,0.08,0.12,1",
                           ClearFlags="All",
                           EnablePostProcessing=True,
                           FieldOfView=85,
                           FovAxis="Horizontal",
                           IsMainCamera=True,
                           Orthographic=False,
                           OrthographicHeight=1200,
                           Priority=1,
                           RenderExcludeTags="",
                           RenderTags="",
                           TargetEye="None",
                           Viewport="0,0,1,1",
                           ZFar=20000,
                           ZNear=1)]),
        go("22222222-2222-2222-2222-000000000020", "Sun",
           pos="0,0,800",
           rot="-0.3535534,0.3535534,0.1464466,0.8535534",
           components=[cmp("Sandbox.DirectionalLight",
                           "33333333-3333-3333-3333-000000000020",
                           FogMode="Enabled", FogStrength=0.5,
                           LightColor="1.0,0.95,0.85,1",
                           SkyColor="0.35,0.45,0.55,1",
                           ShadowBias=0.0005, ShadowCascadeCount=4,
                           ShadowCascadeSplitRatio=0.91, ShadowHardness=0,
                           Shadows=True, Visualizer={})]),
        go("22222222-2222-2222-2222-000000000040", "GameManager",
           components=[cmp("Local.RagdollCannon.GameManager",
                           "33333333-3333-3333-3333-000000000040",
                           AutoPlay=False, AutoInterval=2.5, CooldownSec=1.5)]),
        go("22222222-2222-2222-2222-000000000050", "PostProcessVolume",
           pos="0,0,500", scale="2000,2000,2000",
           components=[cmp("Sandbox.PostProcessVolume",
                           "33333333-3333-3333-3333-000000000050", Size=20000),
                       cmp("Sandbox.Bloom",
                           "33333333-3333-3333-3333-000000000051",
                           Mode="Additive", Strength=0.35, Threshold=0.7,
                           ThresholdWidth=1.0)]),
        go("22222222-2222-2222-2222-000000000060", "Hud",
           components=[cmp("Sandbox.ScreenPanel",
                           "33333333-3333-3333-3333-000000000060",
                           AutoScreenScale=True, Opacity=1, Scale=1,
                           ScaleStrategy="ConsistentHeight",
                           TargetCamera=None, ZIndex=100)],
           children=[
               go("22222222-2222-2222-2222-000000000061", "RagdollHud",
                  components=[cmp("Local.RagdollCannon.RagdollHud",
                                  "33333333-3333-3333-3333-000000000061")])
           ]),
    ],
    "SceneProperties": {
        "NetworkInterpolation": True,
        "TimeScale": 1,
        "WantsSystemScene": False,
        "Metadata": {}
    },
    "ResourceVersion": 3,
    "Title": "Ragdoll Cannon Main",
    "Description": "Fire citizen ragdolls down a course of pendulums and a trampoline into a goal pit.",
    "__references": [],
    "__version": 3
}

Path(__file__).with_name("main.scene").write_text(json.dumps(scene, indent=2))
print("wrote main.scene")
