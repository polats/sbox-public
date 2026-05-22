using System;
using System.Collections.Generic;
using Sandbox;

namespace Woid;

/// <summary>
/// A character in the world. Cache of woid-side state (needs, moodlets) plus
/// the visible bits (position, animation). Updated by EffectInterpreter.
///
/// Movement: SetPos effect sets _targetPos; OnUpdate walks toward it and
/// drives the citizen animgraph params (pattern from
/// examples/npc-patrol/Code/Npcs/Layers/AnimationLayer.cs).
/// </summary>
public sealed class Character : Component
{
	/// <summary>Woid-side character id. Must match /character POST.</summary>
	[Property] public string CharacterId { get; set; }

	[Property] public SkinnedModelRenderer Model { get; set; }

	[Property] public string PersonaName { get; set; } = "";
	[Property, TextArea] public string PersonaAbout { get; set; } = "";
	[Property] public string PersonaVibe { get; set; } = "";
	[Property] public string BrainProvider { get; set; } = "utility";

	[Property, Range( 0, 100 )] public float InitialEnergy { get; set; } = 100f;
	[Property, Range( 0, 100 )] public float InitialSocial { get; set; } = 60f;
	[Property, Range( 0, 100 )] public float InitialHunger { get; set; } = 80f;

	[Property] public float WalkSpeed { get; set; } = 120f;
	[Property] public float TurnSpeed { get; set; } = 6f;
	[Property] public float StopDistance { get; set; } = 6f;

	public Dictionary<string, float> Needs { get; } = new();

	public string CurrentAnim { get; private set; } = "idle";

	public string LastSpeech { get; private set; } = "";
	public string LastSpeechTo { get; private set; } = "";

	Vector3 _targetPos;
	bool _hasTarget;
	bool _sitting;

	protected override void OnStart()
	{
		base.OnStart();
		Needs["energy"] = InitialEnergy;
		Needs["social"] = InitialSocial;
		Needs["hunger"] = InitialHunger;
		if ( Model == null ) Model = Components.GetInDescendantsOrSelf<SkinnedModelRenderer>();
		_targetPos = GameObject.WorldPosition;

		// Idle pose: ground the citizen so it leaves T-pose immediately.
		if ( Model != null )
		{
			Model.Set( "b_grounded", true );
			Model.Set( "move_speed", 0f );
		}
	}

	protected override void OnUpdate()
	{
		var cur = GameObject.WorldPosition;
		var velocity = Vector3.Zero;

		if ( _hasTarget )
		{
			var delta = _targetPos - cur;
			var dist = delta.Length;

			if ( dist > StopDistance )
			{
				var dir = delta.Normal;
				var step = WalkSpeed * Time.Delta;
				if ( step > dist ) step = dist;
				var newPos = cur + dir * step;
				GameObject.WorldPosition = newPos;
				velocity = dir * WalkSpeed;

				// Face direction of travel
				if ( dir.WithZ( 0 ).Length > 0.01f )
				{
					var targetRot = Rotation.LookAt( dir.WithZ( 0 ).Normal, Vector3.Up );
					GameObject.WorldRotation = Rotation.Lerp( GameObject.WorldRotation, targetRot, TurnSpeed * Time.Delta );
				}
			}
			else
			{
				GameObject.WorldPosition = _targetPos;
				_hasTarget = false;
				velocity = Vector3.Zero;
			}
		}

		ApplyAnim( velocity );
	}

	void ApplyAnim( Vector3 velocity )
	{
		if ( Model == null ) return;

		// Common citizen animgraph params. Pattern from npc-patrol's AnimationLayer.
		var reference = GameObject.WorldRotation;
		var forward = reference.Forward.Dot( velocity );
		var sideward = reference.Right.Dot( velocity );

		Model.Set( "b_grounded", true );
		Model.Set( "move_speed", velocity.WithZ( 0 ).Length );
		Model.Set( "move_groundspeed", velocity.WithZ( 0 ).Length );
		Model.Set( "move_x", forward );
		Model.Set( "move_y", sideward );
		Model.Set( "move_z", velocity.z );
		Model.Set( "speed_move", 1f );

		// Sit pose if currently sitting
		if ( _sitting )
		{
			Model.Set( "sit", 1 );
			Model.Set( "b_sit", true );
		}
		else
		{
			Model.Set( "sit", 0 );
			Model.Set( "b_sit", false );
		}
	}

	/// <summary>Set by SetPos effect. Character walks here over time.</summary>
	public void WalkTo( Vector3 worldPos )
	{
		_targetPos = worldPos;
		_hasTarget = true;
		// Don't keep the sit pose while walking somewhere new.
		if ( (worldPos - GameObject.WorldPosition).Length > StopDistance ) _sitting = false;
	}

	public void PlayAnimation( string name, bool loop )
	{
		CurrentAnim = name;
		_sitting = name == "sit";
		Log.Info( $"[Character {CharacterId}] anim → {name} (loop={loop})" );
	}

	public void MutateNeed( string axis, string op, float amount )
	{
		if ( !Needs.ContainsKey( axis ) ) Needs[axis] = 0;
		var v = Needs[axis];
		v = op switch
		{
			"+" => v + amount,
			"-" => v - amount,
			"=" => amount,
			_   => v,
		};
		Needs[axis] = Math.Clamp( v, 0f, 100f );
	}

	public void ShowSpeechBubble( string text, string to, long durationMs )
	{
		LastSpeech = text ?? "";
		LastSpeechTo = to ?? "";
		Log.Info( $"[Character {CharacterId}] says{(string.IsNullOrEmpty(to) ? "" : $" to {to}")}: \"{text}\"" );
	}
}
