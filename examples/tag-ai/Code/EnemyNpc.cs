using Sandbox;
using System;
using System.Linq;

namespace Local.TagAi;

/// <summary>
/// A chase NPC that pathfinds toward the player via NavMeshAgent.
/// Simple two-state machine: Chasing -> Frozen. When tagged it stops the
/// agent, stops animating, and tints itself blue.
/// </summary>
public sealed class EnemyNpc : Component
{
	[Property] public float ChaseSpeed { get; set; } = 80f;
	[Property] public float AgentRadius { get; set; } = 16f;
	[Property] public float AgentAcceleration { get; set; } = 600f;
	[Property] public float RepathInterval { get; set; } = 0.25f;

	public bool IsFrozen { get; private set; }

	private NavMeshAgent _agent;
	private SkinnedModelRenderer _smr;
	private float _repathTimer;
	private Player _player;

	protected override void OnStart()
	{
		base.OnStart();

		_agent = Components.GetOrCreate<NavMeshAgent>();
		_agent.MaxSpeed = ChaseSpeed;
		_agent.Radius = AgentRadius;
		_agent.Acceleration = AgentAcceleration;
		_agent.UpdatePosition = true;
		_agent.UpdateRotation = true;
		_agent.Height = 64f;

		_smr = Components.GetInDescendantsOrSelf<SkinnedModelRenderer>();
		_player = Scene.GetAllComponents<Player>().FirstOrDefault();
	}

	protected override void OnUpdate()
	{
		if ( IsFrozen )
		{
			DriveAnimGraph( Vector3.Zero );
			return;
		}

		_player ??= Scene.GetAllComponents<Player>().FirstOrDefault();
		if ( _player == null || _agent == null ) return;

		_repathTimer -= Time.Delta;
		if ( _repathTimer <= 0f )
		{
			_repathTimer = RepathInterval;
			_agent.MoveTo( _player.WorldPosition );
		}

		DriveAnimGraph( _agent.Velocity );
	}

	private void DriveAnimGraph( Vector3 velocity )
	{
		if ( _smr == null || _smr.Model == null ) return;

		var horiz = velocity.WithZ( 0 );
		var rot = GameObject.WorldRotation;
		var forward = rot.Forward.Dot( horiz );
		var sideward = rot.Right.Dot( horiz );
		float speed = horiz.Length;
		float angle = speed > 1f
			? MathF.Atan2( sideward, forward ).RadianToDegree().NormalizeDegrees()
			: 0f;

		_smr.Set( "move_direction", angle );
		_smr.Set( "move_speed", speed );
		_smr.Set( "move_groundspeed", speed );
		_smr.Set( "move_x", forward );
		_smr.Set( "move_y", sideward );
		_smr.Set( "move_z", 0f );

		_smr.Set( "wish_direction", angle );
		_smr.Set( "wish_speed", speed );
		_smr.Set( "wish_groundspeed", speed );
		_smr.Set( "wish_x", forward );
		_smr.Set( "wish_y", sideward );
		_smr.Set( "wish_z", 0f );

		_smr.Set( "b_grounded", true );
		_smr.Set( "b_jump", false );
		_smr.Set( "b_swim", false );
		_smr.Set( "b_climbing", false );
		_smr.Set( "b_noclip", false );
		_smr.Set( "duck", 0f );
	}

	public void Freeze()
	{
		if ( IsFrozen ) return;
		IsFrozen = true;
		if ( _agent != null )
		{
			_agent.Stop();
			_agent.MaxSpeed = 0f;
			_agent.UpdatePosition = false;
			_agent.UpdateRotation = false;
		}
		if ( _smr != null )
		{
			_smr.Tint = new Color( 0.4f, 0.7f, 1f );
		}
	}
}
