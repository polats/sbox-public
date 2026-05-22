using Sandbox;
using System.Linq;

namespace Local.HammerLevel;

/// <summary>
/// Top-level state for the Hammer-Level demo. Counts targets shot.
/// </summary>
public sealed class GameController : Component
{
	public int Score { get; private set; }
	public int Shots { get; private set; }

	[Property] public SoundEvent ImpactSound { get; set; }

	public void OnShotFired( BaseWeapon weapon ) { Shots++; }
	public void OnTargetHit( Target t, Vector3 pos ) { Score++; }
}
