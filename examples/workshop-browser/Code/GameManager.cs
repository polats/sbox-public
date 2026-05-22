using Sandbox;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Local.WorkshopBrowser;

/// <summary>
/// Drives the Workshop Browser demo. Owns the list of browsable packages,
/// the current asset selection (the "spawner payload"), and the spawn pipeline.
///
/// Demonstrates the *pattern* of Package.MountAsync(ident) for cloud addons.
/// In the sandboxed editor we can't rely on sbox.game auth being valid, so we
/// try real Package.FindAsync first, and seed a simulated catalog as a fallback
/// — the UX is identical from the user's perspective, only the underlying
/// MountAsync call differs.
/// </summary>
public sealed class GameManager : Component
{
	[Property] public bool AutoPlay { get; set; } = false;
	[Property, Range( 0.5f, 6f )] public float AutoInterval { get; set; } = 1.5f;

	/// <summary>Use a hardcoded simulated catalog rather than calling Package.FindAsync.</summary>
	[Property] public bool ForceSimulated { get; set; } = false;

	/// <summary>The browse query passed to Package.FindAsync.</summary>
	[Property] public string DefaultQuery { get; set; } = "type:asset";

	// ------------------------------------------------------------------
	// Public state observed by HUD / WorkshopPanel
	// ------------------------------------------------------------------
	public List<WorkshopPackageInfo> Packages { get; private set; } = new();
	public List<WorkshopPackageInfo> FilteredPackages =>
		string.IsNullOrWhiteSpace( SearchText )
			? Packages
			: Packages.Where( p =>
				(p.Title ?? "").Contains( SearchText, StringComparison.OrdinalIgnoreCase ) ||
				(p.Author ?? "").Contains( SearchText, StringComparison.OrdinalIgnoreCase ) ||
				(p.Description ?? "").Contains( SearchText, StringComparison.OrdinalIgnoreCase ) ).ToList();

	public string SearchText { get; set; } = "";
	public WorkshopPackageInfo SelectedPackage { get; private set; }
	public WorkshopAsset SelectedAsset { get; private set; }

	public bool IsLoadingList { get; private set; }
	public int MountedCount => Packages.Count( p => p.IsMounted );

	/// <summary>True when at least one real cloud Package.MountAsync call returned a non-null result.</summary>
	public bool RealCloudWorked { get; private set; }
	public string Status { get; private set; } = "Idle";

	// ------------------------------------------------------------------
	// Spawned props
	// ------------------------------------------------------------------
	public List<GameObject> SpawnedProps { get; } = new();

	private GameObject _propsRoot;
	private GameObject _floor;
	private float _autoTimer;
	private int _autoStep;
	private bool _autoMountInFlight;
	private CameraComponent _camera;

	protected override async void OnStart()
	{
		base.OnStart();

		EnsureScene();

		// Kick off the initial query.
		_ = RefreshAsync();
	}

	private void EnsureScene()
	{
		_camera = Scene.Camera;

		_floor = Scene.Children.FirstOrDefault( c => c.Name == "Floor" );
		if ( _floor == null )
		{
			_floor = new GameObject( true, "Floor" )
			{
				WorldPosition = Vector3.Zero,
				WorldScale = new Vector3( 30f, 30f, 0.4f ),
			};
			var mr = _floor.Components.Create<ModelRenderer>();
			mr.Model = Model.Load( "models/dev/box.vmdl" );
			mr.Tint = new Color( 0.22f, 0.24f, 0.28f );

			var box = _floor.Components.Create<BoxCollider>();
			box.Scale = new Vector3( 50f, 50f, 50f );
		}

		_propsRoot = Scene.Children.FirstOrDefault( c => c.Name == "Props" );
		if ( _propsRoot == null )
			_propsRoot = new GameObject( true, "Props" ) { WorldPosition = Vector3.Zero };
	}

