using Sandbox;

namespace Local.FpsWeapons;

/// <summary>
/// PhysGun: LMB grabs a Rigidbody; while held, smoothed to camera-forward
/// position. Scroll changes hold distance. RMB freezes and releases.
/// </summary>
public sealed class PhysGunWeapon : BaseWeapon
{
	public override string DisplayName => "PhysGun";
	public override int MaxAmmo => 0;
	public override Color ViewModelTint => new( 0.20f, 0.50f, 0.85f );
	public override Vector3 ViewModelBoxSize => new( 16f, 5f, 9f );

	[Property] public float MinHoldDistance { get; set; } = 60f;
	[Property] public float MaxHoldDistance { get; set; } = 800f;
	[Property] public float DefaultHoldDistance { get; set; } = 200f;
	[Property] public float HoldSmoothing { get; set; } = 18f;

	private float _holdDistance;
	private Rigidbody _held;
	private bool _heldGravityWas;
	private float _heldDampingWas;
	private float _heldAngDampingWas;

	protected override void DecorateViewmodel( GameObject root )
	{
		AddVmPart( "Emitter", new Vector3( 7f, 0f, 1f ), new Vector3( 3f, 4f, 4f ), new Color( 0.40f, 0.85f, 1.0f ) );
		AddVmPart( "Grip", new Vector3( -4f, 0f, -6f ), new Vector3( 4f, 3f, 5f ), new Color( 0.15f, 0.18f, 0.25f ) );
		AddVmPart( "Top", new Vector3( 0f, 0f, 5f ), new Vector3( 8f, 2f, 1.5f ), new Color( 0.10f, 0.30f, 0.55f ) );
	}

	public override void Deploy()
	{
		base.Deploy();
		_holdDistance = DefaultHoldDistance;
	}

	public override void Holster()
	{
		base.Holster();
		Release();
	}

	public override void OnPrimaryAttack()
	{
		if ( _held != null ) return;
		if ( Owner == null ) return;

		var cam = Owner.CameraGameObject;
		if ( cam == null ) return;

		var from = cam.WorldPosition;
		var fwd = cam.WorldRotation.Forward;
		var to = from + fwd * MaxHoldDistance;

		var tr = Scene.Trace.Ray( from, to )
			.IgnoreGameObjectHierarchy( Owner.GameObject )
			.Run();

		if ( !tr.Hit ) return;

		// Walk up to find a Rigidbody
		var go = tr.GameObject;
		Rigidbody rb = null;
		while ( go != null )
		{
			rb = go.Components.Get<Rigidbody>();
			if ( rb != null ) break;
			go = go.Parent;
		}
		if ( rb == null ) return;

		_held = rb;
		_heldGravityWas = rb.Gravity;
		_heldDampingWas = rb.LinearDamping;
		_heldAngDampingWas = rb.AngularDamping;
		rb.Gravity = false;
		rb.LinearDamping = 8f;
		rb.AngularDamping = 8f;
		rb.MotionEnabled = true;

		// Initialize hold distance to current distance
		_holdDistance = MathX.Clamp( (rb.WorldPosition - from).Length, MinHoldDistance, MaxHoldDistance );

		// Inform target it's been grabbed (for any reaction)
		var t = rb.Components.Get<Target>();
		t?.OnGrabbed();
	}

	public override void OnPrimaryRelease()
	{
		Release();
	}

	public override void OnSecondaryAttack()
	{
		// Freeze and release.
		if ( _held == null ) return;
		_held.Velocity = Vector3.Zero;
		_held.AngularVelocity = Vector3.Zero;
		_held.MotionEnabled = false;
		// Keep its gravity off so it stays floating; user pressed freeze.
		_held = null;
	}

	public override void OnScroll( int delta )
	{
		if ( _held == null ) return;
		_holdDistance = MathX.Clamp( _holdDistance + delta * 30f, MinHoldDistance, MaxHoldDistance );
	}

	private void Release()
	{
		if ( _held == null ) return;
		_held.Gravity = _heldGravityWas;
		_held.LinearDamping = _heldDampingWas;
		_held.AngularDamping = _heldAngDampingWas;
		_held = null;
	}

	public override void Tick()
	{
		base.Tick();
		if ( _held == null || !_held.IsValid() ) { _held = null; return; }
		if ( Owner?.CameraGameObject == null ) return;

		var cam = Owner.CameraGameObject;
		var target = cam.WorldPosition + cam.WorldRotation.Forward * _holdDistance;

		// Smoothed move via velocity (works for kinematic-like behavior)
		var cur = _held.WorldPosition;
		var newPos = Vector3.Lerp( cur, target, Time.Delta * HoldSmoothing );
		_held.WorldPosition = newPos;
		_held.Velocity = Vector3.Zero;
		_held.AngularVelocity = _held.AngularVelocity * 0.85f;
	}

	public bool IsHolding => _held != null;
	public Rigidbody Held => _held;
}
