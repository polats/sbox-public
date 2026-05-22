using Sandbox;
using System;
using System.Collections.Generic;

namespace Local.Fireworks;

/// <summary>
/// A single rocket. Spawned at the launch point; rises along +Z at Speed
/// for TimeToBurst seconds; then explodes into ParticleCount small rigid-body
/// spheres tinted BurstColor and pushed outward.
/// </summary>
public sealed class Firework : Component
{
	[Property, Range( 50f, 500f )] public float Speed { get; set; } = 280f;
	[Property, Range( 0.5f, 4f )] public float TimeToBurst { get; set; } = 1.8f;

	[Property, Group( "Burst" )] public Color BurstColor { get; set; } = Color.Red;
	[Property, Group( "Burst" ), Range( 20, 200 )] public int ParticleCount { get; set; } = 80;
	[Property, Group( "Burst" ), Range( 50f, 400f )] public float ParticleSpread { get; set; } = 250f;
	[Property, Group( "Burst" )] public float ParticleLifetime { get; set; } = 2.5f;

	[Property] public SoundEvent LaunchSound { get; set; }
	[Property] public SoundEvent BurstSound { get; set; }

	// Notify on burst (for HUD counter).
	public Action<Firework> OnBurst;

	private float _spawnTime;
	private bool _burst = false;
	private GameObject _trailModel;

	protected override void OnStart()
	{
		base.OnStart();
		_spawnTime = Time.Now;

		// Visible rocket body — a small bright sphere.
		var visual = Scene.CreateObject();
		visual.Name = "RocketVisual";
		visual.Parent = GameObject;
		visual.LocalPosition = Vector3.Zero;
		visual.LocalScale = new Vector3( 8f / 32f, 8f / 32f, 8f / 32f );
		var mr = visual.Components.Create<ModelRenderer>();
		mr.Model = Model.Load( "models/dev/sphere.vmdl" );
		mr.Tint = new Color( 1f, 0.9f, 0.4f );
		_trailModel = visual;

		// Whoosh sound at launch.
		if ( LaunchSound != null )
		{
			try { Sound.Play( LaunchSound, WorldPosition ); } catch { }
		}
	}

	protected override void OnUpdate()
	{
		if ( _burst ) return;

		// Rise straight up.
		var p = WorldPosition;
		p.z += Speed * Time.Delta;
		WorldPosition = p;

		if ( Time.Now - _spawnTime >= TimeToBurst )
		{
			Burst();
		}
	}

	private void Burst()
	{
		_burst = true;
		var burstPos = WorldPosition;

		// Hide the rocket body.
		if ( _trailModel != null && _trailModel.IsValid() )
			_trailModel.Enabled = false;

		// Bang sound.
		if ( BurstSound != null )
		{
			try { Sound.Play( BurstSound, burstPos ); } catch { }
		}

		// Try to spawn a real ParticleEffect first (the primary gap to cover).
		TrySpawnParticleEffect( burstPos );

		// Always also spawn the rigidbody-particle fallback — that's the
		// load-bearing visual. The real ParticleEffect (if it works) gives
		// us bonus extra sparkle.
		SpawnRigidbodyBurst( burstPos );

		OnBurst?.Invoke( this );

		// Self-destruct after the slowest visible particle is gone.
		Components.Create<TimedDestroy>().Lifetime = ParticleLifetime + 0.5f;
	}

	private void TrySpawnParticleEffect( Vector3 pos )
	{
		// Try a known-shipped explosion/sparks particle, if any.
		// Most engine builds ship `particles/explosion.vpcf` or `particles/sparks.vpcf`.
		// We attempt a couple in priority order and stop on the first that loads.
		var paths = new[]
		{
			"particles/sparks.vpcf",
			"particles/explosion.vpcf",
			"particles/impact.flesh.vpcf",
		};
		foreach ( var path in paths )
		{
			var ps = ResourceLibrary.Get<ParticleSystem>( path );
			if ( ps == null ) continue;

			try
			{
				var fxGo = Scene.CreateObject();
				fxGo.Name = "BurstFX";
				fxGo.WorldPosition = pos;
				var pe = fxGo.Components.Create<LegacyParticleSystem>();
				pe.Particles = ps;
				pe.ControlPoints = new List<ParticleControlPoint>
				{
					new ParticleControlPoint { Value = ParticleControlPoint.ControlPointValueInput.Vector3, VectorValue = pos },
				};
				fxGo.Components.Create<TimedDestroy>().Lifetime = 3f;
				return;
			}
			catch
			{
				// fallthrough to next path
			}
		}
	}

	private void SpawnRigidbodyBurst( Vector3 pos )
	{
		// Spawn N small spheres flying outward in a roughly spherical pattern.
		// Each is a rigidbody with low damping + gravity → arc + fall.
		float baseLife = ParticleLifetime;
		for ( int i = 0; i < ParticleCount; i++ )
		{
			var go = Scene.CreateObject();
			go.Name = "BurstParticle";
			go.WorldPosition = pos;
			// 1.6" diameter spark = scale ~ 0.05
			go.WorldScale = new Vector3( 1.6f / 32f, 1.6f / 32f, 1.6f / 32f );

			var mr = go.Components.Create<ModelRenderer>();
			mr.Model = Model.Load( "models/dev/sphere.vmdl" );
			// Tint with a little brightness variance so it looks like glow.
			var tint = BurstColor;
			float jitter = Game.Random.Float( 0.75f, 1.25f );
			tint = new Color(
				Math.Clamp( tint.r * jitter, 0f, 1f ),
				Math.Clamp( tint.g * jitter, 0f, 1f ),
				Math.Clamp( tint.b * jitter, 0f, 1f ),
				1f );
			mr.Tint = tint;
			// Push it onto the bloom layer so the PostProcessVolume bloom picks it up.

			var col = go.Components.Create<SphereCollider>();
			col.Radius = 32f;

			var rb = go.Components.Create<Rigidbody>();
			rb.Gravity = true;
			rb.LinearDamping = 0.5f;
			rb.AngularDamping = 0.5f;
			rb.EnhancedCcd = true;
			rb.MassOverride = 0.01f;

			// Random direction in a sphere, biased slightly upward.
			var dir = new Vector3(
				Game.Random.Float( -1f, 1f ),
				Game.Random.Float( -1f, 1f ),
				Game.Random.Float( -0.3f, 1.2f )
			).Normal;
			float power = ParticleSpread * Game.Random.Float( 0.6f, 1.2f );
			rb.ApplyImpulse( dir * power * rb.Mass );

			// Stagger lifetimes a touch so they don't all vanish at once.
			float life = baseLife * Game.Random.Float( 0.7f, 1.0f );
			go.Components.Create<TimedDestroy>().Lifetime = life;
		}
	}
}
