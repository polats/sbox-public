using Sandbox;
using System;

namespace Local.ShootingGallery;

public enum TargetKind
{
	Can,
	Board,
	Dummy,
}

public sealed class Target : Component
{
	[Property] public TargetKind Kind { get; set; } = TargetKind.Can;
	[Property] public int Score { get; set; } = 10;
	[Property] public float RespawnDelay { get; set; } = 3f;

	public GameController Controller { get; set; }
	public Vector3 SpawnPos { get; set; }
	public Rotation SpawnRot { get; set; } = Rotation.Identity;

	private bool _hit = false;
	private float _hideUntil = 0f;
	private bool _hidden = false;

	protected override void OnStart()
	{
		base.OnStart();
		SpawnPos = GameObject.WorldPosition;
		SpawnRot = GameObject.WorldRotation;
	}

	public void OnShot( Vector3 hitPos, Vector3 normal )
	{
		if ( _hit && _hidden ) return;
		_hit = true;

		Controller?.OnTargetHit( this, hitPos );

		var rb = Components.Get<Rigidbody>();
		if ( rb != null )
		{
			rb.ApplyImpulseAt( hitPos, -normal * 300f * rb.Mass );
			rb.ApplyImpulse( Vector3.Up * 150f * rb.Mass );
		}

		if ( Kind == TargetKind.Board )
		{
			HideForRespawn();
		}
		else if ( Kind == TargetKind.Dummy )
		{
			// rotate 90 degrees forward (fall over)
			var fallAxis = GameObject.WorldRotation.Right;
			GameObject.WorldRotation = Rotation.FromAxis( fallAxis, 80f ) * SpawnRot;
			HideForRespawn( 5f );
		}
	}

	private void HideForRespawn( float delay = -1f )
	{
		if ( delay < 0 ) delay = RespawnDelay;
		_hideUntil = Time.Now + delay;
		_hidden = true;

		foreach ( var mr in Components.GetAll<ModelRenderer>() )
			mr.Enabled = false;
		foreach ( var smr in Components.GetAll<SkinnedModelRenderer>() )
			smr.Enabled = false;
		foreach ( var c in Components.GetAll<Collider>() )
			c.Enabled = false;
		var rb = Components.Get<Rigidbody>();
		if ( rb != null ) rb.MotionEnabled = false;
	}

	private void Respawn()
	{
		_hit = false;
		_hidden = false;
		GameObject.WorldPosition = SpawnPos;
		GameObject.WorldRotation = SpawnRot;

		foreach ( var mr in Components.GetAll<ModelRenderer>() )
			mr.Enabled = true;
		foreach ( var smr in Components.GetAll<SkinnedModelRenderer>() )
			smr.Enabled = true;
		foreach ( var c in Components.GetAll<Collider>() )
			c.Enabled = true;
		var rb = Components.Get<Rigidbody>();
		if ( rb != null )
		{
			rb.Velocity = Vector3.Zero;
			rb.AngularVelocity = Vector3.Zero;
			rb.MotionEnabled = true;
		}
	}

	protected override void OnUpdate()
	{
		if ( _hidden && Time.Now >= _hideUntil )
		{
			Respawn();
		}

		// For cans: count knocked over (significant tilt) as a hit too
		if ( !_hit && Kind == TargetKind.Can )
		{
			var up = GameObject.WorldRotation.Up;
			if ( Vector3.Dot( up, Vector3.Up ) < 0.4f )
			{
				_hit = true;
				Controller?.OnTargetHit( this, GameObject.WorldPosition );
				HideForRespawn();
			}
		}
	}
}
