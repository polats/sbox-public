using Sandbox;
using System;
using System.Linq;

namespace Local.FlappyBird;

public sealed class Pipe : Component
{
	[Property] public float ScrollSpeed { get; set; } = 220f;
	[Property] public float DespawnX { get; set; } = -600f;
	[Property] public float CollisionHalfWidth { get; set; } = 24f;
	[Property] public float GapTop { get; set; } = 350f;
	[Property] public float GapBottom { get; set; } = 200f;

	public bool Scored { get; set; }

	private GameManager _gm;
	private Bird _bird;

	protected override void OnStart()
	{
		base.OnStart();
		_gm = Scene.GetAllComponents<GameManager>().FirstOrDefault();
		_bird = Scene.GetAllComponents<Bird>().FirstOrDefault();
	}

	protected override void OnUpdate()
	{
		if ( _gm is null || _gm.State != GameState.Playing ) return;

		var p = WorldPosition;
		p.x -= ScrollSpeed * Time.Delta;
		WorldPosition = p;

		if ( p.x < DespawnX )
		{
			GameObject.Destroy();
			return;
		}

		if ( _bird is null ) return;

		var birdX = _bird.WorldPosition.x;
		var birdZ = _bird.WorldPosition.z;

		if ( !Scored && p.x < birdX - CollisionHalfWidth )
		{
			Scored = true;
			_gm.AddScore();
		}

		var dx = MathF.Abs( birdX - p.x );
		if ( dx <= CollisionHalfWidth + _bird.CollisionRadius )
		{
			if ( birdZ > GapTop - _bird.CollisionRadius || birdZ < GapBottom + _bird.CollisionRadius )
			{
				_gm.GameOver();
			}
		}
	}
}
