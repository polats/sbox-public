using Sandbox;

namespace Local.NpcPatrol.Npcs.Tasks;

/// <summary>
/// Walk to a fixed position OR chase a moving GameObject. Re-evaluates the path
/// every <see cref="ReevaluateInterval"/> seconds when tracking an object.
/// Port of sandbox/Code/Npcs/Tasks/MoveTo.cs.
/// </summary>
public sealed class MoveToTask : TaskBase
{
	public Vector3? TargetPosition { get; set; }
	public GameObject TargetObject { get; set; }
	public float StopDistance { get; set; } = 16f;
	public float ReevaluateInterval { get; set; } = 0.4f;

	private TimeSince _lastReevaluate;

	public MoveToTask( Vector3 pos, float stopDistance = 16f )
	{
		TargetPosition = pos;
		StopDistance = stopDistance;
	}

	public MoveToTask( GameObject obj, float stopDistance = 50f )
	{
		TargetObject = obj;
		StopDistance = stopDistance;
	}

	protected override void OnStart()
	{
		var pos = ResolvePos();
		if ( !pos.HasValue ) return;
		Npc.Navigation.MoveTo( pos.Value, StopDistance );
		_lastReevaluate = 0;
	}

	protected override TaskStatus OnUpdate()
	{
		if ( TargetObject is not null && !TargetObject.IsValid() )
			return TaskStatus.Failed;

		if ( TargetObject.IsValid() && _lastReevaluate > ReevaluateInterval )
		{
			var pos = ResolvePos();
			if ( pos.HasValue ) Npc.Navigation.MoveTo( pos.Value, StopDistance );
			_lastReevaluate = 0;
		}

		// If we aren't already looking at something and we're walking sideways, rotate to face
		var agent = Npc.Navigation.Agent;
		if ( agent.IsValid() && agent.Velocity.WithZ( 0 ).Length > 1f && !Npc.Animation.LookTarget.HasValue )
		{
			var moveDir = agent.Velocity.WithZ( 0 ).Normal;
			var fwd = Npc.WorldRotation.Forward.WithZ( 0 ).Normal;
			var angle = Vector3.GetAngle( fwd, moveDir );
			if ( angle > 60f )
			{
				var targetRot = Rotation.LookAt( moveDir, Vector3.Up );
				Npc.GameObject.WorldRotation = Rotation.Lerp(
					Npc.WorldRotation, targetRot, Npc.Animation.LookSpeed * Time.Delta );
			}
		}

		return Npc.Navigation.GetStatus();
	}

	private Vector3? ResolvePos()
	{
		if ( TargetObject.IsValid() ) return TargetObject.WorldPosition;
		return TargetPosition;
	}
}
