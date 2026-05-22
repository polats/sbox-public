using Sandbox;

namespace Local.Pool;

public sealed class Pocket : Component, Component.ITriggerListener
{
	public GameController Controller { get; set; }

	void Component.ITriggerListener.OnTriggerEnter( Collider other )
	{
		if ( Controller == null ) return;
		var ball = other.GameObject.Components.Get<PoolBall>()
		         ?? other.GameObject.Components.GetInAncestorsOrSelf<PoolBall>();
		if ( ball == null ) return;
		Controller.OnBallPocketed( ball );
	}
}
