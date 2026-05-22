using Sandbox;
using System.Collections.Generic;

namespace Local.FpsWeapons;

/// <summary>
/// Owns the 3 weapon instances + tracks the active slot.
/// </summary>
public sealed class PlayerInventory : Component
{
	public List<BaseWeapon> Weapons { get; } = new();
	public int ActiveIndex { get; private set; } = -1;

	public BaseWeapon Active => (ActiveIndex >= 0 && ActiveIndex < Weapons.Count) ? Weapons[ActiveIndex] : null;

	public void Add( BaseWeapon w )
	{
		Weapons.Add( w );
	}

	public void Switch( int slot )
	{
		if ( slot < 0 || slot >= Weapons.Count ) return;
		if ( slot == ActiveIndex ) return;
		if ( Active != null ) Active.Holster();
		ActiveIndex = slot;
		Active?.Deploy();
	}
}
