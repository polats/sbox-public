using Sandbox;
using System.Linq;

namespace Local.Parkour;

public sealed class GoalTrigger : Component, Component.ITriggerListener
{
	void Component.ITriggerListener.OnTriggerEnter( Collider other )
	{
		var player = other.GameObject?.Components.Get<Player>()
			?? other.GameObject?.Components.GetInAncestors<Player>();
		if ( player == null ) return;
		player.OnReachedGoal();
	}
}
