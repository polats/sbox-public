using System.Collections.Generic;
using Sandbox;

namespace Woid;

/// <summary>
/// Maps woid-side character ids to local Character components.
/// EffectInterpreter looks up actors via this registry.
/// </summary>
public sealed class CharacterRegistry : Component
{
	readonly Dictionary<string, Character> _byId = new();

	protected override void OnStart()
	{
		base.OnStart();
		// Index any Characters that are already in the scene
		foreach ( var c in Scene.GetAllComponents<Character>() )
		{
			if ( !string.IsNullOrEmpty( c.CharacterId ) ) _byId[c.CharacterId] = c;
		}
	}

	public Character Get( string id ) => _byId.GetValueOrDefault( id );

	public void Register( Character c )
	{
		if ( string.IsNullOrEmpty( c?.CharacterId ) ) return;
		_byId[c.CharacterId] = c;
	}

	public IEnumerable<Character> All() => _byId.Values;
}
