using Sandbox;

namespace Woid;

/// <summary>
/// Canonical sit pattern from engine BaseChair.cs:
///   character.GameObject.SetParent(SeatPosition, false);
///   character.LocalTransform = Transform.Zero;
///   renderer.Set("sit", (int)SitPose);
/// On stand, SetParent(null, true) preserves world transform; we then warp
/// the character to ExitPosition and re-enable its NavMeshAgent.
/// </summary>
public sealed class Chair : Component
{
	/// <summary>Child GameObject placed where the citizen's pelvis goes.</summary>
	[Property] public GameObject SeatPosition { get; set; }

	/// <summary>Where the character stands after standing up. Defaults to chair pos + Forward * 30.</summary>
	[Property] public GameObject ExitPosition { get; set; }

	/// <summary>Animgraph sit_pose enum. 1=Chair, 2=ChairForward, 3=ChairCrossed.</summary>
	[Property] public int SitPose { get; set; } = 1;

	/// <summary>Vertical fine-tune (inches). Multiplied by 12 into sit_offset_height.</summary>
	[Property] public float SitHeight { get; set; } = 0f;

	public string Occupant { get; private set; }

	public bool Sit( Character c )
	{
		if ( c == null || !string.IsNullOrEmpty( Occupant ) ) return false;
		if ( SeatPosition == null )
		{
			Log.Warning( $"[Chair {GameObject.Name}] SeatPosition not set" );
			return false;
		}

		// Stop and freeze the NavMeshAgent so it doesn't pull the character
		// away while parented to the chair.
		var agent = c.Components.Get<NavMeshAgent>();
		if ( agent.IsValid() )
		{
			agent.Stop();
			agent.UpdatePosition = false;
			agent.UpdateRotation = false;
		}

		c.GameObject.SetParent( SeatPosition, false );
		c.LocalPosition = Vector3.Zero;
		c.LocalRotation = Rotation.Identity;

		if ( c.Model.IsValid() )
		{
			c.Model.LocalRotation = Rotation.Identity;
			c.Model.Set( "sit", SitPose );
			c.Model.Set( "sit_offset_height", SitHeight * 12.0f );
			c.Model.Set( "b_sit", true );
		}

		Occupant = c.CharacterId;
		c.SetSitting( true, this );
		return true;
	}

	public void Stand( Character c )
	{
		if ( c == null ) return;
		if ( !string.IsNullOrEmpty( Occupant ) && Occupant != c.CharacterId ) return;

		c.GameObject.SetParent( null, true );

		if ( c.Model.IsValid() )
		{
			c.Model.Set( "sit", 0 );
			c.Model.Set( "b_sit", false );
		}

		// Warp out to the exit spot
		if ( ExitPosition.IsValid() )
			c.WorldPosition = ExitPosition.WorldPosition;
		else
			c.WorldPosition = WorldPosition + GameObject.WorldRotation.Forward * 30f;

		var agent = c.Components.Get<NavMeshAgent>();
		if ( agent.IsValid() )
		{
			agent.UpdatePosition = true;
			agent.UpdateRotation = true;
		}

		Occupant = null;
		c.SetSitting( false, null );
	}
}
