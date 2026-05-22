namespace Local.NpcPatrol.Npcs;

/// <summary>
/// Behavior-tree style task result. Ported from sandbox/Code/Npcs/TaskStatus.cs.
/// </summary>
public enum TaskStatus
{
	Running,
	Success,
	Failed,
	Interrupted,
}
