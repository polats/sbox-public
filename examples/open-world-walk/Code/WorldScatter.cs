using Sandbox;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Local.OpenWorldWalk;

/// <summary>
/// Scatters trees, rocks and grass clumps across the terrain at startup.
/// Spawns plain GameObjects with ModelRenderer components — no real
/// ClutterComponent exists at runtime in s&box (only an editor tool),
/// so we hand-scatter and let the engine's frustum culling handle perf.
/// </summary>
public sealed class WorldScatter : Component
{
	[Property] public int TreeCount { get; set; } = 8;
	[Property] public int RockCount { get; set; } = 15;
	[Property] public int GrassCount { get; set; } = 50;

	[Property] public float WorldRadius { get; set; } = 850f;
	[Property] public int Seed { get; set; } = 1337;

	private const string TreeModel = "models/sbox_props/trees/oak/tree_oak_big_a.vmdl";
	private const string RockModel = "models/props/rock_scatter/rock_scatter_01.vmdl";
	private const string GrassModel = "models/sbox_props/nature/grass_clumps/grass_clump_a.vmdl";

	protected override void OnStart()
	{
		base.OnStart();

		var rng = new Random( Seed );

		Scatter( TreeModel, TreeCount, rng, scaleMin: 0.04f, scaleMax: 0.08f, minDistFromOrigin: 120f );
		Scatter( RockModel, RockCount, rng, scaleMin: 0.6f, scaleMax: 1.4f, minDistFromOrigin: 60f );
		Scatter( GrassModel, GrassCount, rng, scaleMin: 0.8f, scaleMax: 1.6f, minDistFromOrigin: 30f );
	}

	private void Scatter( string modelPath, int count, Random rng, float scaleMin, float scaleMax, float minDistFromOrigin )
	{
		var model = Model.Load( modelPath );
		if ( model == null )
		{
			Log.Warning( $"[WorldScatter] failed to load {modelPath}" );
			return;
		}

		for ( int i = 0; i < count; i++ )
		{
			// Rejection-sample inside circle
			Vector3 pos;
			int tries = 0;
			do
			{
				float x = (float)(rng.NextDouble() * 2 - 1) * WorldRadius;
				float y = (float)(rng.NextDouble() * 2 - 1) * WorldRadius;
				pos = new Vector3( x, y, 0 );
				tries++;
			}
			while ( (pos.WithZ( 0 ).Length < minDistFromOrigin || pos.WithZ( 0 ).Length > WorldRadius) && tries < 20 );

			// Sample terrain height
			float gz = SampleGroundHeight( pos );
			pos.z = gz;

			var go = Scene.CreateObject();
			go.Name = $"{System.IO.Path.GetFileNameWithoutExtension( modelPath )}_{i}";
			go.WorldPosition = pos;
			go.WorldRotation = Rotation.FromYaw( (float)rng.NextDouble() * 360f );
			float s = scaleMin + (float)rng.NextDouble() * (scaleMax - scaleMin);
			go.WorldScale = new Vector3( s, s, s );
			go.SetParent( GameObject );

			var mr = go.Components.Create<ModelRenderer>();
			mr.Model = model;
		}
	}

	private float SampleGroundHeight( Vector3 pos )
	{
		var from = pos + Vector3.Up * 2000f;
		var to = pos - Vector3.Up * 2000f;
		var tr = Scene.Trace.Ray( from, to ).Run();
		return tr.Hit ? tr.HitPosition.z : 0f;
	}
}
