using Sandbox;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Local.MpErgonomics;

/// <summary>
/// Drives the demo: holds the kill-feed and chat-history queues, rotates the
/// "is talking" flag among fake players to flash the voice indicators, and
/// when <see cref="AutoPlay"/> is true, scripts the camera/UI sequence used
/// to record the verification video.
/// </summary>
public sealed class GameManager : Component
{
	public static GameManager Instance { get; private set; }

	[Property] public bool AutoPlay { get; set; } = false;

	// Kill feed
	public class KillEntry
	{
		public string Attacker;
		public string Victim;
		public long AttackerSteamId;
		public bool Headshot;
		public bool AttackerIsMe;
		public RealTimeSince SinceAdded;
	}
	public List<KillEntry> KillFeed { get; } = new();

	// Chat
	public class ChatEntry
	{
		public string Author;
		public string Message;
		public Color Color;
		public RealTimeSince SinceAdded;
	}
	public List<ChatEntry> Chat { get; } = new();

	// Tab scoreboard
	public bool ScoreboardOpen { get; set; }

	// Chat input
	public bool ChatOpen { get; set; }
	public string ChatDraft { get; set; } = "";

	private TimeSince _sinceKill;
	private TimeSince _sinceVoiceRotate;
	private int _voiceIdx;
	private TimeSince _sinceAuto;
	private int _autoSegment = -1;

	private static readonly string[] DemoChatBootstrap = new[]
	{
		"Alice|Anyone wanna queue up?",
		"Bob|gg lobby vibes",
		"Carol|new map looks insane",
	};

	private static readonly string[] DemoChatAuto = new[]
	{
		"Dave|nice scoreboard view",
		"Alice|you on the friends list?",
		"Bob|kill feed working too lol",
	};

	protected override void OnAwake()
	{
		Instance = this;
	}

	protected override void OnStart()
	{
		// Pre-seed three chat messages from named fake players.
		foreach ( var line in DemoChatBootstrap )
		{
			var parts = line.Split( '|' );
			AddChat( parts[0], parts[1], Color.White );
		}

		_sinceKill = -2f;
		_sinceVoiceRotate = 0f;
		_sinceAuto = 0f;
	}

	protected override void OnUpdate()
	{
		// Cycle who's "talking" every ~2s for the voice indicator demo.
		if ( _sinceVoiceRotate > 2f )
		{
			_sinceVoiceRotate = 0f;
			var players = Scene.GetAllComponents<FakePlayer>().Where( p => !p.IsLocal ).ToList();
			foreach ( var p in players ) p.IsTalking = false;
			if ( players.Count > 0 )
			{
				_voiceIdx = (_voiceIdx + 1) % players.Count;
				players[_voiceIdx].IsTalking = true;
			}
		}

		// Tab toggles the scoreboard (held).
		ScoreboardOpen = Input.Down( "Score" );

		// Auto-fire fake kills every 4 seconds.
		if ( _sinceKill > 4f )
		{
			_sinceKill = 0f;
			TriggerRandomKill();
		}

		// Prune old feed/chat entries.
		KillFeed.RemoveAll( e => e.SinceAdded > 5f );
		Chat.RemoveAll( e => e.SinceAdded > 15f );

		if ( AutoPlay )
		{
			TickAutoplay();
		}
	}

	private void TickAutoplay()
	{
		float t = _sinceAuto;
		int seg = -1;
		if ( t < 3f ) seg = 0;       // scoreboard
		else if ( t < 6f ) seg = 1;  // chat
		else if ( t < 10f ) seg = 2; // kills
		else if ( t < 16f ) seg = 3; // voice
		else seg = 0; // loop

		// Scoreboard: hold tab down
		ScoreboardOpen = (seg == 0 || seg == 3);

		if ( seg != _autoSegment )
		{
			_autoSegment = seg;
			if ( seg == 1 )
			{
				// fire one chat message from a fake player
				var idx = (_sinceAuto.Relative.FloorToInt()) % DemoChatAuto.Length;
				var line = DemoChatAuto[Math.Abs( idx )];
				var parts = line.Split( '|' );
				AddChat( parts[0], parts[1], Color.Cyan );
			}
			else if ( seg == 2 )
			{
				TriggerRandomKill();
				TriggerRandomKill();
			}
		}

		if ( t > 16f ) _sinceAuto = 0f;
	}

	public void TriggerRandomKill()
	{
		var players = Scene.GetAllComponents<FakePlayer>().ToList();
		if ( players.Count < 2 ) return;

		var rng = new Random();
		var a = players[rng.Next( players.Count )];
		FakePlayer v;
		int tries = 0;
		do { v = players[rng.Next( players.Count )]; tries++; }
		while ( v == a && tries < 5 );
		if ( v == a ) return;

		bool headshot = rng.Next( 4 ) == 0;
		NotifyKill( v.PlayerName, a.PlayerName, a.FakeSteamId, headshot, a.IsLocal );

		a.Score += 1;
		v.Deaths += 1;
	}

	/// <summary>
	/// Same shape as <c>Sandbox.Feed.NotifyKill</c> so a real multiplayer
	/// game can swap in the canonical UI with no code changes.
	/// </summary>
	public void NotifyKill( string victimName, string attackerName, long attackerSteamId, bool headshot, bool attackerIsMe )
	{
		KillFeed.Add( new KillEntry
		{
			Attacker = attackerName,
			Victim = victimName,
			AttackerSteamId = attackerSteamId,
			Headshot = headshot,
			AttackerIsMe = attackerIsMe,
			SinceAdded = 0f,
		} );
		while ( KillFeed.Count > 6 ) KillFeed.RemoveAt( 0 );
	}

	public void AddChat( string author, string message, Color color )
	{
		Chat.Add( new ChatEntry
		{
			Author = author,
			Message = message,
			Color = color,
			SinceAdded = 0f,
		} );
		while ( Chat.Count > 5 ) Chat.RemoveAt( 0 );
	}
}
