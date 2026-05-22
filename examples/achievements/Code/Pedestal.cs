using Sandbox;
using System.Linq;

namespace Local.Achievements;

/// <summary>
/// Central pedestal sign. Touching it disqualifies the pacifist achievement.
/// </summary>
public sealed class Pedestal : Component
{
	public bool WasTouched { get; private set; }

	protected override void OnUpdate()
	{
		if ( WasTouched ) return;

		var player = Scene.GetAllComponents<PuzzlePlayer>().FirstOrDefault();
		if ( !player.IsValid() ) return;

		var d2 = (player.GameObject.WorldPosition - GameObject.WorldPosition).WithZ( 0 ).LengthSquared;
		// Pedestal collision radius ~25 inches.
		if ( d2 < 25f * 25f )
		{
			WasTouched = true;
			AchievementManager.Instance?.NotifyPedestalTouched();
			Log.Info( "[Achievements] Pedestal touched — pacifist no longer obtainable this attempt." );
		}
	}
}
