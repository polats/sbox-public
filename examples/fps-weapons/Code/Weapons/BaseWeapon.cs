using Sandbox;

namespace Local.FpsWeapons;

/// <summary>
/// Base class for first-person weapons. Each weapon is a Component on its own
/// GameObject parented to the camera, holding a ModelRenderer viewmodel.
/// </summary>
public abstract class BaseWeapon : Component
{
	public abstract string DisplayName { get; }
	/// <summary>Path to the viewmodel .vmdl, or null for a generated box.</summary>
	public virtual string ViewModel => null;
	/// <summary>Tint applied to the fallback box viewmodel.</summary>
	public virtual Color ViewModelTint => new( 0.2f, 0.2f, 0.22f );
	/// <summary>Scale (in world units) of the fallback box viewmodel.</summary>
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
		Holster(); // start hidden until Deploy
		Ammo = MaxAmmo;
	}

	private void EnsureViewmodel()
	{
		if ( _viewmodelGo != null && _viewmodelGo.IsValid() ) return;

		_viewmodelGo = Scene.CreateObject();
		_viewmodelGo.Name = $"VM_{DisplayName}";
		_viewmodelGo.Parent = GameObject;
		// Position relative to camera/parent: forward, right, down
		_viewmodelGo.LocalPosition = new Vector3( 22f, 8f, -8f );
		_viewmodelGo.LocalRotation = Rotation.Identity;

		_viewmodelMr = _viewmodelGo.Components.Create<ModelRenderer>();
		_viewmodelMr.Model = Model.Load( "models/dev/box.vmdl" );
		_viewmodelMr.Tint = ViewModelTint;
		// Box is 50x50x50; scale to weapon size.
		_viewmodelGo.LocalScale = ViewModelBoxSize / 50f;

		// Optional decorative bits per weapon
		DecorateViewmodel( _viewmodelGo );
	}

	/// <summary>Override to add child meshes (sight, magazine, etc).</summary>
	protected virtual void DecorateViewmodel( GameObject root ) { }

	protected GameObject AddVmPart( string name, Vector3 localPos, Vector3 sizeInches, Color tint )
	{
		// Add a child of the viewmodel root, but unscale to keep child sizes independent.
		var go = Scene.CreateObject();
		go.Name = name;
		go.Parent = _viewmodelGo;
		// _viewmodelGo has a non-uniform scale (sizeInches/50). Reverse it so the
		// child's own scale operates in inches.
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
	public virtual void OnSecondaryAttack() { }
	public virtual void OnSecondaryRelease() { }
	public virtual void OnPrimaryRelease() { }
	public virtual void OnReload() { }
	public virtual void OnScroll( int delta ) { }

	/// <summary>Called every frame on the active weapon.</summary>
	public virtual void Tick() { }

	public bool HasAmmo() => MaxAmmo == 0 || Ammo > 0;
}
