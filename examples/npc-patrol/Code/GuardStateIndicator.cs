using Sandbox;
using System.Linq;

namespace Local.NpcPatrol;

/// <summary>
/// Floating colored sphere above the guard's head — driven each frame from
/// <see cref="GuardNpc.StateColor"/>. Placed in the scene as a child GameObject
/// with a ModelRenderer (sphere) that this component re-tints.
/// </summary>
public sealed class GuardStateIndicator : Component
{
	[Property] public GuardNpc Guard { get; set; }
	[Property] public ModelRenderer Renderer { get; set; }

	protected override void OnStart()
	{
		Guard ??= GameObject.Parent?.GetComponent<GuardNpc>()
			?? Scene.GetAllComponents<GuardNpc>().FirstOrDefault();
		Renderer ??= GetComponent<ModelRenderer>();
	}

	protected override void OnUpdate()
	{
		if ( Guard is null || Renderer is null ) return;
		Renderer.Tint = Guard.StateColor;
	}
}
