using Sandbox;
using System;

namespace Local.ToyBox;

/// <summary>
/// Local equivalent of sandbox/Code/Map/FuncMover.cs. Slides between
/// LocalPosition (saved at OnStart) and LocalPosition + LinearDistance using a
/// sinusoid. The sandbox version uses ping-pong-with-pause; sine is simpler
/// and still rideable.
///
/// Carrying riders: we additionally drag any CharacterControllers standing on
/// top of us by the platform's frame delta. The engine's CharacterController
/// doesn't auto-parent to moving geometry.
/// </summary>
[Icon( "open_with" )]
public sealed class FuncMover : Component
{
	[Property] public Vector3 LinearDistance { get; set; } = new Vector3( 0, 200, 0 );
	[Property] public float PeriodSeconds { get; set; } = 4f;
	[Property] public Vector3 RiderHalfExtents { get; set; } = new Vector3( 80, 80, 60 );

	private Vector3 _start;
	private Vector3 _lastPos;

	protected override void OnStart()
	{
		_start = LocalPosition;
		_lastPos = WorldPosition;
	}

	protected override void OnUpdate()
	{
		var omega = MathF.Tau / MathF.Max( 0.01f, PeriodSeconds );
		var t = (MathF.Sin( Time.Now * omega ) + 1f) * 0.5f;
		LocalPosition = _start + LinearDistance * t;

		var newPos = WorldPosition;
		var delta = newPos - _lastPos;
		_lastPos = newPos;

		// Drag riders (CharacterControllers in a small box above the platform top)
		if ( delta.LengthSquared > 0.0001f )
		{
			var center = WorldPosition + Vector3.Up * (RiderHalfExtents.z + 10f);
			foreach ( var cc in Scene.GetAllComponents<CharacterController>() )
			{
				var p = cc.WorldPosition;
				var d = p - center;
				if ( MathF.Abs( d.x ) > RiderHalfExtents.x ) continue;
				if ( MathF.Abs( d.y ) > RiderHalfExtents.y ) continue;
				if ( MathF.Abs( d.z ) > RiderHalfExtents.z ) continue;
				cc.GameObject.WorldPosition += delta;
			}
		}
	}

	protected override void DrawGizmos()
	{
		Gizmo.Draw.Color = Color.Green;
		Gizmo.Draw.LineSphere( Vector3.Zero, 6 );
		Gizmo.Draw.Color = Color.Red;
		Gizmo.Draw.LineSphere( LinearDistance, 6 );
		Gizmo.Draw.Color = Color.White;
		Gizmo.Draw.Line( Vector3.Zero, LinearDistance );
	}
}
