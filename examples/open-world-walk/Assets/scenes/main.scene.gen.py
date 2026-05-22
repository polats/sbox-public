#!/usr/bin/env python3
"""Generate the main.scene file for open-world-walk."""
import json, os

scene = {
  "__guid": "11111111-0000-0000-0000-000000000001",
  "GameObjects": [
    # ---------- Camera ----------
    {
      "__guid": "11111111-0000-0000-0000-00000000c00a",
      "__version": 2,
      "Flags": 0,
      "Name": "Camera",
      "Position": "-300,0,180",
      "Rotation": "0,0.1736,0,0.9848",
      "Scale": "1,1,1",
      "Tags": "",
      "Enabled": True,
      "NetworkMode": 2, "NetworkFlags": 0, "NetworkOrphaned": 0,
      "NetworkTransmit": True, "OwnerTransfer": 1,
      "Components": [
        {
          "__type": "Sandbox.CameraComponent",
          "__guid": "22222222-0000-0000-0000-00000000c00a",
          "__enabled": True, "Flags": 0,
          "OnComponentDestroy": None, "OnComponentDisabled": None,
          "OnComponentEnabled": None, "OnComponentFixedUpdate": None,
          "OnComponentStart": None, "OnComponentUpdate": None,
          "BackgroundColor": "0.55,0.70,0.85,1",
          "ClearFlags": "All",
          "EnablePostProcessing": True,
          "FieldOfView": 70,
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
          "ZFar": 25000,
          "ZNear": 1
        }
      ],
      "Children": []
    },
    # ---------- Sun ----------
    {
      "__guid": "11111111-0000-0000-0000-000000000040",
      "__version": 2,
      "Flags": 0,
      "Name": "Sun",
      "Position": "0,0,600",
      "Rotation": "-0.3535534,0.3535534,0.1464466,0.8535534",
      "Scale": "1,1,1",
      "Tags": "",
      "Enabled": True,
      "NetworkMode": 2, "NetworkFlags": 0, "NetworkOrphaned": 0,
      "NetworkTransmit": True, "OwnerTransfer": 1,
      "Components": [
        {
          "__type": "Sandbox.DirectionalLight",
          "__guid": "22222222-0000-0000-0000-000000000040",
          "__enabled": True, "Flags": 0,
          "OnComponentDestroy": None, "OnComponentDisabled": None,
          "OnComponentEnabled": None, "OnComponentFixedUpdate": None,
          "OnComponentStart": None, "OnComponentUpdate": None,
          "FogMode": "Enabled",
          "FogStrength": 1,
          "LightColor": "1,0.96,0.88,1.8",
          "ShadowBias": 0.0005,
          "ShadowCascadeCount": 4,
          "ShadowCascadeSplitRatio": 0.91,
          "ShadowHardness": 0,
          "Shadows": True,
          "SkyColor": "0.55,0.65,0.78,1",
          "Visualizer": {}
        }
      ],
      "Children": []
    },
    # ---------- Terrain ----------
    {
      "__guid": "11111111-0000-0000-0000-000000000070",
      "__version": 2,
      "Flags": 0,
      "Name": "Terrain",
      "Position": "-1000,-1000,-50",  # centre 2000x2000 around origin
      "Rotation": "0,0,0,1",
      "Scale": "1,1,1",
      "Tags": "",
      "Enabled": True,
      "NetworkMode": 2, "NetworkFlags": 0, "NetworkOrphaned": 0,
      "NetworkTransmit": True, "OwnerTransfer": 1,
      "Components": [
        {
          "__type": "Sandbox.Terrain",
          "__guid": "22222222-0000-0000-0000-000000000070",
          "__enabled": True, "Flags": 0,
          "OnComponentDestroy": None, "OnComponentDisabled": None,
          "OnComponentEnabled": None, "OnComponentFixedUpdate": None,
          "OnComponentStart": None, "OnComponentUpdate": None,
          "Storage": "terrains/world.terrain",
          "MaterialOverride": None,
          "ClipMapLodLevels": 6,
          "ClipMapLodExtentTexels": 256,
          "SubdivisionFactor": 1,
          "SubdivisionLodCount": 3,
          "RenderType": "On",
          # Collider fields
          "Static": True,
          "IsTrigger": False,
          "OnTriggerEnter": None, "OnTriggerExit": None,
          "Surface": None,
        }
      ],
      "Children": []
    },
    # ---------- Volumetric Fog ----------
    {
      "__guid": "11111111-0000-0000-0000-000000000080",
      "__version": 2,
      "Flags": 0,
      "Name": "Fog",
      "Position": "0,0,200",
      "Rotation": "0,0,0,1",
      "Scale": "1,1,1",
      "Tags": "",
      "Enabled": True,
      "NetworkMode": 2, "NetworkFlags": 0, "NetworkOrphaned": 0,
      "NetworkTransmit": True, "OwnerTransfer": 1,
      "Components": [
        {
          "__type": "Sandbox.VolumetricFogVolume",
          "__guid": "22222222-0000-0000-0000-000000000080",
          "__enabled": True, "Flags": 0,
          "OnComponentDestroy": None, "OnComponentDisabled": None,
          "OnComponentEnabled": None, "OnComponentFixedUpdate": None,
          "OnComponentStart": None, "OnComponentUpdate": None,
          # BBox: large volume centered on origin
          "Bounds": {"Mins": "-1200,-1200,-200", "Maxs": "1200,1200,600"},
          "Strength": 0.9,
          "FalloffExponent": 1.0,
        }
      ],
      "Children": []
    },
    # ---------- Scatter ----------
    {
      "__guid": "11111111-0000-0000-0000-000000000090",
      "__version": 2,
      "Flags": 0,
      "Name": "WorldScatter",
      "Position": "0,0,0",
      "Rotation": "0,0,0,1",
      "Scale": "1,1,1",
      "Tags": "",
      "Enabled": True,
      "NetworkMode": 2, "NetworkFlags": 0, "NetworkOrphaned": 0,
      "NetworkTransmit": True, "OwnerTransfer": 1,
      "Components": [
        {
          "__type": "Local.OpenWorldWalk.WorldScatter",
          "__guid": "22222222-0000-0000-0000-000000000090",
          "__enabled": True, "Flags": 0,
          "OnComponentDestroy": None, "OnComponentDisabled": None,
          "OnComponentEnabled": None, "OnComponentFixedUpdate": None,
          "OnComponentStart": None, "OnComponentUpdate": None,
          "TreeCount": 8,
          "RockCount": 15,
          "GrassCount": 50,
          "WorldRadius": 850,
          "Seed": 1337,
        }
      ],
      "Children": []
    },
    # ---------- Player ----------
    {
      "__guid": "11111111-0000-0000-0000-000000000010",
      "__version": 2,
      "Flags": 0,
      "Name": "Player",
      "Position": "0,0,300",
      "Rotation": "0,0,0,1",
      "Scale": "1,1,1",
      "Tags": "",
      "Enabled": True,
      "NetworkMode": 2, "NetworkFlags": 0, "NetworkOrphaned": 0,
      "NetworkTransmit": True, "OwnerTransfer": 1,
      "Components": [
        {
          "__type": "Sandbox.CharacterController",
          "__guid": "33333333-0000-0000-0000-000000000010",
          "__enabled": True, "Flags": 0,
          "OnComponentDestroy": None, "OnComponentDisabled": None,
          "OnComponentEnabled": None, "OnComponentFixedUpdate": None,
          "OnComponentStart": None, "OnComponentUpdate": None,
          "Radius": 16, "Height": 64, "StepHeight": 32,
          "GroundAngle": 60, "Acceleration": 12, "Bounciness": 0.3,
          "UseCollisionRules": False
        },
        {
          "__type": "Local.OpenWorldWalk.Player",
          "__guid": "22222222-0000-0000-0000-000000000010",
          "__enabled": True, "Flags": 0,
          "OnComponentDestroy": None, "OnComponentDisabled": None,
          "OnComponentEnabled": None, "OnComponentFixedUpdate": None,
          "OnComponentStart": None, "OnComponentUpdate": None,
          "WalkSpeed": 200,
          "RunSpeed": 400,
          "RotationSpeed": 10,
          "AutoPlay": False,
        }
      ],
      "Children": [
        {
          "__guid": "11111111-0000-0000-0000-000000000011",
          "__version": 2,
          "Flags": 0,
          "Name": "CitizenModel",
          "Position": "0,0,0",
          "Rotation": "0,0,0,1",
          "Scale": "1,1,1",
          "Tags": "",
          "Enabled": True,
          "NetworkMode": 2, "NetworkFlags": 0, "NetworkOrphaned": 0,
          "NetworkTransmit": True, "OwnerTransfer": 1,
          "Components": [
            {
              "__type": "Sandbox.SkinnedModelRenderer",
              "__guid": "44444444-0000-0000-0000-000000000010",
              "__enabled": True, "Flags": 0,
              "OnComponentDestroy": None, "OnComponentDisabled": None,
              "OnComponentEnabled": None, "OnComponentFixedUpdate": None,
              "OnComponentStart": None, "OnComponentUpdate": None,
              "BodyGroups": 18446744073709551615,
              "CreateAttachments": True,
              "CreateBoneObjects": False,
              "LodOverride": None,
              "MaterialGroup": None,
              "MaterialOverride": None,
              "Materials": None,
              "Model": "models/citizen_human/citizen_human_male.vmdl",
              "RenderOptions": {
                "GameLayer": True,
                "OverlayLayer": False,
                "BloomLayer": False,
                "AfterUILayer": False
              },
              "RenderType": "On",
              "Tint": "1,1,1,1",
              "UseAnimGraph": True
            }
          ],
          "Children": []
        }
      ]
    },
    # ---------- HUD ----------
    {
      "__guid": "11111111-0000-0000-0000-000000000030",
      "__version": 2,
      "Flags": 0,
      "Name": "Hud",
      "Position": "0,0,0",
      "Rotation": "0,0,0,1",
      "Scale": "1,1,1",
      "Tags": "",
      "Enabled": True,
      "NetworkMode": 2, "NetworkFlags": 0, "NetworkOrphaned": 0,
      "NetworkTransmit": True, "OwnerTransfer": 1,
      "Components": [
        {
          "__type": "Sandbox.ScreenPanel",
          "__guid": "22222222-0000-0000-0000-000000000030",
          "__enabled": True, "Flags": 0,
          "OnComponentDestroy": None, "OnComponentDisabled": None,
          "OnComponentEnabled": None, "OnComponentFixedUpdate": None,
          "OnComponentStart": None, "OnComponentUpdate": None,
          "AutoScreenScale": True,
          "Opacity": 1,
          "Scale": 1,
          "ScaleStrategy": "ConsistentHeight",
          "TargetCamera": None,
          "ZIndex": 100
        }
      ],
      "Children": [
        {
          "__guid": "11111111-0000-0000-0000-000000000031",
          "__version": 2,
          "Flags": 0,
          "Name": "HudPanel",
          "Position": "0,0,0",
          "Rotation": "0,0,0,1",
          "Scale": "1,1,1",
          "Tags": "",
          "Enabled": True,
          "NetworkMode": 2, "NetworkFlags": 0, "NetworkOrphaned": 0,
          "NetworkTransmit": True, "OwnerTransfer": 1,
          "Components": [
            {
              "__type": "Local.OpenWorldWalk.Hud",
              "__guid": "22222222-0000-0000-0000-000000000031",
              "__enabled": True, "Flags": 0,
              "OnComponentDestroy": None, "OnComponentDisabled": None,
              "OnComponentEnabled": None, "OnComponentFixedUpdate": None,
              "OnComponentStart": None, "OnComponentUpdate": None,
            }
          ],
          "Children": []
        }
      ]
    },
  ],
  "SceneProperties": {
    "NetworkInterpolation": True,
    "TimeScale": 1,
    "WantsSystemScene": False,
    "Metadata": {}
  },
  "ResourceVersion": 3,
  "Title": "Open World Walk",
  "Description": "Walkable outdoor world with terrain heightmap, scattered clutter, fog and minimap HUD.",
  "__references": [],
  "__version": 3
}

out_path = os.path.join(os.path.dirname(__file__), "main.scene")
with open(out_path, "w") as f:
    json.dump(scene, f, indent=2)
print(f"wrote {out_path}")
