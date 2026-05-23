using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Sandbox;

namespace Woid;

/// <summary>
/// HTTP client to brain-server. Owns the per-character tick loop.
/// Host-authoritative: only one of these runs (on the host) per session.
///
/// Uses Sandbox.Http (whitelisted) — direct System.Net.Http.HttpClient
/// is blocked in game code. URLs must be in .sbproj Metadata.HttpAllowList.
///
/// See examples/woid/CONTRACT.md for the wire format.
/// </summary>
public sealed class WoidClient : Component
{
	[Property] public string BaseUrl { get; set; } = "http://127.0.0.1:8080";
	[Property] public float TickIntervalSec { get; set; } = 2.0f;

	[Property] public CharacterRegistry Characters { get; set; }
	[Property] public ObjectRegistry    Objects    { get; set; }
	[Property] public EffectInterpreter Interpreter { get; set; }

	readonly Dictionary<string, float> _nextTickAt = new();
	readonly HashSet<string> _inFlight = new();
	readonly HashSet<string> _registered = new();
	readonly Queue<string> _toSpawn = new();

	public string LastStatusState { get; private set; } = "unknown";
	public string LastError { get; private set; }

	/// <summary>Force this character's next tick to fire immediately on the next OnUpdate.</summary>
	public void RequestTick( string characterId )
	{
		_nextTickAt[characterId] = 0f;
		Log.Info( $"[WoidClient] force-tick requested for {characterId}" );
	}

	/// <summary>Force-tick every registered character.</summary>
	public void RequestTickAll()
	{
		foreach ( var id in _registered ) _nextTickAt[id] = 0f;
		Log.Info( $"[WoidClient] force-tick requested for ALL ({_registered.Count})" );
	}

	protected override void OnStart()
	{
		base.OnStart();
		foreach ( var c in Characters.All() ) _toSpawn.Enqueue( c.CharacterId );
		_ = BootAsync();
	}

	protected override void OnUpdate()
	{
		foreach ( var c in Characters.All() )
		{
			if ( !_registered.Contains( c.CharacterId ) ) continue;
			if ( _inFlight.Contains( c.CharacterId ) ) continue;
			var next = _nextTickAt.GetValueOrDefault( c.CharacterId, 0f );
			if ( Time.Now < next ) continue;
			_nextTickAt[c.CharacterId] = Time.Now + TickIntervalSec;
			_ = TickOneAsync( c );
		}
	}

	async Task BootAsync()
	{
		for ( int i = 0; i < 30; i++ )
		{
			var ok = await FetchStatusAsync();
			if ( ok && LastStatusState == "ready" ) break;
			await Task.Delay( 500 );
		}
		while ( _toSpawn.Count > 0 )
		{
			var id = _toSpawn.Dequeue();
			await EnsureCharacterAsync( id );
		}
	}

	async Task<bool> FetchStatusAsync()
	{
		try
		{
			var s = await Http.RequestStringAsync( $"{BaseUrl}/status" );
			using var doc = JsonDocument.Parse( s );
			if ( !doc.RootElement.GetProperty( "ok" ).GetBoolean() ) return false;
			LastStatusState = doc.RootElement.GetProperty( "data" ).GetProperty( "state" ).GetString();
			return true;
		}
		catch ( Exception e )
		{
			LastError = $"status: {e.Message}";
			return false;
		}
	}

	async Task EnsureCharacterAsync( string id )
	{
		if ( _registered.Contains( id ) ) return;
		var c = Characters.Get( id );
		if ( c == null ) return;
		try
		{
			var body = JsonSerializer.Serialize( new
			{
				id = id,
				identity = new
				{
					name = string.IsNullOrWhiteSpace( c.PersonaName ) ? id : c.PersonaName,
					about = c.PersonaAbout ?? "",
					vibe = c.PersonaVibe ?? "",
				},
				brain_provider = string.IsNullOrWhiteSpace( c.BrainProvider ) ? "utility" : c.BrainProvider,
			} );
			var resp = await PostJsonAsync( "/character", body );
			if ( resp.GetProperty( "ok" ).GetBoolean() )
			{
				_registered.Add( id );
				Log.Info( $"[WoidClient] registered {id} (brain={c.BrainProvider})" );
			}
		}
		catch ( Exception e ) { LastError = $"register {id}: {e.Message}"; }
	}

