using Sandbox;
using System;
using System.Linq;

namespace Local.FlappyBird;

public sealed class PipeSpawner : Component
{
	[Property] public float SpawnInterval { get; set; } = 1.8f;
	[Property] public float SpawnX { get; set; } = 500f;
	[Property] public float GapSize { get; set; } = 150f;
	[Property] public float GapMinCenter { get; set; } = 200f;
	[Property] public float GapMaxCenter { get; set; } = 420f;
	[Property] public float ScrollSpeed { get; set; } = 220f;
	[Property] public string PipeModel { get; set; } = "models/sbox_props/gutters/gutter_wall_pipe_b.vmdl";
	[Property] public Vector3 PipeScale { get; set; } = new Vector3( 8, 8, 8 );

	private GameManager _gm;
	private float _spawnTimer;

	protected override void OnStart()
	{
		base.OnStart();
		_gm = Scene.GetAllComponents<GameManager>().FirstOrDefault();
		_spawnTimer = 0.6f;
	}

	public void Reset()
	{
		foreach ( var p in Scene.GetAllComponents<Pipe>().ToList() )
		{
			p.GameObject.Destroy();
		}
		_spawnTimer = 0.6f;
	}

	protected override void OnUpdate()
	{
		if ( _gm is null || _gm.State != GameState.Playing ) return;

		_spawnTimer -= Time.Delta;
		if ( _spawnTimer > 0f ) return;
		_spawnTimer = SpawnInterval;

		var centerZ = Random.Shared.Float( GapMinCenter, GapMaxCenter );
		SpawnPipePair( centerZ );
	}

	private void SpawnPipePair( float centerZ )
	{
		var gapTop = centerZ + GapSize * 0.5f;
		var gapBottom = centerZ - GapSize * 0.5f;

		var parent = new GameObject( true, "PipePair" );
		parent.WorldPosition = new Vector3( SpawnX, 0, 0 );

		var pipeCmp = parent.Components.Create<Pipe>();
		pipeCmp.ScrollSpeed = ScrollSpeed;
		pipeCmp.GapTop = gapTop;
		pipeCmp.GapBottom = gapBottom;
		pipeCmp.CollisionHalfWidth = 24f;

		var topVisualLen = 800f;
		var topPipe = new GameObject( true, "Top" );
		topPipe.Parent = parent;
		topPipe.LocalPosition = new Vector3( 0, 0, gapTop + topVisualLen * 0.5f );
		topPipe.LocalRotation = Rotation.FromPitch( 90 );
		topPipe.LocalScale = PipeScale;
		var topR = topPipe.Components.Create<ModelRenderer>();
		topR.Model = Model.Load( PipeModel );
		topR.Tint = new Color( 0.25f, 0.85f, 0.35f );

		var bottomPipe = new GameObject( true, "Bottom" );
		bottomPipe.Parent = parent;
		bottomPipe.LocalPosition = new Vector3( 0, 0, gapBottom - topVisualLen * 0.5f );
		bottomPipe.LocalRotation = Rotation.FromPitch( 90 );
		bottomPipe.LocalScale = PipeScale;
		var botR = bottomPipe.Components.Create<ModelRenderer>();
		botR.Model = Model.Load( PipeModel );
		botR.Tint = new Color( 0.25f, 0.85f, 0.35f );
	}
}
