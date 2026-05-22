using Sandbox;

namespace Local.HammerLevel;

/// <summary>
/// Base FP weapon (cribbed from examples/fps-weapons). Each weapon is a
/// Component on its own GameObject parented to the camera.
/// </summary>
public abstract class BaseWeapon : Component
{
	public abstract string DisplayName { get; }
	public virtual Color ViewModelTint => new( 0.2f, 0.2f, 0.22f );
	public virtual Vector3 ViewModelBoxSize => new( 14f, 4f, 8f );

	public virtual int MaxAmmo => 0;
	public virtual int Ammo { get; protected set; }

	public Player Owner { get; set; }

	private GameObject _viewmodelGo;
	private ModelRenderer _viewmodelMr;

	protected override void OnStart()
	{
		base.OnStart();
		EnsureViewmodel();
		Holster();
		Ammo = MaxAmmo;
	}

	private void EnsureViewmodel()
	{
		if ( _viewmodelGo != null && _viewmodelGo.IsValid() ) return;

		_viewmodelGo = Scene.CreateObject();
		_viewmodelGo.Name = $"VM_{DisplayName}";
		_viewmodelGo.Parent = GameObject;
		_viewmodelGo.LocalPosition = new Vector3( 22f, 8f, -8f );
		_viewmodelGo.LocalRotation = Rotation.Identity;

		_viewmodelMr = _viewmodelGo.Components.Create<ModelRenderer>();
		_viewmodelMr.Model = Model.Load( "models/dev/box.vmdl" );
		_viewmodelMr.Tint = ViewModelTint;
		_viewmodelGo.LocalScale = ViewModelBoxSize / 50f;

		DecorateViewmodel( _viewmodelGo );
	}

	protected virtual void DecorateViewmodel( GameObject root ) { }

	protected GameObject AddVmPart( string name, Vector3 localPos, Vector3 sizeInches, Color tint )
	{
		var go = Scene.CreateObject();
		go.Name = name;
		go.Parent = _viewmodelGo;
		var invParent = new Vector3(
			50f / ViewModelBoxSize.x,
			50f / ViewModelBoxSize.y,
			50f / ViewModelBoxSize.z );
		go.LocalScale = (sizeInches / 50f) * invParent;
		go.LocalPosition = new Vector3(
			localPos.x / ViewModelBoxSize.x,
			localPos.y / ViewModelBoxSize.y,
			localPos.z / ViewModelBoxSize.z );
		var mr = go.Components.Create<ModelRenderer>();
		mr.Model = Model.Load( "models/dev/box.vmdl" );
		mr.Tint = tint;
		return go;
	}

	public virtual void Deploy()
	{
		EnsureViewmodel();
		if ( _viewmodelGo != null ) _viewmodelGo.Enabled = true;
	}

	public virtual void Holster()
	{
		if ( _viewmodelGo != null ) _viewmodelGo.Enabled = false;
	}

	public abstract void OnPrimaryAttack();
	public virtual void OnReload() { }
	public virtual void Tick() { }
	public bool HasAmmo() => MaxAmmo == 0 || Ammo > 0;
}
