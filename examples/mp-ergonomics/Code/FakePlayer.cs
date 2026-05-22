using Sandbox;

namespace Local.MpErgonomics;

/// <summary>
/// A simulated "player" — stands in for a real <see cref="Connection"/> in this
/// single-process demo. All ergonomics features (nameplate, scoreboard, voice
/// indicator, kill feed, chat highlighting) read off these fields so the same
/// UI code would Just Work in a real networked game.
/// </summary>
public sealed class FakePlayer : Component
{
	[Property, Sync] public string PlayerName { get; set; } = "Player";
	[Property, Sync] public long FakeSteamId { get; set; } = 0;
	[Property, Sync] public int Score { get; set; } = 0;
	[Property, Sync] public int Deaths { get; set; } = 0;
	[Property, Sync] public float Health { get; set; } = 100f;
	[Property, Sync] public bool IsTalking { get; set; } = false;
	[Property, Sync] public int Ping { get; set; } = 35;
	[Property, Sync] public bool IsFriend { get; set; } = false;
	[Property, Sync] public bool IsLocal { get; set; } = false;
}
