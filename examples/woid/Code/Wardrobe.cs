using Sandbox;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Woid;

/// <summary>
/// Per-character clothing manager. Wraps the Sandbox <c>ClothingContainer</c>
/// pattern from examples/character-customizer: discovers all Clothing
/// resources, buckets them into 5 UI slots (hat/glasses/jacket/pants/shoes),
/// and lets a UI cycle through them or apply named presets.
///
/// Sits on the same GameObject as <see cref="Character"/>; finds the
/// SkinnedModelRenderer through the Character.
/// </summary>
public sealed class Wardrobe : Component
{
	public static readonly List<string> SlotOrder = new() { "hat", "glasses", "jacket", "pants", "shoes" };

	public Dictionary<string, List<Clothing>> BySlot { get; } = new();
	public Dictionary<string, int> SlotIndex { get; } = new();
	public ClothingContainer Container { get; private set; } = new();
	public string CurrentOutfitName { get; private set; } = "Default";

	SkinnedModelRenderer _body;

	protected override void OnStart()
	{
		base.OnStart();
		_body = Components.Get<SkinnedModelRenderer>();
		if ( !_body.IsValid() ) { Log.Warning( "[Wardrobe] no SkinnedModelRenderer on this GO" ); return; }

		foreach ( var slot in SlotOrder )
		{
			BySlot[slot] = new List<Clothing>();
			SlotIndex[slot] = -1;
		}

		foreach ( var c in ResourceLibrary.GetAll<Clothing>() )
		{
			var slot = SlotForClothing( c );
			if ( slot != null ) BySlot[slot].Add( c );
		}

		Log.Info( $"[Wardrobe {GameObject.Name}] loaded clothing: " +
			string.Join( ", ", SlotOrder.Select( s => $"{s}={BySlot[s].Count}" ) ) );

		ApplyContainer();
	}

	static string SlotForClothing( Clothing c )
	{
		if ( c == null ) return null;
		var cat = c.Category.ToString().ToLowerInvariant();
		if ( cat.StartsWith( "hat" ) || cat == "headwear" || cat == "headtech" || cat == "headband" ) return "hat";
		if ( cat.StartsWith( "glasses" ) ) return "glasses";
		if ( cat == "jacket" || cat == "hoodie" || cat == "coat" || cat == "vest" || cat == "cardigan"
		  || cat == "tops" || cat == "tshirt" || cat == "shirt" || cat == "sweatshirt" || cat == "knitwear" ) return "jacket";
		if ( cat == "trousers" || cat == "jeans" || cat == "shorts" || cat == "bottoms" ) return "pants";
		if ( cat == "shoes" || cat == "boots" || cat == "trainers" || cat == "sneakers"
		  || cat == "sandals" || cat == "heels" || cat == "footwear" ) return "shoes";
		return null;
	}

	public void CycleSlot( string slot, int dir = 1 )
	{
		if ( !BySlot.TryGetValue( slot, out var list ) || list.Count == 0 ) return;
		var cur = SlotIndex[slot];
		var n = list.Count + 1; // includes -1 "none"
		var pos = (cur + 1) + dir;
		pos = ((pos % n) + n) % n;
		SlotIndex[slot] = pos - 1;
		ApplyContainer();
	}

	public void ClearAll()
	{
		foreach ( var s in SlotOrder ) SlotIndex[s] = -1;
		CurrentOutfitName = "Naked";
		ApplyContainer();
	}

	public void ApplyPreset( int idx )
	{
		idx = ((idx % 5) + 5) % 5;
		foreach ( var s in SlotOrder ) SlotIndex[s] = -1;

		switch ( idx )
		{
			case 0:
				CurrentOutfitName = "Default";
				break;
			case 1:
				CurrentOutfitName = "Casual";
				SelectByTitleContains( "hat",    "Cowboy" );
				SelectByTitleContains( "jacket", "Bomber" );
				SelectByTitleContains( "pants",  "Jeans" );
				SelectByTitleContains( "shoes",  "Sneakers" );
				break;
			case 2:
				CurrentOutfitName = "Formal";
				SelectByTitleContains( "glasses", "Nerdy" );
				SelectByTitleContains( "jacket",  "Hoodie" );
				SelectByTitleContains( "pants",   "Jeans" );
				SelectByTitleContains( "shoes",   "Boots" );
				break;
			case 3:
				CurrentOutfitName = "Athletic";
				SelectByTitleContains( "jacket", "Hoodie" );
				SelectByTitleContains( "pants",  "Trackie" );
				SelectByTitleContains( "shoes",  "Sneakers" );
				break;
			case 4:
				CurrentOutfitName = "Random";
				var rng = new Random();
				foreach ( var s in SlotOrder )
				{
					var list = BySlot[s];
					if ( list.Count == 0 ) continue;
					if ( rng.NextDouble() < 0.7 ) SlotIndex[s] = rng.Next( 0, list.Count );
				}
				break;
		}

		ApplyContainer();
	}

	public string CurrentName( string slot )
	{
		if ( !BySlot.TryGetValue( slot, out var list ) ) return "—";
		var i = SlotIndex[slot];
		if ( i < 0 || i >= list.Count ) return "(none)";
		return list[i].Title ?? list[i].ResourceName ?? "?";
	}

	void SelectByTitleContains( string slot, string substr )
	{
		if ( !BySlot.TryGetValue( slot, out var list ) ) return;
		var i = list.FindIndex( c => c.Title != null && c.Title.Contains( substr, StringComparison.OrdinalIgnoreCase ) );
		if ( i < 0 && list.Count > 0 ) i = 0;
		SlotIndex[slot] = i;
	}

	void ApplyContainer()
	{
		if ( !_body.IsValid() ) return;
		Container = new ClothingContainer();
		foreach ( var slot in SlotOrder )
		{
			var idx = SlotIndex[slot];
			if ( idx < 0 ) continue;
			var list = BySlot[slot];
			if ( idx >= list.Count ) continue;
			Container.Add( list[idx] );
		}
		Container.Normalize();
		Container.Apply( _body );
	}
}