	protected override void OnUpdate()
	{
		if ( !AutoPlay ) return;
		if ( IsLoadingList ) return;

		_autoTimer -= Time.Delta;
		if ( _autoTimer > 0f ) return;
		_autoTimer = AutoInterval;

		// Step 0..1: mount the first two packages so the UI shows multiple "Mounted" markers.
		if ( _autoStep < 2 )
		{
			var target = Packages.Skip( _autoStep ).FirstOrDefault( p => !p.IsMounted );
			if ( target != null && !_autoMountInFlight )
			{
				_autoMountInFlight = true;
				_ = MountAndAdvance( target );
				return;
			}
			if ( target == null ) _autoStep++;
			return;
		}

		// Step 2: select the first mounted package.
		if ( _autoStep == 2 )
		{
			var first = Packages.FirstOrDefault( p => p.IsMounted );
			if ( first != null )
			{
				SelectPackage( first );
				_autoStep++;
			}
			return;
		}

		// Step 3+: cycle through assets and spawn one per tick.
		if ( SelectedPackage?.Assets?.Count > 0 )
		{
			var rng = new Random( _autoStep );
			var asset = SelectedPackage.Assets[(_autoStep - 3) % SelectedPackage.Assets.Count];
			SelectedAsset = asset;
			float r = 80f + (float)rng.NextDouble() * 220f;
			float a = (float)(rng.NextDouble() * Math.PI * 2.0);
			var pos = new Vector3( MathF.Cos( a ) * r, MathF.Sin( a ) * r, 50f );
			SpawnAt( pos );
			_autoStep++;
		}
	}

	private async Task MountAndAdvance( WorkshopPackageInfo p )
	{
		await MountPackageAsync( p );
		_autoMountInFlight = false;
	}

	// ------------------------------------------------------------------
	// Workshop list
	// ------------------------------------------------------------------

	public async Task RefreshAsync()
	{
		IsLoadingList = true;
		Status = "Querying sbox.game…";
		Packages.Clear();

		var loaded = false;
		if ( !ForceSimulated )
		{
			try
			{
				var result = await Package.FindAsync( DefaultQuery, take: 12 );
				if ( result?.Packages != null && result.Packages.Length > 0 )
				{
					foreach ( var pkg in result.Packages )
					{
						Packages.Add( new WorkshopPackageInfo
						{
							Ident = pkg.FullIdent,
							Title = pkg.Title ?? pkg.Ident ?? "(untitled)",
							Author = pkg.Org?.Title ?? pkg.Org?.Ident ?? "unknown",
							Description = string.IsNullOrWhiteSpace( pkg.Summary ) ? "Cloud package from sbox.game" : pkg.Summary,
							IconGlyph = "cloud_download",
							TintHint = ColorFromHash( pkg.FullIdent ),
							IsSimulated = false,
						} );
					}
					loaded = true;
					Status = $"Loaded {Packages.Count} packages from sbox.game.";
				}
			}
			catch ( Exception e )
			{
				Log.Warning( $"[WorkshopBrowser] Package.FindAsync failed: {e.Message}" );
			}
		}

		if ( !loaded )
		{
			SeedSimulatedCatalog();
			Status = "Using simulated catalog (sbox.game unreachable or empty).";
		}

		IsLoadingList = false;
		Log.Info( $"[WorkshopBrowser] {Status}" );
	}

