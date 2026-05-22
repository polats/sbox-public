using Sandbox;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace Local.ArenaThemes;

/// <summary>
/// Orchestrates the Arena Theme demo: loads all ArenaTheme GameResources,
/// rebuilds the arena (floor color, sky color, props, lighting) when the
/// theme changes, listens for 1-4 keypresses, persists the last-picked
/// theme via FileSystem.Data, and optionally cycles themes when AutoPlay.
/// </summary>
public sealed class GameManager : Component
{
	[Property] public bool AutoPlay { get; set; } = false;
	[Property, Range( 0.5f, 8f )] public float AutoInterval { get; set; } = 2f;

	[Property, Group( "Arena" ), Range( 200f, 2000f )] public float ArenaHalfExtent { get; set; } = 500f;
	[Property, Group( "Arena" )] public string DefaultThemeName { get; set; } = "Forest";

	public List<ArenaTheme> Themes { get; private set; } = new();
	public ArenaTheme CurrentTheme { get; private set; }
	public int CurrentIndex { get; private set; } = 0;

	private GameObject _floorGo;
	private GameObject _propsRoot;
	private DirectionalLight _sun;
	private CameraComponent _camera;

	private float _autoTimer = 0f;

	private const string SavePath = "arena-themes/last.json";

	protected override void OnStart()
	{
		base.OnStart();

		_camera = Scene.Camera;
		_sun = Scene.GetAllComponents<DirectionalLight>().FirstOrDefault();

		// Discover every ArenaTheme that ships in Assets/themes/.
		Themes = ResourceLibrary.GetAll<ArenaTheme>()
			.OrderBy( t => t.DisplayName )
			.ToList();

		Log.Info( $"[ArenaThemes] Loaded {Themes.Count} themes: {string.Join( ", ", Themes.Select( t => t.DisplayName ) )}" );

		EnsureFloor();
		EnsurePropsRoot();

		// Restore last-picked theme, else fall back to default-by-name.
		var startName = ReadLastThemeName() ?? DefaultThemeName;
		var idx = Themes.FindIndex( t => string.Equals( t.DisplayName, startName, StringComparison.OrdinalIgnoreCase ) );
		if ( idx < 0 ) idx = 0;
		SwitchTo( idx );
	}

	protected override void OnUpdate()
	{
		if ( Themes.Count == 0 ) return;

		// 1-4 (or more) keyboard switching.
		for ( int i = 0; i < Math.Min( Themes.Count, 9 ); i++ )
		{
			if ( Input.Pressed( $"Slot{i + 1}" ) )
			{
				SwitchTo( i );
			}
		}

		if ( AutoPlay )
		{
			_autoTimer += Time.Delta;
			if ( _autoTimer >= AutoInterval )
			{
				_autoTimer = 0f;
				SwitchTo( (CurrentIndex + 1) % Themes.Count );
			}
		}
	}

	public void SwitchTo( int index )
	{
		if ( Themes.Count == 0 ) return;
		index = ((index % Themes.Count) + Themes.Count) % Themes.Count;
		CurrentIndex = index;
		var theme = Themes[index];
		CurrentTheme = theme;

		ApplyTheme( theme );
		SaveLastThemeName( theme.DisplayName );

		Log.Info( $"[ArenaThemes] Switched to theme '{theme.DisplayName}' (index {index}/{Themes.Count})" );
	}

	private void ApplyTheme( ArenaTheme theme )
	{
		// Floor color
		if ( _floorGo != null )
		{
			var mr = _floorGo.Components.Get<ModelRenderer>();
			if ( mr != null ) mr.Tint = theme.FloorColor;
		}

		// Sky / camera clear color
		if ( _camera != null )
		{
			_camera.BackgroundColor = theme.SkyColor;
		}
		if ( _sun != null )
		{
			_sun.SkyColor = theme.SkyColor;
			// Boost or dim the sun light to reflect AmbientIntensity.
			_sun.LightColor = new Color( 1f, 0.97f, 0.9f ) * Math.Max( 0.2f, theme.AmbientIntensity );
		}

		// Replace props
		if ( _propsRoot != null )
		{
			foreach ( var child in _propsRoot.Children.ToList() )
				child.Destroy();
		}

		if ( theme.PropModel != null && _propsRoot != null )
		{
			var rng = new Random( theme.DisplayName.GetHashCode() );
			for ( int i = 0; i < theme.PropCount; i++ )
			{
				float ang = (float)(rng.NextDouble() * Math.PI * 2f);
				float r = MathF.Sqrt( (float)rng.NextDouble() ) * ArenaHalfExtent * 0.85f;
				// Keep props away from the spawn point (center).
				r = Math.Max( r, 120f );
				float x = MathF.Cos( ang ) * r;
				float y = MathF.Sin( ang ) * r;

				var go = new GameObject( true, $"Prop_{i}" )
				{
					Parent = _propsRoot,
					WorldPosition = new Vector3( x, y, 0 ),
					WorldRotation = Rotation.FromYaw( (float)rng.NextDouble() * 360f ),
					WorldScale = new Vector3( theme.PropScale, theme.PropScale, theme.PropScale ),
				};
				var mr = go.Components.Create<ModelRenderer>();
				mr.Model = theme.PropModel;
			}
		}
	}

	private void EnsureFloor()
	{
		_floorGo = Scene.Children.FirstOrDefault( c => c.Name == "Floor" );
		if ( _floorGo != null ) return;

		_floorGo = new GameObject( true, "Floor" )
		{
			WorldPosition = new Vector3( 0, 0, 0 ),
			WorldScale = new Vector3( ArenaHalfExtent / 25f, ArenaHalfExtent / 25f, 0.2f ),
		};
		var mr = _floorGo.Components.Create<ModelRenderer>();
		mr.Model = Model.Load( "models/dev/box.vmdl" );
		mr.Tint = Color.Gray;

		// Static collider so the CharacterController has something to stand on.
		var box = _floorGo.Components.Create<BoxCollider>();
		box.Scale = new Vector3( 50f, 50f, 50f );
		box.Center = Vector3.Zero;
	}

	private void EnsurePropsRoot()
	{
		_propsRoot = Scene.Children.FirstOrDefault( c => c.Name == "Props" );
		if ( _propsRoot != null ) return;

		_propsRoot = new GameObject( true, "Props" )
		{
			WorldPosition = Vector3.Zero,
		};
	}

	// ------------------- Persistence (FileSystem.Data) -------------------

	private record SavedThemeState( string theme );

	private static string ReadLastThemeName()
	{
		try
		{
			if ( !FileSystem.Data.FileExists( SavePath ) )
				return null;
			var json = FileSystem.Data.ReadAllText( SavePath );
			var data = JsonSerializer.Deserialize<SavedThemeState>( json );
			return data?.theme;
		}
		catch ( Exception e )
		{
			Log.Warning( $"[ArenaThemes] Failed to read save file: {e.Message}" );
			return null;
		}
	}

	private static void SaveLastThemeName( string themeName )
	{
		try
		{
			var json = JsonSerializer.Serialize( new SavedThemeState( themeName ) );
			FileSystem.Data.WriteAllText( SavePath, json );
		}
		catch ( Exception e )
		{
			Log.Warning( $"[ArenaThemes] Failed to write save file: {e.Message}" );
		}
	}
}
