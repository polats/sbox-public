using Sandbox;
using System;
using System.Linq;

namespace Local.Achievements;

/// <summary>
/// Floor button. Player walks onto its top to press it. Animates by dipping
/// down briefly when pressed. Reports to the AchievementManager.
/// </summary>
public sealed class ColorButton : Component
{
	[Property] public string ColorId { get; set; } = "red";
	[Property] public Color Tint { get; set; } = Color.Red;

	/// <summary>If pressed at least once this attempt.</summary>
	public bool IsPressed { get; private set; }

	private ModelRenderer _renderer;
	private Vector3 _restPos;
	private float _pressedTime = -999f;

	protected override void OnStart()
	{
		base.OnStart();

		_renderer = Components.Get<ModelRenderer>();
		if ( _renderer != null )
			_renderer.Tint = Tint;

		_restPos = GameObject.LocalPosition;
	}

	protected override void OnUpdate()
	{
		// Spring-back animation
		var since = Time.Now - _pressedTime;
		var dip = since < 0.4f ? MathF.Max( 0f, 1f - (since / 0.4f) ) * 3f : 0f;
		GameObject.LocalPosition = _restPos - new Vector3( 0, 0, dip );

		// Detect overlap by distance
		if ( !IsPressed )
		{
			var player = Scene.GetAllComponents<PuzzlePlayer>().FirstOrDefault();
			if ( player.IsValid() )
			{
				var d2 = (player.GameObject.WorldPosition - GameObject.WorldPosition).WithZ( 0 ).LengthSquared;
				// Buttons are 30x30 inches in XY; trigger when player center is within 22 in.
				if ( d2 < 22f * 22f )
				{
					Press();
				}
			}
		}
	}

	private void Press()
	{
		IsPressed = true;
		_pressedTime = Time.Now;

		// Brighten on press to make it visually distinct.
		if ( _renderer != null )
			_renderer.Tint = Tint * 1.4f;

		Sound.Play( "ui.button.press" );

		AchievementManager.Instance?.NotifyButtonPressed( ColorId );
	}
}
