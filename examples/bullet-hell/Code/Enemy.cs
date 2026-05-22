using Sandbox;
using System;

namespace Local.BulletHell;

/// <summary>
/// Stationary/hovering enemy that periodically emits a radial spread of bullets.
/// </summary>
public sealed class Enemy : Component
{
	[Property] public int BulletsPerBurst { get; set; } = 10;
	[Property] public float BurstInterval { get; set; } = 1.25f;
	[Property] public float BulletSpeed { get; set; } = 400f;
	[Property] public float OrbitRadius { get; set; } = 80f;
	[Property] public float OrbitSpeed { get; set; } = 1f;
	[Property] public int MaxHealth { get; set; } = 5;

	[Sync] public int Health { get; set; }

	private TimeSince _timeSinceBurst;
	private Vector3 _origin;
	private float _angleOffset;

	protected override void OnStart()
	{
		base.OnStart();
		Health = MaxHealth;
		_origin = WorldPosition;
		_timeSinceBurst = 0f;
		_angleOffset = 0f;
		if ( Components.Get<BoxCollider>() is null )
		{
			var col = Components.Create<BoxCollider>();
			col.IsTrigger = true;
			col.Scale = new Vector3( 60, 60, 60 );
		}
	}

	protected override void OnUpdate()
	{
		// Gentle horizontal hover so it isn't a sitting duck.
		_angleOffset += OrbitSpeed * Time.Delta;
		var x = MathF.Cos( _angleOffset ) * OrbitRadius;
		WorldPosition = _origin + new Vector3( 0, x, 0 );

		if ( _timeSinceBurst >= BurstInterval )
		{
			FireSpread();
			_timeSinceBurst = 0f;
		}
	}

	private void FireSpread()
	{
		var n = MathX.Clamp( BulletsPerBurst, 1, 64 );
		for ( int i = 0; i < n; i++ )
		{
			var t = i / (float)n;
			var angle = t * MathF.PI * 2f;
			var dir = new Vector3( MathF.Cos( angle ), MathF.Sin( angle ), 0f );

			var go = new GameObject( true, $"EnemyBullet_{i}" );
			go.WorldPosition = WorldPosition;

			var bullet = go.Components.GetOrCreate<Bullet>();
			bullet.Velocity = dir * BulletSpeed;
			bullet.Friendly = false;
		}
	}

	public void TakeDamage( int amount )
	{
		Health -= amount;
		if ( Health <= 0 )
		{
			// Award points to the (first) player in the scene.
			foreach ( var p in Scene.GetAllComponents<Player>() )
			{
				p.Score += 100;
				break;
			}
			GameObject.Destroy();
		}
	}
}
