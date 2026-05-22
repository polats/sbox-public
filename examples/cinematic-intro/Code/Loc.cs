using System.Collections.Generic;
using Sandbox;

namespace Local.CinematicIntro;

/// <summary>
/// Lightweight localization fallback. We tried hooking into Sandbox.Localization
/// (Game.Language.GetPhrase + Localization/&lt;lang&gt;/*.json files on disk), but:
///
///   1. The active language is read from Sandbox.Application.LanguageCode, which
///      has an *internal* setter. We can't reflect onto it from gameplay code —
///      Sandbox's runtime whitelist blocks PropertyInfo.SetValue on engine types.
///   2. The ConVar "language" exists but `ConsoleSystem.Run("language", "es")`
///      throws "Can't run 'language'" from gameplay code — it's protected.
///   3. Game.Language.FileSystem (the AggregateFileSystem mounted at
///      Localization/) is `internal`, so we can't even load the phrase JSONs
///      ourselves through the engine's mount point.
///
/// So we keep the engine-shaped Localization/&lt;code&gt;/*.json files on disk
/// (loadable by the engine's PhraseCollection in any project that *can* set
/// the language), but at runtime we read a parallel hardcoded dictionary
/// below. The {Var} substitution syntax matches Sandbox.Localization.Phrase.
/// </summary>
public static class Loc
{
	public static string Current { get; private set; } = "en";

	private static readonly Dictionary<string, Dictionary<string, string>> _phrases = new()
	{
		["en"] = new()
		{
			["intro.title"]    = "FORGOTTEN HORIZON",
			["intro.start"]    = "Press SPACE to Start",
			["intro.settings"] = "Settings",
			["intro.quit"]     = "Quit",
			["intro.language"] = "Language: {Lang}",
			["intro.lang_name"] = "English",
		},
		["es"] = new()
		{
			["intro.title"]    = "HORIZONTE OLVIDADO",
			["intro.start"]    = "Pulsa ESPACIO para Empezar",
			["intro.settings"] = "Ajustes",
			["intro.quit"]     = "Salir",
			["intro.language"] = "Idioma: {Lang}",
			["intro.lang_name"] = "Español",
		},
	};

	public static void Set( string code ) => Current = _phrases.ContainsKey( code ) ? code : "en";

	public static string T( string key, Dictionary<string, object> vars = null )
	{
		if ( !_phrases.TryGetValue( Current, out var map ) ) map = _phrases["en"];
		if ( !map.TryGetValue( key, out var val ) ) return key;

		if ( vars == null || !val.Contains( '{' ) ) return val;

		// Tiny {Var} substituter, matching Sandbox.Localization.Phrase semantics
		// (we'd use that class but it's only reachable via the gated container).
		foreach ( var kv in vars )
			val = val.Replace( "{" + kv.Key + "}", kv.Value?.ToString() ?? "" );
		return val;
	}
}
