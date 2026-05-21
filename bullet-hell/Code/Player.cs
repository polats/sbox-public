using Sandbox;

namespace Local.BulletHell;

/// <summary>
/// Top-down player. Moves on the XY plane and fires bullets along +Y.
/// </summary>
public sealed class Player : Component
{
	[Property] public float MoveSpeed { get; set; } = 350f;
	[Property] public float FireInterval { get; set; } = 0.12f;
	[Property] public float BulletSpeed { get; set; } = 900f;
	[Property] public GameObject BulletPrefab { get; set; }

	[Sync] public int Score { get; set; }
	[Sync] public int Health { get; set; } = 3;

	private TimeSince _timeSinceFire;

	protected override void OnStart()
	{
		base.OnStart();
		_timeSinceFire = FireInterval;
		// Trigger collider for hit detection (added at runtime to keep scene JSON simple).
		if ( Components.Get<BoxCollider>() is null )
		{
			var col = Components.Create<BoxCollider>();
			col.IsTrigger = true;
			col.Scale = new Vector3( 60, 60, 60 );
		}
	}

	protected override void OnUpdate()
	{
		if ( Health <= 0 ) return;

		// 2D top-down movement on the XY plane.
		var dir = Vector3.Zero;
		if ( Input.Down( "Forward" ) )  dir += Vector3.Forward;   // +X away from origin in s&box, used as "up" on map
		if ( Input.Down( "Backward" ) ) dir += Vector3.Backward;
		if ( Input.Down( "Left" ) )     dir += Vector3.Left;
		if ( Input.Down( "Right" ) )    dir += Vector3.Right;

		if ( !dir.IsNearZeroLength )
		{
			WorldPosition += dir.Normal * MoveSpeed * Time.Delta;
		}

		// Auto-fire while attack1 is held, or a single-shot tap.
		var firing = Input.Down( "Attack1" ) || Input.Pressed( "Attack1" );
		if ( firing && _timeSinceFire >= FireInterval )
		{
			Fire();
			_timeSinceFire = 0f;
		}
	}

	private void Fire()
	{
		var go = BulletPrefab is not null
			? BulletPrefab.Clone( WorldPosition )
			: new GameObject( true, "Bullet" );

		go.WorldPosition = WorldPosition;

		var bullet = go.Components.GetOrCreate<Bullet>();
		bullet.Velocity = Vector3.Forward * BulletSpeed;   // travels +X ("up" on the top-down plane)
		bullet.Friendly = true;
	}

	public void TakeDamage( int amount )
	{
		Health -= amount;
		if ( Health <= 0 )
		{
			// Game over - just freeze; a proper restart flow is out of scope.
			Log.Info( $"Player died with score {Score}" );
		}
	}
}

