using Local.NpcPatrol.Npcs.Tasks;
using Sandbox;

namespace Local.NpcPatrol.Npcs.Schedules;

/// <summary>
/// Brief alert state — stop, look at the threat, shout "Hey you!", then yield so
/// the NPC's GetSchedule() can pick the ChaseSchedule (which it will, as long as
/// the target is still visible).
/// </summary>
public sealed class AlertSchedule : ScheduleBase
{
	public GameObject Target { get; set; }
	public string AlertLine { get; set; } = "Hey you! Stop right there!";

	protected override void OnStart()
	{
		Npc.Navigation.Stop();
		Npc.Navigation.WishSpeed = 0f;

		if ( Target.IsValid() )
		{
			AddTask( new LookAtTask( Target ) );
		}

		AddTask( new SayTask( AlertLine, 2f ) );
		AddTask( new WaitTask( 0.6f ) );
	}

	protected override void OnEnd()
	{
		// Don't clear look target — chase will pick it up.
	}
}
