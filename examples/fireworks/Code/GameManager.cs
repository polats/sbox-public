using Sandbox;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Local.Fireworks;

/// <summary>
/// Orchestrates the fireworks demo. Listens for left-click → raycasts onto
/// the ground → spawns a Firework. Counts total fireworks fired (for HUD).
/// AutoPlay = true fires one at a random spot every AutoInterval seconds.
/// </summary>
public sealed class GameManager : Component
{
	[Property] public bool AutoPlay { get; set; } = false;
	[Property, Range( 0.5f, 5f )] public float AutoInterval { get; set; } = 1.5f;
	[Property, Range( 100f, 2000f )] public float GroundHalfExtent { get; set; } = 900f;

	[Property] public SoundEvent LaunchSound { get; set; }
	[Property] public SoundEvent BurstSound { get; set; }

	[Property, Group( "Scorch" )] public bool SpawnScorchMarks { get; set; } = true;
	[Property, Group( "Scorch" )] public DecalDefinition ScorchDecal { get; set; }

	public int FireworkCount { get; private set; } = 0;

	private CameraComponent _cam;
	private float _nextAutoTime = 0f;

	private static readonly Color[] PaletteColors = new[]
	{
		new Color( 1.0f, 0.20f, 0.20f ),  // red
		new Color( 1.0f, 0.55f, 0.10f ),  // orange
		new Color( 1.0f, 0.95f, 0.20f ),  // yellow
		new Color( 0.20f, 1.0f, 0.30f ),  // green
		new Color( 0.20f, 0.55f, 1.0f ),  // blue
		new Color( 0.75f, 0.30f, 1.0f ),  // purple
		new Color( 1.0f, 0.40f, 0.85f ),  // pink
		new Color( 0.55f, 0.95f, 1.0f ),  // cyan
		new Color( 1.0f, 1.0f, 1.0f ),    // white
	};

	protected override void OnStart()
	{
		base.OnStart();
		_cam = Scene.Camera;

		// Resolve sounds by name if not assigned in scene.
		LaunchSound ??= ResourceLibrary.Get<SoundEvent>( "sounds/kenney/footsteps/footstep-concrete.sound" );
		BurstSound ??= ResourceLibrary.Get<SoundEvent>( "sounds/impacts/melee/impact-melee-wood.sound" );

		_nextAutoTime = Time.Now + 0.5f;
	}

	protected override void OnUpdate()
	{
		// Manual fire — click anywhere on screen.
		if ( Input.Pressed( "Attack1" ) )
		{
			var hit = TraceMouseToGround();
			if ( hit.HasValue )
			{
				LaunchAt( hit.Value, NextColor() );
			}
		}

		// AutoPlay.
		if ( AutoPlay && Time.Now >= _nextAutoTime )
		{
			float r = GroundHalfExtent * 0.7f;
			var spot = new Vector3(
				Game.Random.Float( -r, r ),
				Game.Random.Float( -r, r ),
				0f );
			LaunchAt( spot, NextColor() );
			_nextAutoTime = Time.Now + AutoInterval;
		}
	}

	private Vector3? TraceMouseToGround()
	{
		if ( _cam == null ) return null;
		var ray = _cam.ScreenPixelToRay( Mouse.Position );
		// Intersect z=0 plane.
		if ( MathF.Abs( ray.Forward.z ) < 0.0001f ) return null;
		float t = ( 0f - ray.Position.z ) / ray.Forward.z;
		if ( t <= 0 ) return null;
		var hit = ray.Position + ray.Forward * t;
		// Clamp to ground extents.
		hit.x = Math.Clamp( hit.x, -GroundHalfExtent, GroundHalfExtent );
		hit.y = Math.Clamp( hit.y, -GroundHalfExtent, GroundHalfExtent );
		hit.z = 0f;
		return hit;
	}

	private int _colorIdx = 0;
	private Color NextColor()
	{
		// Cycle through palette pseudo-randomly.
		_colorIdx = ( _colorIdx + Game.Random.Int( 1, PaletteColors.Length - 1 ) ) % PaletteColors.Length;
		return PaletteColors[_colorIdx];
	}

	public void LaunchAt( Vector3 groundPos, Color color )
	{
		// Spawn a Firework GameObject.
		var go = Scene.CreateObject();
		go.Name = "Firework";
		go.WorldPosition = groundPos + Vector3.Up * 2f;

		var fw = go.Components.Create<Firework>();
		fw.BurstColor = color;
		fw.LaunchSound = LaunchSound;
		fw.BurstSound = BurstSound;
		fw.Speed = Game.Random.Float( 220f, 360f );
		fw.TimeToBurst = Game.Random.Float( 1.4f, 2.2f );
		fw.ParticleCount = Game.Random.Int( 60, 110 );
		fw.ParticleSpread = Game.Random.Float( 180f, 320f );
		fw.OnBurst = _ => FireworkCount++;

		// Scorch mark at the launch point.
		SpawnScorch( groundPos );
	}

	private void SpawnScorch( Vector3 groundPos )
	{
		if ( !SpawnScorchMarks ) return;

		// Preferred: project a DecalDefinition (the secondary gap).
		// Game-code reflection is whitelist-restricted (Type.GetProperty is
		// blocked), so we use the visual fallback below directly. The
		// DecalRenderer Component property name varies across engine
		// builds — discover the correct one out of band (via sbox-eval)
		// and wire ScorchDecal in the .scene file instead of via reflection
		// from game code.
		// Fallback: a thin dark disc using a scaled sphere.
		var go = Scene.CreateObject();
		go.Name = "Scorch";
		go.WorldPosition = groundPos + Vector3.Up * 0.5f;
		// scale a 32"-sphere into a flat disc ~24" diameter
		go.WorldScale = new Vector3( 24f / 32f, 24f / 32f, 0.05f );
		var mr = go.Components.Create<ModelRenderer>();
		mr.Model = Model.Load( "models/dev/sphere.vmdl" );
		mr.Tint = new Color( 0.05f, 0.04f, 0.03f, 1f );
		go.Components.Create<TimedDestroy>().Lifetime = 30f;
	}
}
