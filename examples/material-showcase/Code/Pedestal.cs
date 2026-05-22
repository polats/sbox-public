using Sandbox;
using System.Linq;

namespace Local.MaterialShowcase;

/// <summary>
/// Sits on the root GameObject of a pedestal. Identifies the material variant
/// being shown so the HUD can name it on mouse-hover. Applies the chosen
/// material to the child sphere on OnStart so we don't have to hand-author
/// MaterialOverride in the .scene JSON (which serializes awkwardly).
/// </summary>
public sealed class Pedestal : Component
{
	public enum Variant
	{
		DefaultPbr,
		BrightTint,
		GlowEmissive,
		CustomHologram,
		CustomDissolve,
		MetallicVmat,
	}

	[Property] public string DisplayName { get; set; } = "Unnamed";
	[Property, TextArea] public string Description { get; set; } = "";
	[Property] public Variant Style { get; set; } = Variant.DefaultPbr;
	[Property] public Color SphereTint { get; set; } = Color.White;
	[Property] public Color PedestalTint { get; set; } = Color.Gray;

	public string ApproachLabel { get; private set; } = "";

	protected override void OnStart()
	{
		base.OnStart();

		// Tint the pedestal box itself.
		var selfMr = Components.Get<ModelRenderer>();
		if ( selfMr != null )
			selfMr.Tint = PedestalTint;

		// Find the SkinnedModelRenderer/ModelRenderer on a child named "Sphere".
		var sphereGo = GameObject.Children.FirstOrDefault( c => c.Name == "Sphere" );
		if ( sphereGo == null ) return;

		var renderer = sphereGo.Components.Get<ModelRenderer>();
		if ( renderer == null ) return;

		switch ( Style )
		{
			case Variant.DefaultPbr:
				renderer.Tint = SphereTint;
				ApproachLabel = "stock PBR shader";
				break;

			case Variant.BrightTint:
				renderer.Tint = SphereTint;
				ApproachLabel = "stock shader, saturated Tint";
				break;

			case Variant.GlowEmissive:
				// HDR tint > 1 gets picked up by PostProcessVolume Bloom.
				renderer.Tint = SphereTint;
				ApproachLabel = "stock shader, HDR Tint -> Bloom";
				break;

			case Variant.CustomHologram:
				ApplyMaterial( renderer, "materials/hologram.vmat" );
				ApproachLabel = "custom .shader (hologram)";
				break;

			case Variant.CustomDissolve:
				ApplyMaterial( renderer, "materials/dissolve.vmat" );
				ApproachLabel = "custom .shader (dissolve)";
				break;

			case Variant.MetallicVmat:
				ApplyMaterial( renderer, "materials/metallic.vmat" );
				ApproachLabel = "hand-authored .vmat (complex.vfx + metalness)";
				break;
		}
	}

	private void ApplyMaterial( ModelRenderer renderer, string vmatPath )
	{
		try
		{
			var mat = Material.Load( vmatPath );
			if ( mat == null )
			{
				Log.Warning( $"[Pedestal] Could not load material '{vmatPath}' for '{DisplayName}' — falling back to tint." );
				renderer.Tint = SphereTint;
				return;
			}
			renderer.MaterialOverride = mat;
		}
		catch ( System.Exception e )
		{
			Log.Warning( $"[Pedestal] Material load threw for '{vmatPath}': {e.Message}. Falling back to tint." );
			renderer.Tint = SphereTint;
		}
	}
}
