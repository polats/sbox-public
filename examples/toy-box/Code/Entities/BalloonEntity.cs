using Sandbox;
using System;

namespace Local.ToyBox;

/// <summary>
/// Local equivalent of sandbox/Code/Weapons/ToolGun/Modes/Balloon/BalloonEntity.cs.
/// Sandbox version is a thin Prop+IDamageable that pops on hit; gameplay
/// behavior (gravity off, light lift) is configured on the Rigidbody itself.
/// We drive that behavior explicitly here so the balloon "just floats" without
/// per-scene tuning.
///
/// Wants a Rigidbody on the same GameObject with Gravity=false. Applies a
/// constant upward force and small sinusoidal sway.
/// </summary>
[Icon( "filter_drama" )]
public sealed class BalloonEntity : Component
{
	[Property] public float Lift { get; set; } = 30f;
	[Property] public float MaxAltitude { get; set; } = 80f;
	[Property] public float SwayAmount { get; set; } = 3f;
	[Property] public float SwayPeriod { get; set; } = 2.5f;

	private Vector3 _origin;
	private float _phase;

	protected override void OnStart()
	{
		_origin = WorldPosition;
		_phase = (GameObject.Id.GetHashCode() & 0xff) * 0.05f;
		var rb = GetComponent<Rigidbody>();
		if ( rb.IsValid() )
		{
			rb.Gravity = false;
			rb.LinearDamping = 1.5f;
			rb.AngularDamping = 2f;
		}
	}

	protected override void OnFixedUpdate()
	{
		var rb = GetComponent<Rigidbody>();
		if ( !rb.IsValid() ) return;

		// Hover near a tether ceiling (origin + MaxAltitude)
		var ceil = _origin.z + MaxAltitude;
		var dz = ceil - rb.WorldPosition.z;
		var force = Vector3.Up * Lift * MathF.Sign( dz ) * MathF.Min( 1f, MathF.Abs( dz ) / 20f );

		// Horizontal sway
		var omega = MathF.Tau / SwayPeriod;
		var sway = new Vector3(
			MathF.Cos( Time.Now * omega + _phase ) * SwayAmount,
			MathF.Sin( Time.Now * omega * 1.3f + _phase ) * SwayAmount,
			0 );

		// Velocity-based: easier to tune than ApplyForce against unknown masses.
		var targetVel = (force + sway);
		rb.Velocity = Vector3.Lerp( rb.Velocity, targetVel, MathF.Min( 1f, Time.Delta * 2f ) );
	}
}
