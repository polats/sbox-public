using Sandbox;
using Sandbox.Navigation;
using System;

namespace Woid;

/// <summary>
/// Bakes the scene's NavMesh at runtime so NavMeshAgents can move.
/// Pattern lifted from examples/npc-patrol/Code/GameManager.cs.
/// </summary>
public sealed class NavMeshBaker : Component
{
	public bool Ready { get; private set; }
	public string Status { get; private set; } = "baking navmesh…";

	protected override void OnStart()
	{
		base.OnStart();
		_ = BakeAsync();
	}

	async System.Threading.Tasks.Task BakeAsync()
	{
		try
		{
			var nm = Scene.NavMesh;
			nm.AgentRadius = 16;
			nm.AgentHeight = 64;
			nm.AgentStepSize = 18;
			nm.AgentMaxSlope = 40;
			await nm.Generate( Scene.PhysicsWorld );
			Ready = true;
			Status = "navmesh ready";
			Log.Info( "[NavMeshBaker] ready" );
		}
		catch ( Exception e )
		{
			Status = $"navmesh generate failed: {e.Message}";
			Log.Warning( $"[NavMeshBaker] {Status}" );
			Ready = true; // Don't block — fall back to agents not moving
		}
	}
}
