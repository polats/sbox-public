using System;
using System.Collections.Generic;
using Sandbox;

namespace Local.NpcPatrol.Npcs;

/// <summary>
/// A schedule executes a sequence of <see cref="TaskBase"/> instances.
/// Subclasses set up the task list in <see cref="OnStart"/> and may interrupt
/// themselves by returning true from <see cref="ShouldCancel"/>.
///
/// Direct port of the design in sandbox/Code/Npcs/ScheduleBase.cs.
/// </summary>
public abstract class ScheduleBase
{
	public Npc Npc { get; private set; }
	protected GameObject GameObject => Npc.GameObject;

	private readonly List<TaskBase> _tasks = new();
	private int _currentTaskIndex = 0;

	internal void InternalInit( Npc npc )
	{
		Npc = npc;
		_tasks.Clear();
		_currentTaskIndex = 0;

		OnStart();
		StartCurrentTask();
	}

	internal TaskStatus InternalUpdate()
	{
		if ( _tasks.Count == 0 ) return TaskStatus.Failed;
		if ( _currentTaskIndex >= _tasks.Count ) return TaskStatus.Success;

		if ( ShouldCancel() )
		{
			OnCancelled();
			return TaskStatus.Interrupted;
		}

		var task = _tasks[_currentTaskIndex];
		var status = task.InternalUpdate();

		if ( status is not TaskStatus.Running )
		{
			task.InternalEnd();
			if ( status is TaskStatus.Success )
			{
				_currentTaskIndex++;
				StartCurrentTask();
				return TaskStatus.Running;
			}
			return status;
		}

		return TaskStatus.Running;
	}

	internal void InternalEnd()
	{
		if ( _currentTaskIndex < _tasks.Count )
			_tasks[_currentTaskIndex].InternalEnd();
		_currentTaskIndex = 0;
		OnEnd();
	}

	protected virtual void OnStart() { }
	protected virtual void OnEnd() { }
	protected virtual bool ShouldCancel() => false;
	protected virtual void OnCancelled() { }

	protected void AddTask( TaskBase task ) => _tasks.Add( task );

	private void StartCurrentTask()
	{
		if ( _currentTaskIndex < _tasks.Count )
			_tasks[_currentTaskIndex].Initialize( this );
	}

	public string GetDebugString()
	{
		if ( _currentTaskIndex >= _tasks.Count )
			return $"{GetType().Name}/(none)";
		return $"{GetType().Name}/{_tasks[_currentTaskIndex].GetType().Name}";
	}
}
