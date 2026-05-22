using Editor;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Sandbox;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;
using System.Text;
using System.Text.Json;

namespace Local.CinematicIntro.EditorTools;

/// <summary>
/// Editor-side helper for the sbox-gamedev skill.
///
/// Polls `<project>/.sbox/skill-triggers/` every editor frame. When a
/// trigger file appears, parses its first line as a console command (or
/// special directive) and runs it via `Sandbox.ConsoleSystem.Run`, then deletes
/// the trigger file so it only fires once. Replaces the brittle
/// xdotool-F5-and-pray dance with a deterministic file-poll handshake.
///
/// Trigger formats currently supported:
///   "screenshot WxH"            → screenshot_highres W H
///   "video"                     → toggle recorder
///   "video-clip N"              → play→record N sec→stop→exit-play sequence
///   "cmd <raw console cmd>"     → ConsoleSystem.Run verbatim
///   "play" / "stop-play"        → EditorScene.Play() / .Stop()
///   "probe-models" + paths      → log BOUNDS_PROBE entries
///   "scene-state"               → write JSON snapshot of Game.ActiveScene
///                                 to .sbox/skill-results/<trigger-id>.json
///   "invoke-menu <path>"        → fire a menu item by its dot-path
///                                 (e.g. "Game.Play", "Code.Recompile")
///   "shortcut <name>"           → invoke a named [Shortcut] action
///                                 (e.g. "editor.toggle-play", "editor.save")
///
/// Result conventions:
/// - For triggers with engine-side output (screenshot/video), the engine
///   logs the result path itself ("Screenshot saved to:", "Video recording
///   finished:"); the tool greps the log.
/// - For triggers that return structured data (scene-state), SkillTrigger
///   writes JSON to <project>/.sbox/skill-results/<trigger-id>.json where
///   trigger-id is the basename of the trigger file without its extension.
///   The tool polls for the result file.
/// </summary>
public static class SkillTrigger
{
	private const string TriggerSubdir = ".sbox/skill-triggers";
	private const string ResultSubdir  = ".sbox/skill-results";

	private static RealTimeSince _timeSinceScan = 0;

	// State for the multi-step "video-clip" sequence (play → record →
	// stop → leave play). Managed across editor frames.
	private enum VideoClipPhase { None, WaitForPlay, Recording, Stopping, WaitForExit }
	private static VideoClipPhase _videoPhase = VideoClipPhase.None;
	private static RealTimeSince _videoPhaseStart;
	private static float _videoDuration;

	[EditorEvent.Frame]
	public static void OnFrame()
	{
		// Drive an in-flight video-clip sequence if any.
		TickVideoClip();

		// Only scan triggers ~4 times a second to keep editor responsive.
		if ( _timeSinceScan < 0.25f ) return;
		_timeSinceScan = 0;

		ScanProject( GetActiveProjectRoot() );
	}

	private static void TickVideoClip()
	{
		switch ( _videoPhase )
		{
			case VideoClipPhase.None: return;

			case VideoClipPhase.WaitForPlay:
				// Wait for Game.IsPlaying to actually become true (Play() may
				// be async). Cap at 5s — if play never engages, abort.
				if ( Game.IsPlaying )
				{
					Log.Info( $"[SkillTrigger] video-clip: play engaged after {(float)_videoPhaseStart:F2}s; starting recorder" );
					// CRITICAL: when Play() is invoked programmatically (no
					// SceneRenderingWidget focus change), SceneCamera.RecordingCamera
					// is never set, so RenderPipeline doesn't attach the
					// MediaRecorderLayer and the recorder gets zero frames.
					// Explicitly point RecordingCamera at the active scene's main
					// camera. Both members are internal — reflect.
					EnsureRecordingCameraSet();
					Sandbox.ConsoleSystem.Run( "video" );
					_videoPhase = VideoClipPhase.Recording;
					_videoPhaseStart = 0;
				}
				else if ( _videoPhaseStart >= 5.0f )
				{
					Log.Warning( "[SkillTrigger] video-clip: Play() didn't engage after 5s — aborting" );
					_videoPhase = VideoClipPhase.None;
				}
				return;

			case VideoClipPhase.Recording:
				if ( _videoPhaseStart >= _videoDuration )
				{
					Log.Info( $"[SkillTrigger] video-clip: stopping after {_videoDuration:F1}s" );
					Sandbox.ConsoleSystem.Run( "video" );
					_videoPhase = VideoClipPhase.Stopping;
					_videoPhaseStart = 0;
				}
				return;

			case VideoClipPhase.Stopping:
				// Give encoder ~1s to flush
				if ( _videoPhaseStart >= 1.0f )
				{
					Log.Info( "[SkillTrigger] video-clip: exiting play mode" );
					EditorScene.Stop();
					_videoPhase = VideoClipPhase.WaitForExit;
					_videoPhaseStart = 0;
				}
				return;

			case VideoClipPhase.WaitForExit:
				if ( _videoPhaseStart >= 0.5f )
				{
					Log.Info( "[SkillTrigger] video-clip: done" );
					_videoPhase = VideoClipPhase.None;
				}
				return;
		}
	}

