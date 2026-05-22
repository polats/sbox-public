using Sandbox;

namespace Local.TowerDefense;

/// <summary>
/// Self-destructs its host GameObject after Lifetime seconds. Same pattern
/// used in examples/fireworks for particle clean-up.
/// </summary>
public sealed class TimedDestroy : Component
{
	[Property] public float Lifetime { get; set; } = 1f;

	private float _spawnTime;

	protected override void OnStart()
	{
		base.OnStart();
		_spawnTime = Time.Now;
	}

	protected override void OnUpdate()
	{
		if ( Time.Now - _spawnTime >= Lifetime && GameObject.IsValid() )
		{
			GameObject.Destroy();
		}
	}
}
