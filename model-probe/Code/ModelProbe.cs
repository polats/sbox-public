using Sandbox;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Local.ModelProbe;

/// <summary>
/// Loads each model in ModelPaths and logs its bounding box in a parseable
/// format. Used by tools/sbox-model-info to verify real model dimensions
/// (s&box has no built-in model-info ConCmd, so we use the engine itself
/// as ground truth via Model.Load + Model.Bounds).
///
/// Log format (one line per model):
///   BOUNDS_PROBE <path> mins=x,y,z maxs=x,y,z size=x,y,z
/// Or on failure:
///   BOUNDS_PROBE <path> ERROR=<reason>
/// And once everything is logged:
///   BOUNDS_PROBE_DONE
/// </summary>
public sealed class ModelProbe : Component
{
	[Property] public List<string> ModelPaths { get; set; } = new();

	private bool _ran;

	// OnEnabled fires when the component becomes enabled, including when the
	// scene loads in the editor (no play required). OnStart only fires in
	// play mode. Use OnEnabled so the probe runs as soon as the editor
	// opens the scene.
	protected override void OnEnabled()
	{
		base.OnEnabled();
		Run();
	}

	protected override void OnStart()
	{
		base.OnStart();
		Run();
	}

	private void Run()
	{
		if ( _ran ) return;
		_ran = true;

		Log.Info( $"BOUNDS_PROBE_START count={ModelPaths.Count}" );

		foreach ( var path in ModelPaths.Where( p => !string.IsNullOrWhiteSpace( p ) ) )
		{
			try
			{
				var m = Model.Load( path );
				if ( m == null )
				{
					Log.Info( $"BOUNDS_PROBE {path} ERROR=ModelNull" );
					continue;
				}
				var b = m.Bounds;
				Log.Info( $"BOUNDS_PROBE {path} mins={b.Mins.x:F2},{b.Mins.y:F2},{b.Mins.z:F2} maxs={b.Maxs.x:F2},{b.Maxs.y:F2},{b.Maxs.z:F2} size={b.Size.x:F2},{b.Size.y:F2},{b.Size.z:F2}" );
			}
			catch ( Exception ex )
			{
				Log.Info( $"BOUNDS_PROBE {path} ERROR={ex.GetType().Name}:{ex.Message}" );
			}
		}

		Log.Info( "BOUNDS_PROBE_DONE" );
	}
}
