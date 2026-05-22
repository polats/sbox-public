using Sandbox;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Local.Achievements;

/// <summary>
/// Citizen-driven player with WASD movement and animgraph parameter feeding.
/// When AutoPlay = true, walks itself through a scripted button-pressing route
/// that unlocks 3-4 of the 5 achievements (deliberately slow to skip
/// speedrun and let the panel show a mix).
/// </summary>
public sealed class PuzzlePlayer : Component
{
	[Property] public float MoveSpeed { get; set; } = 220f;
	[Property] public GameObject CameraRig { get; set; }

	/// <summary>
	/// When true, the player walks itself through a scripted route. Used for
	/// the demo recording. MUST default to false so a real user opening the
	/// project gets manual control.
	/// </summary>
	[Property] public bool AutoPlay { get; set; } = false;

	private SkinnedModelRenderer _smr;
	private CharacterController _controller;

	// AutoPlay state
	private int _autoStep = 0;
	private TimeSince _sinceStepStart;
	private List<Vector3> _route = new();
	private TimeSince _sinceAutoStart;

	protected override void OnStart()
	{
		base.OnStart();
		_smr = Components.Get<SkinnedModelRenderer>();
		_controller = Components.GetOrCreate<CharacterController>();
		_controller.Height = 72f;
		_controller.Radius = 16f;

		if ( AutoPlay )
		{
			BuildAutoRoute();
			_sinceAutoStart = 0;
		}
	}

	private void BuildAutoRoute()
	{
		// Visit rainbow order with deliberate pauses, route avoids the
		// central pedestal (kept clear by going around). Designed to unlock:
		//   first_button, all_colors, sequence_master, pacifist
		// Speedrun is intentionally skipped (we pause between presses).
		var byColor = Scene.GetAllComponents<ColorButton>()
			.ToDictionary( b => b.ColorId, b => b.GameObject.WorldPosition );

		foreach ( var col in AchievementManager.RainbowOrder )
		{
			if ( byColor.TryGetValue( col, out var p ) )
			{
				// Step out from center first so the path arcs around pedestal.
				var arc = p.Normal * 30f;
				_route.Add( p + arc );
				_route.Add( p );
			}
		}
	}

	protected override void OnUpdate()
	{
		Vector3 wishDir = Vector3.Zero;

		if ( AutoPlay )
		{
			wishDir = AutoPlayStep();
		}
		else
		{
			if ( Input.Down( "Forward" ) )  wishDir += Vector3.Forward;
			if ( Input.Down( "Backward" ) ) wishDir += Vector3.Backward;
			if ( Input.Down( "Left" ) )     wishDir += Vector3.Left;
			if ( Input.Down( "Right" ) )    wishDir += Vector3.Right;
			if ( !wishDir.IsNearZeroLength ) wishDir = wishDir.Normal;
		}

		// Move via CharacterController
		var targetVel = wishDir * MoveSpeed;
		// Gravity
		if ( _controller.IsOnGround )
			targetVel = targetVel.WithZ( 0 );
		else
			targetVel = targetVel.WithZ( _controller.Velocity.z - 600f * Time.Delta );

		_controller.Velocity = targetVel;
		_controller.Move();

		// Face wish direction
		if ( !wishDir.IsNearZeroLength )
		{
			var targetYaw = MathF.Atan2( wishDir.y, wishDir.x ).RadianToDegree();
			var current = GameObject.WorldRotation.Yaw();
			var lerpedYaw = current.LerpDegreesTo( targetYaw, Time.Delta * 8f );
			GameObject.WorldRotation = Rotation.FromYaw( lerpedYaw );
		}

		// Drive animgraph
		if ( _smr != null )
		{
			var vel = _controller.Velocity;
			var rot = GameObject.WorldRotation;
			var localFwd = rot.Forward.Dot( vel );
			var localRight = rot.Right.Dot( vel );
			_smr.Set( "move_groundspeed", new Vector3( vel.x, vel.y, 0 ).Length );
			_smr.Set( "move_speed", new Vector3( vel.x, vel.y, 0 ).Length );
			_smr.Set( "move_x", localFwd );
			_smr.Set( "move_y", localRight );
			_smr.Set( "move_z", vel.z );
			_smr.Set( "wish_x", localFwd );
			_smr.Set( "wish_y", localRight );
			_smr.Set( "b_grounded", _controller.IsOnGround );
		}
	}

	private Vector3 AutoPlayStep()
	{
		if ( _autoStep >= _route.Count ) return Vector3.Zero;

		var target = _route[_autoStep];
		var toTarget = (target - GameObject.WorldPosition).WithZ( 0 );

		if ( toTarget.Length < 8f )
		{
			_autoStep++;
			_sinceStepStart = 0;
			// Deliberate pause between waypoints — keeps total time past 10s
			// so speedrun stays locked.
			return Vector3.Zero;
		}

		// Pause briefly at each waypoint
		if ( _sinceStepStart < 0.5f && _autoStep > 0 )
			return Vector3.Zero;

		return toTarget.Normal;
	}
}
