# Debugging the s&box Editor

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

## What's not in the log

- **GPU / Vulkan crashes** — those land in stderr of the editor process,
  which Steam swallows. Launch via `tools/sbox-launch <project> --tail-log`
  for the next-best thing.
- **Wine font fixmes** — visible if you launch the binary directly through
  protontricks. Usually noise; only relevant when investigating font issues.
- **DotNet runtime fatal errors** — if the editor doesn't start at all, check
  `~/.local/share/Steam/steamapps/compatdata/2129370/pfx/drive_c/users/steamuser/AppData/Local/Temp/*.log`
  for installer/runtime issues.
