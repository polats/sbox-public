using Sandbox;
using System.Linq;

namespace Local.RagdollCannon;

/// <summary>Sits in the goal pit. Records when a ragdoll body enters and scores it.</summary>
public sealed class PitTrigger : Component, Component.ITriggerListener
{
	public GameManager Manager { get; set; }

	private readonly System.Collections.Generic.HashSet<GameObject> _scoredRagdolls = new();

	void Component.ITriggerListener.OnTriggerEnter( Collider other )
	{
		if ( Manager == null ) return;
		// Walk up from the collider to find the ragdoll root (the one we tagged "ragdoll").
		var go = other.GameObject;
		while ( go != null && !go.Tags.Has( "ragdoll" ) )
			go = go.Parent;
		if ( go == null ) return;
		if ( _scoredRagdolls.Contains( go ) ) return;
		_scoredRagdolls.Add( go );
		Manager.OnRagdollEnteredPit( go );
	}
}
