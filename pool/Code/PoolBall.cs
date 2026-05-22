using Sandbox;
using System;

namespace Local.Pool;

public sealed class PoolBall : Component, Component.ICollisionListener
{
	public GameController Controller { get; set; }
	public bool IsCueBall { get; set; }
	public float LastSoundTime { get; set; }

	void Component.ICollisionListener.OnCollisionStart( Collision c )
	{
		if ( Controller == null ) return;
		// Only play a click on ball-ball contact (other side must have a PoolBall too)
		var otherGo = c.Other.GameObject;
		if ( otherGo == null ) return;
		var other = otherGo.Components.Get<PoolBall>();
		if ( other == null ) return;

		// Debounce sound: at most every 0.05s per ball
		if ( Time.Now - LastSoundTime < 0.05f ) return;
		LastSoundTime = Time.Now;

		// Only play if relative speed is meaningful
		var rb1 = Components.Get<Rigidbody>();
		var rb2 = other.Components.Get<Rigidbody>();
		float relSpeed = ((rb1?.Velocity ?? Vector3.Zero) - (rb2?.Velocity ?? Vector3.Zero)).Length;
		if ( relSpeed < 15f ) return;

		Controller.PlaySound( Controller.BallHitSound, WorldPosition );
	}
}
