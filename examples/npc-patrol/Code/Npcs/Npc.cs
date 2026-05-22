using Local.NpcPatrol.Npcs.Layers;
using Sandbox;
using System;
using System.Collections.Generic;

namespace Local.NpcPatrol.Npcs;

/// <summary>
/// Base NPC component. Wires together the four service layers (Senses, Navigation,
/// Animation, Speech) and runs whatever schedule <see cref="GetSchedule"/> returns
/// each tick. Schedules are pooled in <see cref="_schedules"/> so we don't allocate
/// per state transition.
///
/// Direct port of the design in sandbox/Code/Npcs/Npc.cs + Npc.Schedule.cs +
/// Npc.Layers.cs. The actual sandbox.Npcs.Npc type isn't available to example
/// projects (it lives in the sandbox addon which is a Type=game package that we
/// can't reference) — so we re-author the equivalent classes in our own namespace.
/// The PATTERN is identical; only the namespace differs.
/// </summary>
public abstract class Npc : Component
{
	[Property] public bool ShowDebugOverlay { get; set; }
	[Property] public SkinnedModelRenderer Renderer { get; set; }
	[Property] public string DisplayName { get; set; } = "NPC";

	[RequireComponent] public SensesLayer Senses { get; set; }
	[RequireComponent] public NavigationLayer Navigation { get; set; }
	[RequireComponent] public AnimationLayer Animation { get; set; }
	[RequireComponent] public SpeechLayer Speech { get; set; }

	public ScheduleBase ActiveSchedule { get; private set; }

	private readonly Dictionary<Type, ScheduleBase> _schedules = new();

	protected override void OnStart()
	{
		GameObject.Tags.Add( "npc" );
	}

	protected override void OnUpdate()
	{
		if ( IsProxy ) return;

		TickSchedule();

		if ( ShowDebugOverlay )
			DrawDebugString();
	}

	protected override void OnDisabled() => EndCurrentSchedule();

	/// <summary>
	/// Returns the schedule the NPC should be running this tick. Subclasses override
	/// this with the equivalent of a behaviour-tree's selector node.
	/// </summary>
	public virtual ScheduleBase GetSchedule() => null;

	/// <summary>Get-or-create a pooled schedule instance.</summary>
	protected T GetSchedule<T>() where T : ScheduleBase, new()
	{
		var type = typeof( T );
		if ( !_schedules.TryGetValue( type, out var s ) )
		{
			s = new T();
			_schedules[type] = s;
		}
		return (T)s;
	}

	protected void EndCurrentSchedule()
	{
		ActiveSchedule?.InternalEnd();
		ActiveSchedule = null;
	}

	private void TickSchedule()
	{
		if ( ActiveSchedule is not null )
		{
			var status = ActiveSchedule.InternalUpdate();
			if ( status != TaskStatus.Running )
				EndCurrentSchedule();
			return;
		}

		var next = GetSchedule();
		if ( next is null ) return;

		ActiveSchedule = next;
		next.InternalInit( this );
	}

	private void DrawDebugString()
	{
		var lines = new List<string>();
		lines.Add( $"{DisplayName} [{ActiveSchedule?.GetDebugString() ?? "no schedule"}]" );
		foreach ( var layer in new BaseNpcLayer[] { Senses, Navigation, Animation, Speech } )
		{
			if ( layer is null ) continue;
			var s = layer.GetDebugString();
			if ( !string.IsNullOrEmpty( s ) ) lines.Add( s );
		}

		var text = string.Join( "\n", lines );
		DebugOverlay.Text( WorldPosition + Vector3.Up * 100f, text, color: Color.White );
	}
}
