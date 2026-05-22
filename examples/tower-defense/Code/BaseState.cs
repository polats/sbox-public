using Sandbox;
using System;

namespace Local.TowerDefense;

/// <summary>
/// Game state base — adapted from sbox-bombroyale/Code/states/BaseState.cs.
/// Override OnStateEnter/OnStateUpdate/OnStateExit. GameManager owns the
/// CurrentState reference and swaps states via SetState&lt;T&gt;().
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

/// <summary>Initial state; waiting for the player to click "Start".
/// In autoplay we skip straight into Preparing.</summary>
public sealed class MenuState : BaseState
{
	public override string Name => "Menu";

	public override void OnStateEnter()
	{
		GM.HudHint = "Click anywhere on the ground to start the first wave";
	}

	public override void OnStateUpdate()
	{
		// Player click (or autoplay) → start preparing.
		if ( GM.AutoPlay || (Input.Pressed( "Attack1" ) && GM.GetMouseGroundPos( out var _ )) )
		{
			GM.SetState<PreparingState>();
		}
	}
}

/// <summary>Player places towers; wave starts when prep timer ends or player clicks Ready.</summary>
public sealed class PreparingState : BaseState
{
	private const float PrepDuration = 5f;
	public float TimeLeft => MathF.Max( 0f, PrepDuration - TimeInState );

	public override void OnStateEnter()
	{
		GM.Wave += 1;
		GM.HudHint = $"Wave {GM.Wave} prep — click ground to place tower (50g). Starts in 5s.";
		GM.Save();
	}

	public override void OnStateUpdate()
	{
		GM.HudHint = $"Wave {GM.Wave} starting in {TimeLeft:F1}s — click ground to place tower (50g)";

		// Click → try to place tower.
		if ( Input.Pressed( "Attack1" ) && GM.GetMouseGroundPos( out var pos ) )
		{
			GM.TryPlaceTower( pos );
		}

		if ( TimeInState >= PrepDuration )
			GM.SetState<WaveActiveState>();
	}
}

/// <summary>Wave is running. Spawns creeps over a window; transitions when all dead AND no more pending.</summary>
public sealed class WaveActiveState : BaseState
{
	private const float SpawnWindow = 10f;
	private int _creepsToSpawn;
	private int _creepsSpawned;
	private float _spawnInterval;
	private float _spawnTimer;

	public override void OnStateEnter()
	{
		_creepsToSpawn = 5 + GM.Wave;
		_creepsSpawned = 0;
		_spawnInterval = SpawnWindow / _creepsToSpawn;
		_spawnTimer = 0f;
		GM.HudHint = $"Wave {GM.Wave} — fight!";
		GM.Save();
	}

	public override void OnStateUpdate()
	{
		// Spawn creeps over the window.
		if ( _creepsSpawned < _creepsToSpawn )
		{
			_spawnTimer -= Time.Delta;
			if ( _spawnTimer <= 0f )
			{
				_spawnTimer = _spawnInterval;
				GM.SpawnCreep();
				_creepsSpawned++;
			}
		}

		// During wave, can still place towers if you have gold.
		if ( Input.Pressed( "Attack1" ) && GM.GetMouseGroundPos( out var pos ) )
		{
			GM.TryPlaceTower( pos );
		}

		int alive = GM.AliveCreepCount;
		int remaining = (_creepsToSpawn - _creepsSpawned) + alive;
		GM.HudHint = $"Wave {GM.Wave} — {remaining} creep(s) remaining";

		// Wave over when all spawned + all dead.
		if ( _creepsSpawned >= _creepsToSpawn && alive == 0 )
		{
			GM.SetState<WaveCompleteState>();
		}
	}
}

/// <summary>Brief celebration state between waves.</summary>
public sealed class WaveCompleteState : BaseState
{
	private const float Duration = 2f;

	public override void OnStateEnter()
	{
		if ( GM.Wave > GM.BestWave )
			GM.BestWave = GM.Wave;

		GM.HudHint = $"Wave {GM.Wave} complete!";
		GM.Save();
	}

	public override void OnStateUpdate()
	{
		if ( TimeInState >= Duration )
		{
			if ( GM.Wave >= GameManager.MaxWaves )
				GM.SetState<VictoryState>();
			else
				GM.SetState<PreparingState>();
		}
	}
}

/// <summary>Player won — beat every wave.</summary>
public sealed class VictoryState : BaseState
{
	public override void OnStateEnter()
	{
		GM.HudHint = $"VICTORY! Beat all {GameManager.MaxWaves} waves. Click to restart.";
		GM.Save();
	}

	public override void OnStateUpdate()
	{
		if ( Input.Pressed( "Attack1" ) )
		{
			GM.ResetRun();
			GM.SetState<MenuState>();
		}
	}
}

/// <summary>Base destroyed.</summary>
public sealed class GameOverState : BaseState
{
	public override void OnStateEnter()
	{
		GM.HudHint = $"GAME OVER — Wave {GM.Wave}. Best: {GM.BestWave}. Click to retry.";
		GM.Save();
	}

	public override void OnStateUpdate()
	{
		if ( !GM.AutoPlay && Input.Pressed( "Attack1" ) )
		{
			GM.ResetRun();
			GM.SetState<MenuState>();
		}
	}
}
