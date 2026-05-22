using Sandbox;
using System;
using System.Linq;

namespace Local.TowerDefense;

/// <summary>
/// A citizen that walks along the navmesh from spawn → base. Implements
/// IHealthComponent so towers can damage it. On death, drops gold and spawns
/// a small particle burst. On reaching the base, damages the base and despawns.
/// </summary>
public sealed class Creep : Component, IHealthComponent
{
	[Property] public float MaxHpStat { get; set; } = 30f;
	[Property] public float MoveSpeed { get; set; } = 70f;
	[Property] public int GoldReward { get; set; } = 12;
	[Property] public float DamageToBase { get; set; } = 10f;

	public float CurrentHp { get; private set; }
	public float MaxHp => MaxHpStat;
	public bool IsDead { get; private set; }

	private NavMeshAgent _agent;
	private GameManager _gm;
	private BaseObject _base;

	protected override void OnStart()
	{
		base.OnStart();
		CurrentHp = MaxHpStat;

		_agent = Components.GetOrCreate<NavMeshAgent>();
		_agent.MaxSpeed = MoveSpeed;
		_agent.Radius = 16f;
		_agent.Height = 64f;
		_agent.Acceleration = 600f;
		_agent.UpdatePosition = true;
		_agent.UpdateRotation = true;

		_gm = Scene.GetAllComponents<GameManager>().FirstOrDefault();
		_base = Scene.GetAllComponents<BaseObject>().FirstOrDefault();

		// Initial target.
		if ( _base != null )
			_agent.MoveTo( _base.WorldPosition );
	}

	protected override void OnUpdate()
	{
		if ( IsDead ) return;

		// Refresh destination periodically (base doesn't move, but cheap).
		if ( _base != null && _agent != null )
		{
			_agent.MoveTo( _base.WorldPosition );

			// Reached the base?
			float dist = (WorldPosition.WithZ( 0 ) - _base.WorldPosition.WithZ( 0 )).Length;
			if ( dist < 60f )
			{
				_base.OnDamage( DamageToBase, DamageInfo.Simple( DamageToBase, WorldPosition, GameObject ) );
				DespawnWithoutReward();
				return;
			}
		}

		// Drive the citizen anim graph from agent velocity.
		DriveAnimGraph();
	}

	private void DriveAnimGraph()
	{
		var smr = Components.GetInDescendantsOrSelf<SkinnedModelRenderer>();
		if ( smr == null || smr.Model == null || _agent == null ) return;

		var vel = _agent.Velocity;
		var horiz = vel.WithZ( 0 );
		var rot = GameObject.WorldRotation;
		var fwd = rot.Forward.Dot( horiz );
		var side = rot.Right.Dot( horiz );
		float speed = horiz.Length;

		smr.Set( "move_speed", speed );
		smr.Set( "move_groundspeed", speed );
		smr.Set( "move_x", fwd );
		smr.Set( "move_y", side );
		smr.Set( "wish_speed", speed );
		smr.Set( "wish_groundspeed", speed );
		smr.Set( "wish_x", fwd );
		smr.Set( "wish_y", side );
		smr.Set( "b_grounded", true );
	}

	public void OnDamage( float amount, DamageInfo info )
	{
		if ( IsDead ) return;
		CurrentHp -= amount;
		if ( CurrentHp <= 0f )
		{
			CurrentHp = 0f;
			OnDeath();
		}
	}

	public void OnDeath()
	{
		if ( IsDead ) return;
		IsDead = true;

		// Reward the player.
		if ( _gm != null )
			_gm.AddGold( GoldReward );

		SpawnDeathBurst();

		if ( GameObject.IsValid() )
			GameObject.Destroy();
	}

	private void DespawnWithoutReward()
	{
		IsDead = true;
		if ( GameObject.IsValid() )
			GameObject.Destroy();
	}

	private void SpawnDeathBurst()
	{
		// Cheap "explosion": a handful of tinted spheres flying outward with
		// short TimedDestroy lifetimes. Same pattern as fireworks' rigidbody
		// burst, scaled down.
		var pos = WorldPosition + Vector3.Up * 32f;
		for ( int i = 0; i < 14; i++ )
		{
			var go = Scene.CreateObject();
			go.Name = "DeathParticle";
			go.WorldPosition = pos;
			go.WorldScale = new Vector3( 4f / 32f, 4f / 32f, 4f / 32f );

			var mr = go.Components.Create<ModelRenderer>();
			mr.Model = Model.Load( "models/dev/sphere.vmdl" );
			mr.Tint = new Color( 1f, 0.45f, 0.25f );

			var rb = go.Components.Create<Rigidbody>();
			rb.Gravity = true;
			rb.LinearDamping = 0.6f;
			rb.MassOverride = 0.01f;

			var dir = new Vector3(
				Game.Random.Float( -1f, 1f ),
				Game.Random.Float( -1f, 1f ),
				Game.Random.Float( 0.2f, 1.0f ) ).Normal;
			rb.ApplyImpulse( dir * 80f * rb.Mass );

			go.Components.Create<TimedDestroy>().Lifetime = 1.2f;
		}
	}
}
