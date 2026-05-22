using Sandbox;
using System;
using System.Linq;

namespace Local.WorkshopBrowser;

/// <summary>
/// Floating-camera "player" for the workshop demo. WASD moves a free camera,
/// right-click spawns the currently selected asset at the ground hit position.
/// AutoPlay flies the camera in a slow orbit and (via GameManager) auto-spawns
/// props for the video.
/// </summary>
public sealed class Player : Component
{
	[Property] public float MoveSpeed { get; set; } = 350f;
	[Property] public bool AutoPlay { get; set; } = false;

	private CameraComponent _cam;
	private GameManager _gm;
	private float _orbitAngle;

	protected override void OnStart()
	{
		base.OnStart();
		_cam = Scene.Camera;
		_gm = Scene.GetAllComponents<GameManager>().FirstOrDefault();
	}

	protected override void OnUpdate()
	{
		if ( _cam == null ) return;

		if ( AutoPlay )
		{
			_orbitAngle += Time.Delta * 12f;
			var rad = _orbitAngle * MathF.PI / 180f;
			var r = 520f;
			_cam.GameObject.WorldPosition = new Vector3( MathF.Cos( rad ) * r, MathF.Sin( rad ) * r, 320f );
			_cam.GameObject.WorldRotation = Rotation.LookAt( (Vector3.Zero - _cam.GameObject.WorldPosition).Normal, Vector3.Up );
			return;
		}

		// WASD free camera
		var rot = _cam.GameObject.WorldRotation;
		var fwd = rot.Forward.WithZ( 0 ).Normal;
		var rgt = rot.Right.WithZ( 0 ).Normal;

		Vector3 move = Vector3.Zero;
		if ( Input.Down( "Forward" ) ) move += fwd;
		if ( Input.Down( "Backward" ) ) move -= fwd;
		if ( Input.Down( "Right" ) ) move += rgt;
		if ( Input.Down( "Left" ) ) move -= rgt;
		if ( Input.Down( "Jump" ) ) move += Vector3.Up;
		if ( Input.Down( "Duck" ) ) move -= Vector3.Up;

		if ( move.LengthSquared > 0.01f )
		{
			_cam.GameObject.WorldPosition += move.Normal * MoveSpeed * Time.Delta;
		}

		// Mouse-look while right button held (so left click is free for UI).
		if ( Input.Down( "Attack2" ) )
		{
			var yaw = -Input.MouseDelta.x * 0.15f;
			var pitch = Input.MouseDelta.y * 0.10f;
			var ang = rot.Angles();
			ang.yaw += yaw;
			ang.pitch = Math.Clamp( ang.pitch + pitch, -85f, 85f );
			_cam.GameObject.WorldRotation = Rotation.From( ang );
		}

		// Spawn-at-ground on Attack1 press (left click).
		if ( Input.Pressed( "Attack1" ) && _gm != null && _gm.SelectedAsset != null )
		{
			var camPos = _cam.GameObject.WorldPosition;
			var ray = new Ray( camPos, _cam.GameObject.WorldRotation.Forward );
			var tr = Scene.Trace.Ray( ray, 5000f ).Run();
			if ( tr.Hit )
			{
				_gm.SpawnAt( tr.HitPosition );
			}
		}
	}
}
