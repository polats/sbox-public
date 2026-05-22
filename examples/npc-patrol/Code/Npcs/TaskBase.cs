using System;
namespace Local.NpcPatrol.Npcs;

/// <summary>
/// A single behavior-tree leaf task. The ScheduleBase runs tasks in sequence.
/// Mirrors sandbox/Code/Npcs/TaskBase.cs (the canonical NPC framework).
/// </summary>
public abstract class TaskBase
{
	protected ScheduleBase Schedule { get; private set; }
	protected Npc Npc => Schedule.Npc;

	protected TaskStatus Status { get; private set; }

	internal void Initialize( ScheduleBase schedule )
	{
		Schedule = schedule;
		Status = TaskStatus.Running;
		OnStart();
	}

	internal TaskStatus InternalUpdate()
	{
		Status = OnUpdate();
		return Status;
	}

	internal void InternalEnd()
	{
		if ( Status == TaskStatus.Running )
			Status = TaskStatus.Success;
		OnEnd();
	}

	protected virtual void OnStart() { }
	protected abstract TaskStatus OnUpdate();
	protected virtual void OnEnd() { }
}
