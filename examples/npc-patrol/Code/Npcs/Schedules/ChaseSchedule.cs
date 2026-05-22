using Local.NpcPatrol.Npcs.Tasks;
using Sandbox;

namespace Local.NpcPatrol.Npcs.Schedules;

/// <summary>
/// Run at the target until it's caught (stop within CatchDistance) or we lose sight.
/// "Lose sight" is handled at the Npc level — if Senses.VisibleTargets goes empty,
/// the Npc transitions to InvestigateSchedule.
/// </summary>
public sealed class ChaseSchedule : ScheduleBase
{
	public GameObject Target { get; set; }
	public float ChaseSpeed { get; set; } = 220f;
	public float CatchDistance { get; set; } = 60f;

	protected override void OnStart()
	{
		if ( !Target.IsValid() ) return;

		Npc.Navigation.WishSpeed = ChaseSpeed;
		Npc.Animation.SetLookTarget( Target );

		AddTask( new MoveToTask( Target, CatchDistance ) );
	}

	protected override bool ShouldCancel()
	{
		// Lost the target's GameObject entirely
		if ( !Target.IsValid() ) return true;
		return false;
	}

	protected override void OnEnd()
	{
		Npc.Navigation.WishSpeed = 110f;
	}
}