	// SceneCamera.RecordingCamera is internal; reflect to set it. RenderPipeline
	// uses sceneCamera.IsRecordingCamera (also internal — backed by this static)
	// to decide whether to attach MediaRecorderLayer.
	private static void EnsureRecordingCameraSet()
	{
		try
		{
			var cam = Game.ActiveScene?.Camera;
			if ( cam == null )
			{
				Log.Warning( "[SkillTrigger] video-clip: Game.ActiveScene has no Camera — recording will fail" );
				return;
			}
			// CameraComponent.SceneCamera holds the engine-level SceneCamera.
			var sceneCamProp = cam.GetType().GetProperty( "SceneCamera", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic );
			var sceneCam = sceneCamProp?.GetValue( cam );
			if ( sceneCam == null )
			{
				Log.Warning( "[SkillTrigger] video-clip: CameraComponent.SceneCamera is null" );
				return;
			}
			var sceneCameraType = sceneCam.GetType();
			// Walk up the type hierarchy until we find RecordingCamera (declared on SceneCamera).
			var t = sceneCameraType;
			System.Reflection.PropertyInfo recordingProp = null;
			while ( t != null )
			{
				recordingProp = t.GetProperty( "RecordingCamera", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public );
				if ( recordingProp != null ) break;
				t = t.BaseType;
			}
			if ( recordingProp == null )
			{
				Log.Warning( "[SkillTrigger] video-clip: couldn't find SceneCamera.RecordingCamera property via reflection" );
				return;
			}
			recordingProp.SetValue( null, sceneCam );
			Log.Info( "[SkillTrigger] video-clip: SceneCamera.RecordingCamera set" );
		}
		catch ( Exception ex )
		{
			Log.Warning( $"[SkillTrigger] video-clip: EnsureRecordingCameraSet failed: {ex.GetType().Name}: {ex.Message}" );
		}
	}

	private static void StartVideoClip( float seconds )
	{
		if ( _videoPhase != VideoClipPhase.None )
		{
			Log.Warning( "[SkillTrigger] video-clip already in progress, ignoring" );
			return;
		}
		_videoDuration = Math.Clamp( seconds, 0.5f, 60f );
		Log.Info( $"[SkillTrigger] video-clip: entering play mode for {_videoDuration:F1}s clip" );
		EditorScene.Play();
		_videoPhase = VideoClipPhase.WaitForPlay;
		_videoPhaseStart = 0;
	}

	private static string GetActiveProjectRoot()
	{
		try
		{
			var p = Project.Current;
			if ( p == null ) return null;
			return Path.GetDirectoryName( p.ConfigFilePath );
		}
		catch { return null; }
	}

	private static void ScanProject( string projectRoot )
	{
		if ( string.IsNullOrEmpty( projectRoot ) ) return;
		var triggerDir = Path.Combine( projectRoot, TriggerSubdir );
		if ( !Directory.Exists( triggerDir ) ) return;

		foreach ( var file in Directory.EnumerateFiles( triggerDir ) )
		{
			try
			{
				HandleTriggerFile( file );
			}
			catch ( Exception ex )
			{
				Log.Warning( $"[SkillTrigger] failed to handle {file}: {ex.Message}" );
			}
			finally
			{
				try { File.Delete( file ); } catch { }
			}
		}
	}

