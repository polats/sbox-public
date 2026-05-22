using System.Collections.Generic;

namespace Local.Achievements;

/// <summary>
/// Static catalog of the 5 achievements in this game. Order is the display
/// order in the always-visible panel.
/// </summary>
public static class AchievementCatalog
{
	public record Def( string Id, string Title, string Description );

	public static readonly List<Def> All = new()
	{
		new( "first_button",    "First Touch",      "Press any button." ),
		new( "all_colors",      "Full Spectrum",    "Press every colored button." ),
		new( "sequence_master", "Sequence Master",  "Press in rainbow order: red, yellow, green, blue, purple." ),
		new( "speedrun",        "Speedrunner",      "Press all 5 within 10 seconds." ),
		new( "pacifist",        "Pacifist",         "Complete Full Spectrum without touching the pedestal." ),
	};

	public static Def Get( string id ) => All.Find( a => a.Id == id );
}
