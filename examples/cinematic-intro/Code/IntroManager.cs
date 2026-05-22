using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Sandbox;
using Sandbox.MovieMaker;
using Sandbox.MovieMaker.Compiled;

namespace Local.CinematicIntro;

/// <summary>
/// Orchestrates the cinematic intro: builds a MovieClip in code that animates the
/// camera Transform over ~12 seconds, plays it via Sandbox.MovieMaker.MoviePlayer,
/// and (when AutoPlay) demos the language switch every 6 seconds.
/// </summary>
public sealed class IntroManager : Component
{
	/// <summary> When true, the cinematic auto-starts and language toggles
	/// every 6s for video recording. Default false so the project is playable. </summary>
	[Property] public bool AutoPlay { get; set; } = false;

	[Property] public GameObject CameraObject { get; set; }
	[Property] public MoviePlayer Player { get; set; }

	/// <summary> Total movie length. </summary>
	[Property] public float Duration { get; set; } = 12f;

	private float _autoTimer;
	private bool _started;

	protected override void OnStart()
	{
		if ( Player is null || CameraObject is null )
		{
			Log.Warning( "[IntroManager] Missing CameraObject or MoviePlayer reference" );
			return;
		}

		BuildAndAssignClip();

		if ( AutoPlay )
		{
			Player.Play();
			_started = true;
		}
	}

	protected override void OnUpdate()
	{
		// SPACE starts (just shows it works) — actual "game start" is out of scope.
		if ( Input.Pressed( "jump" ) && !_started )
		{
			Player?.Play();
			_started = true;
		}

		// [L] toggles language manually.
		if ( Input.Pressed( "use" ) )
		{
			ToggleLanguage();
		}

		if ( AutoPlay )
		{
			_autoTimer += Time.Delta;
			if ( _autoTimer >= 6f )
			{
				_autoTimer = 0f;
				ToggleLanguage();
			}
		}
	}

	public static void ToggleLanguage()
	{
		Loc.Set( Loc.Current == "es" ? "en" : "es" );
	}

	public static void SetLanguage( string code )
	{
		Loc.Set( code );
	}

	/// <summary>
	/// Build an in-memory MovieClip that animates the camera transform across
	/// `Duration` seconds via a sampled Transform track. This uses the real
	/// Sandbox.MovieMaker compiled API (MovieClip / CompiledReferenceTrack /
	/// CompiledPropertyTrack / CompiledSampleBlock).
	/// </summary>
	private void BuildAndAssignClip()
	{
		// 3 keyframe transforms (start → mid → end) — we sample between them.
		// Camera starts off to the right, looking left, glides past the props,
		// ends up looking down the line of props from the far side.

		var kf = new (Vector3 pos, Angles ang)[]
		{
			( new Vector3( -260, -340,  90 ), new Angles(  8,  35, 0 ) ),
			( new Vector3(    0, -260, 100 ), new Angles( 12,  80, 0 ) ),
			( new Vector3(  280, -300, 110 ), new Angles( 10, 115, 0 ) ),
		};

		// Sample at 30 Hz for the full duration.
		const int sampleRate = 30;
		var totalSamples = (int)Math.Ceiling( Duration * sampleRate ) + 1;
		var positions = new Vector3[totalSamples];
		var rotations = new Rotation[totalSamples];

		for ( int i = 0; i < totalSamples; i++ )
		{
			float t = i / (float)(totalSamples - 1);  // 0..1
			float seg = t * (kf.Length - 1);
			int i0 = Math.Min( (int)seg, kf.Length - 2 );
			float u = seg - i0;
			// Smoothstep for ease in/out
			u = u * u * (3f - 2f * u);
			positions[i] = Vector3.Lerp( kf[i0].pos, kf[i0 + 1].pos, u );
			rotations[i] = Rotation.Lerp( kf[i0].ang.ToRotation(), kf[i0 + 1].ang.ToRotation(), u );
		}

		var timeRange = new MovieTimeRange( MovieTime.Zero, MovieTime.FromSeconds( Duration ) );

		// GameObject reference track + LocalPosition / LocalRotation property tracks
		// (the same shape the editor's Movie Maker records).
		var cameraGoTrack = MovieClip.RootGameObject( CameraObject.Name, id: Guid.NewGuid() );

		var posTrack = cameraGoTrack
			.Property<Vector3>( nameof( GameObject.LocalPosition ) )
			.WithSamples( timeRange, sampleRate, positions );

		var rotTrack = cameraGoTrack
			.Property<Rotation>( nameof( GameObject.LocalRotation ) )
			.WithSamples( timeRange, sampleRate, rotations );

		var clip = MovieClip.FromTracks( cameraGoTrack, posTrack, rotTrack );

		// Bind the reference track to our camera GameObject.
		Player.Binder.Add( cameraGoTrack, CameraObject );

		Player.Clip = clip;
		Player.IsLooping = false;
		Player.IsPlaying = false;
		Player.CreateTargets = false;  // we've bound it ourselves; don't spawn dupes
	}
}