	private static void HandleTriggerFile( string file )
	{
		var content = File.ReadAllText( file ).Trim();
		if ( string.IsNullOrEmpty( content ) ) return;

		Log.Info( $"[SkillTrigger] firing trigger: {Path.GetFileName( file )} → {content}" );

		var first = content.Split( '\n' )[0].Trim();
		var parts = first.Split( ' ' );

		switch ( parts[0] )
		{
			case "screenshot":
				{
					var wh = parts.Length > 1 ? parts[1] : "1280x720";
					var dims = wh.Split( 'x' );
					var w = dims.Length > 0 && int.TryParse( dims[0], out var pw ) ? pw : 1280;
					var h = dims.Length > 1 && int.TryParse( dims[1], out var ph ) ? ph : 720;
					Sandbox.ConsoleSystem.Run( $"screenshot_highres {w} {h}" );
					break;
				}
			case "video":
				// "video" alone toggles the recorder.
				Sandbox.ConsoleSystem.Run( "video" );
				break;
			case "video-clip":
				// "video-clip N" — runs the full sequence: enter play, start
				// recorder, wait N seconds, stop recorder, exit play.
				{
					var secs = parts.Length > 1 && float.TryParse( parts[1], out var s ) ? s : 5f;
					StartVideoClip( secs );
				}
				break;
			case "play":
				EditorScene.Play();
				break;
			case "stop-play":
				EditorScene.Stop();
				break;
			case "cmd":
				Sandbox.ConsoleSystem.Run( first.Substring( 4 ) );
				break;
			case "probe-models":
				ProbeModels( file );
				break;
			case "scene-state":
				DumpSceneState( file );
				break;
			case "invoke-menu":
				InvokeMenu( file, parts.Length > 1 ? string.Join( " ", parts.Skip( 1 ) ) : "" );
				break;
			case "shortcut":
				InvokeShortcut( file, parts.Length > 1 ? parts[1] : "" );
				break;
			case "eval":
				EvalCSharp( file, content );
				break;
			case "ping":
				// Cheap "are you alive" probe. Used by `sbox-launch --wait-ready`
				// to confirm the editor + extension are responsive before the
				// next tool call. Returns the editor's view of project + scene
				// state so the caller can sanity-check it landed on the right one.
				WriteResult( file, true, null, new
				{
					project = Project.Current?.Config?.Title,
					hasActiveSession = SceneEditorSession.Active != null,
					isPlaying = Game.IsPlaying,
				} );
				break;
			default:
				Log.Warning( $"[SkillTrigger] unknown trigger: {parts[0]}" );
				WriteResult( file, false, $"unknown trigger: {parts[0]}", null );
				break;
		}
	}

	// Result-file plumbing: write to <project>/.sbox/skill-results/<id>.json
	// where <id> is the trigger filename without its extension.
	private static void WriteResult( string triggerFile, bool ok, string error, object data )
	{
		try
		{
			var triggerDir = Path.GetDirectoryName( triggerFile );
			var projectRoot = Path.GetDirectoryName( Path.GetDirectoryName( triggerDir ) );
			var resultsDir = Path.Combine( projectRoot, ResultSubdir );
			Directory.CreateDirectory( resultsDir );
			var id = Path.GetFileNameWithoutExtension( triggerFile );
			var outPath = Path.Combine( resultsDir, id + ".json" );
			var payload = new Dictionary<string, object>
			{
				["ok"] = ok,
				["ts"] = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
				["error"] = error,
				["data"] = data,
			};
			// Atomic write: write to .tmp then rename, so the tool never
			// reads a half-written file.
			var tmp = outPath + ".tmp";
			File.WriteAllText( tmp, JsonSerializer.Serialize( payload, new JsonSerializerOptions { WriteIndented = true } ) );
			File.Move( tmp, outPath, overwrite: true );
		}
		catch ( Exception ex )
		{
			Log.Warning( $"[SkillTrigger] failed to write result for {triggerFile}: {ex.Message}" );
		}
	}

