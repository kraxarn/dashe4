using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using SteamKit2;

namespace dashe4
{
	internal class User
	{
		public string Name;
		public int DC, WR, SetWR, Last, LastJoin, LastLeave, LastMsg;

		private User(string name = null)
		{
			Name = name;
			DC = WR = SetWR = Last = LastJoin = LastLeave = LastMsg = 0;
		}
	}

    public class Settings
	{
		#region Fields
		public enum SpamAction { Kick, Ban, Warn, None }

		public List<UserInfo> Users;

		public string     InvitedID, InvitedName;
		public int        Ver;
		public string     ChatName, ChatID;
		public SpamAction Spam;
		public string     WelcomeMsg, WelcomeEnd;
		public string     LastPoke;
		public SpamAction DCKick;
		public string     TranslateLang;

		public bool Cleverbot;
		public bool CleverbotInst;
		public bool Translate;		// Unused until 3.0
		public bool Commands;
		public bool Welcome;
		//public bool Lenny;		// Removed in 3.0
		public bool Games;
		public bool Define;
		public bool Wiki;
		public bool Search;		// Removed in 2.0.2, Added back in 3.2
		public bool Weather;
		public bool Store;
		public bool Responses;
		public bool Links;
		public bool Rules;
		public bool Poke;
		public bool AllStates;
		public bool AllPoke;
		public bool NewApp;		// Removed in 3.0
		public bool AutoWelcome;

		public int DCLimit;
		public int RandomDelay;
		public int DefineDelay;
		public int GamesDelay;
		public int RecentsDelay;
		public int LennyDelay;
		public int SearchDelay;
		public int YTDelay;
		
		// Version 1 (1.14.3)
		public bool IRC;
		
		// Version 2 (2.0)
		public List<string> SetRules;
		public bool Beta; 			// Unused
		public bool PostAnn; 		// Unused
		public bool FirstJoin;		// Unused
		public int DCKickLimit; 	// Unused
		public int DCBanLimit; 	// Unused
		public int AppNewsID; 		// Unused
		public bool Currency; 		// Unused

		// Version 3 (2.0)
		public class IntTimeout
		{
			public int Random, Define, Games, Recents, Search, YT;
		}
		public IntTimeout Timeout;
		
		// Version 4 (2.0)
		public class IntCustom
		{
			public bool Enabled;
			public bool ModOnly;
			public int Delay;
			public string Command, Response;
		}
		public IntCustom Custom;
		
		// Version 5 (3.0)
		public class IntAutoKick
		{
			public SpamAction Mode;
			public string     User;
		}
		public IntAutoKick AutoKick;

		public IntTimeout Delay;
		
		// 3.2.2
		public string ClearMsg;
		public class IntWordFilter
		{
			public bool         Enabled, IgnoreMods;
			public SpamAction   Action;
			public List<string> Filter;
		}
		public IntWordFilter WordFilter;
		#endregion

		// Create new default settings
		public Settings(string chatRoomID)
		{
			// Should IDs be strings or ints?

			/*
			 * Not implemented fields
			 * InvitedID
			 * InvitedName
			 * Ver
			 *
			 */

			ChatName = "NoName";
			ChatID   = chatRoomID;
			Users = new List<UserInfo>();

			Spam          = SpamAction.Kick;
			WelcomeMsg    = "Welcome";
			WelcomeEnd    = "!";
			LastPoke      = "NoPoke";			// Or maybe just null
			DCKick        = SpamAction.Kick;
			TranslateLang = "en";

			Cleverbot     = false;
			CleverbotInst = false;
			Translate     = false;
			Commands      = true;
			Welcome       = true;

			Games       = true;
			Define      = true;
			Wiki        = true;
			Search      = true;		// Removed in 2.0.2 (Readded later)
			Weather     = true;
			Store       = true;
			Responses   = true;
			Links       = true;
			Rules       = true;
			Poke        = true;
			AllStates   = false;
			AllPoke     = false;
			NewApp      = false;	// Removed in 3.0
			AutoWelcome = false;

			// Are these even used?
			// Use Delay.[rule] instead
			DCLimit      = 5;
			RandomDelay  = 120;
			DefineDelay  = 300;
			GamesDelay   = 120;
			RecentsDelay = 120;
			SearchDelay  = 120;
			YTDelay      = 120;

			// Version 1 (1.14.3)
			IRC = false;

			// Version 2 (2.0)
			// All unused currently, except SetRules
			SetRules    = new List<string>();
			Beta        = false;
			PostAnn     = false;
			FirstJoin   = true;
			DCKickLimit = 3;
			DCBanLimit  = 5;
			AppNewsID   = 0;
			Currency    = true;

			// Version 3 (2.0)
			// To be used in 3.0
			Timeout = new IntTimeout
			{
				Random  = 0,
				Define  = 0,
				Games   = 0,
				Recents = 0,
				Search  = 0,
				YT      = 0
			};

			// Version 4 (2.0)
			Custom = new IntCustom
			{
				Enabled  = false,
				ModOnly  = false,
				Delay    = 5,
				Command  = "!custom",
				Response = "Custom response"
			};

			// Version 5 (3.0)
			AutoKick = new IntAutoKick
			{
				Mode = SpamAction.None,
				User = null
			};

			// Unused
			// Use in 3.1 (4.0)
			Delay = new IntTimeout
			{
				Random  = 120,
				Define  = 300,
				Games   = 120,
				Recents = 120,
				Search  = 120,
				YT      = 120
			};
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
			File.WriteAllText("settings.json", json);
			return true;
		}
	}
}