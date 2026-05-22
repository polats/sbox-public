using Sandbox;
using System.Linq;

namespace Local.HammerLevel;

/// <summary>
/// Citizen dummy target. Ragdolls on hit; respawns after delay.
/// </summary>
public sealed class Target : Component
{
	[Property] public float RespawnDelay { get; set; } = 6f;
	[Property] public float HitImpulse { get; set; } = 250f;
	public GameController Controller { get; set; }

	public Vector3 SpawnPos { get; set; }
	public Rotation SpawnRot { get; set; } = Rotation.Identity;

	private bool _down;
	private float _respawnAt;

	protected override void OnStart()
	{
		base.OnStart();
		SpawnPos = GameObject.WorldPosition;
		SpawnRot = GameObject.WorldRotation;
		if ( Controller == null )
			Controller = Scene.GetAllComponents<GameController>().FirstOrDefault();
	}

	public void OnShot( Vector3 hitPos, Vector3 dir, float damage )
	{
		Controller?.OnTargetHit( this, hitPos );

		var rb = Components.Get<Rigidbody>();
		if ( rb != null && rb.MotionEnabled )
		{
			rb.ApplyImpulseAt( hitPos, dir.Normal * HitImpulse * rb.Mass );
			rb.ApplyImpulse( Vector3.Up * 60f * rb.Mass );
		}

		if ( !_down )
		{
			_down = true;
			_respawnAt = Time.Now + RespawnDelay;
			var fallAxis = GameObject.WorldRotation.Right;
			GameObject.WorldRotation = Rotation.FromAxis( fallAxis, 80f ) * SpawnRot;
		}
	}

	protected override void OnUpdate()
	{
		if ( _down && Time.Now >= _respawnAt )
		{
			Respawn();
		}
	}

	private void Respawn()
	{
		_down = false;
		GameObject.WorldPosition = SpawnPos;
		GameObject.WorldRotation = SpawnRot;
		var rb = Components.Get<Rigidbody>();
		if ( rb != null )
		{
			rb.Velocity = Vector3.Zero;
			rb.AngularVelocity = Vector3.Zero;
			rb.MotionEnabled = true;
			rb.Gravity = true;
		}
	}
}
