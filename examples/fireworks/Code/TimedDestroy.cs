using Sandbox;

namespace Local.Fireworks;

/// <summary>
/// Self-destructs its host GameObject after Lifetime seconds. Cheaper than
/// scheduling timers from outside; survives parent destroys via IsValid checks.
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
