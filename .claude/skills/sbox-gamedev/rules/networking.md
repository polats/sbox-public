# Networking (`[Sync]`, `[Rpc.*]`, `Connection`, `Networking`)

Learnings from `examples/ctf-arena/` (single-process simulation, real
networking attributes wired). Read this when adding any multiplayer
feature — even if your game is single-player today, knowing the
attributes lets you write code that *would* network correctly.

## Attribute palette

### `[Sync]` — free-running tick replication

Annotate a property; the engine ticks the value to all clients
automatically.

```csharp
[Sync] public int Score { get; set; }
[Sync] public Vector3 NetVelocity { get; set; }
[Sync] public bool IsCrouched { get; set; }
```

### `[Sync(SyncFlags.FromHost)]` — host-authoritative

Only the host's writes propagate; client writes are ignored by the
replicator. Use for game state that must be consistent (scores, win
flags, who-holds-the-flag).

```csharp
[Sync(SyncFlags.FromHost)] public Guid RedFlagCarrier { get; set; }
[Sync(SyncFlags.FromHost)] public Team WinningTeam { get; set; }
```

You still need a code-level guard: `if ( !Networking.IsHost ) return;`
before writing — otherwise client code that *tries* to write does
nothing (no exception, just silently no-ops). Gate explicitly.

### `[Rpc.Broadcast]` — call on host, body runs on every client

```csharp
[Rpc.Broadcast]
public void AnnounceScore( int scorerTeam, int red, int blue )
{
    Sound.Play( scoreSound );
    ShowBanner( $"{scorerTeam} scores!" );
}
```

The body runs **everywhere — including the calling host**. If you
don't want the host to also execute (because it already did the work
that triggered the RPC), use `[Rpc.Broadcast(NetFlags.HostOnly)]` to
mean "send to all clients except host".

### `[Rpc.Host]` — client-to-host

Any client (including the host) calls; the body runs only on the
host. Use for "ask the server to do X".

```csharp
[Rpc.Host]
public void RequestRestart()
{
    if ( !Networking.IsHost ) return;
    ResetScores();
    SetState<LobbyState>();
}
```

### `[Rpc.Owner]` — runs only on the connection that owns the object

```csharp
[Rpc.Owner]
public void ShowOwnerToast( string msg )
{
    HudOverlay.Push( msg );
}
```

Owner of a GameObject is set when it's network-spawned. For player
characters, this is typically the connection that controls them.

## `Networking.IsHost` and `Networking.IsClient`

The booleans you'll guard everything on:

```csharp
if ( !Networking.IsHost ) return;     // host-only logic
if ( Networking.IsClient ) { ... }    // skip on host
```

Single-process play with `GameNetworkType = Multiplayer` (set in the
.sbproj) still has `Networking.IsHost = true` — so host-gated code
runs and produces correct results. The `[Sync]` and `[Rpc.*]`
machinery compiles and runs; it just has no remote peers to replicate
to until you actually connect a client.

## Single-process simulation is a valid demo path

For autonomous workflows where spawning two connected editor
instances isn't practical:

1. Set `GameNetworkType = Multiplayer` and `MaxPlayers = 8` in the
   .sbproj so the engine compiles the networking codegen.
2. Annotate your synced state with the real attributes.
3. Drive both "players" with bots (or one bot + the local input).
4. Document that no remote peer was actually connected — but the code
   patterns are correct and would replicate if a peer joined.

This produces a verifiable artifact (running game with score/state)
without needing to solve the multi-process orchestration problem.

## API gotchas surfaced this run

- **`Scene.IsPlaying` does not exist.** Use `Game.IsPlaying`. Symptom:
  `CS0117 'Scene' does not contain a definition for 'IsPlaying'`. The
  error line reported by the generator is the *post-codegen* line, not
  your source line — can look mysterious.
- **`Guid.IsValid()` does not exist.** That's a GameObject/Component
  helper. To check whether a synced `Guid` field is set, compare to
  `default`:
  ```csharp
  if ( RedFlagCarrier != default ) { ... }
  ```
- **`[Rpc.Broadcast]` body runs on the host too.** When the host
  invokes a Broadcast method, every client (including the host)
  executes the body. If you want client-only execution (e.g. cosmetic
  effects you already played locally on the host), use
  `[Rpc.Broadcast(NetFlags.HostOnly)]` — read the name backwards:
  "host's invocation only goes to other clients."
- **Editor scene-resource cache is aggressive.** Editing `.scene` on
  disk doesn't always refresh — even after `Asset.Compile(true)` and
  `EditorScene.LoadFromResource(...)`. The reliable fix is to restart
  the editor (`sbox-launch --kill && sbox-launch <p> --wait-ready`).
  For iteration, set live values via `sbox-eval` reflection rather
  than scene-file edits.

## Worked-example references

For non-trivial patterns (network spawn, ownership transfer, host
migration, lobby code), study these in this repo:

- `sbox-bombroyale/Code/Player.cs` — `[Sync]`/`[Rpc]` on a full
  multiplayer player Component.
- `sbox-bombroyale/Code/BombRoyale.cs` — game state with
  `Networking.GameLoaded`, host-only spawn, broadcast events.
- `sandbox/Code/Save/SaveSystem.cs` — sync-aware persistence (saves
  collect `[Sync]` values across all components).

## Skip until you actually need them

- `Connection.All` enumeration for per-connection logic.
- `INetworkSerializable` for custom-typed sync state.
- `[HostSync]` (older variant of `[Sync(SyncFlags.FromHost)]`).
- Manual `NetworkSpawn` from arbitrary contexts; let the engine spawn
  via prefab references.
- `Network.AssignOwnership` for transferring control mid-game.
