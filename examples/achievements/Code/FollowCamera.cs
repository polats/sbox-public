using Sandbox;
using System.Linq;

namespace Local.Achievements;

/// <summary>
/// 3rd-person camera that softly follows the player. Fixed angle (no mouse
/// look) so the autoplay video frames the room consistently.
/// </summary>
public sealed class FollowCamera : Component
{
	[Property] public Vector3 Offset { get; set; } = new( -260f, 0f, 180f );
	[Property] public Vector3 LookAtOffset { get; set; } = new( 0, 0, 55 );
	[Property] public float Smoothing { get; set; } = 4f;

	protected override void OnUpdate()
	{
		var player = Scene.GetAllComponents<PuzzlePlayer>().FirstOrDefault();
		if ( !player.IsValid() ) return;

		var target = player.GameObject.WorldPosition;
		var desired = target + Offset;
		WorldPosition = Vector3.Lerp( WorldPosition, desired, Time.Delta * Smoothing );
		WorldRotation = Rotation.LookAt( (target + LookAtOffset) - WorldPosition );
	}
}
