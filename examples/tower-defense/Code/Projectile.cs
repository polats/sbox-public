using Sandbox;
using System;

namespace Local.TowerDefense;

/// <summary>
/// A "hitscan-ish" projectile: not a real Rigidbody-driven thing, just a
/// visual GameObject that flies straight at its target each frame and deals
/// damage on contact (or self-destructs if target dies first). Leaves a
/// short particle trail behind via spawned-and-fading sphere objects.
/// </summary>
public sealed class Projectile : Component
{
	[Property] public float Speed { get; set; } = 1200f;
	[Property] public float Damage { get; set; } = 12f;
	[Property] public float MaxLifetime { get; set; } = 3f;

	public Creep Target { get; set; }
	public GameObject Attacker { get; set; }

	private float _spawnTime;
	private float _trailTimer;

	protected override void OnStart()
	{
		base.OnStart();
		_spawnTime = Time.Now;
	}

	protected override void OnUpdate()
	{
		if ( Time.Now - _spawnTime > MaxLifetime )
		{
			GameObject.Destroy();
			return;
		}

		if ( Target == null || !Target.GameObject.IsValid() || Target.IsDead )
		{
			GameObject.Destroy();
			return;
		}

		var toTarget = Target.WorldPosition + Vector3.Up * 40f - WorldPosition;
		float dist = toTarget.Length;
		var step = Speed * Time.Delta;

		// Trail: spawn a fading sphere every few frames.
		_trailTimer -= Time.Delta;
		if ( _trailTimer <= 0f )
		{
			_trailTimer = 0.04f;
			SpawnTrailPuff();
		}

		if ( step >= dist )
		{
			// Hit.
			Target.OnDamage( Damage, DamageInfo.Simple( Damage, WorldPosition, Attacker ) );
			GameObject.Destroy();
			return;
		}

		WorldPosition += toTarget.Normal * step;
	}

	private void SpawnTrailPuff()
	{
		var go = Scene.CreateObject();
		go.Name = "TrailPuff";
		go.WorldPosition = WorldPosition;
		go.WorldScale = new Vector3( 3f / 32f, 3f / 32f, 3f / 32f );
		var mr = go.Components.Create<ModelRenderer>();
		mr.Model = Model.Load( "models/dev/sphere.vmdl" );
		mr.Tint = new Color( 1.0f, 0.75f, 0.1f, 1f );
		go.Components.Create<TimedDestroy>().Lifetime = 0.25f;
	}
}