	async Task TickOneAsync( Character c )
	{
		_inFlight.Add( c.CharacterId );
		try
		{
			var obs = ObservationBuilder.Build( c, Objects, Characters );
			var body = JsonSerializer.Serialize( obs );
			var resp = await PostJsonAsync( "/brain/step", body );

			if ( !resp.GetProperty( "ok" ).GetBoolean() ) return;
			if ( !resp.GetProperty( "data" ).TryGetProperty( "actions", out var actionsEl ) ) return;

			foreach ( var actionEl in actionsEl.EnumerateArray() )
			{
				await ResolveAndApplyAsync( c.CharacterId, actionEl );
			}
		}
		catch ( Exception e ) { LastError = $"tick {c.CharacterId}: {e.Message}"; }
		finally { _inFlight.Remove( c.CharacterId ); }
	}

	async Task ResolveAndApplyAsync( string actor, JsonElement actionEl )
	{
		// First slice: /verb/resolve not implemented yet. Local synthesis.
		// Phase 1.5 replaces this with a POST to /verb/resolve.
		var verb = actionEl.GetProperty( "verb" ).GetString();
		var args = actionEl.GetProperty( "args" );
		var effects = SynthesizeEffects( actor, verb, args );
		Interpreter.Apply( effects );
		await Task.CompletedTask;
	}

	List<Effect> SynthesizeEffects( string actor, string verb, JsonElement args )
	{
		var list = new List<Effect>();
		switch ( verb )
		{
			case "sit":
				var objId = args.GetProperty( "object_id" ).GetString();
				if ( Objects.Get( objId ) == null )
				{
					Log.Warning( $"[SynthesizeEffects] sit: unknown object {objId}" );
					break;
				}
				// Composite effect: walks via NavMesh, snaps to seat on arrival.
				list.Add( new SitOnChair { Kind = "sit_on_chair", Actor = actor, ObjectId = objId } );
				list.Add( new Occupy { Kind = "occupy", Actor = actor, ObjectId = objId, Release = false } );
				list.Add( new Need { Kind = "need", Actor = actor, Axis = "energy", Op = "+", Amount = 5 } );
				break;

			case "say":
				var text = args.TryGetProperty( "text", out var txEl ) ? txEl.GetString() ?? "" : "";
				var to   = args.TryGetProperty( "to",   out var toEl ) ? toEl.GetString() ?? "" : "";
				list.Add( new SpeechBubble { Kind = "speech_bubble", Actor = actor, Text = text, To = to, DurationMs = 5000 } );
				list.Add( new Need { Kind = "need", Actor = actor, Axis = "social", Op = "+", Amount = 8 } );

				// Look-at: speaker → listener and listener → speaker.
				var speaker = Characters.Get( actor );
				var listener = !string.IsNullOrEmpty( to ) ? Characters.Get( to ) : null;
				if ( speaker.IsValid() && listener.IsValid() )
				{
					speaker.LookAt( listener.GameObject );
					listener.LookAt( speaker.GameObject );
				}

				// Push the speech back into brain-server's perception bus.
				_ = PushPerceptionAsync( target: !string.IsNullOrEmpty( to ) ? to : "*all*", evt: new {
					kind = "speech", from_id = actor, text, to,
					ts = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
				} );
				break;
		}
		return list;
	}

	async Task PushPerceptionAsync( string target, object evt )
	{
		try
		{
			var body = JsonSerializer.Serialize( new { target, @event = evt } );
			await PostJsonAsync( "/perception/event", body );
		}
		catch ( Exception e ) { LastError = $"perception/event: {e.Message}"; }
	}

	async Task<JsonElement> PostJsonAsync( string path, string body )
	{
		// Sandbox.Http.RequestStringAsync signature: (url, method, content, headers, ct)
		using var content = new StringContent( body, Encoding.UTF8, "application/json" );
		var s = await Http.RequestStringAsync( $"{BaseUrl}{path}", "POST", content );
		using var doc = JsonDocument.Parse( s );
		return doc.RootElement.Clone();
	}
}
