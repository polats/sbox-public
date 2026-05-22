using Sandbox;

namespace Local.RagdollCannon;

/// <summary>Destroys the GameObject after Lifetime seconds.</summary>
public sealed class RagdollLifetime : Component
{
	[Property] public float Lifetime { get; set; } = 8f;
	public GameManager Manager { get; set; }
	private float _spawnTime;

	protected override void OnStart()
	{
		base.OnStart();
		_spawnTime = Time.Now;
	}

	protected override void OnUpdate()
	{
		if ( Time.Now - _spawnTime >= Lifetime )
		{
			GameObject.Destroy();
		}
	}
}
