using Sandbox;
using System;

namespace Local.BulletHell;

/// <summary>
/// Straight-line projectile. Travels at a constant Velocity, despawns on lifetime
/// or world bounds, and damages the opposing side on trigger overlap.
/// </summary>
public sealed class Bullet : Component, Component.ITriggerListener
{
	[Property] public Vector3 Velocity { get; set; }
	[Property] public float Lifetime { get; set; } = 4f;

	/// <summary>True if fired by the player; false if fired by an enemy.</summary>
	[Property] public bool Friendly { get; set; }

	/// <summary>Half-extent of the playfield used for out-of-bounds despawn.</summary>
	[Property] public float WorldBound { get; set; } = 2000f;

	private TimeSince _timeAlive;

	protected override void OnStart()
	{
		base.OnStart();
		_timeAlive = 0f;

		// Visibility for runtime-spawned bullets (no prefab path).
		if ( Components.Get<ModelRenderer>() is null )
		{
			var mr = Components.Create<ModelRenderer>();
			mr.Model = Model.Load( "models/dev/sphere.vmdl" );
			mr.Tint = Friendly ? new Color( 0.4f, 0.9f, 1.0f ) : new Color( 1.0f, 0.7f, 0.2f );
		}
		// Trigger collider so OnTriggerEnter fires.
		if ( Components.Get<BoxCollider>() is null )
		{
			var col = Components.Create<BoxCollider>();
			col.IsTrigger = true;
			col.Scale = new Vector3( 20, 20, 20 );
		}
		GameObject.WorldScale = new Vector3( 0.15f, 0.15f, 0.15f );
	}

	protected override void OnUpdate()
	{
		WorldPosition += Velocity * Time.Delta;

		if ( _timeAlive >= Lifetime )
		{
			GameObject.Destroy();
			return;
		}

		var p = WorldPosition;
		if ( MathF.Abs( p.x ) > WorldBound
			|| MathF.Abs( p.y ) > WorldBound
			|| MathF.Abs( p.z ) > WorldBound )
		{
			GameObject.Destroy();
		}
	}

	void Component.ITriggerListener.OnTriggerEnter( Collider other )
	{
		var go = other.GameObject;
		if ( go is null ) return;

		if ( Friendly )
		{
			if ( go.Components.Get<Enemy>() is { } enemy )
			{
				enemy.TakeDamage( 1 );
				GameObject.Destroy();
			}
		}
		else
		{
			if ( go.Components.Get<Player>() is { } player )
			{
				player.TakeDamage( 1 );
				GameObject.Destroy();
			}
		}
	}
}
