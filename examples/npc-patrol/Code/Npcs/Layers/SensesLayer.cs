using System;
using Sandbox;
using System.Collections.Generic;

namespace Local.NpcPatrol.Npcs.Layers;

/// <summary>
/// Handles environmental awareness — finds objects in scan range, tests line-of-sight,
/// and exposes hostile lists filtered by <see cref="TargetTags"/>.
///
/// Extends the sandbox SensesLayer with a sight-cone (FOV) test so the guard can
/// only see things in front of them.
/// </summary>
public sealed class SensesLayer : BaseNpcLayer
{
	[Property] public float SightRange { get; set; } = 600f;
	[Property] public float HearingRange { get; set; } = 300f;
	[Property, Range( 30, 180 )] public float SightConeDegrees { get; set; } = 90f;

	/// <summary>All tags the NPC scans for and caches.</summary>
	[Property] public TagSet ScanTags { get; set; } = new TagSet { "player" };

	/// <summary>Tags treated as hostile targets. <see cref="VisibleTargets"/> only contains these.</summary>
	[Property] public TagSet TargetTags { get; set; } = new TagSet { "player" };

	public float ScanInterval { get; set; } = 0.1f;

	public GameObject Nearest { get; private set; }
	public float DistanceToNearest { get; private set; } = float.MaxValue;
	public List<GameObject> VisibleTargets { get; private set; } = new();
	public List<GameObject> AudibleTargets { get; private set; } = new();

	private TimeSince _lastScan;

	protected override void OnUpdate()
	{
		if ( IsProxy ) return;
		if ( Npc is null ) return;

		if ( _lastScan > ScanInterval )
		{
			Scan();
			_lastScan = 0;
		}
	}

	public override string GetDebugString()
	{
		if ( VisibleTargets.Count == 0 && AudibleTargets.Count == 0 ) return null;
		return $"Senses: {VisibleTargets.Count} visible, {AudibleTargets.Count} audible";
	}

	private void Scan()
	{
		VisibleTargets.Clear();
		AudibleTargets.Clear();
		Nearest = null;
		DistanceToNearest = float.MaxValue;

		// NOTE: FindInPhysics only returns objects with a real Collider — the player has
		// a CharacterController which is NOT a Collider (see rules/animations.md), so it
		// would never be found. Iterate all GameObjects and filter by tag instead.
		var all = Npc.Scene.GetAllObjects( true );

		foreach ( var obj in all )
		{
			if ( !obj.IsValid() ) continue;
			// Skip inherited tags — only consider the top-level GameObject of each tagged hierarchy,
			// otherwise a single player produces ~40 detected bones at almost-identical positions.
			if ( obj.Root != obj ) continue;
			if ( !obj.Tags.HasAny( ScanTags ) ) continue;
			if ( !obj.Tags.HasAny( TargetTags ) ) continue;

			var distance = Npc.WorldPosition.Distance( obj.WorldPosition );

			// Audible if anywhere in the sphere
			if ( distance <= HearingRange )
				AudibleTargets.Add( obj );

			// Visible if in sight cone AND has line of sight
			if ( distance <= SightRange && IsInSightCone( obj ) && HasLineOfSight( obj ) )
			{
				VisibleTargets.Add( obj );

				if ( distance < DistanceToNearest )
				{
					DistanceToNearest = distance;
					Nearest = obj;
				}
			}
		}
	}

	private bool IsInSightCone( GameObject target )
	{
		var forward = Npc.WorldRotation.Forward.WithZ( 0 ).Normal;
		var toTarget = (target.WorldPosition - Npc.WorldPosition).WithZ( 0 ).Normal;
		var cosHalfAngle = MathF.Cos( (SightConeDegrees * 0.5f) * MathF.PI / 180f );
		return Vector3.Dot( forward, toTarget ) >= cosHalfAngle;
	}

	private bool HasLineOfSight( GameObject target )
	{
		var eye = Npc.WorldPosition + Vector3.Up * 64f;
		var aim = target.WorldPosition + Vector3.Up * 40f;

		var trace = Npc.Scene.Trace.Ray( eye, aim )
			.IgnoreGameObjectHierarchy( Npc.GameObject )
			.WithoutTags( "trigger" )
			.Run();

		return !trace.Hit || trace.GameObject == target || target.IsDescendant( trace.GameObject );
	}

	public GameObject GetNearestVisible()
	{
		GameObject nearest = null;
		float best = float.MaxValue;
		foreach ( var obj in VisibleTargets )
		{
			var d = Npc.WorldPosition.Distance( obj.WorldPosition );
			if ( d < best ) { best = d; nearest = obj; }
		}
		return nearest;
	}

	public override void ResetLayer()
	{
		VisibleTargets.Clear();
		AudibleTargets.Clear();
		Nearest = null;
		DistanceToNearest = float.MaxValue;
	}
}
