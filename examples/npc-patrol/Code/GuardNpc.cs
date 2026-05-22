using Local.NpcPatrol.Npcs;
using Local.NpcPatrol.Npcs.Schedules;
using Sandbox;
using System.Collections.Generic;
using System.Linq;

namespace Local.NpcPatrol;

/// <summary>
/// A patrolling guard. Walks between Waypoints; transitions to Alert → Chase when
/// the player enters its sight cone. Loses interest after 3s of lost sight and
/// returns to Patrol via an Investigate side-trip.
///
/// The 3-state visualization (green/yellow/red) reads <see cref="State"/>.
/// </summary>
public sealed class GuardNpc : Npc
{
	public enum GuardState { Patrolling, Alert, Chasing, Investigating }

	[Property] public List<GameObject> Waypoints { get; set; } = new();
	[Property] public float LoseSightDelay { get; set; } = 3f;
	[Property] public float ChaseSpeed { get; set; } = 220f;

	public GuardState State { get; private set; } = GuardState.Patrolling;
	private bool _loggedInitial = false;

	private int _patrolIndex;
	private GameObject _currentTarget;
	private Vector3? _lastKnownPosition;
	private TimeSince _timeSinceLastSeen = 100f;

	protected override void OnStart()
	{
		base.OnStart();
		DisplayName = "Guard";

		// Be sure waypoints get used in scene order — sort by name for stable ordering
		Waypoints = Waypoints.Where( w => w.IsValid() ).OrderBy( w => w.Name ).ToList();
	}

	public override ScheduleBase GetSchedule()
	{
		var visible = Senses.GetNearestVisible();
		var prevState = State;

		// === target acquisition ===
		if ( visible.IsValid() )
		{
			_currentTarget = visible;
			_lastKnownPosition = visible.WorldPosition;
			_timeSinceLastSeen = 0;
		}

		// === state pick ===
		// First time we see them → Alert; subsequent ticks while still visible → Chase
		if ( visible.IsValid() )
		{
			if ( prevState == GuardState.Patrolling || prevState == GuardState.Investigating )
			{
				State = GuardState.Alert;
				LogTransition( prevState, State );
				var alert = GetSchedule<AlertSchedule>();
				alert.Target = visible;
				return alert;
			}

			// Already engaged → keep chasing
			State = GuardState.Chasing;
			if ( prevState != GuardState.Chasing ) LogTransition( prevState, State );

			var chase = GetSchedule<ChaseSchedule>();
			chase.Target = visible;
			chase.ChaseSpeed = ChaseSpeed;
			return chase;
		}

		// === lost-sight handling ===
		if ( _lastKnownPosition.HasValue && _timeSinceLastSeen < LoseSightDelay )
		{
			// Recently saw them but not now — investigate
			State = GuardState.Investigating;
			if ( prevState != GuardState.Investigating ) LogTransition( prevState, State );

			var inv = GetSchedule<InvestigateSchedule>();
			inv.LastKnownPosition = _lastKnownPosition.Value;
			return inv;
		}

		// Forget the target entirely
		if ( _lastKnownPosition.HasValue )
		{
			_lastKnownPosition = null;
			_currentTarget = null;
		}

		// === default: patrol ===
		State = GuardState.Patrolling;
		if ( prevState != GuardState.Patrolling || !_loggedInitial )
		{
			LogTransition( prevState, State );
			_loggedInitial = true;
		}

		var patrol = GetSchedule<PatrolSchedule>();
		patrol.Waypoints = Waypoints.Select( w => w.WorldPosition ).ToList();
		patrol.CurrentIndex = _patrolIndex;
		_patrolIndex = (_patrolIndex + 1) % System.Math.Max( 1, Waypoints.Count );
		return patrol;
	}

	private void LogTransition( GuardState from, GuardState to )
	{
		Log.Info( $"[GuardNpc] {DisplayName}: {from} → {to}" );
	}

	public Color StateColor => State switch
	{
		GuardState.Patrolling => new Color( 0.35f, 0.95f, 0.4f ),
		GuardState.Alert => new Color( 1f, 0.85f, 0.2f ),
		GuardState.Chasing => new Color( 1f, 0.25f, 0.25f ),
		GuardState.Investigating => new Color( 1f, 0.55f, 0.2f ),
		_ => Color.White,
	};
}
