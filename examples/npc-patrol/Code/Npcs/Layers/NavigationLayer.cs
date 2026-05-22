using Sandbox;

namespace Local.NpcPatrol.Npcs.Layers;

/// <summary>
/// Wraps the <see cref="NavMeshAgent"/> so tasks can issue MoveTo commands and query
/// progress without poking at the agent directly. Drives the citizen animgraph each
/// frame from the agent's velocity (delegated to <see cref="AnimationLayer.SetMove"/>).
///
/// Direct port of sandbox/Code/Npcs/Layers/NavigationLayer.cs.
/// </summary>
public sealed class NavigationLayer : BaseNpcLayer
{
	public NavMeshAgent Agent { get; private set; }
	public Vector3? MoveTarget { get; private set; }

	[Property] public float StopDistance { get; set; } = 16f;

	/// <summary>Desired movement speed. Schedules can raise this to make the NPC run.</summary>
	public float WishSpeed { get; set; } = 110f;

	protected override void OnStart()
	{
		Agent = Npc.GetComponent<NavMeshAgent>();
	}

	public void MoveTo( Vector3 target, float stopDistance = 16f )
	{
		MoveTarget = target;
		StopDistance = stopDistance;

		if ( Agent.IsValid() )
		{
			Agent.MoveTo( target );
			if ( Agent.TargetPosition.HasValue )
				MoveTarget = Agent.TargetPosition.Value;
		}
	}

	public void Stop()
	{
		MoveTarget = null;
		if ( Agent.IsValid() ) Agent.Stop();
	}

	protected override void OnUpdate()
	{
		if ( IsProxy ) return;

		if ( Agent.IsValid() )
		{
			Agent.MaxSpeed = WishSpeed;
			if ( Npc.Animation.IsValid() )
				Npc.Animation.SetMove( Agent.Velocity, Agent.WorldRotation );
		}
	}

	public override string GetDebugString()
	{
		if ( !MoveTarget.HasValue ) return null;
		var dist = Npc.WorldPosition.Distance( MoveTarget.Value ).CeilToInt();
		return $"Nav: {GetStatus()} ({dist}u)";
	}

	public TaskStatus GetStatus()
	{
		if ( !MoveTarget.HasValue ) return TaskStatus.Success;
		var distance = Npc.WorldPosition.Distance( MoveTarget.Value );
		if ( distance <= StopDistance ) return TaskStatus.Success;
		if ( Agent.IsValid() && !Agent.IsNavigating ) return TaskStatus.Failed;
		return TaskStatus.Running;
	}

	public override void ResetLayer()
	{
		MoveTarget = null;
		if ( Agent.IsValid() ) Agent.Stop();
	}
}
