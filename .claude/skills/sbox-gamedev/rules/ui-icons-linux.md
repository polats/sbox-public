# UI Icons on Linux: Use Material Icons, Not Emoji

Wine's DirectWrite implementation can't render emoji codepoints reliably. The
result is "tofu" boxes-with-X where `[Icon("📦")]` should appear. The fix is
to use **Material Icons ligature names** instead of emoji characters.

Full diagnostic notes in [`EMOJI_ICONS_ISSUE.md`](../../../EMOJI_ICONS_ISSUE.md)
at the repo root. This file is the development rule: don't write new emoji
icon attributes on Linux.

## The rule

**Don't** write:
```csharp
[Icon( "📦" )]                            // ❌ tofu under Wine
@attribute [Icon( "🔧" )]                 // ❌
<div class="icon">📦</div>                // ❌
```

**Do** write:
```csharp
[Icon( "inventory_2" )]                   // ✅ Material Icons ligature
@attribute [Icon( "build" )]              // ✅
<div class="icon">inventory_2</div>       // ✅ (with .icon { font-family: "Material Icons" })
```

## Looking up names

Use the tool:
```
tools/sbox-icon wrench       → handyman / construction / build
tools/sbox-icon save          → save / save_alt / save_as
tools/sbox-icon spawn         → (via alias) inventory_2 / add_box
```

The icons gallery is at <https://fonts.google.com/icons> — useful when the
fuzzy matcher comes up empty. Pass any name you find verbatim to `[Icon(…)]`.

## SCSS requirement

A `<div class="icon">name</div>` only renders as a glyph if the SCSS rule for
that `.icon` sets the font-family:

```scss
.icon
{
    font-family: "Material Icons";
    font-size: 24px;
    line-height: 1;
}
```

`tools/sbox-new-ui --with-icon <name>` includes this stanza automatically.
The shipped sandbox project has it in:
- `Code/UI/Components/MenuPanel.razor.scss`
- `Code/UI/Pressable/PressableTooltip.razor.scss`
- `Code/UI/SpawnMenuModeBar.razor.scss` (added by our patch)

If your panel renders the icon name as literal text (e.g. you see "build"
instead of a hammer glyph), add or extend the `.icon { font-family: … }` rule.

## Quick lookup for common UI concepts

| Concept           | Material Icons name      | Used by (in sandbox)            |
|-------------------|--------------------------|---------------------------------|
| Spawn / box       | `inventory_2`            | SpawnMenu                       |
| Tools / build     | `build`                  | UtilitiesPage, ToolsTab         |
| World / globe     | `language` / `public`    | UtilityTab                      |
| Search            | `search`                 | ContextMenuHost                 |
| Settings / cog    | `settings`               | HydraulicTool                   |
| Save              | `save`                   | SaveMenu, Buttons               |
| Cleanup / broom   | `cleaning_services`      | CleanupPage                     |
| Effects / paint   | `palette`                | EffectsHost                     |
| Users / group     | `group`                  | UsersPage                       |
| Weapon            | `gps_fixed`              | WeaponSettingsPage              |
| Robot / AI        | `smart_toy`              | AiSettingsPage                  |
| Link / weld       | `link`                   | Weld, LinkerTool                |
| Block             | `block`                  | NoCollide                       |
| Rocket / thrust   | `rocket_launch`          | ThrusterTool                    |
| Books / stack     | `library_books`          | StackerTool                     |
| Delete            | `delete`                 | Remover, IconPanel              |
| Brush / decal     | `brush`                  | DecalTool                       |
| Folder            | `folder`                 | Dupes categories                |
| Add               | `add`, `add_box`         | scaffolding wizards             |
| Check             | `check`, `check_circle`  | Confirm buttons                 |
| Close             | `close`                  | Cancel buttons                  |
| Sync / refresh    | `sync`, `refresh`        | SpawnlistView                   |
| Cloud upload      | `cloud_upload`           | SpawnlistView                   |

## Existing emoji in this repo

The `sandbox/` submodule has 47 `[Icon( emoji )]` attributes pre-patched to
Material Icons names — see commit `linux-proton-emoji-icons`. If you pull
upstream `Facepunch/sandbox`, the emoji return and you'll need to re-patch.

Run `tools/sbox-doctor <project-dir>` after a submodule update to verify state.

## Cross-platform note

Material Icons names work on Windows too — they're not a Linux-only choice. If
you intend to merge your project's changes back upstream (or want compatibility
with Windows-using collaborators), Material Icons is the portable answer.
Emoji-in-source only works on Windows; Linux/Proton can't catch up to Windows'
DirectWrite emoji fallback until Wine implements it.
