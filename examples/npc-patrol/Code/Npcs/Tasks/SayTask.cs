using Sandbox;

namespace Local.NpcPatrol.Npcs.Tasks;

/// <summary>One-shot speech task. Completes immediately; the SpeechLayer keeps the subtitle visible for the duration.</summary>
public sealed class SayTask : TaskBase
{
	public string Message { get; set; }
	public float Duration { get; set; }

	public SayTask( string message, float duration = 2.5f )
	{
		Message = message;
		Duration = duration;
	}

	protected override void OnStart()
	{
		Npc.Speech.Say( Message, Duration );
	}

	protected override TaskStatus OnUpdate() => TaskStatus.Success;
}
