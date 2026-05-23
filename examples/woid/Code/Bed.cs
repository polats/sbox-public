using Sandbox;

namespace Woid;

/// <summary>
/// A bed-like surface. The citizen rig has no lying-prone animation, so this
/// fakes it with `sit = Floor` + `sit_pose = 2` (forward lean) for a passable
/// "rested" pose. Snap-pattern matches Chair: SetParent to RestPosition +
/// LocalTransform.Zero. Stand() returns to normal.
/// </summary>
public sealed class Bed : Component
{
	[Property] public GameObject RestPosition { get; set; }
	[Property] public GameObject ExitPosition { get; set; }

	/// <summary>animgraph sit enum. 2 = Floor.</summary>
	[Property] public int SitPose { get; set; } = 2;

	/// <summary>animgraph sit_pose float. 2 = forward lean for a flatter pose.</summary>
	[Property] public float SitPoseFloat { get; set; } = 2f;

	[Property] public float SitHeight { get; set; } = 0f;

	public string Occupant { get; private set; }

	public bool Sleep( Character c )
	{
		if ( c == null || !string.IsNullOrEmpty( Occupant ) ) return false;
		if ( RestPosition == null )
		{
			Log.Warning( $"[Bed {GameObject.Name}] RestPosition not set" );
			return false;
		}

		var agent = c.Components.Get<NavMeshAgent>();
		if ( agent.IsValid() )
		{
			agent.Stop();
			agent.UpdatePosition = false;
			agent.UpdateRotation = false;
		}

		c.GameObject.SetParent( RestPosition, false );
		c.LocalPosition = Vector3.Zero;
		c.LocalRotation = Rotation.Identity;

		if ( c.Model.IsValid() )
		{
			c.Model.LocalRotation = Rotation.Identity;
			c.Model.Set( "sit", SitPose );
			c.Model.Set( "sit_pose", SitPoseFloat );
			c.Model.Set( "sit_offset_height", SitHeight * 12.0f );
			c.Model.Set( "b_sit", true );
			c.Model.Set( "eyes_closed", 1.0f );   // sleeping eyes
		}

		Occupant = c.CharacterId;
		c.SetSitting( true, null ); // pass null chair so Chair-specific code doesn't fire
		c.SetOccupiedBed( this );
		return true;
	}

	public void Wake( Character c )
	{
		if ( c == null ) return;
		if ( !string.IsNullOrEmpty( Occupant ) && Occupant != c.CharacterId ) return;

		c.GameObject.SetParent( null, true );

		if ( c.Model.IsValid() )
		{
			c.Model.Set( "sit", 0 );
			c.Model.Set( "b_sit", false );
			c.Model.Set( "eyes_closed", 0f );
		}

		if ( ExitPosition.IsValid() )
			c.WorldPosition = ExitPosition.WorldPosition;
		else
			c.WorldPosition = WorldPosition + GameObject.WorldRotation.Forward * 40f;

		var agent = c.Components.Get<NavMeshAgent>();
		if ( agent.IsValid() )
		{
			agent.UpdatePosition = true;
			agent.UpdateRotation = true;
		}

		Occupant = null;
		c.SetSitting( false, null );
		c.SetOccupiedBed( null );
	}
}
