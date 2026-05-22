using Sandbox;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace Local.Achievements;

/// <summary>
/// Central achievement bookkeeping. Tracks button-press history, evaluates
/// the 5 conditions, fires Sandbox.Services.Achievements.Unlock, queues
/// toast notifications, and persists unlocked state via FileSystem.Data.
/// </summary>
public sealed class AchievementManager : Component
{
	private const string SavePath = "achievements/state.json";

	/// <summary>Rainbow order used by the sequence_master achievement.</summary>
	public static readonly string[] RainbowOrder = { "red", "yellow", "green", "blue", "purple" };

	private record SavedAchievements( HashSet<string> Unlocked );

	[Property] public bool ResetOnStart { get; set; } = false;

	public HashSet<string> Unlocked { get; private set; } = new();

	// Per-attempt state
	private readonly List<string> _pressedOrder = new();
	private readonly HashSet<string> _pressedSet = new();
	private TimeSince _sinceFirstPress;
	private bool _hasPressed;
	private bool _pedestalTouched;

	// Toast queue
	public sealed class Toast
	{
		public string Id;
		public string Title;
		public string Description;
		public RealTimeSince Since;
	}

	public List<Toast> ActiveToasts { get; } = new();
	private const float ToastDurationSeconds = 3.5f;

	public static AchievementManager Instance { get; private set; }

	protected override void OnEnabled()
	{
		base.OnEnabled();
		Instance = this;
		Load();

		if ( ResetOnStart )
		{
			Unlocked.Clear();
			Save();
		}
	}

	protected override void OnUpdate()
	{
		// Expire old toasts
		for ( int i = ActiveToasts.Count - 1; i >= 0; i-- )
		{
			if ( ActiveToasts[i].Since > ToastDurationSeconds )
				ActiveToasts.RemoveAt( i );
		}
	}

	public void NotifyButtonPressed( string colorId )
	{
		if ( !_hasPressed )
		{
			_hasPressed = true;
			_sinceFirstPress = 0;
		}

		// Track even duplicates for sequence verification
		_pressedOrder.Add( colorId );
		_pressedSet.Add( colorId );

		// 1. first_button
		Unlock( "first_button" );

		// 3. sequence_master: rainbow order, possibly with intermediate
		//    presses that match the next-expected color. We check: the
		//    distinct-first-occurrence sequence matches RainbowOrder.
		var distinctOrder = _pressedOrder.Distinct().ToList();
		if ( distinctOrder.Count == RainbowOrder.Length
			 && distinctOrder.SequenceEqual( RainbowOrder ) )
		{
			Unlock( "sequence_master" );
		}

		// 2. all_colors
		if ( _pressedSet.Count >= RainbowOrder.Length )
		{
			Unlock( "all_colors" );

			// 4. speedrun
			if ( _sinceFirstPress <= 10f )
				Unlock( "speedrun" );

			// 5. pacifist
			if ( !_pedestalTouched )
				Unlock( "pacifist" );
		}
	}

	public void NotifyPedestalTouched()
	{
		_pedestalTouched = true;
	}

	/// <summary>
	/// Unlock and persist. Calls Sandbox.Services.Achievements.Unlock(name).
	/// Idempotent — re-unlocking does nothing (and no duplicate toast).
	/// </summary>
	public void Unlock( string id )
	{
		if ( Unlocked.Contains( id ) ) return;

		Unlocked.Add( id );

		// Fire the actual achievement to sbox.game services.
		// Static, void, synchronous. Confirmed via reflection.
		// Fully-qualified because our namespace `Local.Achievements` shadows
		// the `Sandbox.Services.Achievements` type when only `using Sandbox.Services`.
		Sandbox.Services.Achievements.Unlock( id );

		// Persist immediately so kill -> relaunch retains it.
		Save();

		// Queue toast.
		var def = AchievementCatalog.Get( id );
		if ( def != null )
		{
			ActiveToasts.Add( new Toast { Id = id, Title = def.Title, Description = def.Description, Since = 0 } );
			Log.Info( $"[Achievements] Unlocked: {id} ({def.Title})" );
		}
	}

	private void Save()
	{
		try
		{
			FileSystem.Data.CreateDirectory( "achievements" );
			var data = new SavedAchievements( Unlocked );
			FileSystem.Data.WriteAllText( SavePath, JsonSerializer.Serialize( data ) );
		}
		catch ( Exception e )
		{
			Log.Warning( $"[Achievements] Save failed: {e.Message}" );
		}
	}

	private void Load()
	{
		try
		{
			if ( !FileSystem.Data.FileExists( SavePath ) ) return;
			var raw = FileSystem.Data.ReadAllText( SavePath );
			var data = JsonSerializer.Deserialize<SavedAchievements>( raw );
			if ( data?.Unlocked != null )
			{
				Unlocked = data.Unlocked;
				Log.Info( $"[Achievements] Loaded {Unlocked.Count} prior unlocks: {string.Join( ", ", Unlocked )}" );

				// Re-fire to the service so it knows about prior unlocks too.
				// (Idempotent on the service side.)
				foreach ( var id in Unlocked )
					Sandbox.Services.Achievements.Unlock( id );
			}
		}
		catch ( Exception e )
		{
			Log.Warning( $"[Achievements] Load failed: {e.Message}" );
		}
	}
}
