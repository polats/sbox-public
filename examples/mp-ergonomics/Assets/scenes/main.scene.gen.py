"""Generates Assets/scenes/main.scene for mp-ergonomics.

Minimal scene: Camera, Sun, Lobby (builds room+4 fake players at runtime),
local Player (citizen + CharacterController), GameManager, HUD ScreenPanel
hosting the kill-feed/voices/chat/scoreboard panels.
"""
import json
from pathlib import Path

G = {
    "scene":       "11111111-0000-0000-0000-000000000001",
    "camera":      "11111111-0000-0000-0000-00000000c00a",
    "camera_cmp":  "22222222-0000-0000-0000-00000000c00a",
    "sun":         "11111111-0000-0000-0000-000000000040",
    "sun_cmp":     "22222222-0000-0000-0000-000000000040",
    "lobby":       "11111111-0000-0000-0000-000000000005",
    "lobby_cmp":   "22222222-0000-0000-0000-000000000005",
    "gm":          "11111111-0000-0000-0000-000000000007",
    "gm_cmp":      "22222222-0000-0000-0000-000000000007",
    "player":      "11111111-0000-0000-0000-000000000010",
    "player_cmp":  "22222222-0000-0000-0000-000000000010",
    "player_cc":   "33333333-0000-0000-0000-000000000010",
    "player_fp":   "55555555-0000-0000-0000-000000000010",
    "player_child":"11111111-0000-0000-0000-000000000011",
    "player_smr":  "44444444-0000-0000-0000-000000000010",
    "hud":         "11111111-0000-0000-0000-000000000030",
    "screenpanel": "22222222-0000-0000-0000-000000000030",
    "hud_root":    "11111111-0000-0000-0000-000000000031",
    "hud_root_c":  "22222222-0000-0000-0000-000000000031",
    "killfeed":    "11111111-0000-0000-0000-000000000032",
    "killfeed_c":  "22222222-0000-0000-0000-000000000032",
    "voices":      "11111111-0000-0000-0000-000000000033",
    "voices_c":    "22222222-0000-0000-0000-000000000033",
    "chat":        "11111111-0000-0000-0000-000000000034",
    "chat_c":      "22222222-0000-0000-0000-000000000034",
    "score":       "11111111-0000-0000-0000-000000000035",
    "score_c":     "22222222-0000-0000-0000-000000000035",
    "help":        "11111111-0000-0000-0000-000000000036",
    "help_c":      "22222222-0000-0000-0000-000000000036",
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


camera = go(
    G["camera"], "Camera",
    pos="-220,0,140",
    rot="0,0.1736,0,0.9848",
    components=[
        cmp("Sandbox.CameraComponent", G["camera_cmp"], **NULLS,
            BackgroundColor="0.15,0.18,0.25,1",
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

sun = go(
    G["sun"], "Sun",
    pos="0,0,400", rot="-0.3535534,0.3535534,0.1464466,0.8535534",
    components=[
        cmp("Sandbox.DirectionalLight", G["sun_cmp"], **NULLS,
            FogMode="Disabled", FogStrength=1,
            LightColor="1,0.97,0.9,1.4",
            ShadowBias=0.0005, ShadowCascadeCount=4,
            ShadowCascadeSplitRatio=0.91, ShadowHardness=0,
            Shadows=True,
            SkyColor="0.35,0.4,0.55,1",
            Visualizer={}),
    ],
)

lobby = go(
    G["lobby"], "Lobby", pos="0,0,0",
    components=[cmp("Local.MpErgonomics.Lobby", G["lobby_cmp"], **NULLS, BuildOnStart=True)],
)

gm = go(
    G["gm"], "GameManager", pos="0,0,0",
    components=[cmp("Local.MpErgonomics.GameManager", G["gm_cmp"], **NULLS, AutoPlay=False)],
)

# Local player
player_model_child = go(
    G["player_child"], "CitizenModel",
    components=[
        cmp("Sandbox.SkinnedModelRenderer", G["player_smr"], **NULLS,
            BodyGroups=18446744073709551615,
            CreateAttachments=True, CreateBoneObjects=False,
            LodOverride=None, MaterialGroup=None,
            MaterialOverride=None, Materials=None,
            Model="models/citizen_human/citizen_human_male.vmdl",
            RenderOptions={"GameLayer": True, "OverlayLayer": False, "BloomLayer": False, "AfterUILayer": False},
            RenderType="On", Tint="1,1,1,1",
            UseAnimGraph=True),
    ],
)

player = go(
    G["player"], "Player", pos="0,0,16",
    components=[
        cmp("Sandbox.CharacterController", G["player_cc"], **NULLS,
            Radius=16, Height=64, StepHeight=18, GroundAngle=50,
            Acceleration=12, Bounciness=0.3, UseCollisionRules=False),
        cmp("Local.MpErgonomics.Player", G["player_cmp"], **NULLS,
            WalkSpeed=180, RunSpeed=320, RotationSpeed=10, AutoPlay=False),
        cmp("Local.MpErgonomics.FakePlayer", G["player_fp"], **NULLS,
            PlayerName="You", FakeSteamId=76561197960265727,
            Score=12, Deaths=7, Health=100, IsTalking=False,
            Ping=15, IsFriend=False, IsLocal=True),
    ],
    children=[player_model_child],
)

hud = go(
    G["hud"], "Hud",
    components=[
        cmp("Sandbox.ScreenPanel", G["screenpanel"], **NULLS,
            AutoScreenScale=True, Opacity=1, Scale=1,
            ScaleStrategy="ConsistentHeight", TargetCamera=None, ZIndex=100),
    ],
    children=[
        go(G["killfeed"], "KillFeed",
           components=[cmp("Local.MpErgonomics.KillFeedPanel", G["killfeed_c"], **NULLS)]),
        go(G["voices"], "Voices",
           components=[cmp("Local.MpErgonomics.VoicesPanel", G["voices_c"], **NULLS)]),
        go(G["chat"], "Chat",
           components=[cmp("Local.MpErgonomics.ChatPanel", G["chat_c"], **NULLS)]),
        go(G["score"], "Scoreboard",
           components=[cmp("Local.MpErgonomics.ScoreboardPanel", G["score_c"], **NULLS)]),
        go(G["help"], "Help",
           components=[cmp("Local.MpErgonomics.HelpHint", G["help_c"], **NULLS)]),
    ],
)

scene = {
    "__guid": G["scene"],
    "GameObjects": [camera, sun, lobby, gm, player, hud],
    "SceneProperties": {
        "NetworkInterpolation": True,
        "TimeScale": 1,
        "WantsSystemScene": False,
        "Metadata": {},
    },
    "ResourceVersion": 3,
    "Title": "Lobby",
    "Description": "Multiplayer ergonomics demo — scoreboard, nameplates, kill feed, voice indicators, chat.",
    "__references": [],
    "__version": 3,
}

out = Path(__file__).resolve().parent / "main.scene"
out.write_text(json.dumps(scene, indent=2), encoding="utf-8")
print(f"wrote {out} ({out.stat().st_size} bytes)")
