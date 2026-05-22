using Sandbox;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Local.BodygroupMorph;

/// <summary>
/// Drives the Bodygroup + Morph Showcase:
/// - Three citizens on pedestals demonstrating bodygroups, morphs/tints, and combined effects.
/// - Camera orbits around the row of pedestals.
/// - HUD has 3 columns of live controls.
/// - AutoPlay cycles a scripted demo over ~12s for the verification video.
/// </summary>
public sealed class ShowcaseManager : Component
{
	[Property] public bool AutoPlay { get; set; } = false;

	[Property] public SkinnedModelRenderer Citizen1 { get; set; } // Bodygroups
	[Property] public SkinnedModelRenderer Citizen2 { get; set; } // Morphs / Tints
	[Property] public SkinnedModelRenderer Citizen3 { get; set; } // Combined

	[Property] public CameraComponent OrbitCamera { get; set; }
	[Property] public GameObject CenterTarget { get; set; }

	[Property, Range( 200f, 800f )] public float OrbitRadius { get; set; } = 480f;
	[Property, Range( 50f, 300f )] public float OrbitHeight { get; set; } = 110f;
	[Property, Range( 5f, 60f )] public float OrbitSpeed { get; set; } = 36f;

	// ---- Citizen 1: bodygroup toggle state ----
	public List<string> BodygroupNames { get; private set; } = new();
	public Dictionary<string, bool> BodygroupHidden { get; private set; } = new();

	// ---- Citizen 2: morph values OR tint demo ----
	public bool HasMorphs { get; private set; }
	public List<string> MorphNames { get; private set; } = new();
	public Dictionary<string, float> MorphValues { get; private set; } = new();

	// Tint demo fallback (when morphs are empty, which is the documented expectation).
	public float TintR { get; set; } = 1f;
	public float TintG { get; set; } = 1f;
	public float TintB { get; set; } = 1f;
	public int MaterialGroupIndex { get; set; } = 0;
	public List<string> MaterialGroupNames { get; private set; } = new();

	// ---- Citizen 3: combined ----
	public ClothingContainer Container3 { get; private set; } = new();
	public Dictionary<string, bool> Bg3Hidden { get; private set; } = new();
	public float Tint3R { get; set; } = 1f;
	public float Tint3G { get; set; } = 1f;
	public float Tint3B { get; set; } = 1f;
	public bool HasClothing { get; private set; }

	private float _orbitAngle = 270f; // start looking at front of pedestals (along -Y? we'll use +X view)
	private float _autoTimer = 0f;

	protected override void OnStart()
	{
		base.OnStart();

		// Standard citizen bodygroup names (per character-customization.md).
		// SetBodyGroup is a name-lookup at runtime; unknown names are silent no-ops.
		// We list the commonly-shipping ones; user can prune via inspector if needed.
		BodygroupNames = new List<string> { "head", "chest", "legs", "hands", "feet", "hair" };
		foreach ( var n in BodygroupNames )
		{
			BodygroupHidden[n] = false;
			Bg3Hidden[n] = false;
		}
		Log.Info( $"[Showcase] Bodygroup names (assumed standard set): {string.Join( ", ", BodygroupNames )}" );

		// Discover morphs on citizen 2
		if ( Citizen2.IsValid() )
		{
			try
			{
				var names = Citizen2.Morphs?.Names?.ToList();
				if ( names != null && names.Count > 0 )
				{
					HasMorphs = true;
					// Pick expressive morphs if available, otherwise fall back to first 3.
					var preferred = new[] { "jawOpen", "mouthSmile_L", "eyeBlink_L", "browInnerUp", "eyeWide_L" };
					foreach ( var p in preferred )
					{
						if ( names.Contains( p ) && MorphNames.Count < 3 )
							MorphNames.Add( p );
					}
					if ( MorphNames.Count == 0 )
						MorphNames = names.Take( 3 ).ToList();
					foreach ( var n in MorphNames ) MorphValues[n] = 0f;
				}
			}
			catch ( Exception e )
			{
				Log.Info( $"[Showcase] Morphs unavailable: {e.Message}" );
			}

			// Discover material groups (always useful)
			if ( Citizen2.Model != null )
			{
				try
				{
					var mgCount = Citizen2.Model.MaterialGroupCount;
					for ( int i = 0; i < mgCount; i++ )
					{
						var n = Citizen2.Model.GetMaterialGroupName( i );
						if ( !string.IsNullOrEmpty( n ) ) MaterialGroupNames.Add( n );
					}
				}
				catch { /* MaterialGroup API may differ across versions */ }
			}
		}
		Log.Info( $"[Showcase] HasMorphs={HasMorphs} morphs=[{string.Join( ",", MorphNames )}] matGroups=[{string.Join( ",", MaterialGroupNames )}]" );

		// Citizen 3: apply a randomized clothing set
		ApplyRandomClothing();
	}

