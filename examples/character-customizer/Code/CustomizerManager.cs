using Sandbox;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Local.CharacterCustomizer;

/// <summary>
/// Drives the Character Customizer demo:
/// - Loads all Clothing GameResources from Assets/clothing/.
/// - Holds a ClothingContainer + applies it to the citizen's SkinnedModelRenderer.
/// - Orbits the camera around the citizen (turntable).
/// - Handles preset outfit selection (1-5), cycling clothing per slot, bodygroup toggles.
/// - AutoPlay cycles through presets every AutoInterval seconds for the verification video.
/// </summary>
public sealed class CustomizerManager : Component
{
	[Property] public bool AutoPlay { get; set; } = false;
	[Property, Range( 0.5f, 8f )] public float AutoInterval { get; set; } = 2.5f;

	[Property] public SkinnedModelRenderer BodyRenderer { get; set; }
	[Property] public CameraComponent OrbitCamera { get; set; }
	[Property] public GameObject CitizenRoot { get; set; }

	[Property, Range( 100f, 500f )] public float OrbitRadius { get; set; } = 220f;
	[Property, Range( 30f, 200f )] public float OrbitHeight { get; set; } = 80f;
	[Property, Range( 1f, 90f )] public float OrbitSpeed { get; set; } = 18f;

	/// <summary>
	/// All clothing resources available, indexed by slot key (hat/glasses/jacket/pants/shoes).
	/// </summary>
	public Dictionary<string, List<Clothing>> BySlot { get; private set; } = new();
	public List<string> SlotOrder { get; } = new() { "hat", "glasses", "jacket", "pants", "shoes" };

	/// <summary>
	/// Currently-selected index per slot. -1 means "none worn".
	/// </summary>
	public Dictionary<string, int> SlotIndex { get; private set; } = new();

	public ClothingContainer Container { get; private set; } = new();

	public string CurrentOutfitName { get; private set; } = "Default";
	public bool HideHead { get; private set; }
	public bool HideChest { get; private set; }

	private float _orbitAngle = 0f;
	private float _autoTimer = 0f;
	private int _autoPresetIndex = 0;

	protected override void OnStart()
	{
		base.OnStart();

		// Discover clothing resources, then bucket them by slot.
		var all = ResourceLibrary.GetAll<Clothing>().ToList();
		foreach ( var slot in SlotOrder )
		{
			BySlot[slot] = new List<Clothing>();
			SlotIndex[slot] = -1;
		}

		foreach ( var c in all )
		{
			var slot = SlotForClothing( c );
			if ( slot == null ) continue;
			BySlot[slot].Add( c );
		}

		Log.Info( $"[Customizer] Loaded {all.Count} clothing resources. Per-slot: " +
			string.Join( ", ", SlotOrder.Select( s => $"{s}={BySlot[s].Count}" ) ) );

		ApplyContainer();
	}

	/// <summary>
	/// Classify a Clothing resource into one of our 5 UI slots. Falls back to null
	/// if the category doesn't map to a slot we care about.
	/// </summary>
	private static string SlotForClothing( Clothing c )
	{
		if ( c == null ) return null;
		var cat = c.Category.ToString().ToLowerInvariant();
		if ( cat.StartsWith( "hat" ) || cat == "headwear" || cat == "headtech" || cat == "headband" )
			return "hat";
		if ( cat.StartsWith( "glasses" ) )
			return "glasses";
		if ( cat == "jacket" || cat == "hoodie" || cat == "coat" || cat == "vest" || cat == "cardigan" || cat == "tops" || cat == "tshirt" || cat == "shirt" || cat == "sweatshirt" || cat == "knitwear" )
			return "jacket";
		if ( cat == "trousers" || cat == "jeans" || cat == "shorts" || cat == "bottoms" )
			return "pants";
		if ( cat == "shoes" || cat == "boots" || cat == "trainers" || cat == "sneakers" || cat == "sandals" || cat == "heels" || cat == "footwear" )
			return "shoes";
		return null;
	}

