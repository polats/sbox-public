using Editor;
using Sandbox;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace Local.CoinRush.EditorTools;

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
///   <file content = "screenshot WxH">         → screenshot_highres W H
///   <file content = "video start">            → video
///   <file content = "video stop">             → video
///   <file content = "cmd <raw console cmd>">  → run that command verbatim
///   <file content = "probe-models">           → read trigger.json's "paths"
///                                              and log BOUNDS_PROBE entries
///
/// Result location (for tools to find):
///   The engine logs the result path itself ("Screenshot saved to: …" or
///   "Video recording finished: …"). The tool watches the editor log for
///   those lines; this helper just kicks off the work.
/// </summary>
public static class SkillTrigger
{
	private const string TriggerSubdir = ".sbox/skill-triggers";

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
				{
					ProbeModels( file );
					break;
				}
			default:
				Log.Warning( $"[SkillTrigger] unknown trigger: {parts[0]}" );
				break;
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

