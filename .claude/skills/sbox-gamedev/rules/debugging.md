# Debugging the s&box Editor

## When to relaunch vs. when to recompile (don't burn cycles)

Editor cold-start is **30-45s**. In-editor recompile via
`sbox-recompile` is **~0.2s**. Use the right tool for what you changed.

| What you changed | Right tool | Why |
|---|---|---|
| Existing `.cs` file's contents | `sbox-recompile` | Forces `Sandbox.Project.CompileAsync()`; editor stays warm. |
| Existing `.razor` / `.razor.scss` content | `sbox-recompile` | Same. |
| **Added** a new `.cs` / `.razor` / `.scene` file | `sbox-launch --kill && sbox-launch <p> --wait-ready` | Wine's file watcher misses creates. |
| Renamed / deleted a source file | `sbox-launch --kill …` | Same. |
| Changed `*.csproj` references / project structure | `sbox-launch --kill …` | Project system re-reads on launch only. |
| First-time `Editor/SkillTrigger.cs` install | `sbox-launch --kill …` | Editor needs to compile the new editor-side assembly. |
| Just want to query state (`sbox-scene`, `sbox-eval`) | nothing — neither | Editor stays as it is. |
| Triggering existing functionality (`sbox-screenshot`, `sbox-do`) | nothing | Triggers are real-time. |

If `sbox-recompile` "succeeds" but your change didn't take effect, fall
back to `sbox-launch --kill …` — Wine probably didn't see the edit. This
is rare but worth the manual escalation when it happens.



The editor runs through Proton; standard Linux debugging tools see the host
process but not Wine internals. The most useful information lives in two logs.

## Log locations

| Log | Path |
|---|---|
| Editor / project session | `~/.steam/root/steamapps/common/sbox/logs/sbox-dev.log` |
| Game (standalone) sessions | `~/.steam/root/steamapps/common/sbox/logs/sbox.log` |
| Compatdata wine debug | `~/.local/share/Steam/steamapps/compatdata/2129370/pfx/drive_c/users/steamuser/AppData/Local/Temp/` |

The editor truncates / rotates `sbox-dev.log` between sessions; entries
accumulate in append mode and old runs land in zipped `.zip` files alongside.

Use `tools/sbox-logs --summary` to get a fast count of what's been happening:

```
$ tools/sbox-logs --summary
log: ~/.steam/root/steamapps/common/sbox/logs/sbox-dev.log
82 entries grouped:
  asset         12  ████████████
  reflection    14  ██████████████
  other         56  ████████████████████████████████████████████████████████
```

## Error categories and what they mean

| Category | Pattern | Meaning |
|---|---|---|
| `compile` | `[Generic] Error \|` | C# managed compile error in your project code. **Always real.** Fix the source code. |
| `asset` | `ERROR_FILEOPEN`, `File not found` | A `.vmdl_c`/`.vmat_c`/`.vsnd_c` etc. couldn't be loaded. Either the asset doesn't exist, the project's content depot is missing pieces (very common from Steam-installed s&box), or the Linux asset compiler can't generate it (`textures/generated/imagefile/<hash>.vtex_c`). The first means a fix in code; the latter two are Linux-side and may be unfixable. |
| `shader` | `Error creating shader …` | Shader compilation failed at runtime. Often the shader source needs Source 2's shader compiler which may have Linux gaps. |
| `null` | `Value cannot be null. (Parameter 'text')` in `SkiaSharp.SKCanvas.DrawText` | RichTextKit asked Skia to draw a `null` `SKTextBlob`. Means the body font doesn't cover a codepoint and Wine's font fallback returned null. Fix: ensure the codepoint's font is registered (see `linux-setup.md`) or remove the offending character (see `ui-icons-linux.md`). |
| `missing` | `Missing Component: couldn't find …` | A scene references a component type that's not in the current build. Either you renamed/removed it (open the scene, drop the missing slot) or compile errors prevented it from being available. |
| `directwrite` | `IDWriteColorGlyphRunEnumerator::GetCurrentRun failed` | Known Wine bug rendering color emoji glyphs. **Cosmetic only** — doesn't crash the editor. Mitigate by using outline fonts (we strip COLR/CPAL from `seguiemj.ttf`) or sidestep entirely by using Material Icons names. |
| `reflection` | `[Reflection] Failed to clean / NullStaticReferences` | The hot-reload cleanup couldn't reset some static state. Usually harmless; restart the editor if the project starts behaving oddly. |

## Workflow for diagnosing a problem

1. **Reproduce, then check the log.** `tools/sbox-logs --errors --tail 30`
   shows the most recent error entries with categories.
2. **Compile errors first.** If `compile` category is non-zero, fix those —
   nothing else matters until the project rebuilds cleanly.
3. **Missing components next.** Compile fixes often resolve cascading
   `missing` warnings.
4. **Asset failures last.** Many are environmental (Linux can't compile a
   particular asset). Determine if your code path actually needs the asset.
5. **Ignore `directwrite` and `reflection` unless visible.** They spam the log
   but don't break gameplay.

## "Editor opened but the world is empty / objects look broken"

Most likely the menu/avatar/jungle preview scenes failed to load some assets
(see `asset` rows). The editor still functions; you just can't preview those
specific scenes. Open your own project's scene instead — your assets are
probably fine.

