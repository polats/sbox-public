"""Scene builder for coin-rush. Run to (re)generate Assets/scenes/main.scene.
Top-down camera, directional light, a player, three coins, and a HUD ScreenPanel.
Colliders for Player and Coin are created in code (OnStart) to avoid scene-JSON
shape gotchas around Sandbox.BoxCollider."""
import json
from pathlib import Path

G = {
    "scene":         "11111111-0000-0000-0000-000000000001",
    "camera":        "11111111-0000-0000-0000-00000000c00a",
    "camera_cmp":    "22222222-0000-0000-0000-00000000c00a",
    "sun":           "11111111-0000-0000-0000-000000000040",
    "sun_cmp":       "22222222-0000-0000-0000-000000000040",
    "player":        "11111111-0000-0000-0000-000000000010",
    "player_model":  "22222222-0000-0000-0000-000000000010",
    "player_cmp":    "22222222-0000-0000-0000-000000000011",
    "coin1":         "11111111-0000-0000-0000-000000000021",
    "coin1_model":   "22222222-0000-0000-0000-000000000021",
    "coin1_cmp":     "33333333-0000-0000-0000-000000000021",
    "coin2":         "11111111-0000-0000-0000-000000000022",
    "coin2_model":   "22222222-0000-0000-0000-000000000022",
    "coin2_cmp":     "33333333-0000-0000-0000-000000000022",
    "coin3":         "11111111-0000-0000-0000-000000000023",
    "coin3_model":   "22222222-0000-0000-0000-000000000023",
    "coin3_cmp":     "33333333-0000-0000-0000-000000000023",
    "hud":           "11111111-0000-0000-0000-000000000030",
    "screenpanel":   "22222222-0000-0000-0000-000000000030",
    "scorepanel":    "11111111-0000-0000-0000-000000000031",
    "scorepanel_c":  "22222222-0000-0000-0000-000000000031",
}

LIFECYCLE_NULLS = dict(
    OnComponentDestroy=None,
    OnComponentDisabled=None,
    OnComponentEnabled=None,
    OnComponentFixedUpdate=None,
    OnComponentStart=None,
    OnComponentUpdate=None,
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
        "Sandbox.ModelRenderer", guid, **LIFECYCLE_NULLS,
        BodyGroups=18446744073709551615,
        CreateAttachments=False,
        LodOverride=None,
        MaterialGroup=None,
        MaterialOverride=None,
        Materials=None,
        Model=model_path,
        RenderOptions={"GameLayer": True, "OverlayLayer": False, "BloomLayer": False, "AfterUILayer": False},
        RenderType="On",
        Tint=tint,
    )


camera = go(
    G["camera"], "Camera",
    pos="100,0,800",
    rot="0,0.7071068,0,0.7071068",
    components=[
        cmp("Sandbox.CameraComponent", G["camera_cmp"], **LIFECYCLE_NULLS,
            BackgroundColor="0.05,0.06,0.10,1",
            ClearFlags="All",
            EnablePostProcessing=False,
            FieldOfView=70,
            FovAxis="Horizontal",
            IsMainCamera=True,
            Orthographic=False,
            OrthographicHeight=1200,
            PostProcessAnchor=None,
            Priority=1,
            RenderExcludeTags="",
            RenderTags="",
            RenderTexture=None,
            TargetEye="None",
            Viewport="0,0,1,1",
            ZFar=10000,
            ZNear=1,
            ),
    ],
)

sun = go(
    G["sun"], "Sun",
    pos="0,0,800", rot="-0.3535534,0.3535534,0.1464466,0.8535534",
    components=[
        cmp("Sandbox.DirectionalLight", G["sun_cmp"], **LIFECYCLE_NULLS,
            FogMode="Enabled",
            FogStrength=1,
            LightColor="1,1,1,1",
            ShadowBias=0.0005,
            ShadowCascadeCount=4,
            ShadowCascadeSplitRatio=0.91,
            ShadowHardness=0,
            Shadows=True,
            SkyColor="0.3,0.4,0.5,1",
            Visualizer={},
            ),
    ],
)

player = go(
    G["player"], "Player", pos="0,0,16", scale="0.5,0.5,0.5",
    components=[
        model_renderer(G["player_model"], "models/citizen_human/citizen_human_male.vmdl", "1,1,1,1"),
        cmp("Local.CoinRush.Player", G["player_cmp"], **LIFECYCLE_NULLS,
            MoveSpeed=350.0,
            ),
    ],
)


def coin(go_guid, model_guid, cmp_guid, pos):
    return go(
        go_guid, "Coin", pos=pos, scale="0.3,0.3,0.3",
        components=[
            model_renderer(model_guid, "models/citizen_props/coin01.vmdl", "1,1,1,1"),
            cmp("Local.CoinRush.Coin", cmp_guid, **LIFECYCLE_NULLS, Value=10),
        ],
    )


coin1 = coin(G["coin1"], G["coin1_model"], G["coin1_cmp"], "200,-200,16")
coin2 = coin(G["coin2"], G["coin2_model"], G["coin2_cmp"], "0,200,16")
coin3 = coin(G["coin3"], G["coin3_model"], G["coin3_cmp"], "-200,-100,16")

hud = go(
    G["hud"], "Hud",
    components=[
        cmp("Sandbox.ScreenPanel", G["screenpanel"], **LIFECYCLE_NULLS,
            AutoScreenScale=True,
            Opacity=1, Scale=1,
            ScaleStrategy="ConsistentHeight",
            TargetCamera=None,
            ZIndex=100,
            ),
    ],
    children=[
        go(G["scorepanel"], "ScoreHud",
           components=[cmp("Local.CoinRush.ScoreHud", G["scorepanel_c"], **LIFECYCLE_NULLS)]),
    ],
)

scene = {
    "__guid": G["scene"],
    "GameObjects": [camera, sun, player, coin1, coin2, coin3, hud],
    "SceneProperties": {
        "NetworkInterpolation": True,
        "TimeScale": 1,
        "WantsSystemScene": False,
        "Metadata": {},
    },
    "ResourceVersion": 3,
    "Title": "Main",
    "Description": "Coin-rush prototype: top-down player collects 3 coins.",
    "__references": [],
    "__version": 3,
}

out = Path(__file__).resolve().parent / "main.scene"
out.write_text(json.dumps(scene, indent=2), encoding="utf-8")
print(f"wrote {out} ({out.stat().st_size} bytes)")
