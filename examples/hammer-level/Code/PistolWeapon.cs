using Sandbox;

namespace Local.HammerLevel;

public sealed class PistolWeapon : BaseWeapon
{
	public override string DisplayName => "Pistol";
	public override int MaxAmmo => 12;
	public override Color ViewModelTint => new( 0.18f, 0.18f, 0.20f );
	public override Vector3 ViewModelBoxSize => new( 12f, 3.5f, 7f );

	[Property] public float FireInterval { get; set; } = 0.15f;
	[Property] public float ReloadTime { get; set; } = 1.2f;
	[Property] public float Damage { get; set; } = 25f;
	[Property] public float RecoilDegrees { get; set; } = 0.5f;
	[Property] public float ShakeAmount { get; set; } = 1.5f;

	private float _nextFire = 0f;
	private float _reloadUntil = 0f;

	protected override void DecorateViewmodel( GameObject root )
	{
		AddVmPart( "Sight", new Vector3( 3f, 0f, 4.2f ), new Vector3( 2f, 1f, 1f ), new Color( 0.05f, 0.05f, 0.05f ) );
		AddVmPart( "Barrel", new Vector3( 7f, 0f, 1f ), new Vector3( 3f, 1.5f, 1.5f ), new Color( 0.10f, 0.10f, 0.12f ) );
		AddVmPart( "Grip", new Vector3( -3f, 0f, -5f ), new Vector3( 4f, 2.8f, 4f ), new Color( 0.30f, 0.20f, 0.12f ) );
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
		Owner?.FireHitscan( spread: 0.005f, pellets: 1, damage: Damage );
		Owner?.AddRecoil( RecoilDegrees, ShakeAmount );
	}

	public override void OnReload()
	{
		if ( IsReloading ) return;
		if ( Ammo == MaxAmmo ) return;
		_reloadUntil = Time.Now + ReloadTime;
	}
}
