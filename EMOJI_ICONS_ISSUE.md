# Emoji-as-Icons Don't Render Under Proton

## Symptom

In the s&box editor on Linux/Proton, many in-game UI tab icons render as boxes
with an X ("tofu") instead of the expected emoji glyph. Examples in the sandbox
project: 📦 (spawn), 🔧 (utilities), 🌍 (utility), 🔍 (context menu),
🥽 (weld), 🍔 (mass), and most ToolGun mode icons.

The editor itself runs fine; this is purely a glyph-rendering failure for
emoji codepoints.

## Root cause

The icons are emoji characters in `[Icon( "📦" )]` attributes in `sandbox/Code/`,
rendered by `Sandbox.UI` (SkiaSharp + RichTextKit). Under Wine, two related
bugs prevent the emoji glyphs from appearing:

1. **Wine `IDWriteColorGlyphRunEnumerator::GetCurrentRun` is incomplete.**
   When SkiaSharp/Qt encounters an emoji codepoint and asks Wine's DirectWrite
   to render the color glyphs from Segoe UI Emoji (COLR format), the call
   fails with `HRESULT 0x8007139f`. Observed in the log as:
   ```
   QtCriticalMsg: QWindowsFontEngineDirectWrite::imageForGlyph:
     IDWriteColorGlyphRunEnumerator::GetCurrentRun failed (Unknown error 0x8007139f.)
   ```
2. **Wine's `IDWriteFontFallback::MapCharacters` is non-deterministic for
   emoji codepoints.** Even with the real Microsoft Segoe UI Emoji installed,
   registered with the proper "Segoe UI Emoji" family name, and SystemLink
   fallback entries pointing body fonts at it, Wine returns the font for
   only a subset of codepoints. Codepoints in the same Unicode block can
   behave differently: 📦 (U+1F4E6) renders, 🍔 (U+1F354) doesn't — and no
   configuration we tried fixes that asymmetry.

## What does *not* work (tried and confirmed)

- Dropping `NotoColorEmoji.ttf` (CBDT) into the prefix — Skia returns null
  `SKTextBlob` (`Value cannot be null. (Parameter 'text')` spam).
- Twemoji Mozilla (COLRv1) renamed to `Segoe UI Emoji` — Skia builds a
  non-null blob, but Wine renders garbled "broken texture" glyphs (COLRv1
  unsupported).
- Noto Emoji (B&W outline) renamed to `Segoe UI Emoji` — tofu (Wine doesn't
  pick it via font fallback for emoji codepoints).
- Registering many emoji-coverage fonts (Symbols2, FontAwesome, OpenSymbol)
  — partial coverage fonts get matched for ASCII text and re-trigger the
  null-blob crash spam.
- Real Microsoft `seguiemj.ttf` from Windows 10/11 — color path fails per
  bug #1 above.
- Stripping COLR/CPAL from `seguiemj.ttf` so Wine uses outline glyphs —
  some codepoints render, others still don't (bug #2).
- Wine SystemLink registry entries prepending Segoe UI Emoji to fallback
  chains for Arial, Segoe UI, Inter, Poppins, etc. — no change.

These are confirmed Wine limitations, not configuration issues. References:
[SkiaSharp #3244](https://github.com/mono/SkiaSharp/issues/3244),
[WineHQ forum #39807](https://forum.winehq.org/viewtopic.php?t=39807).

## Workaround applied

The sandbox project's emoji icon attributes were converted to **Material Icons
ligature names**, which Wine renders reliably.

Two changes made to the `sandbox` submodule:

1. `sandbox/Code/UI/SpawnMenuModeBar.razor.scss` — added a `.icon` rule
   inside `SpawnMenuModeBar` setting `font-family: "Material Icons"`:

   ```scss
   .menu-mode-button .icon
   {
       font-family: "Material Icons";
       font-size: 32px;
       line-height: 1;
   }
   ```

2. All `[Icon( "📦" )]`-style attributes across `sandbox/Code/UI/` and
   `sandbox/Code/Weapons/ToolGun/Modes/` rewritten to use Material Icons
   ligature names. Sample mapping:

   | Emoji | Replacement | Where |
   |-------|-------------|-------|
   | 📦 | `inventory_2` | SpawnMenu.razor |
   | 🔧 | `build` | UtilitiesPage / ToolsTab |
   | 🌍 | `language` | UtilityTab |
   | 🔍 | `search` | ContextMenuHost |
   | 🎨 | `palette` | EffectsHost |
   | 💾 | `save` | SaveMenu |
   | 🧹 | `cleaning_services` | CleanupPage |
   | 👥 | `group` | UsersPage |
   | 🔫 | `gps_fixed` | WeaponSettingsPage |
   | 🤖 | `smart_toy` | AiSettingsPage |
   | 🥽 | `link` | Weld |
   | ⛔ | `block` | NoCollide |
   | 🚀 | `rocket_launch` | Thruster |
   | 📚 | `library_books` | Stacker |
   | … | … | … |

   Full mapping in commit. 47 attributes changed in 34 files.

## Prerequisites for the workaround to render

The Wine prefix `compatdata/2129370` needs **Material Icons** registered:

1. `MaterialIcons-Regular.ttf` placed in
   `~/.local/share/Steam/steamapps/compatdata/2129370/pfx/drive_c/windows/Fonts/`.
2. Registry entry in
   `~/.local/share/Steam/steamapps/compatdata/2129370/pfx/system.reg` under
   `[Software\\Microsoft\\Windows NT\\CurrentVersion\\Fonts]`:
   ```
   "Material Icons (TrueType)"="MaterialIcons-Regular.ttf"
   ```

See [UBUNTU_SETUP.md](./UBUNTU_SETUP.md) for the broader Linux setup steps.

## Caveats

- This patch lives in the local `sandbox` submodule. If you ever update the
  submodule from Facepunch upstream, the emoji attributes will come back and
  the patch will need to be re-applied (or stored as a maintained branch).
- Material Icons ligature names aren't 1:1 with the original emojis;
  some replacements are approximations (e.g. 🛞 wheel → `circle`,
  🪤 trap → `close_fullscreen`).
- Other `<div class="icon">` consumers (e.g. Dupes folder grid, individual
  ToolGun mode renderers) may need their own SCSS `font-family: "Material Icons"`
  rule if their icons render as literal text after the swap.

## Long-term fix

The proper fix lives in Wine — either:

- Implement `IDWriteColorGlyphRunEnumerator::GetCurrentRun` so COLR emoji
  fonts render, or
- Wire `IDWriteFontFallback::MapCharacters` to consult the prefix's Fonts
  registry / SystemLink chain for emoji codepoints.

Until then, this source patch is the only deterministic way to keep the UI
icons readable on Linux.
