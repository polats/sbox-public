using Sandbox;
using System.Collections.Generic;

namespace Local.WorkshopBrowser;

/// <summary>
/// A lightweight DTO describing one browsable workshop entry — either a real
/// sbox.game Package fetched via Package.FindAsync, or a simulated local fallback
/// for when the cloud backend isn't authenticated/reachable from the sandbox.
/// </summary>
public sealed class WorkshopPackageInfo
{
	public string Ident { get; set; }
	public string Title { get; set; }
	public string Author { get; set; }
	public string Description { get; set; }
	public string IconGlyph { get; set; } = "inventory_2";
	public Color TintHint { get; set; } = Color.White;

	// True if we mounted this successfully (cloud) or "activated" it (simulated).
	public bool IsMounted { get; set; }

	// Sources we can spawn from once mounted.
	// For real cloud packages, populated from heuristic model paths.
	// For simulated, populated by us with built-in engine models so the demo always works.
	public List<WorkshopAsset> Assets { get; set; } = new();

	// Marker for "this entry is faked because backend was unreachable / unauth'd".
	public bool IsSimulated { get; set; }
}

public sealed class WorkshopAsset
{
	public string Name { get; set; }
	public string ModelPath { get; set; }
	public Color Tint { get; set; } = Color.White;
}