	private void SeedSimulatedCatalog()
	{
		// 8 fake "cloud" packages, each with a model that's known to ship with the engine
		// so spawning always works without needing the actual sbox.game backend.
		var seeds = new[]
		{
			new WorkshopPackageInfo
			{
				Ident = "local.sim.props_office",
				Title = "Office Props",
				Author = "facepunch",
				Description = "Desks, chairs, monitors. Classic office furniture pack.",
				IconGlyph = "chair",
				TintHint = new Color( 0.6f, 0.7f, 0.9f ),
				Assets = new()
				{
					new() { Name = "Crate", ModelPath = "models/dev/box.vmdl", Tint = new Color(0.7f,0.5f,0.3f) },
					new() { Name = "Pillar", ModelPath = "models/dev/box.vmdl", Tint = new Color(0.4f,0.4f,0.5f) },
					new() { Name = "Cube", ModelPath = "models/dev/box.vmdl", Tint = new Color(0.9f,0.9f,0.9f) },
				}
			},
			new WorkshopPackageInfo
			{
				Ident = "local.sim.weapon_pack",
				Title = "Weapon Pack — Vol 1",
				Author = "facepunch",
				Description = "Pistols, rifles, melee. Animated 3rd-person ready.",
				IconGlyph = "build",
				TintHint = new Color( 0.9f, 0.4f, 0.3f ),
				Assets = new()
				{
					new() { Name = "Block-A", ModelPath = "models/dev/box.vmdl", Tint = new Color(0.85f,0.2f,0.2f) },
					new() { Name = "Block-B", ModelPath = "models/dev/box.vmdl", Tint = new Color(0.95f,0.55f,0.1f) },
				}
			},
			new WorkshopPackageInfo
			{
				Ident = "local.sim.citizen_clothing",
				Title = "Citizen Clothing",
				Author = "facepunch",
				Description = "Hats, shirts, pants, shoes for the citizen avatar.",
				IconGlyph = "checkroom",
				TintHint = new Color( 0.4f, 0.8f, 0.5f ),
				Assets = new()
				{
					new() { Name = "Hat", ModelPath = "models/dev/box.vmdl", Tint = new Color(0.2f,0.7f,0.3f) },
					new() { Name = "Shirt", ModelPath = "models/dev/box.vmdl", Tint = new Color(0.3f,0.85f,0.55f) },
				}
			},
			new WorkshopPackageInfo
			{
				Ident = "local.sim.nature_trees",
				Title = "Nature: Trees",
				Author = "envato",
				Description = "Oak, pine, birch. LODs included.",
				IconGlyph = "park",
				TintHint = new Color( 0.3f, 0.7f, 0.4f ),
				Assets = new()
				{
					new() { Name = "Trunk", ModelPath = "models/dev/box.vmdl", Tint = new Color(0.35f,0.22f,0.12f) },
					new() { Name = "Crown", ModelPath = "models/dev/box.vmdl", Tint = new Color(0.18f,0.55f,0.22f) },
				}
			},
			new WorkshopPackageInfo
			{
				Ident = "local.sim.sci_fi_props",
				Title = "Sci-Fi Props",
				Author = "stellarworks",
				Description = "Crates, terminals, glowing panels. Sci-fi flair.",
				IconGlyph = "rocket",
				TintHint = new Color( 0.4f, 0.5f, 1f ),
				Assets = new()
				{
					new() { Name = "Terminal", ModelPath = "models/dev/box.vmdl", Tint = new Color(0.2f,0.5f,1f) },
					new() { Name = "Crate", ModelPath = "models/dev/box.vmdl", Tint = new Color(0.6f,0.7f,1f) },
					new() { Name = "Pillar", ModelPath = "models/dev/box.vmdl", Tint = new Color(0.9f,0.4f,1f) },
				}
			},
			new WorkshopPackageInfo
			{
				Ident = "local.sim.lo_fi_pack",
				Title = "Lo-Fi Pixel Pack",
				Author = "pixelfox",
				Description = "Stylised low-poly props. Soft palette.",
				IconGlyph = "palette",
				TintHint = new Color( 1f, 0.7f, 0.5f ),
				Assets = new()
				{
					new() { Name = "Cube", ModelPath = "models/dev/box.vmdl", Tint = new Color(1f,0.6f,0.4f) },
					new() { Name = "Slab", ModelPath = "models/dev/box.vmdl", Tint = new Color(1f,0.85f,0.6f) },
				}
			},
			new WorkshopPackageInfo
			{
				Ident = "local.sim.audio_fx",
				Title = "Ambient Audio FX",
				Author = "soundroom",
				Description = "Footsteps, wind, rain, UI clicks.",
				IconGlyph = "graphic_eq",
				TintHint = new Color( 0.8f, 0.7f, 0.3f ),
				Assets = new()
				{
					new() { Name = "Speaker", ModelPath = "models/dev/box.vmdl", Tint = new Color(0.9f,0.8f,0.3f) },
				}
			},
			new WorkshopPackageInfo
			{
				Ident = "local.sim.test_gadgets",
				Title = "Test Gadgets",
				Author = "indiedev",
				Description = "Small experimental items. Bouncing cubes, light beacons.",
				IconGlyph = "scatter_plot",
				TintHint = new Color( 0.7f, 0.4f, 0.9f ),
				Assets = new()
				{
					new() { Name = "Beacon", ModelPath = "models/dev/box.vmdl", Tint = new Color(0.7f,0.3f,0.95f) },
					new() { Name = "Bouncer", ModelPath = "models/dev/box.vmdl", Tint = new Color(0.95f,0.3f,0.7f) },
				}
			},
		};

		foreach ( var s in seeds )
		{
			s.IsSimulated = true;
			Packages.Add( s );
		}
	}

	// ------------------------------------------------------------------
	// Mount
	// ------------------------------------------------------------------

