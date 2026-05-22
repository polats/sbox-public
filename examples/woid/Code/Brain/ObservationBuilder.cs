using System.Collections.Generic;
using System.Linq;

namespace Woid;

/// <summary>
/// Builds an Observation from a Character's current state. See CONTRACT.md §Observation.
/// First slice: no perception events, no moodlets. Phase 1.5 adds those.
/// </summary>
public static class ObservationBuilder
{
	static int _tick;

	public static Dictionary<string, object> Build( Character c, ObjectRegistry objects = null, CharacterRegistry characters = null )
	{
		_tick++;
		var nearby = objects?.All()
			.Select( o => new { id = o.ObjectId, type = o.Type, occupant = o.Occupant } )
			.ToArray() ?? System.Array.Empty<object>();

		// Other characters in scene + their latest spoken line (for the LLM to react to).
		var others = characters?.All()
			.Where( other => other.CharacterId != c.CharacterId )
			.Select( other => new {
				id = other.CharacterId,
				name = string.IsNullOrEmpty( other.PersonaName ) ? other.CharacterId : other.PersonaName,
				vibe = other.PersonaVibe,
				last_said = other.LastSpeech,
				last_said_to = other.LastSpeechTo,
				current_anim = other.CurrentAnim,
			} ).ToArray() ?? System.Array.Empty<object>();

		return new Dictionary<string, object>
		{
			["selfId"] = c.CharacterId,
			["tick"] = _tick,
			["trigger"] = new { kind = "heartbeat" },
			["perception"] = System.Array.Empty<object>(),
			["needs"] = new Dictionary<string, float>( c.Needs ),
			["moodlets"] = System.Array.Empty<object>(),
			["traits"] = System.Array.Empty<string>(),
			["game"] = new
			{
				location = new { x = c.GameObject.WorldPosition.x, y = c.GameObject.WorldPosition.y, z = c.GameObject.WorldPosition.z },
				current_anim = c.CurrentAnim,
				nearby_objects = nearby,
				nearby_characters = others,
			},
		};
	}
}
