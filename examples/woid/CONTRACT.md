# woid ↔ s&box contract — v0.1

The boundary between woid's brain-server (Node, source-of-truth for
cognition) and s&box (renderer, world owner, multiplayer host).
Transport-agnostic JSON over HTTP. localhost-only; no auth in v0.1.

Mirror this file at `woid/agent-sandbox/brain-server/CONTRACT.md`. Both
sides must agree before any breaking change.

## Service location

| Service | Default | Env override |
|---|---|---|
| brain-server | `http://127.0.0.1:8080` | `BRAIN_SERVER_PORT` (server-side), `BRAIN_SERVER_URL` (client-side) |

8080 because s&box's `Sandbox.Http` only allows loopback ports
**80, 443, 8080, 8443** from game code unless the editor is launched with
`-allowlocalhttp`. We pick 8080 (canonical alt-HTTP) so the game works
without any launch flag. Clients should respect `BRAIN_SERVER_URL` env
if set, falling back to the default.

## Versioning

Every response includes `"contract": "0.1"` at the top level. Bump on any
schema change that removes or renames a field; additive changes don't
bump. Sbox checks the version on first connect and refuses to start if
it doesn't recognize the major.

```json
{ "contract": "0.1", "ok": true, "data": { ... } }
{ "contract": "0.1", "ok": false, "error": { "code": "...", "message": "..." } }
```

All endpoints return this envelope. `data` shape is endpoint-specific.

---

## Endpoints

SSE is used only where data changes faster than tick rate or where the
user is actively watching a stream. Everywhere else: sync request/response.

| Method | Path | Mode | Notes |
|---|---|---|---|
| GET  | `/status`                  | sync + `?stream` SSE | stream for boot-progress UX |
| GET  | `/verbs`                   | sync | static |
| POST | `/character`               | sync | idempotent on id |
| GET  | `/character/:id`           | sync | rarely changes |
| POST | `/perception/event`        | sync | fire-and-forget write |
| GET  | `/perception/:id`          | sync + `?stream` SSE | events fire between ticks |
| POST | `/brain/step`              | sync; SSE via `Accept: text/event-stream` | stream for LLM token render |
| POST | `/verb/resolve`            | sync | request/response |
| GET  | `/brain/observation/:id`   | sync | tick-rate updates, poll 1-2s |
| GET  | `/brain/actions/:id`       | sync | tick-rate updates, poll 1-2s |
| GET  | `/brain/trace/:id`         | sync | tick-rate updates, poll 1-2s |
| GET  | `/brain/stats/:id`         | sync | slow-moving counters |
| POST | `/persona/generate`        | sync; SSE via `Accept: text/event-stream` | stream for live generation |
| GET  | `/storyteller/cards`       | sync | new card at most every few minutes |

**Streaming surface: 4 endpoints.** They are the only ones where data
arrives faster than 1-2s polling can match: LLM token streams (persona,
brain/step), perception events (between ticks), boot-time download
progress (sub-second).

### SSE event vocabulary

All four streams use a shared event name set:

```
event: snapshot      → initial full state on subscribe; data = same shape as sync
event: delta         → incremental change; data = {patch...} or new item
event: token         → LLM token (persona/generate, brain/step only)
event: error         → {code, message}; sbox decides reconnect
event: done          → terminal event; server will close the connection
event: ping          → heartbeat every 15s; sbox ignores
```

Every subscribe opens with one `snapshot` event so the client has full
state immediately, then receives `delta`s. Sbox uses one shared SSE
parser (~80 lines) across all stream endpoints.

### SSE event vocabulary

All streams use a shared event name set:

```
event: snapshot      → initial full state on subscribe; data = same shape as sync
event: delta         → incremental change; data = {patch...} or new item
event: token         → LLM token (persona/generate, brain/step only)
event: error         → {code, message}; sbox decides reconnect
event: done          → terminal event; server will close the connection
event: ping          → heartbeat every 15s; sbox ignores
```

Every subscribe opens with one `snapshot` event so the client has full
state immediately, then receives `delta`s. Sbox uses one shared SSE
parser (~80 lines) across all stream endpoints.

---

## Core schemas

### Observation

What a character sees this tick. Sent in `POST /brain/step` body.

```json
{
  "selfId": "alice",
  "tick": 412,
  "trigger": { "kind": "heartbeat" },
  "perception": [ /* PerceptionEvent[] — delta since last tick */ ],
  "needs":    { "energy": 42, "social": 60, "hunger": 80 },
  "moodlets": [
    { "id": "slept_well", "weight": 4, "expires_ts": 1747930000000 }
  ],
  "traits":   [ "introvert" ],
  "game":     { /* opaque, sbox-defined; passed through */ }
}
```

`trigger.kind` ∈ `"heartbeat"` | `"event"` | `"player_input"`. Brain
uses it to decide latency budget (heartbeat → utility-only OK; player_input
→ LLM if available).

`game` is opaque to the harness; sbox stuffs world-snapshot data here
(visible objects, current room, etc.) for verb resolution.

