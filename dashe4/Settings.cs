using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using SteamKit2;

namespace dashe4
{
	/// <summary>
	/// Settings for 4.x and later
	/// </summary>
    public class Settings
	{
		#region Classes

		public class TimeoutSettings
		{
			public int Random;
			public int Define;
			public int Games;
			public int Recents;
			public int Yt;

			public TimeoutSettings() 
				=> Random = Define = Games = Recents = Yt = 0;
		}

		public class CustomSettings
		{
			public bool Enabled;
			public bool ModOnly;	// Unused

			public int Delay;		// Unused

			public string Command;
			public string Response;

			public CustomSettings()
			{
				Enabled = false;
				ModOnly = false;

				Delay = 5;

				Command  = "!custom";
				Response = "Custom response";
			}
		}

		public class AutoKickSettings
		{
			public ulong User;

			public ESpamAction Mode;

			public AutoKickSettings()
			{
				User = 0;

				Mode = ESpamAction.None;
			}
		}

		public class DelaySettings
		{
			public int Random;
			public int Define;
			public int Games;
			public int Recents;
			public int Search;
			public int Yt;

			public DelaySettings()
			{
				Random = Games = Recents = Search = Yt = 120;
				Define = 300;
			}
		}

		public class WordFilterSettings
		{
			public bool Enabled;
			public bool IgnoreMods;

			public ESpamAction Action;

			public List<string> Filter;

			public WordFilterSettings()
			{
				Enabled    = false;
				IgnoreMods = true;

				Action = ESpamAction.Kick;

				Filter = new List<string>();
			}
		}

		#endregion

		#region Fields

		public List<UserInfo> Users;

		public List<string> SetRules;

		public int Ver;
		public int DcLimit;
		public int DcKickLimit;
		public int DcBanLimit;
		public int AppNewsID;

		public string ChatName;
		public string WelcomeMsg;
		public string WelcomeEnd;
		public string InvitedName;
		public string TranslateLang;
		public string ClearMsg;

		public ulong ChatID;
		public ulong InvitedID;
		public ulong LastPoke;

		public ESpamAction Spam;
		public ESpamAction DcKick;

		public bool Cleverbot;
		public bool Translate;
		public bool Commands;
		public bool Welcome;
		public bool Games;
		public bool Define;
		public bool Wiki;
		public bool Search;
		public bool Weather;
		public bool Store;
		public bool Responses;
		public bool Links;
		public bool Rules;
		public bool Poke;
		public bool AllStates;
		public bool AllPoke;
		public bool NewApp;
		public bool AutoWelcome;
		public bool Beta;
		public bool PostAnn;
		public bool FirstJoin;
		public bool Currency;
		public bool AutoLeave;

		public TimeoutSettings Timeout;

		public CustomSettings Custom;

		public AutoKickSettings AutoKick;

		public DelaySettings Delay;

		public WordFilterSettings WordFilter;

		#endregion

		#region String fields

		public object this[string name]
		{
			get => GetType().GetField(name)?.GetValue(this);
			set => GetType().GetField(name)?.SetValue(this, value);
		}

		#endregion
		
		/// <summary>
		/// Create new default settings
		/// </summary>
		/// <param name="chatRoomID"> Chatroom to create for </param>
		public Settings(ulong chatRoomID)
		{
			Users    = new List<UserInfo>();
			SetRules = new List<string>();

			Ver         = 6;	// Legacy is 5
			DcLimit     = 5;
			DcKickLimit = 3;	// Unused
			DcBanLimit  = 5;	// Unused
			AppNewsID   = 0;	// Unused

			ChatName      = "NoName";
			WelcomeMsg    = "Welcome";
			WelcomeEnd    = "!";
			InvitedName   = null;		// tempInvitedName
			TranslateLang = "en";
			ClearMsg      = ":3";

			ChatID    = chatRoomID;
			InvitedID = 0;			// tempInvitedID
			LastPoke  = 0;

			Spam   = ESpamAction.Kick;
			DcKick = ESpamAction.Kick;

			Cleverbot   = false;
			Translate   = false;
			Commands    = true;
			Welcome     = true;
			Games       = true;
			Define      = true;
			Wiki        = true;
			Search      = true;
			Weather     = true;
			Store       = true;
			Responses   = true;
			Links       = true;
			Rules       = true;
			Poke        = true;
			AllStates   = false;
			AllPoke     = false;
			NewApp      = false;	// Unused
			AutoWelcome = false;	// Unused
			Beta        = false;	// Unused
			PostAnn     = false;	// Unused
			FirstJoin   = true;		// Unused
			Currency    = true;		// Unused
			AutoLeave   = true;

			Timeout    = new TimeoutSettings();
			Custom     = new CustomSettings();
			AutoKick   = new AutoKickSettings();
			Delay      = new DelaySettings();
			WordFilter = new WordFilterSettings();
		}

		// Save
		public bool Save()
		{
			// For now, just save to settings.json

			// Serialise the object to json
			var json = JsonConvert.SerializeObject(this, Formatting.Indented);

			// Check if it failed
			if (string.IsNullOrEmpty(json))
				return false;

			// Write to file if ok
			File.WriteAllText($"settings/{ChatID}.json", json);

			return true;
		}
	}
}