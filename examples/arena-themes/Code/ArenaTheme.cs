using Sandbox;

namespace Local.ArenaThemes;

/// <summary>
/// A visual theme for the arena. Defines floor color, prop model + count, sky tint,
/// and ambient lighting intensity. Ships as a `.theme` GameResource so multiple
/// themes can live in Assets/themes/ and be picked at runtime.
/// </summary>
[GameResource( "Arena Theme", "theme", "Visual theme for the arena (floor color, prop model, sky tint).", Icon = "palette" )]
public class ArenaTheme : GameResource
{
	[Property] public string DisplayName { get; set; } = "Unnamed";

	[Property, Group( "Colors" )] public Color FloorColor { get; set; } = Color.Gray;
	[Property, Group( "Colors" )] public Color SkyColor { get; set; } = new Color( 0.05f, 0.06f, 0.10f );

	[Property, Group( "Props" )] public Model PropModel { get; set; }
	[Property, Group( "Props" ), Range( 4, 32 )] public int PropCount { get; set; } = 12;
	[Property, Group( "Props" ), Range( 0.05f, 3f )] public float PropScale { get; set; } = 1f;

	[Property, Group( "Lighting" ), Range( 0.1f, 3f )] public float AmbientIntensity { get; set; } = 1f;
}
