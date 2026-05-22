using Sandbox;

namespace Local.RagdollCannon;

/// <summary>Briefly scales+fades a sphere as a cheap muzzle flash, then destroys.</summary>
public sealed class MuzzleFlashFade : Component
{
	[Property] public float Lifetime { get; set; } = 0.18f;
	private float _spawnTime;
	private ModelRenderer _mr;
	private Vector3 _startScale;
	private Color _startTint;

	protected override void OnStart()
	{
		base.OnStart();
		_spawnTime = Time.Now;
		_mr = Components.Get<ModelRenderer>();
		_startScale = GameObject.WorldScale;
		_startTint = _mr != null ? _mr.Tint : Color.White;
	}

	protected override void OnUpdate()
	{
		float t = (Time.Now - _spawnTime) / Lifetime;
		if ( t >= 1f )
		{
			GameObject.Destroy();
			return;
		}
		GameObject.WorldScale = _startScale * (1f + t * 1.6f);
		if ( _mr != null )
			_mr.Tint = _startTint.WithAlpha( 1f - t );
	}
}
