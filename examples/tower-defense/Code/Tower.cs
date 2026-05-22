using Sandbox;
using System;
using System.Linq;

namespace Local.TowerDefense;

public enum TowerType
{
	Basic,
}

/// <summary>
/// A stationary tower that scans for the nearest creep every ScanInterval
/// seconds and fires a Projectile at it. Variants of damage/range/firerate
/// are exposed via [Property] (the "[Property] variants" secondary gap).
/// </summary>
public sealed class Tower : Component
{
	[Property] public TowerType Type { get; set; } = TowerType.Basic;
	[Property, Range( 100f, 800f )] public float Range { get; set; } = 350f;
	[Property, Range( 1f, 100f )] public float Damage { get; set; } = 12f;
	[Property, Range( 0.1f, 3f )] public float FireRate { get; set; } = 1.0f; // shots/sec
	[Property, Range( 0.05f, 1f )] public float ScanInterval { get; set; } = 0.25f;

	private float _scanTimer;
	private float _fireCooldown;
	private Creep _target;

	protected override void OnUpdate()
	{
		_scanTimer -= Time.Delta;
		_fireCooldown -= Time.Delta;

		if ( _scanTimer <= 0f )
		{
			_scanTimer = ScanInterval;
			_target = AcquireTarget();
		}

		if ( _target == null || !_target.GameObject.IsValid() || _target.IsDead )
		{
			_target = null;
			return;
		}

		// Aim turret visually (rotate the head GameObject).
		var head = GameObject.Children.FirstOrDefault( c => c.Name == "Head" );
		if ( head != null )
		{
			var toTarget = (_target.WorldPosition - head.WorldPosition).WithZ( 0 );
			if ( toTarget.LengthSquared > 0.1f )
				head.WorldRotation = Rotation.LookAt( toTarget, Vector3.Up );
		}

		if ( _fireCooldown <= 0f )
		{
			_fireCooldown = 1f / FireRate;
			Fire( _target );
		}
	}

	private Creep AcquireTarget()
	{
		Creep best = null;
		float bestDist = Range * Range;
		foreach ( var c in Scene.GetAllComponents<Creep>() )
		{
			if ( c == null || !c.GameObject.IsValid() || c.IsDead ) continue;
			float d2 = (c.WorldPosition - WorldPosition).LengthSquared;
			if ( d2 <= bestDist )
			{
				bestDist = d2;
				best = c;
			}
		}
		return best;
	}

	private void Fire( Creep target )
	{
		var origin = WorldPosition + Vector3.Up * 90f;
		var go = Scene.CreateObject();
		go.Name = "Projectile";
		go.WorldPosition = origin;
		go.WorldScale = new Vector3( 6f / 32f, 6f / 32f, 6f / 32f );

		var mr = go.Components.Create<ModelRenderer>();
		mr.Model = Model.Load( "models/dev/sphere.vmdl" );
		mr.Tint = new Color( 1.0f, 0.85f, 0.2f );

		var p = go.Components.Create<Projectile>();
		p.Target = target;
		p.Damage = Damage;
		p.Attacker = GameObject;
	}
}
