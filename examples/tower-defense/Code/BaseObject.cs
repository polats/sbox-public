using Sandbox;
using System;
using System.Linq;

namespace Local.TowerDefense;

/// <summary>
/// The player's base — sits at the end of the path. Implements IHealthComponent
/// so creeps damage it on reach-base. When HP hits 0, GameManager transitions
/// to GameOver.
/// </summary>
public sealed class BaseObject : Component, IHealthComponent
{
	[Property] public float MaxHpStat { get; set; } = 100f;

	public float CurrentHp { get; set; } = 100f;
	public float MaxHp => MaxHpStat;
	public bool IsDead => CurrentHp <= 0f;

	private GameManager _gm;

	protected override void OnStart()
	{
		base.OnStart();
		_gm = Scene.GetAllComponents<GameManager>().FirstOrDefault();
		// HP is set by GameManager from save state, but make sure it's not 0
		// if no save existed.
		if ( CurrentHp <= 0f )
			CurrentHp = MaxHpStat;
	}

	public void OnDamage( float amount, DamageInfo info )
	{
		if ( IsDead ) return;
		CurrentHp = MathF.Max( 0f, CurrentHp - amount );

		// Tint flash via the renderer.
		var mr = Components.GetInDescendantsOrSelf<ModelRenderer>();
		if ( mr != null )
		{
			float f = CurrentHp / MaxHp;
			mr.Tint = Color.Lerp( new Color( 0.7f, 0.1f, 0.1f ), new Color( 0.2f, 0.7f, 1f ), f );
		}

		if ( _gm != null )
			_gm.NotifyBaseDamaged();

		if ( CurrentHp <= 0f )
			OnDeath();
	}

	public void OnDeath()
	{
		if ( _gm != null )
			_gm.NotifyBaseDestroyed();
	}
}
