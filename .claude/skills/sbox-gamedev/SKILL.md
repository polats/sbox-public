---
name: sbox-gamedev
description: Develop games in s&box (Facepunch's Source 2 + .NET engine) on Linux/Proton. Use when the user wants to start an s&box dev session, open or edit a project (sandbox/, sbox-bombroyale/, or anything under ~/.local/share/Steam/.../Documents/s&box projects/), scaffold a Component or Razor UI panel, look up a Material Icons name, troubleshoot Linux-specific editor errors (font/case-sensitivity/asset compilation), or debug the editor log. Triggers include "s&box", "sbox", "sandbox engine", "let's open the editor", "add a component", "razor panel", "fix the bombroyale build".
allowed-tools: Bash(.claude/skills/sbox-gamedev/tools/*:*)
metadata:
  tags: sbox, sandbox, source2, dotnet, gamedev, linux, proton, razor, skiasharp
---

# s&box Game Development on Linux

This skill captures what we've learned getting the s&box editor running on Ubuntu
via Steam+Proton and developing in it. It's an MVP scope — Linux setup, the
component model, Razor/SCSS UI, the Linux-specific icon workaround, and
debugging via the editor log.

## When to use

Use this skill whenever the user is working in or talking about s&box: opening
the editor, writing a Component, building a UI panel, troubleshooting compile
errors, or debugging why something doesn't render under Proton.

## How to use

### On invocation
Run `tools/sbox-doctor` first — it verifies the Linux setup (Proton prefix,
.NET 10 in the prefix, Material Icons font registered, no case-conflict dirs in
the current project). Report green/red briefly, then ask what to do.

### Lookup index
- [rules/linux-setup.md](rules/linux-setup.md) — Proton prefix, .NET install, font registry, common gotchas. Reference when env-related errors appear.
- [rules/component-lifecycle.md](rules/component-lifecycle.md) — Component skeleton, required `using`s, axis convention, input actions, Components.Get, prefabs, network sync basics.
- [rules/ui-razor-scss.md](rules/ui-razor-scss.md) — Razor file structure (with `@using` requirements), BuildHash, common patterns from `sandbox/Code/UI/`.
- [rules/ui-icons-linux.md](rules/ui-icons-linux.md) — Why emoji `[Icon("📦")]` don't render under Wine; use Material Icons names. Includes a lookup table.
- [rules/scenes.md](rules/scenes.md) — `.scene` JSON format, quaternion rotation cheatsheet, component field shapes (Camera/ModelRenderer/ScreenPanel/BoxCollider/DirectionalLight), scene-load gotchas. **Read this when authoring or modifying scenes from a script.**
- [rules/physics.md](rules/physics.md) — `Rigidbody`/`Collider` setup, units & scale, impulses, EnhancedCcd for fast bodies, `Collision.Other` is a struct, `Collider.Elasticity`/`Friction` are `float?` (not Curve), how to "park" a body without it falling forever, listening to collisions/triggers, and sound-on-impact patterns. **Read when building any physics-driven game.**
- [rules/animations.md](rules/animations.md) — citizen model + animgraph parameter reference (the 16 you actually need out of 75 the model exposes), local-space velocity computation for the 8-way locomotion blend, edge-triggered jump pattern, `CharacterController` is *not* a `Collider`, footstep timing without anim events, 3rd-person camera follow pattern. **Read when building any character-driven game.**
- [rules/debugging.md](rules/debugging.md) — Log location, error patterns we've seen, when to suspect Linux-specific vs real bug.

### Tools
All under `tools/` — invoke via Bash. Pre-authorized by `allowed-tools`.

- `sbox-doctor [project-path]` — verify the Linux setup (Proton prefix, .NET 10, font registration, Proton Experimental forced); optionally scan a project for case-conflict dirs. **Run this first when invoked.**
- `sbox-logs [--summary|--errors|--follow|--category <name>] [--tail N]` — tail and classify the editor log. Categories: `compile`, `asset`, `shader`, `null`, `missing`, `directwrite`, `reflection`, `other`.
- `sbox-launch <project-dir>` — kill any running editor and relaunch directly on the given project. Use this when you **added** a new source file, renamed/deleted one, changed csproj refs, or just installed `Editor/SkillTrigger.cs` — Wine's file watcher misses creates. For edits to *existing* files, use `sbox-recompile` instead — it's ~0.2s vs ~35s. Flags: `--wait-ready` blocks until SkillTrigger answers a ping, `--tail-log` streams classified errors, `--kill` alone just kills running editors.
- `sbox-recompile` — trigger an in-editor recompile by calling `Sandbox.Project.CompileAsync()` via the running editor's reflection. **Takes ~0.2s** vs ~35s for kill+relaunch. Use whenever you only edited the contents of existing source files (no creates/deletes). See `rules/debugging.md` → "When to relaunch vs. when to recompile" for the full table.
- `sbox-set-startup-scene <project-dir> <scene-path>` — prime `<project>/.sbox/project.json` so the editor auto-opens a specific scene on next launch. Editor must be closed. Use right after generating a scene file so the next `sbox-launch` opens directly into it.
- `sbox-models [query] [--category NAME] [--update] [--cloud]` — search the engine's installed model library (2800+ paths) and return canonical reference strings ready to paste into a scene's `"Model": "models/..."` field. Defaults to ~700 always-available models from `core+addons`; pass `--cloud` to include the 2100 cloud-pulled ones (which need project access to actually load).
- `sbox-model-info <path...>` or `sbox-model-info --query <q>` — return verified bounding-box dimensions for one or more models, by loading each via `Model.Load` and reading `Model.Bounds`. Use this **before placing any unfamiliar model in a scene** — model names lie (`coin01` is a 27" Mario-style prop, `tree_oak_big_a` is 85 ft tall). Trigger-file based (same architecture as `sbox-screenshot`): writes to whichever project the editor's currently on, falls back to `model-probe/` (a small worker project at the repo root) if no editor is running. ~1s response once the project's editor-side helper is compiled.
- `sbox-scene [project] [--filter PREFIX] [--grep STR] [--json]` — snapshot the editor's currently-open scene. Returns every GameObject with position/rotation/scale, its components, and each component's `[Property]` values. Trigger-file based (~1s). Use when you need to know **what's actually in the scene right now** (post-load, post-spawn, after the user manually edited something) without re-reading the .scene file. With no `project` arg, queries whichever project the editor is currently running on.
- `sbox-eval '<csharp>'` — evaluate a C# expression or statement block inside the running editor's process. Uses Roslyn (`Microsoft.CodeAnalysis.CSharp`, already in s&box's `bin/managed/`) to compile + emit + load + invoke. Available namespaces: `System`, `System.Collections.Generic`, `System.Linq`, `System.Text`, `Sandbox`, `Editor`. Expressions are auto-wrapped (`return (expr);`); statement blocks must contain an explicit `return`. Use when you need to know what something *actually* returns right now: `sbox-eval 'SceneEditorSession.Active?.Scene.GetAllComponents<ModelRenderer>().Count()'` → `8`. ~1s. Errors come back with full Roslyn diagnostics.
- `sbox-do <action> [args…]` — universal action driver. Subcommands: `menu <Game/Play>` (invoke a menu item by slash-separated path), `shortcut <editor.toggle-play>` (invoke any registered `[Shortcut("name", …)]` by its identifier), `cmd "<raw concmd>"` (run any console command), `play` / `stop-play` (toggle play mode). All ~1s, all via the trigger-file pattern. Discover available shortcut identifiers by grepping the engine source: `grep -r '\[Shortcut(' engine/ | grep -oE '"[^"]+"' | sort -u`.
- `sbox-screenshot <project> [--width W] [--height H] [--out PATH] [--mode screenshot|video] [--duration S]` — capture a screenshot (PNG) or video (MP4) of a project's scene. Output saved to `<project>/captures/<project>-<timestamp>.<ext>` by default; pass `--out PATH` to override. **Architecture**: writes a trigger file under `<project>/.sbox/skill-triggers/`; an editor-side helper (`Editor/SkillTrigger.cs`, auto-installed into the project on first use) sees the file, runs the corresponding ConCmd, deletes the trigger. Capture time ~5s once the editor is warm — no xdotool, no F5, no kill+relaunch. First call after installing the helper triggers a recompile (~30s). Video mode kicks off a play→record→stop→exit sequence internally; screenshot stays in edit mode. **Caveats:** (a) `screenshot_highres` taps post-postprocess camera output (warm, correct colors). `video` taps the SceneLayer color target pre-postprocess, so video colors look cooler/bluer than screenshots — engine pipeline-tap difference, not a tool bug. (b) Edit-mode screenshots don't render the HUD (`ScreenPanel` only renders in play). Video clips do show the HUD because the trigger enters play. (c) Video is AV1+Opus by default; tool auto-re-encodes to H.264 via ffmpeg when `--out` is set.

### Auto-play for video verification (and how to disable it before "done")

To prove a game works without anyone manually pressing keys, agents commonly
add an autonomous-demo path that drives the game from inside the Player /
GameController Component. This is **useful for the verification video** but
must be **disabled before declaring the game done**, otherwise the user
opening the project in the editor sees the game play itself.

**The convention**:

1. Expose an `[Property] public bool AutoPlay { get; set; } = false;` field
   on the Player/GameController Component.
2. Wrap all autonomous-demo logic in `if ( AutoPlay ) { … }` blocks.
3. **In the `.scene` file**, set `"AutoPlay": true` on that Component while
   recording. The scene file is JSON — edit the value directly.
4. **After the video is captured and you're about to declare done**: flip
   the scene-file value back to `"AutoPlay": false` (or remove the field
   entirely so the code default takes effect). Verify with:
   `grep -E '"AutoPlay"|= true' <project>/Code/*.cs <project>/Assets/scenes/*.scene`
   — both should show false (or absent).
5. The code default must be `false` so a fresh user opening the project
   gets a playable game, not a self-playing one.

Don't ship games that play themselves. The user has to manually verify
this in the final report ("AutoPlay defaults to false in both code and
scene file").

### Resuming after a context compaction

Long autonomous builds will hit context compaction. **Before starting fresh
work in `<project>/`, check whether the project already exists and what
state it's in:**

1. `ls <project>/Code/ <project>/Assets/scenes/` — see what's already there.
2. `sbox-scene` (if the editor's running on it) — current GameObject tree.
3. `ls <project>/captures/` — sorted by mtime, the most recent screenshot
   shows what the game last looked like. Open it with `Read`.
4. `<project>/.sbox/skill-results/*.json` — recent tool replies (eval
   results, scene snapshots) tell you what the prior session was checking.

If you find substantial prior work, treat it as the starting point and
build forward — don't redo what's already done. The first dry-run of
`flappy-bird` did exactly this after compaction (correctly).

### End-to-end "create a game from scratch" workflow

1. **Verify env**: `sbox-doctor`.
2. **Create project tree** by hand: `<project>/<name>.sbproj` + `<project>/Code/*.cs` + optional `<project>/Code/UI/*.razor*` — follow templates in `rules/component-lifecycle.md` and `rules/ui-razor-scss.md`. Don't forget `using System;` / `@using System.Linq;`.
3. **Write the scene** in `<project>/Assets/scenes/<name>.scene` — see `rules/scenes.md` for the JSON format and a Python generator pattern.
4. **Prime startup scene**: `sbox-launch --kill && sbox-set-startup-scene <project> scenes/<name>.scene`.
5. **Launch & verify**: `sbox-launch <project>`, then `sbox-logs --errors --tail 20` to spot compile or scene-load problems.
6. **Iterate on code/scene**: edit files, then `sbox-launch <project>` again (it kills + relaunches so Wine picks up the changes).

All worked-example games live under `<repo>/games/`. Skim several when starting a new project — each one covers different patterns:

| Game | Demonstrates |
|---|---|
| `games/coin-rush/` | basic Component + scene + HUD scaffold, scene generated from Python |
| `games/flappy-bird/` | side-scroller + game states (Menu/Playing/GameOver) + Razor HUD |
| `games/pool/` | Rigidbody + Collider physics, sound-on-event, trigger pockets, mouse drag aim |
| `games/parkour/` | CharacterController, citizen animgraph parameters, 3rd-person camera follow |
| `games/shooting-gallery/` | First-person mouse-look, hitscan via `Scene.Trace.Ray`, target hit/respawn |
| `games/bullet-hell/` | Top-down arcade prototype with multiple enemy/bullet types |

Plus `<repo>/model-probe/` (kept at the repo root, NOT under games/) — a worker project used by `sbox-model-info` as a fallback when no editor is running. Don't delete it.

New games go under `games/<name>/` to keep the repo root clean.

## Source patches in this repo

If you're working in this repo's submodules, two upstream issues are already patched:
- `sandbox/`: emoji `[Icon( "📦" )]` attributes rewritten to Material Icons names so they render under Wine. See [EMOJI_ICONS_ISSUE.md](../../../EMOJI_ICONS_ISSUE.md).
- `sbox-bombroyale/`: case-conflicting `Code/` and `code/` directories merged. See the submodule's `linux-case-fix` branch.

Don't reintroduce emoji `[Icon(…)]` attributes when writing new code on Linux — use Material Icons names from the table in `rules/ui-icons-linux.md`.
