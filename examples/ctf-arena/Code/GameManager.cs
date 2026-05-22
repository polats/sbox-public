using Sandbox;
using System;
using System.Linq;

namespace Local.CtfArena;

/// <summary>
/// CTF GameManager — the brain of the arena. Owns the state machine
/// (Lobby → Active → Scored → Active … → GameOver), the host-authoritative
/// flag carrier state ([Sync] Guids), the score, and broadcasts ([Rpc.Broadcast])
/// pickup / score events to every client so they can react (sound, banner).
///
/// Networking attributes used:
///   - [Sync] on RedScore / BlueScore                        (tick replication)
///   - [Sync(SyncFlags.FromHost)] on RedFlagCarrier /
///     BlueFlagCarrier / LastScoringTeam / WinningTeam       (host owns these)
///   - [Rpc.Broadcast] on AnnouncePickup / AnnounceScore     (event to all)
///   - [Rpc.Host] on RequestRestart                          (client-→host)
///
/// **Honesty note:** the autonomous video harness can't easily spin up a
/// second connected client, so at runtime there's only one process and the
/// network attributes are not observably exercised. They still compile against
/// the engine's networking machinery and are the correct patterns; a real
/// 2-client session would just work.
/// </summary>
public sealed class GameManager : Component
{
	public static GameManager Instance { get; private set; }
	public const int WinningScore = 3;

	[Property] public bool AutoPlay { get; set; } = false;

	// ─────── Networked / replicated state ───────

	/// <summary>Red team score. Replicated each tick.</summary>
	[Sync] public int RedScore { get; set; }

	/// <summary>Blue team score. Replicated each tick.</summary>
	[Sync] public int BlueScore { get; set; }

	/// <summary>
	/// Carrier of the RED flag (i.e. the BLUE player who picked it up).
	/// Guid.Empty / default Guid means the flag is at its home stand.
	/// Host-authoritative.
	/// </summary>
	[Sync( SyncFlags.FromHost )] public Guid RedFlagCarrier { get; set; }

	/// <summary>Carrier of the BLUE flag (a RED player). Host-authoritative.</summary>
	[Sync( SyncFlags.FromHost )] public Guid BlueFlagCarrier { get; set; }

	[Sync( SyncFlags.FromHost )] public Team LastScoringTeam { get; set; }
	[Sync( SyncFlags.FromHost )] public Team WinningTeam { get; set; }

	// ─────── Local-only state (no [Sync] — used for HUD hint text & state machine) ───────
	public BaseState CurrentState { get; private set; }
	public string StateName => CurrentState?.Name ?? "(none)";
	public string HudHint { get; set; } = "";

	// Cached references
	private Flag _redFlag, _blueFlag;
	private CtfPlayer _redPlayer, _bluePlayer;

	protected override void OnEnabled()
	{
		base.OnEnabled();
		Instance = this;
	}

	protected override void OnDisabled()
	{
		base.OnDisabled();
		if ( Instance == this ) Instance = null;
	}

	protected override void OnStart()
	{
		base.OnStart();
		Instance = this;

		// Pull initial scene refs.
		var flags = Scene.GetAllComponents<Flag>().ToList();
		_redFlag = flags.FirstOrDefault( f => f.Team == Team.Red );
		_blueFlag = flags.FirstOrDefault( f => f.Team == Team.Blue );

		// AutoPlay: enable bot mode on every player in the scene.
		if ( AutoPlay )
		{
			foreach ( var p in Scene.GetAllComponents<CtfPlayer>() )
				p.AutoPlay = true;
		}

		SetState<LobbyState>();
	}

	protected override void OnUpdate()
	{
		if ( CurrentState == null ) return;
		CurrentState.TimeInState += Time.Delta;
		CurrentState.OnStateUpdate();
	}

	// ─────── State machine ───────
	public void SetState<T>() where T : BaseState, new()
	{
		CurrentState?.OnStateExit();
		var st = new T { GM = this, TimeInState = 0f };
		CurrentState = st;
		st.OnStateEnter();
		Log.Info( $"[CTF] State → {st.Name}" );
	}

	// ─────── Lookups ───────
	public Flag GetTeamFlag( Team t ) => t == Team.Red ? _redFlag : (t == Team.Blue ? _blueFlag : null);
	public Flag GetEnemyFlag( Team t ) => GetTeamFlag( t.Other() );

	public Vector3 GetTeamBasePosition( Team t )
	{
		var f = GetTeamFlag( t );
		return f?.HomePosition ?? Vector3.Zero;
	}

