using Sandbox;
using System;
using System.Linq;

namespace Local.ShootingGallery;

/// <summary>
/// First-person player. Mouse-look only (no movement). Click to fire a
/// hitscan ray from the camera; visualize the trace; tell hit Targets.
/// </summary>
public sealed class Player : Component
{
	[Property] public float MouseSensitivity { get; set; } = 0.08f;
	[Property] public float MaxTraceDistance { get; set; } = 12000f;

	[Property] public bool AutoPlay { get; set; } = false;
	[Property] public float AutoFireInterval { get; set; } = 0.55f;

	private Angles _eyeAngles;
	private GameObject _cameraGo;
	private CameraComponent _camera;
	private GameController _gm;

	private float _autoTimer = 0f;
	private int _autoTargetIdx = 0;
	private float _autoSweepT = 0f;

	private float _muzzleFlashUntil = 0f;
	private GameObject _tracerGo;
	private float _tracerUntil = 0f;

	protected override void OnStart()
	{
		base.OnStart();

		// Find camera child
		_cameraGo = GameObject.Children.FirstOrDefault( c => c.Components.Get<CameraComponent>() != null );
		if ( _cameraGo == null ) _cameraGo = Scene.Camera?.GameObject;
		_camera = _cameraGo?.Components.Get<CameraComponent>();

		_gm = Scene.GetAllComponents<GameController>().FirstOrDefault();

		// Initial aim slightly forward toward the targets
		_eyeAngles = new Angles( 0, 0, 0 );
		ApplyAngles();

		Mouse.Visible = false;
	}

	protected override void OnUpdate()
	{
		if ( _camera == null ) return;
		if ( _gm != null && _gm.GameOver )
		{
			Mouse.Visible = true;
			return;
		}

		Mouse.Visible = false;

		if ( AutoPlay )
		{
			AutoplayAim();
		}
		else
		{
			_eyeAngles.yaw -= Input.MouseDelta.x * MouseSensitivity;
			_eyeAngles.pitch = Math.Clamp( _eyeAngles.pitch + Input.MouseDelta.y * MouseSensitivity, -89f, 89f );
		}

		ApplyAngles();

		bool wantFire = false;
		if ( !AutoPlay && Input.Pressed( "Attack1" ) ) wantFire = true;
		if ( AutoPlay )
		{
			_autoTimer -= Time.Delta;
			if ( _autoTimer <= 0f )
			{
				wantFire = true;
				_autoTimer = AutoFireInterval;
			}
		}

		if ( wantFire ) Fire();

		// Tracer fade
		if ( _tracerGo != null && _tracerGo.IsValid() && Time.Now > _tracerUntil )
		{
			_tracerGo.Destroy();
			_tracerGo = null;
		}
	}

	private void AutoplayAim()
	{
		// Pick a live target periodically and aim at it with a smooth lerp
		var targets = _gm?.Targets?.Where( t => t != null && t.IsValid() && t.GameObject.Enabled ).ToList();
		if ( targets == null || targets.Count == 0 )
		{
			// Sweep slowly
			_autoSweepT += Time.Delta;
			_eyeAngles.yaw = MathF.Sin( _autoSweepT * 0.4f ) * 35f;
			_eyeAngles.pitch = -5f + MathF.Sin( _autoSweepT * 0.7f ) * 5f;
			return;
		}

		_autoSweepT += Time.Delta;
		int idx = ((int)(_autoSweepT * 0.7f)) % targets.Count;
		_autoTargetIdx = idx;
		var tgt = targets[idx];
		// Aim at the center of the target with a tiny offset so we sometimes miss
		var aimPos = tgt.GameObject.WorldPosition + new Vector3( 0, 0, 8 );
		var camPos = _cameraGo.WorldPosition;
		var dir = (aimPos - camPos).Normal;

		var wantAngles = Rotation.LookAt( dir, Vector3.Up ).Angles();
		// add slight jitter so it's not robotic
		wantAngles.yaw += MathF.Sin( _autoSweepT * 5.3f ) * 1.2f;
		wantAngles.pitch += MathF.Cos( _autoSweepT * 4.1f ) * 0.8f;

		_eyeAngles.yaw = MathX.Lerp( _eyeAngles.yaw, wantAngles.yaw, Time.Delta * 6f );
		_eyeAngles.pitch = MathX.Lerp( _eyeAngles.pitch, wantAngles.pitch, Time.Delta * 6f );
		_eyeAngles.pitch = Math.Clamp( _eyeAngles.pitch, -89f, 89f );
	}

	private void ApplyAngles()
	{
		var rot = Rotation.From( _eyeAngles.pitch, _eyeAngles.yaw, 0 );
		if ( _cameraGo != null ) _cameraGo.WorldRotation = rot;
		// Optionally rotate the body's yaw
		GameObject.WorldRotation = Rotation.FromYaw( _eyeAngles.yaw );
	}

	private void Fire()
	{
		if ( _gm != null && _gm.GameOver ) return;

		_gm?.OnShotFired();

		var camPos = _cameraGo.WorldPosition;
		var camFwd = _cameraGo.WorldRotation.Forward;
		var end = camPos + camFwd * MaxTraceDistance;

		var tr = Scene.Trace.Ray( camPos, end )
			.IgnoreGameObjectHierarchy( GameObject )
			.Run();

		var hitEnd = tr.Hit ? tr.HitPosition : end;
		SpawnTracer( camPos, hitEnd );

		if ( tr.Hit )
		{
			// Walk up the hierarchy looking for a Target component
			var go = tr.GameObject;
			Target target = null;
			while ( go != null )
			{
				target = go.Components.Get<Target>();
				if ( target != null ) break;
				go = go.Parent;
			}
			if ( target != null )
			{
				target.OnShot( tr.HitPosition, tr.Normal );
			}
			else
			{
				// Generic impact sound for misses-into-wall
				if ( _gm?.ImpactSound != null ) Sound.Play( _gm.ImpactSound, tr.HitPosition );
			}
		}
	}

	private void SpawnTracer( Vector3 from, Vector3 to )
	{
		if ( _tracerGo != null && _tracerGo.IsValid() ) _tracerGo.Destroy();

		var go = Scene.CreateObject();
		go.Name = "Tracer";
		var mid = (from + to) * 0.5f;
		var dir = to - from;
		float length = dir.Length;
		if ( length < 1f ) length = 1f;
		go.WorldPosition = mid;
		go.WorldRotation = Rotation.LookAt( dir.Normal, Vector3.Up );
		// Box is 50x50x50; we want a thin beam length × 0.6 × 0.6
		go.WorldScale = new Vector3( length / 50f, 0.6f / 50f, 0.6f / 50f );
		var mr = go.Components.Create<ModelRenderer>();
		mr.Model = Model.Load( "models/dev/box.vmdl" );
		mr.Tint = new Color( 1.0f, 0.85f, 0.20f, 0.8f );

		_tracerGo = go;
		_tracerUntil = Time.Now + 0.08f;
	}
}