	protected override void OnUpdate()
	{
		// Orbit camera around the row
		var center = CenterTarget.IsValid() ? CenterTarget.WorldPosition : Vector3.Zero;
		center += Vector3.Up * OrbitHeight;

		_orbitAngle += OrbitSpeed * Time.Delta;
		if ( _orbitAngle > 360f ) _orbitAngle -= 360f;

		var rad = _orbitAngle * MathF.PI / 180f;
		var camPos = center + new Vector3( MathF.Cos( rad ) * OrbitRadius, MathF.Sin( rad ) * OrbitRadius, 0 );

		if ( OrbitCamera.IsValid() )
		{
			OrbitCamera.WorldPosition = camPos;
			OrbitCamera.WorldRotation = Rotation.LookAt( (center - camPos).Normal );
		}

		if ( AutoPlay )
		{
			_autoTimer += Time.Delta;
			DriveAutoPlay( _autoTimer );
		}
	}

	private void DriveAutoPlay( float t )
	{
		// 0-3s: cycle bodygroups on citizen 1
		// 3-6s: cycle morphs/tints on citizen 2
		// 6-12s: combined effects on citizen 3
		// 12s+: loop
		float cyc = t % 12f;

		// --- Citizen 1: bodygroups ---
		// 0..1s: hide head
		// 1..2s: hide head + chest
		// 2..3s: hide all
		// 3..6s: all visible
		// 6..9s: hide head only
		// 9..12s: all visible
		bool hideHead = (cyc < 3f) || (cyc >= 6f && cyc < 9f);
		bool hideChest = (cyc >= 1f && cyc < 3f);
		bool hideHair = (cyc >= 2f && cyc < 3f);
		SetBg1IfPresent( "head", hideHead );
		SetBg1IfPresent( "chest", hideChest );
		SetBg1IfPresent( "hair", hideHair );

		// --- Citizen 2: morphs OR tints + material groups ---
		float seg2 = (cyc - 3f) / 3f;
		if ( cyc >= 3f && cyc < 6f )
		{
			if ( HasMorphs )
			{
				float v = 0.5f + 0.5f * MathF.Sin( seg2 * MathF.PI * 2f );
				int i = 0;
				foreach ( var name in MorphNames )
				{
					float phase = (i + 1) * 0.7f;
					MorphValues[name] = 0.5f + 0.5f * MathF.Sin( seg2 * MathF.PI * 2f + phase );
					ApplyMorph( name );
					i++;
				}
			}
			else
			{
				// Cycle tints
				TintR = 0.5f + 0.5f * MathF.Sin( seg2 * MathF.PI * 2f );
				TintG = 0.5f + 0.5f * MathF.Sin( seg2 * MathF.PI * 2f + 2f );
				TintB = 0.5f + 0.5f * MathF.Sin( seg2 * MathF.PI * 2f + 4f );
				ApplyCitizen2Tint();

				// Cycle material group too if any
				if ( MaterialGroupNames.Count > 1 )
				{
					int idx = (int)(seg2 * MaterialGroupNames.Count) % MaterialGroupNames.Count;
					if ( idx != MaterialGroupIndex )
					{
						MaterialGroupIndex = idx;
						ApplyCitizen2MaterialGroup();
					}
				}
			}
		}

		// --- Citizen 3: combined ---
		if ( cyc >= 6f )
		{
			float seg3 = (cyc - 6f) / 6f;
			// Toggle head visibility under hat, chest under hoodie
			SetBg3IfPresent( "head", cyc >= 8f && cyc < 10f );
			SetBg3IfPresent( "chest", cyc >= 9f );

			Tint3R = 0.6f + 0.4f * MathF.Sin( seg3 * MathF.PI * 2f );
			Tint3G = 0.6f + 0.4f * MathF.Sin( seg3 * MathF.PI * 2f + 2f );
			Tint3B = 0.6f + 0.4f * MathF.Sin( seg3 * MathF.PI * 2f + 4f );
			ApplyCitizen3Tint();
		}
	}

