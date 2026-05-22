using Sandbox;

namespace Local.NpcPatrol.Npcs.Tasks;

/// <summary>Pause for a fixed duration. Port of sandbox Wait task.</summary>
public sealed class WaitTask : TaskBase
{
	public float Duration { get; set; }
	private TimeUntil _end;

	public WaitTask( float duration ) { Duration = duration; }

	protected override void OnStart() { _end = Duration; }
	protected override TaskStatus OnUpdate() => _end ? TaskStatus.Success : TaskStatus.Running;
}