	protected override void OnUpdate()
	{
		// Orbit the camera around the citizen.
		var citizenPos = (CitizenRoot.IsValid() ? CitizenRoot.WorldPosition : Vector3.Zero) + Vector3.Up * OrbitHeight;
		_orbitAngle += OrbitSpeed * Time.Delta;
		if ( _orbitAngle > 360f ) _orbitAngle -= 360f;

		var rad = _orbitAngle * MathF.PI / 180f;
		var camPos = citizenPos + new Vector3( MathF.Cos( rad ) * OrbitRadius, MathF.Sin( rad ) * OrbitRadius, 0 );

		if ( OrbitCamera.IsValid() )
		{
			OrbitCamera.WorldPosition = camPos;
			OrbitCamera.WorldRotation = Rotation.LookAt( (citizenPos - camPos).Normal );
		}

		// Number keys 1-5 → presets.
		for ( int i = 0; i < 5; i++ )
		{
			if ( Input.Pressed( $"Slot{i + 1}" ) )
				ApplyPreset( i );
		}

		if ( AutoPlay )
		{
			_autoTimer += Time.Delta;
			if ( _autoTimer >= AutoInterval )
			{
				_autoTimer = 0f;
				ApplyPreset( _autoPresetIndex % 5 );
				_autoPresetIndex++;
			}
		}
	}

	// ---------------- Outfit changes ----------------

	public void CycleSlot( string slot, int dir = 1 )
	{
		if ( !BySlot.ContainsKey( slot ) ) return;
		var list = BySlot[slot];
		if ( list.Count == 0 ) return;

		var cur = SlotIndex[slot];
		// cycle through: -1 (none), 0, 1, ... count-1, back to -1.
		var n = list.Count + 1; // includes "none"
		var pos = (cur + 1) + dir; // shift so none = 0
		pos = ((pos % n) + n) % n;
		SlotIndex[slot] = pos - 1;

		ApplyContainer();
	}

	public void SetSlot( string slot, int index )
	{
		if ( !BySlot.ContainsKey( slot ) ) return;
		SlotIndex[slot] = Math.Clamp( index, -1, BySlot[slot].Count - 1 );
		ApplyContainer();
	}

	public void ClearAll()
	{
		foreach ( var s in SlotOrder ) SlotIndex[s] = -1;
		CurrentOutfitName = "Default";
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
				SelectByTitleContains( "hat", "Cowboy" );
				SelectByTitleContains( "jacket", "Bomber" );
				SelectByTitleContains( "pants", "Jeans" );
				SelectByTitleContains( "shoes", "Sneakers" );
				break;
			case 2:
				CurrentOutfitName = "Formal";
				SelectByTitleContains( "glasses", "Nerdy" );
				SelectByTitleContains( "jacket", "Hoodie" );
				SelectByTitleContains( "pants", "Jeans" );
				SelectByTitleContains( "shoes", "Boots" );
				break;
			case 3:
				CurrentOutfitName = "Athletic";
				SelectByTitleContains( "jacket", "Hoodie" );
				SelectByTitleContains( "pants", "Trackie" );
				SelectByTitleContains( "shoes", "Sneakers" );
				break;
			case 4:
				CurrentOutfitName = "Random";
				var rng = new Random();
				foreach ( var s in SlotOrder )
				{
					var list = BySlot[s];
					if ( list.Count == 0 ) continue;
					// 70% chance of wearing something in this slot.
					if ( rng.NextDouble() < 0.7 )
						SlotIndex[s] = rng.Next( 0, list.Count );
				}
				break;
		}

		ApplyContainer();
	}

	private void SelectByTitleContains( string slot, string substr )
	{
		if ( !BySlot.TryGetValue( slot, out var list ) ) return;
		var i = list.FindIndex( c => c.Title != null && c.Title.Contains( substr, StringComparison.OrdinalIgnoreCase ) );
		if ( i < 0 && list.Count > 0 ) i = 0;
		SlotIndex[slot] = i;
	}

	private void ApplyContainer()
	{
		if ( !BodyRenderer.IsValid() ) return;

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
		Container.Apply( BodyRenderer );

		// Bodygroups are reset by Apply (it manages them based on clothing.HideBody),
		// so re-apply our toggles afterward.
		ApplyBodygroups();
	}

	// ---------------- Bodygroup toggles ----------------

	public void ToggleHideHead()
	{
		HideHead = !HideHead;
		ApplyBodygroups();
	}

	public void ToggleHideChest()
	{
		HideChest = !HideChest;
		ApplyBodygroups();
	}

	private void ApplyBodygroups()
	{
		if ( !BodyRenderer.IsValid() ) return;
		BodyRenderer.SetBodyGroup( "head", HideHead ? 1 : 0 );
		BodyRenderer.SetBodyGroup( "chest", HideChest ? 1 : 0 );
	}
}
