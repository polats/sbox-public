using Sandbox;
using System;

namespace Local.TowerDefense;

/// <summary>
/// Shared damage system: anything that takes damage in this game implements
/// IHealthComponent. Towers fire damage through this interface — they don't
/// care whether the target is a creep or the base.
/// </summary>
public interface IHealthComponent
{
	float CurrentHp { get; }
	float MaxHp { get; }
	bool IsDead { get; }
	void OnDamage( float amount, DamageInfo info );
	void OnDeath();
}

/// <summary>
/// What hit you, from where, with what payload. Kept lean — extend later.
/// </summary>
public struct DamageInfo
{
	public float Amount;
	public Vector3 HitPosition;
	public GameObject Attacker;

	public static DamageInfo Simple( float amount, Vector3 hitPos, GameObject attacker = null )
		=> new DamageInfo { Amount = amount, HitPosition = hitPos, Attacker = attacker };
}
