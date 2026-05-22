using Sandbox;
using System;
using System.Linq;

namespace Local.FlappyBird;

public sealed class Bird : Component
{
	[Property] public float Gravity { get; set; } = 900f;
	[Property] public float FlapVelocity { get; set; } = 380f;
	[Property] public float MaxFallSpeed { get; set; } = 700f;
	[Property] public Vector3 StartPosition { get; set; } = new Vector3( 0, 0, 200 );
	[Property] public float CollisionRadius { get; set; } = 18f;
	[Property] public float GroundZ { get; set; } = 20f;
	[Property] public float CeilingZ { get; set; } = 600f;

	public bool IsFrozen { get; set; } = true;
	public float VelocityZ { get; private set; }

	private GameManager _gm;

	protected override void OnStart()
	{
		base.OnStart();
		_gm = Scene.GetAllComponents<GameManager>().FirstOrDefault();
		ResetToStart();
	}

	public void ResetToStart()
	{
		WorldPosition = StartPosition;
		WorldRotation = Rotation.Identity;
		VelocityZ = 0f;
	}

	public void Flap()
	{
		VelocityZ = FlapVelocity;
	}

	protected override void OnUpdate()
	{
		if ( IsFrozen )
		{
			var hover = MathF.Sin( Time.Now * 4f ) * 8f;
			WorldPosition = StartPosition + new Vector3( 0, 0, hover );
			WorldRotation = Rotation.Identity;
			return;
		}

		if ( Input.Pressed( "Jump" ) )
			Flap();

		VelocityZ -= Gravity * Time.Delta;
		if ( VelocityZ < -MaxFallSpeed ) VelocityZ = -MaxFallSpeed;

		var p = WorldPosition;
		p.z += VelocityZ * Time.Delta;

		if ( p.z <= GroundZ )
		{
			p.z = GroundZ;
			WorldPosition = p;
			_gm?.GameOver();
			return;
		}
		if ( p.z >= CeilingZ )
		{
			p.z = CeilingZ;
			VelocityZ = 0f;
		}

		WorldPosition = p;

		var pitchDeg = Math.Clamp( VelocityZ * 0.05f, -60f, 30f );
		WorldRotation = Rotation.FromPitch( -pitchDeg );
	}
}
