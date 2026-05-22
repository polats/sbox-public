using Sandbox;

namespace Local.MaterialShowcase;

/// <summary>
/// Spins the GameObject around the Z axis at a configurable rate.
/// Used on the showcase spheres so the material reads from every angle.
/// </summary>
public sealed class Spinner : Component
{
	[Property] public float DegreesPerSecond { get; set; } = 30f;

	protected override void OnUpdate()
	{
		var rot = GameObject.WorldRotation;
		rot *= Rotation.FromYaw( DegreesPerSecond * Time.Delta );
		GameObject.WorldRotation = rot;
	}
}
