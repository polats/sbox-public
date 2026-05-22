using Sandbox;
using System;
using System.Collections.Generic;

namespace Local.MpErgonomics;

/// <summary>
/// Builds a small lobby room and spawns four named fake-player citizens
/// (Alice/Bob/Carol/Dave) on its floor. Each gets a <see cref="FakePlayer"/>
/// data component plus a world-space <see cref="WorldPanel"/> hosting a
/// <see cref="Nameplate"/> Razor panel that floats above their head.
/// </summary>
public sealed class Lobby : Component
{
	[Property] public bool BuildOnStart { get; set; } = true;

	private static readonly (string name, long steamId, int score, int deaths, float health, bool isFriend, Vector3 pos)[] FakePlayers = new[]
	{
		("Alice",  76561197960265728L,  17,  8, 92f, true,  new Vector3(  200, -150, 0 )),
		("Bob",    76561197960265729L,   9, 14, 78f, false, new Vector3( -180,  100, 0 )),
		("Carol",  76561197960265730L,  22,  5, 100f, true, new Vector3(  150,  200, 0 )),
		("Dave",   76561197960265731L,   4, 21, 45f, false, new Vector3( -200, -120, 0 )),
	};

	protected override void OnStart()
	{
		if ( !BuildOnStart ) return;
		BuildRoom();
		SpawnFakePlayers();
	}

	private void BuildRoom()
	{
		var floor = new GameObject( true, "Floor" );
		floor.SetParent( GameObject );
		floor.WorldPosition = new Vector3( 0, 0, -16 );
		var fr = floor.Components.Create<ModelRenderer>();
		fr.Model = Model.Load( "models/dev/plane.vmdl" );
		fr.Tint = new Color( 0.55f, 0.6f, 0.68f, 1f );
		floor.WorldScale = new Vector3( 12, 12, 1 );

		// Four walls
		BuildWall( new Vector3( 0, 600, 100 ), new Vector3( 12, 0.4f, 2f ), new Color( 0.5f, 0.45f, 0.4f ) );
		BuildWall( new Vector3( 0, -600, 100 ), new Vector3( 12, 0.4f, 2f ), new Color( 0.5f, 0.45f, 0.4f ) );
		BuildWall( new Vector3( 600, 0, 100 ), new Vector3( 0.4f, 12, 2f ), new Color( 0.5f, 0.45f, 0.4f ) );
		BuildWall( new Vector3( -600, 0, 100 ), new Vector3( 0.4f, 12, 2f ), new Color( 0.5f, 0.45f, 0.4f ) );

		// A few decorations: pillars
		for ( int i = 0; i < 4; i++ )
		{
			var ang = i * MathF.PI / 2f + MathF.PI / 4f;
			var pos = new Vector3( MathF.Cos( ang ) * 420f, MathF.Sin( ang ) * 420f, 80 );
			var pillar = new GameObject( true, $"Pillar{i}" );
			pillar.SetParent( GameObject );
			pillar.WorldPosition = pos;
			var pr = pillar.Components.Create<ModelRenderer>();
			pr.Model = Model.Load( "models/dev/box.vmdl" );
			pr.Tint = new Color( 0.4f, 0.4f, 0.45f );
			pillar.WorldScale = new Vector3( 0.5f, 0.5f, 1.6f );
		}
	}

	private void BuildWall( Vector3 pos, Vector3 scale, Color tint )
	{
		var w = new GameObject( true, "Wall" );
		w.SetParent( GameObject );
		w.WorldPosition = pos;
		w.WorldScale = scale;
		var r = w.Components.Create<ModelRenderer>();
		r.Model = Model.Load( "models/dev/box.vmdl" );
		r.Tint = tint;
	}

	private void SpawnFakePlayers()
	{
		foreach ( var def in FakePlayers )
		{
			var go = new GameObject( true, def.name );
			go.SetParent( GameObject );
			go.WorldPosition = def.pos;
			// face the lobby centre
			var toCentre = (-def.pos).WithZ( 0 );
			if ( toCentre.Length > 1 )
				go.WorldRotation = Rotation.LookAt( toCentre.Normal, Vector3.Up );

			var fp = go.Components.Create<FakePlayer>();
			fp.PlayerName = def.name;
			fp.FakeSteamId = def.steamId;
			fp.Score = def.score;
			fp.Deaths = def.deaths;
			fp.Health = def.health;
			fp.IsFriend = def.isFriend;
			fp.IsLocal = false;
			fp.Ping = 20 + (int)((def.steamId & 0x3f));

			// Citizen model child
			var modelGo = new GameObject( true, "Citizen" );
			modelGo.SetParent( go );
			var smr = modelGo.Components.Create<SkinnedModelRenderer>();
			smr.Model = Model.Load( "models/citizen_human/citizen_human_male.vmdl" );
			smr.UseAnimGraph = true;
			smr.Set( "b_grounded", true );

			// Nameplate as WorldPanel floating above head
			var npGo = new GameObject( true, "NameplateAnchor" );
			npGo.SetParent( go );
			npGo.LocalPosition = new Vector3( 0, 0, 90 );
			var wp = npGo.Components.Create<WorldPanel>();
			wp.PanelSize = new Vector2( 600, 200 );
			wp.RenderScale = 1f;
			wp.LookAtCamera = true;
			var npComp = npGo.Components.Create<Nameplate>();
			npComp.Player = fp;
		}
	}
}
