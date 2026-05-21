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
- [rules/component-lifecycle.md](rules/component-lifecycle.md) — Component skeleton, OnStart/OnUpdate, Components.Get, prefabs, network sync basics.
- [rules/ui-razor-scss.md](rules/ui-razor-scss.md) — Razor file structure, BuildHash, common patterns from `sandbox/Code/UI/`.
- [rules/ui-icons-linux.md](rules/ui-icons-linux.md) — Why emoji `[Icon("📦")]` don't render under Wine; use Material Icons names. Includes a lookup table.
- [rules/debugging.md](rules/debugging.md) — Log location, error patterns we've seen, when to suspect Linux-specific vs real bug.

### Tools
Two helpers under `tools/` — invoke via Bash. Pre-authorized by `allowed-tools`.

- `sbox-doctor [project-path]` — verify the Linux setup (Proton prefix, .NET 10, font registration, Proton Experimental forced); optionally scan a project for case-conflict dirs. Run this first when invoked.
- `sbox-logs [--summary|--errors|--follow|--category <name>] [--tail N]` — tail and classify the editor log at `~/.steam/root/steamapps/common/sbox/logs/sbox-dev.log`. Categories: `compile`, `asset`, `shader`, `null`, `missing`, `directwrite`, `reflection`, `other`.

For everything else, prefer reading the relevant rule file and acting from there:
- **Picking an icon**: see the lookup table in `rules/ui-icons-linux.md`, or browse <https://fonts.google.com/icons>. Pass any name verbatim into `[Icon("…")]`.
- **Launching the editor**: `steam steam://rungameid/2129370` (the editor app id) — or hit Play in the Steam UI.
- **Scaffolding a Component / Razor panel**: copy the templates from `rules/component-lifecycle.md` / `rules/ui-razor-scss.md` and adjust namespace to match the project's `.sbproj`.

## Source patches in this repo

If you're working in this repo's submodules, two upstream issues are already patched:
- `sandbox/`: emoji `[Icon( "📦" )]` attributes rewritten to Material Icons names so they render under Wine. See [EMOJI_ICONS_ISSUE.md](../../../EMOJI_ICONS_ISSUE.md).
- `sbox-bombroyale/`: case-conflicting `Code/` and `code/` directories merged. See the submodule's `linux-case-fix` branch.

Don't reintroduce emoji `[Icon(…)]` attributes when writing new code on Linux — use Material Icons names from the table in `rules/ui-icons-linux.md`.
