using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Woid;

/// <summary>
/// Effect kernel — what sbox actually applies to the world.
/// Discriminated by "kind". See CONTRACT.md §Effect.
/// Unknown kinds are dropped (not errored) so woid can add new kinds
/// without breaking older sbox builds.
/// </summary>
public abstract class Effect
{
	public string Kind { get; init; }

	/// <summary>Parse one Effect from a JsonElement. Returns null for unknown kinds.</summary>
	public static Effect Parse( JsonElement json )
	{
		if ( !json.TryGetProperty( "kind", out var kindEl ) ) return null;
		var kind = kindEl.GetString();
		return kind switch
		{
			"set_pos"        => Deser<SetPos>( json ),
			"play_anim"      => Deser<PlayAnim>( json ),
			"occupy"         => Deser<Occupy>( json ),
			"need"           => Deser<Need>( json ),
			"moodlet"        => Deser<Moodlet>( json ),
			"advance_sim"    => Deser<AdvanceSim>( json ),
			"speech_bubble"  => Deser<SpeechBubble>( json ),
			"perceive"       => Deser<Perceive>( json ),
			"sit_on_chair"   => Deser<SitOnChair>( json ),
			"stand_up"       => Deser<StandUp>( json ),
			"sleep_in_bed"   => Deser<SleepInBed>( json ),
			"drink_holdable" => Deser<DrinkHoldable>( json ),
			_                => null, // unknown kind → drop, see CONTRACT.md
		};
	}

	public static List<Effect> ParseArray( JsonElement arr )
	{
		var list = new List<Effect>();
		foreach ( var item in arr.EnumerateArray() )
		{
			var e = Parse( item );
			if ( e != null ) list.Add( e );
			else Log.Info( $"[Effect] dropped unknown kind: {item}" );
		}
		return list;
	}

	private static T Deser<T>( JsonElement json ) where T : Effect
	{
		return JsonSerializer.Deserialize<T>( json.GetRawText(), Json.Opts );
	}
}

public sealed class SetPos : Effect
{
	[JsonPropertyName( "actor" )]     public string Actor { get; init; }
	[JsonPropertyName( "x" )]         public float X { get; init; }
	[JsonPropertyName( "y" )]         public float Y { get; init; }
	[JsonPropertyName( "z" )]         public float Z { get; init; }
}

public sealed class PlayAnim : Effect
{
	[JsonPropertyName( "actor" )] public string Actor { get; init; }
	[JsonPropertyName( "name" )]  public string Name  { get; init; }
	[JsonPropertyName( "loop" )]  public bool   Loop  { get; init; }
}

public sealed class Occupy : Effect
{
	[JsonPropertyName( "actor" )]     public string Actor    { get; init; }
	[JsonPropertyName( "object_id" )] public string ObjectId { get; init; }
	[JsonPropertyName( "release" )]   public bool   Release  { get; init; }
}

public sealed class Need : Effect
{
	[JsonPropertyName( "actor" )]  public string Actor  { get; init; }
	[JsonPropertyName( "axis" )]   public string Axis   { get; init; }
	[JsonPropertyName( "op" )]     public string Op     { get; init; }   // "+", "-", "="
	[JsonPropertyName( "amount" )] public float  Amount { get; init; }
}

public sealed class Moodlet : Effect
{
	[JsonPropertyName( "actor" )]       public string Actor      { get; init; }
	[JsonPropertyName( "id" )]          public string Id         { get; init; }
	[JsonPropertyName( "weight" )]      public float  Weight     { get; init; }
	[JsonPropertyName( "duration_ms" )] public long   DurationMs { get; init; }
}

public sealed class AdvanceSim : Effect
{
	[JsonPropertyName( "minutes" )] public float Minutes { get; init; }
}

public sealed class SpeechBubble : Effect
{
	[JsonPropertyName( "actor" )]       public string Actor      { get; init; }
	[JsonPropertyName( "text" )]        public string Text       { get; init; }
	[JsonPropertyName( "to" )]          public string To         { get; init; }
	[JsonPropertyName( "duration_ms" )] public long   DurationMs { get; init; }
}

public sealed class Perceive : Effect
{
	[JsonPropertyName( "target" )] public string         Target { get; init; }
	[JsonPropertyName( "event" )]  public JsonElement    Event  { get; init; }
}

/// <summary>
/// Sit-on-chair: actor walks to the chair via NavMesh, then snap-sits
/// once within seat distance. Composite over set_pos+play_anim+occupy.
/// </summary>
public sealed class SitOnChair : Effect
{
	[JsonPropertyName( "actor" )]     public string Actor    { get; init; }
	[JsonPropertyName( "object_id" )] public string ObjectId { get; init; }
}

public sealed class StandUp : Effect
{
	[JsonPropertyName( "actor" )]     public string Actor    { get; init; }
	[JsonPropertyName( "object_id" )] public string ObjectId { get; init; }
}

public sealed class SleepInBed : Effect
{
	[JsonPropertyName( "actor" )]     public string Actor    { get; init; }
	[JsonPropertyName( "object_id" )] public string ObjectId { get; init; }
}

/// <summary>
/// Pick up a HoldableProp object and "use" it (b_attack pulses while held).
/// Drop after duration_ms; durationMs 0 = hold indefinitely until next walk.
/// </summary>
public sealed class DrinkHoldable : Effect
{
	[JsonPropertyName( "actor" )]       public string Actor      { get; init; }
	[JsonPropertyName( "object_id" )]   public string ObjectId   { get; init; }
}