	private static void DumpSceneState( string triggerFile )
	{
		try
		{
			// In edit mode, the scene under the editor's cursor lives at
			// SceneEditorSession.Active.Scene. Fall back to Game.ActiveScene
			// (which is what play mode populates).
			var scene = SceneEditorSession.Active?.Scene ?? Game.ActiveScene;
			if ( scene == null )
			{
				WriteResult( triggerFile, false, "no active scene", null );
				return;
			}

			var gos = new List<object>();
			foreach ( var go in scene.GetAllObjects( true ) )
			{
				var components = new List<object>();
				foreach ( var c in go.Components.GetAll() )
				{
					var props = new Dictionary<string, object>();
					try
					{
						foreach ( var p in c.GetType().GetProperties() )
						{
							if ( p.GetCustomAttribute<PropertyAttribute>() == null ) continue;
							try { props[p.Name] = p.GetValue( c )?.ToString(); }
							catch { /* getter threw */ }
						}
					}
					catch { /* type reflection threw */ }
					components.Add( new
					{
						type = c.GetType().FullName,
						enabled = c.Active,
						properties = props,
					} );
				}
				gos.Add( new
				{
					id = go.Id.ToString(),
					name = go.Name,
					enabled = go.Active,
					position = $"{go.WorldPosition.x:F2},{go.WorldPosition.y:F2},{go.WorldPosition.z:F2}",
					rotation = $"{go.WorldRotation.x:F4},{go.WorldRotation.y:F4},{go.WorldRotation.z:F4},{go.WorldRotation.w:F4}",
					scale = $"{go.WorldScale.x:F2},{go.WorldScale.y:F2},{go.WorldScale.z:F2}",
					tags = go.Tags?.TryGetAll()?.ToArray() ?? Array.Empty<string>(),
					componentCount = components.Count,
					components = components,
				} );
			}

			// Scene.Title is obsolete in favor of a SceneInformation component;
			// pull from that if present, else fall back to the file name.
			string sceneName = null;
			try
			{
				var info = scene.GetAllComponents<SceneInformation>().FirstOrDefault();
				sceneName = info?.Title;
			}
			catch { /* SceneInformation may not exist in older builds */ }
			sceneName ??= "(unnamed scene)";

			WriteResult( triggerFile, true, null, new { sceneName, gameObjectCount = gos.Count, gameObjects = gos } );
		}
		catch ( Exception ex )
		{
			WriteResult( triggerFile, false, $"{ex.GetType().Name}: {ex.Message}", null );
		}
	}

	private static void InvokeMenu( string triggerFile, string menuPath )
	{
		// menuPath is slash- or dot-separated, e.g. "Game/Play" or "Code.Recompile".
		// We walk the menu tree manually because Menu.Options / Menu.Menus are
		// protected; reflect to read them, match by Title (case-insensitive).
		try
		{
			// EditorMainWindow.Current is internal — reflect.
			var win = (Widget)typeof( EditorMainWindow )
				.GetField( "Current", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public )
				?.GetValue( null );
			var menuBar = win == null ? null : (MenuBar)win.GetType()
				.GetProperty( "MenuBar", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic )
				?.GetValue( win );
			if ( menuBar == null )
			{
				WriteResult( triggerFile, false, "no editor main window / menu bar", null );
				return;
			}

			var parts = menuPath.Split( new[] { '/', '.' }, StringSplitOptions.RemoveEmptyEntries );
			if ( parts.Length == 0 )
			{
				WriteResult( triggerFile, false, "empty menu path", null );
				return;
			}

			Menu menu = FindChildMenu( menuBar, parts[0] );
			if ( menu == null )
			{
				WriteResult( triggerFile, false, $"top-level menu not found: {parts[0]}", null );
				return;
			}

			Option opt = null;
			for ( int i = 1; i < parts.Length; i++ )
			{
				// Try option first; fall through to submenu lookup if absent
				// (the last path component is usually an Option; intermediate
				// components are usually submenus).
				opt = FindOption( menu, parts[i] );
				if ( opt == null )
				{
					var sub = FindChildMenu( menu, parts[i] );
					if ( sub == null )
					{
						WriteResult( triggerFile, false, $"not found: {parts[i]} (in {(i == 1 ? parts[0] : parts[i - 1])})", null );
						return;
					}
					menu = sub;
				}
				else if ( i < parts.Length - 1 )
				{
					// Found an option but path continues — descend into its menu if it has one
					var sub = FindChildMenu( menu, parts[i] );
					if ( sub == null )
					{
						WriteResult( triggerFile, false, $"option {parts[i]} has no submenu", null );
						return;
					}
					menu = sub;
					opt = null;
				}
			}

			if ( opt?.Triggered != null )
			{
				opt.Triggered.Invoke();
				Log.Info( $"[SkillTrigger] invoked menu: {menuPath}" );
				WriteResult( triggerFile, true, null, new { menuPath } );
			}
			else
			{
				WriteResult( triggerFile, false, $"option {menuPath} has no Triggered action", null );
			}
		}
		catch ( Exception ex )
		{
			WriteResult( triggerFile, false, $"{ex.GetType().Name}: {ex.Message}", null );
		}
	}

