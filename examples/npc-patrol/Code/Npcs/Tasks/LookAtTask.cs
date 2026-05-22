using Sandbox;

namespace Local.NpcPatrol.Npcs.Tasks;

/// <summary>
/// Set a persistent look target on the AnimationLayer and wait until the body
/// has rotated to face it. Port of sandbox LookAt task.
/// </summary>
public sealed class LookAtTask : TaskBase
{
	public Vector3? TargetPosition { get; set; }
	public GameObject TargetObject { get; set; }

	public LookAtTask( Vector3 pos ) { TargetPosition = pos; }
	public LookAtTask( GameObject obj ) { TargetObject = obj; }

	protected override void OnStart()
	{
		if ( TargetObject.IsValid() ) Npc.Animation.SetLookTarget( TargetObject );
		else if ( TargetPosition.HasValue ) Npc.Animation.SetLookTarget( TargetPosition.Value );
	}

	protected override TaskStatus OnUpdate()
	{
		if ( !TargetObject.IsValid() && !TargetPosition.HasValue )
			return TaskStatus.Failed;
		return Npc.Animation.IsFacingTarget() ? TaskStatus.Success : TaskStatus.Running;
	}
}