	// ---------------- Public UI hooks ----------------

	public void ToggleBg1( string name )
	{
		if ( !BodygroupHidden.ContainsKey( name ) ) return;
		BodygroupHidden[name] = !BodygroupHidden[name];
		if ( Citizen1.IsValid() )
			Citizen1.SetBodyGroup( name, BodygroupHidden[name] ? 1 : 0 );
	}

	private void SetBg1IfPresent( string name, bool hidden )
	{
		if ( !BodygroupHidden.ContainsKey( name ) ) return;
		if ( BodygroupHidden[name] == hidden ) return;
		BodygroupHidden[name] = hidden;
		if ( Citizen1.IsValid() )
			Citizen1.SetBodyGroup( name, hidden ? 1 : 0 );
	}

	public void SetMorph( string name, float value )
	{
		MorphValues[name] = value;
		ApplyMorph( name );
	}

	private void ApplyMorph( string name )
	{
		if ( !Citizen2.IsValid() ) return;
		try { Citizen2.Morphs.Set( name, MorphValues[name] ); } catch { }
	}

	public void ApplyCitizen2Tint()
	{
		if ( !Citizen2.IsValid() ) return;
		Citizen2.Tint = new Color( TintR, TintG, TintB, 1f );
	}

	public void ApplyCitizen2MaterialGroup()
	{
		if ( !Citizen2.IsValid() ) return;
		if ( MaterialGroupIndex < 0 || MaterialGroupIndex >= MaterialGroupNames.Count ) return;
		try { Citizen2.MaterialGroup = MaterialGroupNames[MaterialGroupIndex]; } catch { }
	}

	public void CycleMaterialGroup( int dir )
	{
		if ( MaterialGroupNames.Count == 0 ) return;
		MaterialGroupIndex = ((MaterialGroupIndex + dir) % MaterialGroupNames.Count + MaterialGroupNames.Count) % MaterialGroupNames.Count;
		ApplyCitizen2MaterialGroup();
	}

	public void ToggleBg3( string name )
	{
		if ( !Bg3Hidden.ContainsKey( name ) ) return;
		Bg3Hidden[name] = !Bg3Hidden[name];
		ApplyBg3();
	}

	private void SetBg3IfPresent( string name, bool hidden )
	{
		if ( !Bg3Hidden.ContainsKey( name ) ) return;
		if ( Bg3Hidden[name] == hidden ) return;
		Bg3Hidden[name] = hidden;
		ApplyBg3();
	}

	private void ApplyBg3()
	{
		if ( !Citizen3.IsValid() ) return;
		foreach ( var kv in Bg3Hidden )
			Citizen3.SetBodyGroup( kv.Key, kv.Value ? 1 : 0 );
	}

	public void ApplyCitizen3Tint()
	{
		if ( !Citizen3.IsValid() ) return;
		Citizen3.Tint = new Color( Tint3R, Tint3G, Tint3B, 1f );
	}

	public void ApplyRandomClothing()
	{
		if ( !Citizen3.IsValid() ) return;
		var all = ResourceLibrary.GetAll<Clothing>().ToList();
		if ( all.Count == 0 )
		{
			Log.Info( "[Showcase] No clothing resources found." );
			return;
		}
		HasClothing = true;

		// Pick one from each category if possible
		Container3 = new ClothingContainer();
		var byCat = all.GroupBy( c => c.Category.ToString() ).ToList();
		var rng = new Random( 7 );
		foreach ( var g in byCat )
		{
			var pick = g.ElementAt( rng.Next( g.Count() ) );
			Container3.Add( pick );
		}
		Container3.Normalize();
		Container3.Apply( Citizen3 );

		// After Apply, re-apply our bodygroup toggles
		ApplyBg3();
	}
}
