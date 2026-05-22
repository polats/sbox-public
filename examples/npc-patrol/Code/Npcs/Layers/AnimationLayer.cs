using System;
using Sandbox;

namespace Local.NpcPatrol.Npcs.Layers;

/// <summary>
/// Pushes velocity and look-at parameters into the citizen's animgraph each frame.
/// Tasks call <see cref="SetLookTarget"/> / <see cref="ClearLookTarget"/>; the
/// NavigationLayer calls <see cref="SetMove"/> with the agent's current velocity.
///
/// Distilled from sandbox/Code/Npcs/Layers/AnimationLayer.cs — kept the params that
/// the parkour/tag-ai citizens actually need (see rules/animations.md "the 16 you
/// actually drive").
/// </summary>
public sealed class AnimationLayer : BaseNpcLayer
{
	[Property] public float LookSpeed { get; set; } = 4f;
	[Property] public float MaxHeadAngle { get; set; } = 45f;
	[Property] public float AimStrength { get; set; } = 1f;

	public Vector3? LookTarget { get; private set; }
	public GameObject LookTargetObject { get; private set; }

	private SkinnedModelRenderer Renderer => Npc.Renderer;
	private Vector3 _moveVelocity;
	private Rotation _moveRotation = Rotation.Identity;
	private float _lastYaw = float.NaN;

	public void SetLookTarget( GameObject target )
	{
		LookTargetObject = target;
		LookTarget = target.IsValid() ? target.WorldPosition : null;
	}

	public void SetLookTarget( Vector3 worldPos )
	{
		LookTargetObject = null;
		LookTarget = worldPos;
	}

	public void ClearLookTarget()
	{
		LookTargetObject = null;
		LookTarget = null;

		if ( Renderer.IsValid() )
		{
			Renderer.SetLookDirection( "aim_eyes", Vector3.Zero, 0f );
			Renderer.SetLookDirection( "aim_head", Vector3.Zero, 0f );
			Renderer.SetLookDirection( "aim_body", Vector3.Zero, 0f );
		}
	}

	public bool IsFacingTarget()
	{
		if ( !LookTarget.HasValue ) return true;
		var dir = (LookTarget.Value.WithZ( 0 ) - Npc.WorldPosition.WithZ( 0 )).Normal;
		var angle = Vector3.GetAngle( Npc.WorldRotation.Forward.WithZ( 0 ), dir );
		return angle <= MaxHeadAngle;
	}

	/// <summary>Called by NavigationLayer each frame with the agent's velocity.</summary>
	public void SetMove( Vector3 velocity, Rotation reference )
	{
		_moveVelocity = velocity;
		_moveRotation = reference;
	}

	protected override void OnUpdate()
	{
		if ( Npc is null ) return;
		if ( !Renderer.IsValid() ) return;

		// Track moving look targets
		if ( LookTargetObject.IsValid() )
			LookTarget = LookTargetObject.WorldPosition;

		if ( LookTarget.HasValue )
			ApplyLook( LookTarget.Value );

		ApplyMove( _moveVelocity, _moveRotation );

		Renderer.Set( "b_grounded", true );
		Renderer.Set( "b_jump", false );
		Renderer.Set( "b_swim", false );
		Renderer.Set( "b_climbing", false );
		Renderer.Set( "b_noclip", false );
	}

	private void ApplyLook( Vector3 worldPos )
	{
		var full = (worldPos - Npc.WorldPosition).Normal;
		var flat = (worldPos - Npc.WorldPosition).WithZ( 0 ).Normal;

		Renderer.SetLookDirection( "aim_eyes", full, AimStrength );
		Renderer.SetLookDirection( "aim_head", full, AimStrength );
		Renderer.SetLookDirection( "aim_body", full, AimStrength * 0.5f );

		// If body would have to twist past MaxHeadAngle, rotate the body towards target.
		var angle = Vector3.GetAngle( Npc.WorldRotation.Forward, flat );
		if ( angle > MaxHeadAngle )
		{
			var targetRot = Rotation.LookAt( flat, Vector3.Up );
			Npc.GameObject.WorldRotation = Rotation.Lerp( Npc.WorldRotation, targetRot, LookSpeed * Time.Delta );
		}
	}

	private void ApplyMove( Vector3 velocity, Rotation reference )
	{
		if ( reference.w == 0f ) return;

		var forward = reference.Forward.Dot( velocity );
		var sideward = reference.Right.Dot( velocity );
		var angle = MathF.Atan2( sideward, forward ).RadianToDegree().NormalizeDegrees();

		var yaw = reference.Angles().yaw.NormalizeDegrees();
		float rotationSpeed = 0f;
		if ( float.IsNaN( _lastYaw ) ) _lastYaw = yaw;
		else
		{
			var dy = Angles.NormalizeAngle( yaw - _lastYaw );
			rotationSpeed = Time.Delta > 0 ? MathF.Abs( dy ) / Time.Delta : 0f;
			_lastYaw = yaw;
		}

		Renderer.Set( "move_direction", angle );
		Renderer.Set( "move_speed", velocity.Length );
		Renderer.Set( "move_groundspeed", velocity.WithZ( 0 ).Length );
		Renderer.Set( "move_x", forward );
		Renderer.Set( "move_y", sideward );
		Renderer.Set( "move_z", velocity.z );
		Renderer.Set( "move_rotationspeed", rotationSpeed );
		Renderer.Set( "speed_move", 1f );
	}

	public override void ResetLayer()
	{
		_moveVelocity = default;
		_lastYaw = float.NaN;
		ClearLookTarget();
		if ( Renderer.IsValid() )
		{
			Renderer.Set( "move_speed", 0f );
			Renderer.Set( "move_groundspeed", 0f );
		}
	}
}
