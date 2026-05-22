# Cinematics: MovieMaker + ActionGraph + Localization

Three niche systems with very different maturity for autonomous use.
Learnings from `examples/cinematic-intro/` (camera fly-through past
props with localized title screen).

## MovieMaker / MoviePlayer (camera animations) — WORKS at runtime

**Verdict**: real and usable from autonomous code, **without** authoring
a `.movie` GameResource file on disk. Build an in-memory `MovieClip`,
bind references, hand it to a `MoviePlayer` Component.

```csharp
using Sandbox.MovieMaker;

// 1. Author tracks: a GameObject reference track + property sub-tracks
var clipBuilder = new MovieClipBuilder();
var cameraTrack = clipBuilder.RootGameObject( cameraGo );

// 2. CRITICAL: property tracks are SEPARATE for Position/Rotation/Scale,
//    NOT a fused Transform track. Engine MovieRecorder records exactly
//    these three names: LocalPosition (Vector3), LocalRotation (Rotation),
//    LocalScale (Vector3).
var posTrack = cameraTrack.Property<Vector3>( nameof(GameObject.LocalPosition) );
var rotTrack = cameraTrack.Property<Rotation>( nameof(GameObject.LocalRotation) );

posTrack.WithSamples( /* CompiledSampleBlock<Vector3> with your keyframes */ );
rotTrack.WithSamples( /* CompiledSampleBlock<Rotation> */ );

var clip = MovieClip.FromTracks( cameraTrack, posTrack, rotTrack );
//                                ^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^^
//                                Pass each track explicitly — the factory's
//                                DiscoverTracksInHierarchy walks up via
//                                track.Parent only, not down. Just passing
//                                the GameObject reference track loses the
//                                property tracks.

// 3. Attach to MoviePlayer + bind the reference track to the actual GameObject
var mp = cameraGo.Components.Create<MoviePlayer>();
mp.Binder.Add( cameraTrack, cameraGo );
mp.CreateTargets = false;   // CRITICAL — true spawns DUPLICATES of every
                            // referenced GameObject. Set false after manual bind.
mp.Clip = clip;
mp.Play();
```

The MoviePlayer's own `OnUpdate` advances time and applies samples —
**don't** also lerp in your `OnUpdate`.

**Authoring `.movie` files standalone**: editor-only territory.
Moviemaker UI builds the EditorData JSON + a separate Compiled tree,
and round-tripping by hand is several days of reverse-engineering for
no agent-side benefit. Use the in-memory `MovieClip` route above.

## ActionGraph — punt to C# state machines

**Verdict**: pivot to a plain C# state machine in your Component.

The `.action` resource is a `Facepunch.ActionGraphs.ActionGraph`
deserialized from a `JsonNode` containing the full visual-scripting
graph (node positions, pin types, link routing metadata, etc).

- No published schema for the format.
- No example `.action` files anywhere in the repo to crib from.
- Authoring by hand is reverse-engineering with very low value/effort
  ratio.

Just write a switch on an enum in your `OnUpdate`. State machines are
~20 lines of C# you can read and debug.

If a future engine version ships authorable `.action` examples, revisit.

## Localization — `.json` files ship, switching is hard

**Verdict**: file format is real and works; runtime switching needs
careful workarounds.

### Author the phrase files

`Localization/<langcode>/<sheet>.json`:

```json
{
  "intro.title": "Forgotten Horizon",
  "intro.start": "Press SPACE to Start",
  "intro.settings": "Settings",
  "intro.quit": "Quit"
}
```

Match the structure from `sandbox/Localization/es/menu.json`. The
engine's `Sandbox.LanguageContainer` loads these.

### Reading via the engine's resolver

```csharp
var phrase = Game.Language.GetPhrase( "intro.title" );
```

This works fine — assuming the editor's language is already what you
want. The catch is changing it from game code.

### Setting `Application.LanguageCode` from game code — blocked

```csharp
// ✗ blocked by the gameplay-code whitelist
Application.LanguageCode = "es";

// ✗ also blocked
var prop = typeof(Application).GetProperty( "LanguageCode" );
prop.SetValue( null, "es" );   // PropertyInfo.SetValue is not whitelisted
```

Trying to switch language from a game-side Component fails because:

1. `Sandbox.Application.LanguageCode` has an `internal set`.
2. `System.Reflection.PropertyInfo.SetValue` is blocked by the
   game-code whitelist (same restriction as `Type.GetProperty(string)`
   noted in `rules/component-lifecycle.md`).
3. `[ConVar("language")]` exists but has `ConVarFlags.Protected` —
   `ConsoleSystem.Run("language", "es")` throws
   `Can't run 'language'` at runtime from a Component.
4. `Sandbox.Game.Language.FileSystem` is `internal`, so you can't
   build your own resolver against the engine's mounted JSON files
   either.

### Workable pattern: parallel C# dictionary

Ship the engine `.json` files for reference / future migration, but
implement runtime switching with your own `Dictionary<string, Dictionary<string,string>>`:

```csharp
public static class Loc
{
    private static string _lang = "en";
    private static readonly Dictionary<string, Dictionary<string,string>> _phrases
        = new()
        {
            ["en"] = new() { ["intro.title"] = "Forgotten Horizon", … },
            ["es"] = new() { ["intro.title"] = "Horizonte Olvidado", … },
        };

    public static string T( string key ) =>
        _phrases.TryGetValue( _lang, out var d ) && d.TryGetValue( key, out var s )
            ? s : key;

    public static void Set( string code ) => _lang = code;
}
```

Call `Loc.T("intro.title")` from Razor. Honest fallback. In a project
where the user sets language via the editor menu (the supported path),
`Game.Language.GetPhrase` resolves the same keys from the same JSONs —
just not when the *game* wants to drive the switch.

## Cross-cutting gotchas this run surfaced

- **`CreateTargets = true` on MoviePlayer spawns duplicates** of every
  referenced GameObject if you haven't pre-bound. Always set false
  after manual `Binder.Add`.
- **Scene-level Component references in JSON need all four fields**:
  `_type: "component"`, `component_id`, `go` (parent GameObject id),
  and `component_type` (short class name). A `component_id`-only ref
  looks plausible to a human reader but doesn't resolve at load time.
- **`ConsoleSystem.Run` on Protected ConVars throws `Can't run '<name>'`
  at runtime**, repeatedly inside `OnUpdate`. Guard with a try/catch
  or check the cvar's flags first if you must call it speculatively.
- **`Application.LanguageCode` is `internal set`** — see localization
  section above.
- **The compile-time whitelist blocks more than just
  `Type.GetProperty`**: it also covers `PropertyInfo.SetValue` and
  several other reflection mutation paths. Reach into engine
  internals from editor-side code (`sbox-eval`, Editor/ csproj),
  not game-side code.
