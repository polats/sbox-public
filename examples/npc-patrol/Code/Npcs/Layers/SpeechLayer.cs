using Sandbox;

namespace Local.NpcPatrol.Npcs.Layers;

/// <summary>
/// Minimal speech layer — keeps a single subtitle string above the NPC and a cooldown
/// so the guard doesn't spam "Hey you!". A trimmed-down version of
/// sandbox/Code/Npcs/Layers/SpeechLayer.cs.
/// </summary>
public sealed class SpeechLayer : BaseNpcLayer
{
	public string CurrentSpeech { get; private set; }
	public bool IsSpeaking => CurrentSpeech is not null;

	[Property] public float Cooldown { get; set; } = 4f;

	private TimeSince _lastSpoke = 100f;
	private TimeUntil _subtitleEnd;

	public bool CanSpeak => _lastSpoke > Cooldown;

	public void Say( string message, float duration = 2.5f )
	{
		if ( string.IsNullOrEmpty( message ) ) return;
		CurrentSpeech = message;
		_subtitleEnd = duration;
		_lastSpoke = 0;
	}

	public void Stop()
	{
		CurrentSpeech = null;
	}

	protected override void OnUpdate()
	{
		if ( CurrentSpeech is not null && _subtitleEnd )
			CurrentSpeech = null;

		if ( CurrentSpeech is not null )
			DrawSpeech();
	}

	public override string GetDebugString() => IsSpeaking ? $"\"{CurrentSpeech}\"" : null;

	private void DrawSpeech()
	{
		if ( Npc.Scene.Camera is null ) return;

		var worldPos = Npc.WorldPosition + Vector3.Up * 88f;
		var screenPos = Npc.Scene.Camera.PointToScreenPixels( worldPos, out var behind );
		if ( behind ) return;

		var text = TextRendering.Scope.Default;
		text.Text = CurrentSpeech;
		text.FontSize = 16;
		text.FontName = "Poppins";
		text.FontWeight = 600;
		text.TextColor = Color.White;
		text.Outline = new TextRendering.Outline { Color = Color.Black.WithAlpha( 0.8f ), Size = 3, Enabled = true };

		Npc.DebugOverlay.ScreenText( screenPos, text, TextFlag.CenterBottom );
	}

	public override void ResetLayer() => Stop();
}
