# GameResource and asset loading

Learnings from `examples/arena-themes/` (4 `.theme` GameResources, runtime
switching, persisted last-pick via FileSystem.Data). Read this when you
need data-driven content (themes, weapon definitions, level configs,
item lists, etc).

## Declaring a custom GameResource

```csharp
using Sandbox;

namespace Local.ArenaThemes;

[GameResource( "Arena Theme", "theme",
    "Visual theme for the arena (floor color, prop model, sky tint).",
    Icon = "palette" )]
public class ArenaTheme : GameResource
{
    [Property] public string DisplayName { get; set; } = "Unnamed";
    [Property] public Color FloorColor { get; set; } = Color.Gray;
    [Property] public Model PropModel { get; set; }            // Model picker in inspector
    [Property, Range(4, 32)] public int PropCount { get; set; } = 12;
    [Property, Group("Lighting"), Range(0.1f, 3f)]
    public float AmbientIntensity { get; set; } = 1f;
}
```

Three attribute args matter:
1. **Display name** — what shows in the asset browser.
2. **File extension** — without the dot. Files of `*.<ext>` become this
   GameResource type. Pick something short and unique to your game
   (`theme`, `weapon`, `level`).
3. **Description** — tooltip in the asset browser.
4. `Icon = "palette"` — a Material Icons name shown next to the
   resource in the asset browser.

Field types the inspector picks for automatically: `string`, `int`,
`float`, `bool`, `Color`, `Vector3`, `Model`, `Material`, `Sound`,
`Prefab`, enum, list of any of the above, and nested `GameResource`
references.

## Registration happens on assembly compile

The editor learns about your `[GameResource]` type when your project's
assembly compiles. Workflow:

1. Add the `ArenaTheme.cs` file.
2. `sbox-launch --kill && sbox-launch examples/<proj> --wait-ready` —
   restart is required for new files (Wine's watcher misses creates).
3. The editor's asset browser now recognises `.theme` files. You can
   right-click → New → Arena Theme to create one in the editor.

Or — for autonomous workflows — write the `.theme` JSON directly to
disk (see format below) and the editor picks them up after the next
relaunch.

## `.theme` file JSON format

Don't try to serialize via `JsonSerializer.Serialize(yourResource)` —
nested types like `Material` contain ref-struct accessors
(`Material.FlagsAccessor`) that the framework serializer chokes on.
**The engine uses its own serializer with custom handling** for these.

For *authoring*, write the JSON by hand using the engine's text shape:

```json
{
  "DisplayName": "Forest",
  "FloorColor": "0.18,0.45,0.20,1",
  "SkyColor": "0.10,0.22,0.12,1",
  "PropModel": "models/sbox_props/trees/oak/tree_oak_big_a.vmdl",
  "PropCount": 14,
  "PropScale": 0.12,
  "AmbientIntensity": 1.0,
  "__references": [],
  "__version": 0
}
```

Shape rules:
- `Color` → `"r,g,b,a"` text form (floats 0..1).
- `Vector3` → `"x,y,z"` text form.
- `Model`/`Material`/`Sound`/`Prefab` → resource-path string
  (`"models/foo.vmdl"`).
- `__references: []` and `__version: 0` are the engine's GameResource
  footer — always include.

## Loading at runtime

```csharp
// Load one by path
var theme = ResourceLibrary.Get<ArenaTheme>( "themes/forest.theme" );

// Load all of a type — picks up every .theme under Assets/, any subdir
var all = ResourceLibrary.GetAll<ArenaTheme>();
foreach ( var t in all )
    Log.Info( $"loaded {t.DisplayName}" );
```

`GetAll<T>()` is the path for populating selectors/menus dynamically.

## Save/Load player state with `FileSystem.Data`

```csharp
using System.Text.Json;

private const string SavePath = "your-project/state.json";
private record SavedState( string lastTheme, int score );

void Save( SavedState s )
{
    FileSystem.Data.WriteAllText( SavePath, JsonSerializer.Serialize( s ) );
}

SavedState Load()
{
    if ( !FileSystem.Data.FileExists( SavePath ) ) return null;
    return JsonSerializer.Deserialize<SavedState>( FileSystem.Data.ReadAllText( SavePath ) );
}
```

`FileSystem.Data` is the writable per-project data directory. Survives
editor restarts. For records with primitive fields (string/int/float
/bool/Color-as-string), `System.Text.Json` works fine. For records
containing engine types (Model/Material), see the GameResource caveat
above — store the *resource path* (string) instead and re-resolve via
`ResourceLibrary.Get<T>(path)` on load.

## `System.Text.Json` + `Vector3`: use 3 floats, not engine "x,y,z"

When you save data via `FileSystem.Data.WriteAllText` + `JsonSerializer`,
you're using `System.Text.Json` — **not** the engine's GameResource
serializer. The engine's "x,y,z" text encoding for `Vector3` is NOT
applied; STJ writes it as `{"x": ..., "y": ..., "z": ...}` or fails
to roundtrip. Easier: store coordinates as three separate floats:

```csharp
private record TowerData( string Type, float X, float Y, float Z );
private record SaveState( int Gold, int Wave, List<TowerData> Towers );
```

Convert at the boundary (`new Vector3(t.X, t.Y, t.Z)` on load,
`new TowerData(type, pos.x, pos.y, pos.z)` on save). Verified pattern
from `examples/tower-defense/`.

## sbox-eval can't see `Local.<X>` namespaces directly

`sbox-eval` snippets are compiled against engine assemblies only; your
project's `Local.MyGame.Foo` namespace is invisible to the auto-import.

To call into project code from `sbox-eval`, reflect via AppDomain:

```bash
sbox-eval '
  var asm = AppDomain.CurrentDomain.GetAssemblies()
    .First( a => a.GetName().Name == "package.local.your_project" );
  var type = asm.GetType( "Local.YourProject.ArenaTheme" );
  return type.GetMethod( "SomeStatic" ).Invoke( null, null );
'
```

The assembly name is `package.local.<project_ident_snake_case>` — see
the next section.
