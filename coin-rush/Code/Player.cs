using Sandbox;

namespace Local.CoinRush;

/// <summary>
/// Top-down player. Moves on the XY plane with WASD and collects coins via trigger overlap.
/// </summary>
public sealed class Player : Component
{
	[Property] public float MoveSpeed { get; set; } = 350f;

	[Sync] public int Score { get; set; }

	protected override void OnStart()
	{
		base.OnStart();
		if ( Components.Get<BoxCollider>() is null )
		{
			var col = Components.Create<BoxCollider>();
			col.IsTrigger = true;
			col.Scale = new Vector3( 60, 60, 60 );
		}
	}

	protected override void OnUpdate()
	{
		var dir = Vector3.Zero;
		if ( Input.Down( "Forward" ) )  dir += Vector3.Forward;
		if ( Input.Down( "Backward" ) ) dir += Vector3.Backward;
		if ( Input.Down( "Left" ) )     dir += Vector3.Left;
		if ( Input.Down( "Right" ) )    dir += Vector3.Right;

		if ( !dir.IsNearZeroLength )
		{
			WorldPosition += dir.Normal * MoveSpeed * Time.Delta;
		}
	}
}
