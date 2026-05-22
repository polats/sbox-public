using Sandbox;

namespace Local.NpcPatrol.Npcs.Layers;

/// <summary>
/// A behavior layer provides specific services for tasks to use — we don't use behavior
/// layers for state, they are services attached to the same GameObject as the Npc.
///
/// Direct port of the design in sandbox/Code/Npcs/Layers/BaseNpcLayer.cs.
/// </summary>
public abstract class BaseNpcLayer : Component
{
	private Npc _npc;

	/// <summary>The owning NPC on the same GameObject.</summary>
	protected Npc Npc
	{
		get
		{
			_npc ??= GetComponent<Npc>();
			return _npc;
		}
	}

	/// <summary>Optional debug string shown in the NPC debug overlay. Return null to skip.</summary>
	public virtual string GetDebugString() => null;

	/// <summary>Reset any runtime state on this layer.</summary>
	public virtual void ResetLayer() { }
}
