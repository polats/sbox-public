using Sandbox;
using Sandbox.Navigation;
using System;
using System.Linq;

namespace Local.NpcPatrol;

/// <summary>
/// Bakes the navmesh at runtime, then enables the guard. Surfaces a one-line
/// status the HUD reads.
/// </summary>
public sealed class GameManager : Component
{
	[Property] public bool BakeAtRuntime { get; set; } = true;

	public string Status { get; private set; } = "Bringing up navmesh…";
	public GuardNpc Guard { get; private set; }

	private bool _navMeshReady;

	protected override void OnStart()
	{
		base.OnStart();
		Guard = Scene.GetAllComponents<GuardNpc>().FirstOrDefault();
		// Don't disable the guard; the NavMeshAgent simply won't move until the navmesh is up,
		// and the Npc's schedule will retry each tick. This lets the layers tick safely.

		if ( BakeAtRuntime ) KickNavMeshGen();
		else _navMeshReady = Scene.NavMesh.IsEnabled;
	}

	private async void KickNavMeshGen()
	{
		try
		{
			var nm = Scene.NavMesh;
			nm.AgentRadius = 16;
			nm.AgentHeight = 64;
			nm.AgentStepSize = 18;
			nm.AgentMaxSlope = 40;
			Status = "Baking navmesh…";
			await nm.Generate( Scene.PhysicsWorld );
			Status = "Patrol running.";
			_navMeshReady = true;
			Log.Info( "[NpcPatrol] NavMesh ready." );
		}
		catch ( Exception e )
		{
			Log.Warning( $"[NpcPatrol] NavMesh generate failed: {e.Message}" );
			_navMeshReady = true;
		}
	}

	protected override void OnUpdate()
	{
		if ( !_navMeshReady ) return;
		if ( !Guard.IsValid() ) { Status = "(no guard)"; return; }
		Status = $"Guard: {Guard.State}";
	}
}