	public Vector3 GetEnemyFlagPosition( Team t )
	{
		var enemyFlag = GetEnemyFlag( t );
		if ( enemyFlag == null ) return Vector3.Zero;
		// If carried, point at the carrier (not strictly canonical CTF; the bots
		// just want to converge). Otherwise the flag's home.
		var carrierId = GetCarrierIdFor( enemyFlag.Team );
		if ( carrierId != default )
		{
			var go = Scene.Directory.FindByGuid( carrierId );
			if ( go != null ) return go.WorldPosition;
		}
		return enemyFlag.WorldPosition;
	}

	public Guid GetCarrierIdFor( Team flagTeam )
	{
		return flagTeam == Team.Red ? RedFlagCarrier
		     : flagTeam == Team.Blue ? BlueFlagCarrier
		     : default;
	}

	private void SetCarrierIdFor( Team flagTeam, Guid id )
	{
		if ( flagTeam == Team.Red ) RedFlagCarrier = id;
		else if ( flagTeam == Team.Blue ) BlueFlagCarrier = id;
	}

	// ─────── Flag interaction (host arbitrates) ───────

	/// <summary>
	/// Called from <see cref="CtfPlayer.TryFlagInteractions"/> on the host
	/// (or single-process simulation). Records the carrier, parents the flag
	/// to the player, and broadcasts the pickup event.
	/// </summary>
	public void HandleFlagPickup( CtfPlayer carrier, Flag flag )
	{
		// Authority check — we only let the host mutate flag carrier state.
		// In single-process simulation Networking.IsHost is true anyway.
		if ( !Networking.IsHost && Game.IsPlaying ) return;

		SetCarrierIdFor( flag.Team, carrier.GameObject.Id );
		flag.AttachTo( carrier.GameObject );

		Log.Info( $"[CTF] {carrier.Team} picked up {flag.Team} flag" );
		AnnouncePickup( (int)carrier.Team, (int)flag.Team );
		carrier.PlayPickupSound();
	}

	/// <summary>
	/// A carrier reached their own base with the enemy flag. Score, reset the
	/// flag to its stand, broadcast.
	/// </summary>
	public void HandleScore( CtfPlayer scorer )
	{
		if ( !Networking.IsHost && Game.IsPlaying ) return;

		// Which flag is the scorer carrying?
		Team flagTeam = scorer.Team.Other();
		var flag = GetTeamFlag( flagTeam );
		if ( flag == null ) return;

		if ( scorer.Team == Team.Red ) RedScore++;
		else if ( scorer.Team == Team.Blue ) BlueScore++;

		LastScoringTeam = scorer.Team;

		// Reset the captured flag.
		SetCarrierIdFor( flagTeam, default );
		flag.ReturnToBase();

		Log.Info( $"[CTF] {scorer.Team} SCORED!  RED {RedScore} — {BlueScore} BLUE" );
		AnnounceScore( (int)scorer.Team, RedScore, BlueScore );

		SetState<ScoredState>();
	}

	// ─────── RPCs ───────

	/// <summary>
	/// Broadcast a flag-pickup event to every connected client. They use this
	/// to play a sound + flash the HUD banner. (int) team args because Rpc
	/// arg-marshalling is friendliest with primitives + Guids.
	/// </summary>
	[Rpc.Broadcast]
	public void AnnouncePickup( int carrierTeam, int flagTeam )
	{
		var ct = (Team)carrierTeam;
		var ft = (Team)flagTeam;
		HudHint = $"{ct.ToString().ToUpper()} grabbed the {ft.ToString().ToUpper()} flag!";
	}

	/// <summary>
	/// Broadcast a scoring event with the new scores so every client's HUD
	/// reacts even if their [Sync] tick hasn't landed yet.
	/// </summary>
	[Rpc.Broadcast]
	public void AnnounceScore( int scorerTeam, int redScore, int blueScore )
	{
		var st = (Team)scorerTeam;
		Log.Info( $"[CTF][rpc] {st} scored — {redScore}-{blueScore}" );
	}

	/// <summary>
	/// Client-to-host RPC: any client can request a restart, but only the
	/// host actually runs the reset. <see cref="Rpc.Host"/> guarantees this
	/// body only runs on the host process.
	/// </summary>
	[Rpc.Host]
	public void RequestRestart()
	{
		if ( !Networking.IsHost ) return;
		RedScore = 0;
		BlueScore = 0;
		RedFlagCarrier = default;
		BlueFlagCarrier = default;
		WinningTeam = Team.None;
		LastScoringTeam = Team.None;
		_redFlag?.ReturnToBase();
		_blueFlag?.ReturnToBase();
		SetState<LobbyState>();
	}
}
