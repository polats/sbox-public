# Razor UI + SCSS in s&box

In-game UI in s&box is **Blazor-style Razor** rendered by **SkiaSharp**. Each
panel is a `.razor` file with matching `.razor.scss` (Source 2-flavored SCSS,
not standard CSS — different layout model and a different shorthand set).

## `@inherits` — Panel vs PanelComponent

There are **two** different base classes for Razor panels, and picking the
wrong one is silent in the compiler but breaks the scene loader:

- **`@inherits Panel`** — for panels nested *inside* another panel (children
  of a `ScreenPanel` rendered through Blazor composition, or sub-panels of a
  parent `PanelComponent`). These render only when their parent renders them.
- **`@inherits PanelComponent`** — for panels you attach to a GameObject as
  a Component in `.scene` JSON. This is the only form the scene loader can
  resolve via `__type`.

If you see `Missing Component: couldn't find Component type X.Y.MyPanel` in
the editor log when loading a scene, the Razor class is inheriting from
`Panel` and needs to inherit from `PanelComponent` instead.

## Required `@using` directives

Razor-generated C# does **not** auto-import `System` or `System.Linq`. If your
`@code` block uses `HashCode`, `FirstOrDefault`, `Where`, etc., you need:

```razor
@namespace YourGame
@using System;
@using System.Linq;
@using Sandbox;
@using Sandbox.UI;
@inherits PanelComponent
```

Without these you get compile errors like `The name 'HashCode' does not exist`
or `'IEnumerable<T>' does not contain a definition for 'FirstOrDefault'` in
the generated `_gen_<file>.razor_*.cs`. Semicolons at line-end are tolerated
either way.

To scaffold a panel, drop `.razor` + matching `.razor.scss` files into the
project's `Code/UI/` (or a subdir) — see the templates further down.

## File layout

```
Code/
└── UI/
    ├── Hud.razor
    ├── Hud.razor.scss
    └── Vitals/
        ├── HealthBar.razor
        ├── HealthBar.razor.scss
        └── Vitals.cs              ← optional code-behind
```

The editor compiles `.razor` → C# classes on save. Hot reload picks up changes
within ~1s; if it doesn't, the editor's console reports the compile error.

## Anatomy of a panel

```razor
@namespace MyGame
@using System
@using System.Linq
@using Sandbox
@using Sandbox.UI
@inherits PanelComponent

<root>
    <div class="header">
        <div class="icon">favorite</div>
        <div class="title">@Title</div>
    </div>
    <div class="body">@ChildContent</div>
</root>

@code
{
    [Parameter] public string Title { get; set; } = "Default";
    [Parameter] public RenderFragment ChildContent { get; set; }

    // BuildHash decides when to re-render. Include every field your output
    // depends on — otherwise the UI silently goes stale.
    protected override int BuildHash() => HashCode.Combine( Title );

    protected override void OnAfterTreeRender( bool firstTime )
    {
        if ( firstTime ) { … }
    }
}
```

Key conventions:
- `<root>` is required and becomes the panel's own bounding box.
- Use `class="..."` for SCSS hooks; `@onclick=` and `@oninput=` for events.
- `BuildHash` must change when any displayed value changes; forgetting this is
  the #1 cause of "UI shows old value" bugs.

## The SCSS file

Source 2's SCSS dialect is similar to standard SCSS but with key differences:
- The selector starts with the **panel name** (matches `<root>` of the matching
  .razor), not a `.class` from the markup.
- Properties are CSS-ish but layout uses **flexbox by default**, not block.
- `font-family: "Material Icons";` is mandatory for icon glyphs — see
  `ui-icons-linux.md`.
- Variables like `$body-font`, `$title-font`, `$primary-color` are defined in
  the sandbox project's `Code/UI/Theme.scss`. Reuse them.

```scss
HealthBar
{
    flex-direction: row;
    align-items: center;
    gap: 0.5rem;
    padding: 0.5rem 1rem;
    background-color: rgba( black, 0.5 );

    .icon
    {
        font-family: "Material Icons";
        font-size: 24px;
    }

    .title
    {
        font-family: $title-font;
        font-size: 18px;
        color: white;
    }

    &.warn
    {
        background-color: rgba( red, 0.4 );
    }
}
```

## Common patterns

**Bind state to a Component**
```razor
@using Sandbox
@inherits Panel
@namespace MyGame

<root class=@(Player.IsHurt ? "warn" : "")>
    <label>@Player.Health</label>
</root>

@code {
    public Player Player { get; set; }
    protected override int BuildHash() => HashCode.Combine( Player?.Health, Player?.IsHurt );
}
```

**List of items with virtual grid** (large data sets)
```razor
<VirtualGrid Items=@Items ItemSize=@(120)>
    <Item Context="item">
        <SpawnMenuIcon Title=@item.Title Icon=@item.Icon />
    </Item>
</VirtualGrid>
```

**Button with icon (idiomatic in sandbox)**
```razor
<Button Text="Save" Icon="save" class="menu-action primary" @onclick=@OnSaveClick />
```
The `Icon="save"` value is a Material Icons name — see `rules/ui-icons-linux.md` for the lookup table or browse <https://fonts.google.com/icons>.

## Razor in this repo

Reference implementations live under `sandbox/Code/UI/`:
- `SpawnMenuModeBar.razor` — top tab bar pattern (we patched its SCSS to give
  `.icon` the Material Icons font).
- `Controls/StringQueryPopup.razor` — input + confirm/cancel button pattern.
- `Pressable/PressableTooltip.razor` — tooltip with title/icon/description.

## Pitfalls

- **No re-render on state change** → check `BuildHash` includes the field.
- **`Style.SetBackgroundImage("…")` for dynamic images.** SCSS can't bind to
  C# expressions; use the `Style` API in `OnAfterTreeRender` or a `style=…`
  attribute.
- **`.razor.scss` SCSS errors don't surface as compile errors** — the panel
  just renders unstyled. Check the editor's UI inspector.
- **Avoid CRLF line endings on Linux.** The editor on Windows writes CRLF; if
  you open and save the file on Linux it switches to LF and `git diff` floods.
