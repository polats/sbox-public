using Sandbox;
using System;

namespace Local.CtfArena;

/// <summary>
/// Same damage-system interface from tower-defense — reused here so this
/// project demonstrates the standard CTF concept (even though the minimal
/// CTF loop never actually fires damage). Kept for parity with the gap-coverage
/// series and so future weapon additions just plug in.
/// </summary>
public interface IHealthComponent
{
	float CurrentHp { get; }
	float MaxHp { get; }
	bool IsDead { get; }
	void OnDamage( float amount, DamageInfo info );
	void OnDeath();
}

public struct DamageInfo
{
	public float Amount;
	public Vector3 HitPosition;
	public GameObject Attacker;

	public static DamageInfo Simple( float amount, Vector3 hitPos, GameObject attacker = null )
		=> new DamageInfo { Amount = amount, HitPosition = hitPos, Attacker = attacker };
}

public enum Team
{
	None = 0,
	Red = 1,
	Blue = 2,
}

public static class TeamExtensions
{
	public static Team Other( this Team t ) => t == Team.Red ? Team.Blue : (t == Team.Blue ? Team.Red : Team.None);
	public static Color ToColor( this Team t ) => t switch
	{
		Team.Red => new Color( 0.95f, 0.25f, 0.25f ),
		Team.Blue => new Color( 0.25f, 0.5f, 0.95f ),
		_ => Color.White,
	};
}