	private static Menu FindChildMenu( object parent, string title )
	{
		var list = GetNonPublicEnumerable( parent, "Menus" );
		if ( list == null ) return null;
		foreach ( var m in list )
		{
			if ( m is Menu menu && string.Equals( menu.Title, title, StringComparison.OrdinalIgnoreCase ) )
				return menu;
		}
		return null;
	}

	private static Option FindOption( Menu menu, string title )
	{
		var list = GetNonPublicEnumerable( menu, "Options" );
		if ( list == null ) return null;
		foreach ( var o in list )
		{
			if ( o is Option opt && string.Equals( opt.Text, title, StringComparison.OrdinalIgnoreCase ) )
				return opt;
		}
		return null;
	}

	private static System.Collections.IEnumerable GetNonPublicEnumerable( object obj, string fieldName )
	{
		if ( obj == null ) return null;
		var t = obj.GetType();
		while ( t != null )
		{
			var f = t.GetField( fieldName, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public );
			if ( f != null )
				return f.GetValue( obj ) as System.Collections.IEnumerable;
			var p = t.GetProperty( fieldName, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public );
			if ( p != null )
				return p.GetValue( obj ) as System.Collections.IEnumerable;
			t = t.BaseType;
		}
		return null;
	}

	private static void InvokeShortcut( string triggerFile, string shortcutName )
	{
		try
		{
			if ( string.IsNullOrEmpty( shortcutName ) )
			{
				WriteResult( triggerFile, false, "empty shortcut name", null );
				return;
			}

			// EditorShortcuts.Entries is a public list keyed on .Identifier.
			var entry = EditorShortcuts.Entries.FirstOrDefault( e => e.Identifier == shortcutName );
			if ( entry == null )
			{
				WriteResult( triggerFile, false, $"shortcut not found: {shortcutName}", null );
				return;
			}

			// Each Entry has a public bool Invoke(bool force = false).
			var ok = entry.Invoke( force: true );
			Log.Info( $"[SkillTrigger] invoked shortcut: {shortcutName} (ok={ok})" );
			WriteResult( triggerFile, ok, ok ? null : "shortcut returned false (no matching target widget?)", new { shortcutName } );
		}
		catch ( Exception ex )
		{
			WriteResult( triggerFile, false, $"{ex.GetType().Name}: {ex.Message}", null );
		}
	}

