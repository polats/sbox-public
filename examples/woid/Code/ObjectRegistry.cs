using System.Collections.Generic;
using Sandbox;

namespace Woid;

/// <summary>One smart-object instance in the scene.</summary>
public sealed class WoidObject : Component
{
	[Property] public string ObjectId { get; set; }
	[Property] public string Type { get; set; }

	/// <summary>Set by Occupy effect; null = free.</summary>
	public string Occupant { get; set; }
}

/// <summary>Maps woid-side object ids to local WoidObject components.</summary>
public sealed class ObjectRegistry : Component
{
	readonly Dictionary<string, WoidObject> _byId = new();

	protected override void OnStart()
	{
		base.OnStart();
		foreach ( var o in Scene.GetAllComponents<WoidObject>() )
		{
			if ( !string.IsNullOrEmpty( o.ObjectId ) ) _byId[o.ObjectId] = o;
		}
	}

	public WoidObject Get( string id ) => _byId.GetValueOrDefault( id );
	public IEnumerable<WoidObject> All() => _byId.Values;
}
