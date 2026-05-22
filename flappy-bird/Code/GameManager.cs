using Sandbox;
using System;
using System.Linq;

namespace Local.FlappyBird;

public enum GameState
{
	Menu,
	Playing,
	GameOver,
}

public sealed class GameManager : Component
{
	[Property] public Bird Bird { get; set; }
	[Property] public PipeSpawner Spawner { get; set; }
	[Property] public bool AutoPlay { get; set; } = false;
	[Property] public float AutoMenuDelay { get; set; } = 1.2f;
	[Property] public float AutoRestartDelay { get; set; } = 1.5f;

	public GameState State { get; private set; } = GameState.Menu;
	public int Score { get; set; }
	public int HighScore { get; private set; }

	private float _stateTimer;
	private float _autoFlapTimer;

	protected override void OnStart()
	{
		base.OnStart();
		Bird ??= Scene.GetAllComponents<Bird>().FirstOrDefault();
		Spawner ??= Scene.GetAllComponents<PipeSpawner>().FirstOrDefault();
		EnterMenu();
	}

	protected override void OnUpdate()
	{
		_stateTimer += Time.Delta;
		switch ( State )
		{
			case GameState.Menu:
				if ( Input.Pressed( "Jump" ) ||
				     ( AutoPlay && _stateTimer >= AutoMenuDelay ) )
					StartGame();
				break;
			case GameState.Playing:
				if ( AutoPlay && Bird is not null )
				{
					_autoFlapTimer -= Time.Delta;
					var targetZ = PredictTargetZ();
					var needFlap = Bird.VelocityZ < 50f &&
					               Bird.WorldPosition.z < targetZ;
					if ( needFlap && _autoFlapTimer <= 0f )
					{
						Bird.Flap();
						_autoFlapTimer = 0.18f;
					}
				}
				break;
			case GameState.GameOver:
				if ( Input.Pressed( "Reload" ) ||
				     ( AutoPlay && _stateTimer >= AutoRestartDelay ) )
					EnterMenu();
				break;
		}
	}

	private float PredictTargetZ()
	{
		var pipes = Scene.GetAllComponents<Pipe>()
			.Where( p => p.WorldPosition.x > Bird.WorldPosition.x - 80f )
			.OrderBy( p => p.WorldPosition.x )
			.FirstOrDefault();
		if ( pipes is null ) return 320f;
		return ( pipes.GapTop + pipes.GapBottom ) * 0.5f;
	}

	private void EnterMenu()
	{
		State = GameState.Menu;
		Score = 0;
		_stateTimer = 0f;
		Spawner?.Reset();
		if ( Bird is not null )
		{
			Bird.ResetToStart();
			Bird.IsFrozen = true;
		}
	}

	private void StartGame()
	{
		State = GameState.Playing;
		Score = 0;
		_stateTimer = 0f;
		_autoFlapTimer = 0f;
		Spawner?.Reset();
		if ( Bird is not null )
		{
			Bird.ResetToStart();
			Bird.IsFrozen = false;
			Bird.Flap();
		}
	}

	public void GameOver()
	{
		if ( State != GameState.Playing ) return;
		State = GameState.GameOver;
		_stateTimer = 0f;
		if ( Score > HighScore ) HighScore = Score;
		if ( Bird is not null ) Bird.IsFrozen = true;
	}

	public void AddScore()
	{
		Score++;
	}
}
