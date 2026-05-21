using Sandbox;
using System.Linq;

namespace Local.CoinRush;

/// <summary>
/// Collectible coin. When the player walks into it, score goes up and the coin is destroyed.
/// </summary>
public sealed class Coin : Component, Component.ITriggerListener
{
	[Property] public int Value { get; set; } = 10;

	private bool _collected;

	protected override void OnStart()
	{
		base.OnStart();
		if ( Components.Get<BoxCollider>() is null )
		{
			var col = Components.Create<BoxCollider>();
			col.IsTrigger = true;
			col.Scale = new Vector3( 40, 40, 40 );
		}
	}

	void Component.ITriggerListener.OnTriggerEnter( Collider other )
	{
		if ( _collected ) return;

		var player = other.GameObject.Components.Get<Player>()
			?? other.GameObject.Components.GetInAncestorsOrSelf<Player>();
		if ( player is null ) return;

		_collected = true;
		player.Score += Value;

		var coins = Scene.GetAllComponents<Coin>().Count( c => c != this && !c._collected );
		if ( coins == 0 )
		{
			Log.Info( "All collected!" );
		}

		GameObject.Destroy();
	}
}
