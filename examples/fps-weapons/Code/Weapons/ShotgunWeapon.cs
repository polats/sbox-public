using Sandbox;

namespace Local.FpsWeapons;

public sealed class ShotgunWeapon : BaseWeapon
{
	public override string DisplayName => "Shotgun";
	public override int MaxAmmo => 6;
	public override Color ViewModelTint => new( 0.30f, 0.20f, 0.12f );
	public override Vector3 ViewModelBoxSize => new( 28f, 4f, 7f );

	[Property] public float FireInterval { get; set; } = 0.6f;
	[Property] public float ReloadTime { get; set; } = 2.0f;
	[Property] public int PelletCount { get; set; } = 8;
	[Property] public float PelletDamage { get; set; } = 12f;
	[Property] public float Spread { get; set; } = 0.075f;
	[Property] public float RecoilDegrees { get; set; } = 3f;
	[Property] public float ShakeAmount { get; set; } = 8f;

	private float _nextFire = 0f;
	private float _reloadUntil = 0f;

	protected override void DecorateViewmodel( GameObject root )
	{
		// Pump
		AddVmPart( "Pump", new Vector3( 4f, 0f, -2.5f ), new Vector3( 4f, 3f, 2f ), new Color( 0.10f, 0.08f, 0.06f ) );
		// Barrel
		AddVmPart( "Barrel", new Vector3( 10f, 0f, 1.5f ), new Vector3( 8f, 2f, 2f ), new Color( 0.08f, 0.08f, 0.08f ) );
		// Stock
		AddVmPart( "Stock", new Vector3( -10f, 0f, -1f ), new Vector3( 8f, 2.5f, 4.5f ), new Color( 0.18f, 0.12f, 0.06f ) );
	}

	public bool IsReloading => Time.Now < _reloadUntil;

	public override void Tick()
	{
		base.Tick();
		if ( IsReloading && Time.Now >= _reloadUntil )
		{
			Ammo = MaxAmmo;
		}
	}

	public override void OnPrimaryAttack()
	{
		if ( IsReloading ) return;
		if ( Time.Now < _nextFire ) return;
		if ( Ammo <= 0 )
		{
			OnReload();
			return;
		}
		Ammo--;
		_nextFire = Time.Now + FireInterval;
		Owner?.FireHitscan( spread: Spread, pellets: PelletCount, damage: PelletDamage );
		Owner?.AddRecoil( RecoilDegrees, ShakeAmount );
	}

	public override void OnReload()
	{
		if ( IsReloading ) return;
		if ( Ammo == MaxAmmo ) return;
		_reloadUntil = Time.Now + ReloadTime;
	}
}
