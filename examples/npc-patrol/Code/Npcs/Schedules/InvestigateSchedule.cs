using Local.NpcPatrol.Npcs.Tasks;
using Sandbox;

namespace Local.NpcPatrol.Npcs.Schedules;

/// <summary>
/// Walk to the last-known position of the lost target, look around, then return
/// to patrol. The GuardNpc clears its last-known-pos when this schedule ends.
/// </summary>
public sealed class InvestigateSchedule : ScheduleBase
{
	public Vector3 LastKnownPosition { get; set; }

	protected override void OnStart()
	{
		Npc.Navigation.WishSpeed = 150f;

		var target = LastKnownPosition;
		var snapped = Npc.Scene.NavMesh?.GetClosestPoint( target, 200f );
		if ( snapped.HasValue ) target = snapped.Value;

		AddTask( new MoveToTask( target, 30f ) );
		AddTask( new SayTask( "Hmm, must've been the wind...", 2.5f ) );
		AddTask( new WaitTask( 1.5f ) );
	}

	protected override bool ShouldCancel()
	{
		// Re-acquired the target → bail so chase can resume
		return Npc.Senses.GetNearestVisible().IsValid();
	}

	protected override void OnEnd()
	{
		Npc.Animation.ClearLookTarget();
	}
}