	// Evaluate a C# snippet via raw Roslyn (no Microsoft.CodeAnalysis.Scripting
	// available in s&box's managed bin). The trigger body after the "eval"
	// line is the user's code; we try to interpret it as an expression first
	// (wrap in `return …;`), fall back to statements (must contain an explicit
	// return). Result, stdout, and timing are written to the result file.
	private static void EvalCSharp( string triggerFile, string fullContent )
	{
		try
		{
			// Strip the leading "eval" line; everything else is the snippet.
			var nl = fullContent.IndexOf( '\n' );
			var userCode = (nl < 0 ? "" : fullContent.Substring( nl + 1 )).Trim();
			if ( string.IsNullOrEmpty( userCode ) )
			{
				WriteResult( triggerFile, false, "empty eval body", null );
				return;
			}

			// Try expression form first; if the snippet contains a semicolon
			// or `return` keyword, treat as statements.
			bool isExpression = !userCode.Contains( ';' ) && !userCode.Contains( "return " );
			string body = isExpression
				? $"return ({userCode});"
				: userCode;

			var source = $@"
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Sandbox;
using Editor;

public static class _SkillEval
{{
    public static object Run()
    {{
        {body}
    }}
}}";
			var tree = CSharpSyntaxTree.ParseText( source );
			var refs = AppDomain.CurrentDomain.GetAssemblies()
				.Where( a => !a.IsDynamic && !string.IsNullOrEmpty( a.Location ) )
				.Select( a =>
				{
					try { return MetadataReference.CreateFromFile( a.Location ); }
					catch { return null; }
				} )
				.Where( r => r != null )
				.Cast<MetadataReference>()
				.ToArray();

			var compilation = CSharpCompilation.Create(
				"SkillEval_" + Guid.NewGuid().ToString( "N" ),
				new[] { tree },
				refs,
				new CSharpCompilationOptions( OutputKind.DynamicallyLinkedLibrary,
					optimizationLevel: OptimizationLevel.Release ) );

			using var ms = new MemoryStream();
			var emitResult = compilation.Emit( ms );
			if ( !emitResult.Success )
			{
				var errors = emitResult.Diagnostics
					.Where( d => d.Severity == DiagnosticSeverity.Error )
					.Select( d => d.ToString() )
					.ToArray();
				WriteResult( triggerFile, false, "compile failed",
					new { diagnostics = errors, source } );
				return;
			}

			ms.Position = 0;
			var asm = AssemblyLoadContext.Default.LoadFromStream( ms );
			var type = asm.GetType( "_SkillEval" );
			var method = type?.GetMethod( "Run" );
			if ( method == null )
			{
				WriteResult( triggerFile, false, "_SkillEval.Run() not found in emitted assembly", null );
				return;
			}

			var sw = System.Diagnostics.Stopwatch.StartNew();
			object rv;
			try
			{
				rv = method.Invoke( null, null );
			}
			catch ( TargetInvocationException tie )
			{
				var inner = tie.InnerException ?? tie;
				WriteResult( triggerFile, false, $"runtime error: {inner.GetType().Name}: {inner.Message}",
					new { stackTrace = inner.StackTrace } );
				return;
			}
			sw.Stop();

			// Stringify the result. Enumerables get their items printed
			// individually; other objects use ToString().
			string formatted;
			object structured;
			if ( rv == null )
			{
				formatted = "null";
				structured = null;
			}
			else if ( rv is System.Collections.IEnumerable en && rv is not string )
			{
				var items = new List<string>();
				int count = 0;
				foreach ( var item in en )
				{
					if ( count++ >= 100 ) { items.Add( "…(truncated)" ); break; }
					items.Add( item?.ToString() ?? "null" );
				}
				formatted = $"[{count} items] " + string.Join( ", ", items.Take( 10 ) ) + (items.Count > 10 ? "…" : "");
				structured = items;
			}
			else
			{
				formatted = rv.ToString();
				structured = formatted;
			}

			WriteResult( triggerFile, true, null, new
			{
				result = formatted,
				resultType = rv?.GetType().FullName ?? "null",
				items = structured,
				elapsedMs = sw.ElapsedMilliseconds,
			} );
		}
		catch ( Exception ex )
		{
			WriteResult( triggerFile, false, $"{ex.GetType().Name}: {ex.Message}", null );
		}
	}

	private static void ProbeModels( string triggerFile )
	{
		// trigger file format: first line "probe-models", subsequent lines are
		// model paths to probe (one per line). Same output as the play-mode
		// ModelProbe.cs Component, but fires from edit mode so no F5 needed.
		var lines = File.ReadAllLines( triggerFile );
		var paths = lines.Skip( 1 ).Where( s => !string.IsNullOrWhiteSpace( s ) ).ToList();
		Log.Info( $"BOUNDS_PROBE_START count={paths.Count}" );
		foreach ( var path in paths )
		{
			try
			{
				var m = Model.Load( path );
				if ( m == null )
				{
					Log.Info( $"BOUNDS_PROBE {path} ERROR=ModelNull" );
					continue;
				}
				var b = m.Bounds;
				Log.Info( $"BOUNDS_PROBE {path} mins={b.Mins.x:F2},{b.Mins.y:F2},{b.Mins.z:F2} maxs={b.Maxs.x:F2},{b.Maxs.y:F2},{b.Maxs.z:F2} size={b.Size.x:F2},{b.Size.y:F2},{b.Size.z:F2}" );
			}
			catch ( Exception ex )
			{
				Log.Info( $"BOUNDS_PROBE {path} ERROR={ex.GetType().Name}:{ex.Message}" );
			}
		}
		Log.Info( "BOUNDS_PROBE_DONE" );
	}
}

