# Bullet Hell

A minimum-viable bullet-hell prototype for s&box.

## What's in here

```
bullet-hell/
├── bullet-hell.sbproj      # Project manifest (Org: local, Ident: bullet_hell)
├── Code/
│   ├── Player.cs           # Top-down movement, fires bullets on input
│   ├── Enemy.cs            # Hovering enemy, emits radial bullet spreads
│   ├── Bullet.cs           # Straight-line projectile, trigger-based hits
│   └── UI/
│       ├── ScoreHud.razor       # HUD: score + health, with Material Icons
│       └── ScoreHud.razor.scss
└── README.md
```

All code lives in the `Local.BulletHell` namespace (derived from the .sbproj
`Org` + `Ident`).

## Opening the project

1. Launch the s&box editor (Steam → tools → s&box).
2. From the project picker, choose **Add Project** and pick
   `/home/paul/projects/sbox-public/bullet-hell/bullet-hell.sbproj`.
3. The editor will compile `Code/*.cs` and the `.razor` UI on first open.

You'll need to assemble a scene yourself:
- Drop a `Player` component on a GameObject for the player.
- Drop one or more `Enemy` components on GameObjects above the player on the
  XY playfield.
- Add the `ScoreHud` panel to a `ScreenPanel` so it shows on the HUD.
- Add `Collider`s (sphere or box, **IsTrigger** enabled) to player, enemies,
  and bullets so `ITriggerListener.OnTriggerEnter` fires.

The prototype creates raw `GameObject`s for bullets at runtime — no prefab is
required, though you can wire a prefab into `Player.BulletPrefab` if you want
visuals.

## Gameplay

- Move with WASD on the XY plane (top-down).
- Hold **Attack1** (left mouse) to fire a stream of bullets in the +X direction.
- Enemies hover and periodically emit a 10-bullet radial spread (configurable
  per-enemy via `BulletsPerBurst`, `BurstInterval`).
- Player has 3 HP; each enemy bullet hit costs 1. Killing an enemy scores 100.

## Notes

- Bullets despawn after 4 seconds or when leaving a ±2000 unit world bound.
- Single-player only. `[Sync]` attributes are present on `Score`/`Health` so it
  could be extended to networked play, but no host/proxy split is implemented.
- HUD icon uses the Material Icons font (`scoreboard`, `favorite`). Do not
  swap these for emoji — Wine's DirectWrite can't render emoji glyphs reliably.
