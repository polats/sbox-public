using Sandbox;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Local.MaterialShowcase;

/// <summary>
/// Top-level controller for the Material Showcase scene.
/// - Identifies which pedestal the cursor is over (camera ray vs pedestals).
/// - Toggles the global PostProcessVolume bloom on key '1' so the user can
///   see how HDR Tints + custom shaders react to bloom.
/// - In AutoPlay mode, slowly orbits the gallery focusing on each pedestal
///   for ~2 seconds, then loops.
/// </summary>
public sealed class GameManager : Component
{
	[Property] public bool AutoPlay { get; set; } = false;
	[Property, Range( 0.5f, 6f )] public float SecondsPerPedestal { get; set; } = 2.0f;
	[Property, Range( 100f, 800f )] public float OrbitRadius { get; set; } = 380f;
	[Property, Range( 30f, 200f )] public float OrbitHeight { get; set; } = 110f;

	public Pedestal HoverPedestal { get; private set; }
	public bool BloomEnabled { get; private set; } = true;

	private List<Pedestal> _pedestals = new();
	private CameraComponent _camera;
	private Bloom _bloom;
	private float _autoTimer = 0f;
	private int _autoIdx = 0;

	protected override void OnStart()
	{
		base.OnStart();

		_camera = Scene.Camera;

		// Discover bloom on the PostProcessVolume.
		_bloom = Scene.GetAllComponents<Bloom>().FirstOrDefault();

		// Order pedestals left-to-right (by world Y, since they're in a row along Y).
		_pedestals = Scene.GetAllComponents<Pedestal>()
			.OrderBy( p => p.GameObject.WorldPosition.y )
			.ToList();

		Log.Info( $"[MaterialShowcase] {_pedestals.Count} pedestals found:" );
		foreach ( var p in _pedestals )
			Log.Info( $"  - {p.DisplayName}: {p.ApproachLabel}" );
	}

	protected override void OnUpdate()
	{
		UpdateHover();
		UpdateInput();

		if ( AutoPlay )
			UpdateAutoOrbit();
	}

	private void UpdateInput()
	{
		// '1' toggles bloom.
		if ( Input.Pressed( "Slot1" ) && _bloom != null )
		{
			BloomEnabled = !BloomEnabled;
			_bloom.Enabled = BloomEnabled;
			Log.Info( $"[MaterialShowcase] Bloom: {(BloomEnabled ? "ON" : "OFF")}" );
		}
	}

	private void UpdateHover()
	{
		if ( _camera == null )
		{
			HoverPedestal = null;
			return;
		}

		var ray = _camera.ScreenPixelToRay( Mouse.Position );
		var tr = Scene.Trace.Ray( ray, 5000f )
			.WithoutTags( "nohover" )
			.Run();

		if ( !tr.Hit || tr.GameObject == null )
		{
			HoverPedestal = null;
			return;
		}

		// Walk up to find a Pedestal component.
		var go = tr.GameObject;
		Pedestal found = null;
		while ( go != null )
		{
			found = go.Components.Get<Pedestal>();
			if ( found != null ) break;
			go = go.Parent;
		}
		HoverPedestal = found;
	}

	private void UpdateAutoOrbit()
	{
		if ( _pedestals.Count == 0 || _camera == null ) return;

		_autoTimer += Time.Delta;
		if ( _autoTimer >= SecondsPerPedestal )
		{
			_autoTimer = 0f;
			_autoIdx = ( _autoIdx + 1 ) % _pedestals.Count;
		}

		// Smoothly interpolate camera between two adjacent pedestals.
		float t = MathF.Min( _autoTimer / SecondsPerPedestal, 1f );
		var target = _pedestals[_autoIdx].GameObject.WorldPosition + new Vector3( 0, 0, 60f );

		// Camera offset: orbit point sits OrbitRadius units along -X (looking at +Y row).
		// Actually pedestals lie on the Y axis row, so we step the camera in Y too.
		var lookAt = target;
		var camPos = new Vector3( -OrbitRadius, lookAt.y, OrbitHeight + lookAt.z * 0.2f );

		// Smooth in towards target (no abrupt jumps between pedestals).
		_camera.WorldPosition = Vector3.Lerp( _camera.WorldPosition, camPos, Time.Delta * 3.5f );
		var aim = ( lookAt - _camera.WorldPosition ).Normal;
		_camera.WorldRotation = Rotation.LookAt( aim, Vector3.Up );
	}
}