	/// <summary>
	/// Mount a workshop package — the canonical entry point for "install this
	/// addon from the cloud into the current scene's content browser".
	///
	/// For real packages this is just <see cref="Package.MountAsync(string, bool)"/>.
	/// For simulated packages we flip the IsMounted flag — same UI affordance.
	/// </summary>
	public async Task MountPackageAsync( WorkshopPackageInfo info )
	{
		if ( info == null || info.IsMounted ) return;

		Status = $"Mounting {info.Title}…";
		Log.Info( $"[WorkshopBrowser] Mounting {info.Ident} (simulated={info.IsSimulated})" );

		if ( !info.IsSimulated )
		{
			try
			{
				// CANONICAL PATTERN — the real cloud mount call.
				var pkg = await Package.MountAsync( info.Ident, partial: false );
				if ( pkg != null && pkg.IsMounted() )
				{
					RealCloudWorked = true;
					info.IsMounted = true;
					// We have no way of reliably enumerating individual model files inside
					// a cloud package without engine-specific manifest parsing; fall back to
					// a built-in placeholder asset for spawning so the demo stays useful.
					if ( info.Assets.Count == 0 )
					{
						info.Assets.Add( new WorkshopAsset
						{
							Name = $"{info.Title} placeholder",
							ModelPath = "models/dev/box.vmdl",
							Tint = info.TintHint,
						} );
					}
					Status = $"Mounted {info.Title} from sbox.game.";
					return;
				}
				Status = $"Mount of {info.Ident} returned null — falling back to simulated.";
			}
			catch ( Exception e )
			{
				Log.Warning( $"[WorkshopBrowser] MountAsync failed for {info.Ident}: {e.Message}" );
				Status = $"Cloud mount failed: {e.Message}";
			}

			// Real mount failed — promote to simulated so the rest of the demo still works.
			info.IsSimulated = true;
			info.Assets = new List<WorkshopAsset>
			{
				new() { Name = $"{info.Title} A", ModelPath = "models/dev/box.vmdl", Tint = info.TintHint },
				new() { Name = $"{info.Title} B", ModelPath = "models/dev/box.vmdl", Tint = info.TintHint * 0.6f },
			};
		}

		// Simulated path — mark mounted, the assets are already pre-populated.
		await Task.Delay( 200 ); // tiny artificial delay so the user sees the spinner
		info.IsMounted = true;
		Status = $"Mounted (simulated) {info.Title}.";
	}

	// ------------------------------------------------------------------
	// Selection + Spawn
	// ------------------------------------------------------------------

	public void SelectPackage( WorkshopPackageInfo info )
	{
		SelectedPackage = info;
		SelectedAsset = info?.Assets?.FirstOrDefault();
	}

	public void SelectAsset( WorkshopAsset asset )
	{
		SelectedAsset = asset;
	}

	/// <summary>Spawn the currently-selected asset at the given world position.</summary>
	public void SpawnAt( Vector3 position )
	{
		var asset = SelectedAsset;
		if ( asset == null ) return;

		var go = new GameObject( true, $"Spawned_{SpawnedProps.Count}" )
		{
			Parent = _propsRoot,
			WorldPosition = position + Vector3.Up * 20f,
			WorldRotation = Rotation.FromYaw( (float)(new Random().NextDouble() * 360.0) ),
			WorldScale = new Vector3( 0.3f, 0.3f, 0.3f ),
		};

		var mr = go.Components.Create<ModelRenderer>();
		mr.Model = Model.Load( asset.ModelPath );
		mr.Tint = asset.Tint;

		SpawnedProps.Add( go );
		Log.Info( $"[WorkshopBrowser] Spawned '{asset.Name}' at {position} (total {SpawnedProps.Count})" );
	}

	public void ClearSpawned()
	{
		foreach ( var go in SpawnedProps.ToList() )
			go.Destroy();
		SpawnedProps.Clear();
	}

	// ------------------------------------------------------------------
	// Helpers
	// ------------------------------------------------------------------

	private static Color ColorFromHash( string s )
	{
		if ( string.IsNullOrEmpty( s ) ) return Color.White;
		int h = s.GetHashCode();
		float r = ((h & 0xFF) / 255f) * 0.6f + 0.3f;
		float g = (((h >> 8) & 0xFF) / 255f) * 0.6f + 0.3f;
		float b = (((h >> 16) & 0xFF) / 255f) * 0.6f + 0.3f;
		return new Color( r, g, b );
	}
}
