using Sandbox;
using System;

namespace Local.ToyBox;

/// <summary>
/// Local equivalent of sandbox/Code/Weapons/ToolGun/Modes/Thruster/ThrusterEntity.cs.
/// Upstream applies impulse in WorldRotation.Up * -10000 per ClientInput tick;
/// here we apply a steady force in a configurable axis, with timer-driven on/off
/// pulses so the video shows it firing visibly.
/// </summary>
[Icon( "rocket_launch" )]
public sealed class ThrusterEntity : Component
{
	[Property] public Vector3 Axis { get; set; } = Vector3.Up;
	[Property] public float Power { get; set; } = 7000f;
	[Property] public float OnDuration { get; set; } = 1.0f;
	[Property] public float OffDuration { get; set; } = 0.8f;
	[Property] public GameObject FlameVisual { get; set; }

	public bool IsFiring { get; private set; }

	private float _stateUntil;

	protected override void OnStart()
	{
		_stateUntil = Time.Now + OffDuration;
		IsFiring = false;
		UpdateFlame();
	}

	protected override void OnFixedUpdate()
	{
		if ( Time.Now >= _stateUntil )
		{
			IsFiring = !IsFiring;
			_stateUntil = Time.Now + (IsFiring ? OnDuration : OffDuration);
			UpdateFlame();
		}

		if ( !IsFiring ) return;

		var rb = GetComponent<Rigidbody>();
		if ( !rb.IsValid() ) return;

		var worldAxis = WorldRotation * Axis.Normal;
		rb.ApplyForce( worldAxis * Power );
	}

	private void UpdateFlame()
	{
		if ( FlameVisual.IsValid() )
			FlameVisual.Enabled = IsFiring;
	}

	protected override void DrawGizmos()
	{
		Gizmo.Draw.Color = IsFiring ? Color.Red : Color.Gray;
		Gizmo.Draw.Arrow( Vector3.Zero, Axis.Normal * 60f );
	}
}
