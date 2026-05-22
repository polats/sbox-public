using Sandbox;
using System.Linq;

namespace Local.FpsWeapons;

/// <summary>
/// Citizen dummy target. On hit, falls over and hides for a respawn delay.
/// Can also be PhysGun-grabbed via attached Rigidbody on the parent.
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

			// Fall over (rotate forward)
			var fallAxis = GameObject.WorldRotation.Right;
			GameObject.WorldRotation = Rotation.FromAxis( fallAxis, 80f ) * SpawnRot;
		}
	}

	public void OnGrabbed()
	{
		// Lift slightly when grabbed by physgun — no other side effects
		Controller?.OnTargetGrabbed( this );
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
