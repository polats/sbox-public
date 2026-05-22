using Sandbox;
using System;
using System.Linq;

namespace Local.Pool;

/// <summary>
/// Visual aim indicator: a thin stretched box from cue ball along aim direction.
/// Hidden while shooting / when balls are moving.
/// </summary>
public sealed class AimLine : Component
{
	public const float Length = 30f;
	public const float Thickness = 0.4f;

	private GameController _gm;
	private ModelRenderer _renderer;

	protected override void OnStart()
	{
		base.OnStart();
		_gm = Scene.GetAllComponents<GameController>().FirstOrDefault();
		_renderer = Components.Get<ModelRenderer>();
		if ( _renderer == null )
		{
			_renderer = Components.Create<ModelRenderer>();
			_renderer.Model = Model.Load( "models/dev/box.vmdl" );
			_renderer.Tint = new Color( 1f, 1f, 1f, 0.7f );
		}
	}

	protected override void OnUpdate()
	{
		if ( _gm == null || _gm.CueBall == null || !_gm.CueBall.IsValid() )
		{
			if ( _renderer != null ) _renderer.Enabled = false;
			return;
		}
		bool visible = _gm.State == GameState.Aiming;
		if ( _renderer != null ) _renderer.Enabled = visible;
		if ( !visible ) return;

		var ball = _gm.CueBall.WorldPosition;
		var dir = _gm.AimDirection;
		float len = Length + _gm.PowerNormalized * 30f;
		var center = ball + dir * (len * 0.5f + GameController.BallRadius);
		WorldPosition = center;
		WorldRotation = Rotation.LookAt( dir, Vector3.Up );
		// Box is 50x50x50. Scale to (len, thick, thick).
		WorldScale = new Vector3( len / 50f, Thickness / 50f, Thickness / 50f );
		if ( _renderer != null )
		{
			// red as it charges up
			float p = _gm.PowerNormalized;
			_renderer.Tint = new Color( 1f, 1f - p, 1f - p, 0.85f );
		}
	}
}