### PerceptionEvent

Typed event. Brains may ignore unknown `kind`s.

```json
{ "kind": "movement", "who_id": "bob", "x": 1, "y": 2, "ts": 1747929900000 }
{ "kind": "speech",   "from_id": "bob", "text": "morning",  "ts": ... }
{ "kind": "action_rejected", "verb": "sit", "reason": "occupied", "ts": ... }
{ "kind": "moodlet_added", "id": "slept_well", "weight": 4, "ts": ... }
{ "kind": "chat", "from": "player", "to": "alice", "text": "...", "ts": ... }
```

`kind` is a free string; namespaced kinds (`"sims:dance_started"`) are
allowed. Required fields: `kind`, `ts`. Everything else is per-kind.

### Action

What a brain wants to do. Returned from `POST /brain/step`.

```json
{ "verb": "sit", "args": { "object_id": "chair_1" } }
{ "verb": "move_to", "args": { "x": 2, "y": 0, "z": 3 } }
{ "verb": "say", "args": { "text": "morning", "to": "*nearby*" } }
```

Brain returns `Action[]`. Sbox calls `/verb/resolve` for each to obtain
Effects. Multiple actions = multi-step intent for this tick.

### Effect

What sbox actually applies to the world. Returned from `POST /verb/resolve`.

Discriminator: `kind`. Catalog (v0.1):

```json
{ "kind": "set_pos", "actor": "alice", "x": 2, "y": 0, "z": 3 }
{ "kind": "play_anim", "actor": "alice", "name": "sit", "loop": true }
{ "kind": "occupy", "actor": "alice", "object_id": "chair_1", "release": false }
{ "kind": "need", "actor": "alice", "axis": "energy", "op": "+", "amount": 5 }
{ "kind": "moodlet", "actor": "alice", "id": "slept_well", "weight": 4, "duration_ms": 28800000 }
{ "kind": "advance_sim", "minutes": 480 }
{ "kind": "speech_bubble", "actor": "alice", "text": "morning", "duration_ms": 4000 }
{ "kind": "perceive", "target": "*nearby*", "event": { /* PerceptionEvent */ } }
```

**Sbox MUST implement an interpreter for each `kind`. Unknown kinds are
logged and dropped (not errored) so new effect kinds in woid don't
break older sbox builds — they just fail soft.**

`actor` may be `"*world*"` for non-character effects. `target` accepts
`*nearby*`, `*all*`, or a specific id.

`op` for needs: `"+"` (add, clamp 0-100), `"-"`, `"="` (set absolute).

### VerbDecl

Returned from `GET /verbs`. Sbox uses this to surface verbs in UI
(e.g. player-triggered "ask to dance" options).

```json
{
  "name": "sit",
  "args": {
    "object_id": { "type": "string", "required": true, "desc": "smart-object instance id" }
  },
  "prompt": "Sit on a chair-like object. Replenishes energy slowly.",
  "preconditions": ["adjacent", "object_capacity_available"]
}
```

### Character

```json
{
  "id": "alice",
  "identity": {
    "name": "Alice",
    "about": "introvert; loves overcast mornings",
    "avatar": { /* sbox ClothingContainer JSON, opaque to brain */ }
  },
  "needs":    { "energy": 100, "social": 60, "hunger": 80 },
  "traits":   [],
  "moodlets": [],
  "location": { "x": 0, "y": 0, "z": 0, "room": "living" }
}
```

### Status

`GET /status` — the only endpoint sbox calls before "ready". Drives the
boot UI in the editor bootstrapper.

```json
{
  "contract": "0.1", "ok": true,
  "data": {
    "state": "ready" | "loading" | "downloading" | "error",
    "binary":  { "present": true, "version": "b3567" },
    "model":   { "present": true, "name": "gemma-4-E2B-it" },
    "llm":     { "provider": "llama-server" | "ollama" | "claude" | "nim" | "utility-only", "ready": true },
    "progress": { "kind": "binary" | "model", "got": 1234567, "total": 2600000000, "bytesPerSec": 50000000 },
    "error":   { "code": "...", "message": "..." }
  }
}
```

Progress shape matches mobile's `gemmaLocal.downloadModel(onProgress)`
emit shape exactly, so sbox/mobile UIs render the same code.

---

## Endpoint details

### POST /character

```json
// request
{ "id": "alice", "identity": { "name": "Alice", "about": "..." } }
// response
{ "contract": "0.1", "ok": true, "data": { "character": { /* Character */ } } }
```

Idempotent on `id` — POSTing again resets.

### POST /perception/event

```json
// request
{ "target": "alice" | "*nearby*", "near": { "x": 1, "y": 2, "z": 3, "radius": 5 }, "event": { /* PerceptionEvent */ } }
// response
{ "contract": "0.1", "ok": true, "data": { "delivered_to": ["alice", "bob"] } }
```

### POST /brain/step

```json
// request body: Observation (see above)
// response
{ "contract": "0.1", "ok": true, "data": { "actions": [ /* Action[] */ ], "trace_id": "..." } }
```

