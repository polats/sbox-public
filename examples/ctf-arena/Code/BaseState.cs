using Sandbox;
using System;

namespace Local.CtfArena;

/// <summary>
/// Game-state machine: Lobby → Active → Scored → Active → … → GameOver.
/// Adapted from sbox-bombroyale/Code/states/BaseState.cs and tower-defense.
/// </summary>
public abstract class BaseState
{
	public GameManager GM { get; internal set; }
	public float TimeInState { get; internal set; }
	public virtual string Name => GetType().Name;

	public virtual void OnStateEnter() { }
	public virtual void OnStateUpdate() { }
	public virtual void OnStateExit() { }
}

/// <summary>Initial state — count-down briefly then move to Active.</summary>
public sealed class LobbyState : BaseState
{
	public override string Name => "Lobby";
	private const float LobbyDuration = 2.5f;
	public float TimeLeft => MathF.Max( 0f, LobbyDuration - TimeInState );

	public override void OnStateEnter()
	{
		GM.HudHint = "Get ready — match starts in 3";
	}

	public override void OnStateUpdate()
	{
		GM.HudHint = $"Match starts in {TimeLeft:F1}s";
		if ( TimeInState >= LobbyDuration )
			GM.SetState<ActiveState>();
	}
}

/// <summary>Match is live — flags can be picked up, captures count.</summary>
public sealed class ActiveState : BaseState
{
	public override string Name => "Active";

	public override void OnStateEnter()
	{
		GM.HudHint = "Capture the enemy flag and bring it to your base!";
	}

	public override void OnStateUpdate()
	{
		// Win check
		if ( GM.RedScore >= GameManager.WinningScore )
		{
			GM.WinningTeam = Team.Red;
			GM.SetState<GameOverState>();
			return;
		}
		if ( GM.BlueScore >= GameManager.WinningScore )
		{
			GM.WinningTeam = Team.Blue;
			GM.SetState<GameOverState>();
			return;
		}
	}
}

/// <summary>Brief celebration after a capture before returning to Active.</summary>
public sealed class ScoredState : BaseState
{
	public override string Name => "Scored";
	private const float Duration = 2.0f;

	public override void OnStateEnter()
	{
		var who = GM.LastScoringTeam;
		GM.HudHint = $"{who.ToString().ToUpper()} SCORED!  RED {GM.RedScore} — {GM.BlueScore} BLUE";
	}

	public override void OnStateUpdate()
	{
		if ( TimeInState >= Duration )
			GM.SetState<ActiveState>();
	}
}

/// <summary>Someone hit the winning score.</summary>
public sealed class GameOverState : BaseState
{
	public override string Name => "GameOver";

	public override void OnStateEnter()
	{
		GM.HudHint = $"GAME OVER — {GM.WinningTeam.ToString().ToUpper()} WINS  ({GM.RedScore} — {GM.BlueScore})";
	}
}
