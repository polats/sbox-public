using Sandbox;
using System;

namespace Local.SkillHelper;

/// <summary>
/// Skill helper Component injected by tools/sbox-screenshot (and sbox-record).
/// In play mode, fires `screenshot_highres W H` (or starts/stops video
/// recording for Duration seconds) and logs the request so the tool can
/// match the result file.
///
/// Logs:
///   SCREENSHOT_REQUEST WxH       (right before triggering)
///   SCREENSHOT_REQUEST_DONE
/// or:
///   VIDEO_REQUEST_START dur=Ns
///   VIDEO_REQUEST_STOP
/// </summary>
public sealed class Capture : Component
{
	public enum CaptureMode { Screenshot, Video }

	[Property] public CaptureMode Mode { get; set; } = CaptureMode.Screenshot;
	[Property] public int Width { get; set; } = 1280;
	[Property] public int Height { get; set; } = 720;
	[Property] public float DelaySeconds { get; set; } = 0.5f;
	[Property] public float VideoDurationSeconds { get; set; } = 5f;

	private TimeSince _timeSinceStart;
	private bool _kickedOff;
	private bool _stoppedVideo;

	protected override void OnStart()
	{
		base.OnStart();
		_timeSinceStart = 0f;
		_kickedOff = false;
		_stoppedVideo = false;
	}

	protected override void OnUpdate()
	{
		if ( !_kickedOff && _timeSinceStart >= DelaySeconds )
		{
			_kickedOff = true;
			Trigger();
		}

		if ( Mode == CaptureMode.Video && _kickedOff && !_stoppedVideo
			 && _timeSinceStart >= DelaySeconds + VideoDurationSeconds )
		{
			_stoppedVideo = true;
			Log.Info( "VIDEO_REQUEST_STOP" );
			ConsoleSystem.Run( "video" );
		}
	}

	private void Trigger()
	{
		if ( Mode == CaptureMode.Screenshot )
		{
			Log.Info( $"SCREENSHOT_REQUEST {Width}x{Height}" );
			ConsoleSystem.Run( $"screenshot_highres {Width} {Height}" );
			Log.Info( "SCREENSHOT_REQUEST_DONE" );
		}
		else
		{
			Log.Info( $"VIDEO_REQUEST_START dur={VideoDurationSeconds:F1}s" );
			ConsoleSystem.Run( "video" );
		}
	}
}