`trace_id` references the entry sbox can later fetch via `/brain/trace/:id`.

### POST /verb/resolve

```json
// request
{
  "actor": "alice",
  "verb":  "sit",
  "args":  { "object_id": "chair_1" },
  "world": { /* small snapshot sbox provides: nearby objects, character positions */ }
}
// response
{ "contract": "0.1", "ok": true, "data": { "effects": [ /* Effect[] */ ] } }
```

Returns `ok: false` with `error.code: "precondition_failed"` if the verb
can't apply (e.g. object occupied). Sbox should NOT auto-retry; the
brain's next tick will re-decide.

### POST /persona/generate

Sync (default — `Accept: application/json`):
```json
{ "contract": "0.1", "ok": true,
  "data": { "name": "Alice", "about": "...", "avatar_hint": "...", "ms": 4200, "tokens": 87 } }
```

Stream (`Accept: text/event-stream`):
```
event: token
data: {"text": "Alice"}

event: token
data: {"text": " is"}

event: delta
data: {"persona": {"name": "Alice", "about": "...", "avatar_hint": "..."}}

event: done
data: {"ms": 4200, "tokens": 87}
```

Sbox uses stream to drive the live "Generate" UI; CI tests and other
tooling use sync.

### POST /brain/step (stream mode)

With `Accept: text/event-stream`, the response streams LLM generation
and ends with the resolved actions:

```
event: token
data: {"text": "She's tired"}

event: token
data: {"text": ", the chair"}

event: delta
data: {"actions": [{"verb": "sit", "args": {"object_id": "chair_1"}}], "trace_id": "..."}

event: done
data: {"ms": 320, "tokens": 24}
```

Sbox in stream mode renders incoming tokens directly into the
inspector's "current turn" panel while the brain is still thinking,
then applies actions when the `delta` arrives.

For utility-only ticks (no LLM), the stream emits a single `delta` event
with `actions` immediately, then `done`. Sbox doesn't need to special-case.

### GET /brain/trace/:id

```json
{
  "contract": "0.1", "ok": true,
  "data": {
    "entries": [
      {
        "trace_id": "...", "ts": 1747929900000,
        "turns": [
          { "role": "system",    "text": "...", "tokens": 412 },
          { "role": "user",      "text": "...", "tokens": 156 },
          { "role": "assistant", "text": "...", "tokens": 24, "ms": 320 },
          { "role": "tool",      "name": "sit", "args": { "object_id": "chair_1" } }
        ],
        "actions_emitted": [ /* Action[] */ ]
      }
    ]
  }
}
```

If the brain ran in utility-only mode (no LLM), `turns` is empty and the
inspector renders "utility brain — no LLM turn".

---

## Errors

```json
{ "contract": "0.1", "ok": false,
  "error": { "code": "precondition_failed", "message": "object_id=chair_1 is occupied", "details": { ... } } }
```

Codes used in v0.1:

| Code | Meaning |
|---|---|
| `not_ready` | Brain-server still booting (model loading or binary download) |
| `unknown_character` | `:id` doesn't exist; sbox should POST /character first |
| `unknown_verb` | Verb name not in /verbs |
| `invalid_args` | Verb args fail schema |
| `precondition_failed` | Verb's preconditions don't hold given world snapshot |
| `provider_error` | LLM provider failed (network, rate-limit) — brain may have fallen back to utility |
| `internal` | Bug; report it |

Sbox handles each:
- `not_ready` → keep polling `/status`
- `unknown_*` → log + skip the tick
- `precondition_failed` → drop the action; let next tick decide
- `provider_error` → surface in inspector, world keeps running
- `internal` → log + circuit-break

---

## Compatibility policy

**Additive changes don't bump the version**:
- New endpoints
- New Effect kinds (sbox drops unknown kinds; older sbox keeps working)
- New PerceptionEvent kinds (brains drop unknown kinds; harmless)
- New optional fields on existing schemas

**Breaking changes bump major** (`0.1` → `0.2` → `1.0`):
- Renamed or removed fields
- Changed required-ness of existing fields
- Changed endpoint paths

**Effect kind catalog is the contract's load-bearing surface.** Adding
`kind: "play_sound"` is additive; renaming `set_pos` → `teleport` is
breaking. When proposing a new Effect kind, add to the catalog table
above with example payload before implementing it on either side.

## Out of scope for v0.1

- Auth (assumed localhost-only)
- Multi-player brain-server (one brain-server per sbox host; clients
  don't talk to brain-server directly)
- Persistence across brain-server restarts (state in memory)
- Pagination on `/perception/:id` beyond `since`
- WebSocket — SSE covers our cases; bidirectional streaming not needed
  (sbox writes via POST, reads via SSE)
- Schema validation library — both sides hand-validate; consider
  zod/json-schema when the surface stabilizes
- Reconnect/resume semantics — sbox re-subscribes from scratch on
  connection drop; brain-server emits a fresh `snapshot` on each open
