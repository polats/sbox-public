using Sandbox;

namespace Local.RagdollCannon;

/// <summary>
/// Attached to obstacles (pendulum bobs, trampoline pad). When a ragdoll body
/// part collides, play a soft thud at the impact point. Rate-limited per object.
/// </summary>
public sealed class ObstacleImpactSound : Component, Component.ICollisionListener
{
	private float _nextSoundTime = 0f;
	private SoundEvent _thud;

	protected override void OnStart()
	{
		base.OnStart();
		_thud = ResourceLibrary.Get<SoundEvent>( "sounds/impacts/melee/impact-melee-flesh.sound" );
	}

	void Component.ICollisionListener.OnCollisionStart( Collision collision )
	{
		if ( Time.Now < _nextSoundTime ) return;
		// collision.Other is a struct — access GameObject directly (may be null).
		var otherGo = collision.Other.GameObject;
		if ( otherGo == null ) return;
		// Only care about ragdoll impacts
		var go = otherGo;
		while ( go != null && !go.Tags.Has( "ragdoll" ) ) go = go.Parent;
		if ( go == null ) return;
		_nextSoundTime = Time.Now + 0.12f;
		if ( _thud != null ) Sound.Play( _thud, WorldPosition );
	}
}