## "Nothing renders in the spawn menu / icons missing"

Two separate failure modes overlap here:

- **Big prop thumbnails are blank** — the `thumb:` URL protocol needs the
  Linux asset compiler that isn't shipped. See `linux-setup.md` → "What does
  NOT work on Linux". Not fixable from outside.
- **Toolbar icons are tofu** — emoji in source code; see `ui-icons-linux.md`.
  Patchable by replacing with Material Icons names.

## Tail with classification while developing

For active iteration, run in another terminal:

```
tools/sbox-logs --follow --errors
```

This streams new error entries as they happen with category coloring, so you
see at a glance whether your latest edit broke compilation, references a
missing asset, or triggered a different class of failure.

## `sbox-recompile` returns "RanToCompletion" even when the compile fails

The tool kicks off `Sandbox.Project.CompileAsync()` and waits on the
returned `Task` — but **the Task completes successfully whether the
compile succeeded OR failed**. The Task just represents "the compile
pipeline ran"; the result (a `CompilerOutput`) is what carries success
or errors.

You'll see:
```
$ sbox-recompile
recompile status=RanToCompletion in 0.2s
```

…and assume everything's good. But if the source had a CS-error, the
old assembly stays loaded. New code changes don't take effect.
Symptom: editing a `.cs` file changes nothing visible at runtime; old
behavior persists; AutoPlay loop looks unchanged.

**Always verify a recompile by greppinging the log:**

```bash
.claude/skills/sbox-gamedev/tools/sbox-logs --category compile --tail 5
```

If the latest line is `Compile of 'local.<project>' OK`, you're good.
If it's `… Failed`, fix the error and re-compile. Don't trust the
recompile tool's own "status=RanToCompletion" as proof of success.

## `.vmap` files are binary-only — no hand-authoring path

Hammer maps (`.vmap`) ship as binary DMX (`encoding binary 9 format
vmap 29`) and the runtime loads via `NativeEngine.SceneMap` after the
asset compiler produces `.vpk`/`.world_c`. No text DMX variant ships;
the schemas in `engine/Definitions/hammer/MapDoc/Nodes/*.def` are
read-only. Hand-authoring a `.vmap` requires byte-level surgery
(string pools, internal pointers, half-edge mesh data via
`CDmePolygonMesh`) and is intractable for autonomous workflows.

**Pragmatic alternative**: build the same "indoor level" using scene
JSON GameObjects with `BoxCollider` + `ModelRenderer` (stretched
`box.vmdl`) per brush. Semantically equivalent to what
`MapLoader.CreateStaticModel()` does for `func_brush` entities
internally, just expressed as a `.scene` file. See
`examples/hammer-level/` for the worked pattern.

## `sbox-eval` blocks the engine main thread

Anything you run via `sbox-eval` executes synchronously on the editor's
main thread. **Don't `Thread.Sleep` inside an eval call** — it freezes
the physics simulation, the renderer, everything. Symptoms: the editor
appears to hang; ragdolls freeze mid-air; the eval result file never
appears.

If you need to wait between two pieces of inspection, sleep from the
shell between two separate `sbox-eval` calls instead. Each eval should
do one cheap thing and return.

## Failure mode: "Video recording finished" log line but no file on disk

Symptom: `sbox-screenshot --mode video` reports success in the log:

```
[Generic] Video recording started: screenshots\sbox.YYYY...mp4
[Generic] Video recording finished: <a href="screenshots\sbox.YYYY...mp4">…</a>
```

…but no MP4 exists at that path. Screenshots taken in the same play session
work fine. Confusing because the engine logs "finished" unconditionally on
`StopRecording`, even when the VideoWriter was never initialized.

Root cause: `MediaRecorderLayer` is only attached to a camera whose
`SceneCamera.IsRecordingCamera` is true, and that property is a derived
check against the static `SceneCamera.RecordingCamera`. That static is set
by `SceneRenderingWidget` **only when the widget has Qt focus** — which
doesn't happen when Play is triggered programmatically (file trigger, no
input event). So MediaRecorderLayer never attaches → recorder gets zero
frames → 0-byte file → "finished" log line.

Fix: SkillTrigger.cs's video-clip handler now reflects into
`SceneCamera.RecordingCamera` and sets it to `Game.ActiveScene.Camera.SceneCamera`
explicitly right before the `video` ConCmd fires. If you ever see this
failure mode return, check that `EnsureRecordingCameraSet()` is being
called and the reflection didn't break against an engine update.

Diagnostic: after a video-clip trigger, the log should show:

```
[SkillTrigger] video-clip: SceneCamera.RecordingCamera set
```

If that line is missing, the fix didn't run.

## What's not in the log

- **GPU / Vulkan crashes** — those land in stderr of the editor process,
  which Steam swallows. The dev log is the next-best signal — run
  `tools/sbox-logs --follow` in another terminal while reproducing.
- **Wine font fixmes** — visible if you launch the binary directly through
  protontricks. Usually noise; only relevant when investigating font issues.
- **DotNet runtime fatal errors** — if the editor doesn't start at all, check
  `~/.local/share/Steam/steamapps/compatdata/2129370/pfx/drive_c/users/steamuser/AppData/Local/Temp/*.log`
  for installer/runtime issues.
