using Local.NpcPatrol.Npcs.Tasks;
using Sandbox;
using System.Collections.Generic;

namespace Local.NpcPatrol.Npcs.Schedules;

/// <summary>
/// Walk to the next waypoint, idle for a beat, then advance. The schedule cancels
/// itself the instant senses report a visible target — the NPC's GetSchedule()
/// then picks AlertSchedule instead.
///
/// Same shape as sandbox/Code/Npcs/Combat/CombatPatrolSchedule.cs.
/// </summary>
public sealed class PatrolSchedule : ScheduleBase
{
	public List<Vector3> Waypoints { get; set; } = new();
	public int CurrentIndex { get; set; } = 0;
	public float WaitAtWaypoint { get; set; } = 2f;

	protected override void OnStart()
	{
		if ( Waypoints.Count == 0 ) return;

		Npc.Navigation.WishSpeed = 110f; // walk speed

		var target = Waypoints[CurrentIndex];

		// Snap to a navmesh-valid position
		var snapped = Npc.Scene.NavMesh?.GetClosestPoint( target, 200f );
		if ( snapped.HasValue ) target = snapped.Value;

		AddTask( new MoveToTask( target, 24f ) );
		AddTask( new WaitTask( WaitAtWaypoint ) );

		// Advance for next time this schedule runs
		CurrentIndex = (CurrentIndex + 1) % Waypoints.Count;
	}

	protected override bool ShouldCancel()
	{
		// Cancel as soon as we see a hostile — let the parent NPC pick an alert/chase schedule
		return Npc.Senses.GetNearestVisible().IsValid();
	}
}
