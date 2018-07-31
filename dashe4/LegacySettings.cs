// ReSharper disable InconsistentNaming MemberHidesStaticFromOuterClass
using System.Collections.Generic;

namespace dashe4
{
	/// <summary>
	/// Settings for 3.x and earlier
	/// <para> This will eventually be removed (4.1?) </para>
	/// </summary>
	public class LegacySettings
	{
		// NOTE: Settings uses the new UserInfo class
		public class User
		{
			public string Name;

			public int DC;			// (Disconnects)
			public int WR;			// (Warnings)
			public int SetWR;		// (SetWarnings)
			public int Last;		// DateTime (LastTime)
			public int LastJoin;	// DateTime
			public int LastLeave;	// DateTime
			public int LastMsg;		// DateTime (LastMessage)

			private User(string name = null)
			{
				Name = name;
				DC = WR = SetWR = Last = LastJoin = LastLeave = LastMsg = 0;
			}
		}

		public class TimeoutSettings
		{
			public int Random;
			public int Define;
			public int Games;
			public int Recents;
			public int YT;			// (Yt)
		}

		public class CustomSettings
		{
			public bool Enabled;
			public bool ModOnly;

			public int Delay;

			public string Command;
			public string Response;
		}

		public class AutoKickSettings
		{
			public string User;     // ulong
			public string Mode;		// ESpamAction
		}

		public class DelaySettings
		{
			public int Random;
			public int Define;
			public int Games;
			public int Recents;
			public int Lenny;		// Removed
			public int Search;
			public int YT;			// (Yt)
		}

		public class WordFilterSettings
		{
			public bool Enabled;
			public bool IgnoreMods;

			public string Action;	// ESpamAction
			public string Filter;	// List<string>
		}

		public int Ver;
		public int DCLimit;         // (DcLimit)
		public int DefineDelay;		// Removed
		public int GamesDelay;		// Removed
		public int RecentsDelay;	// Removed
		public int LennyDelay;		// Removed
		public int SearchDelay;		// Removed
		public int YTDelay;			// Removed
		public int DCKickLimit;		// (DcKickLimit)
		public int DCBanLimit;		// (DcBanLimit)
		public int AppNewsID;

		public string ChatName;
		public string ChatID;		// ulong
		public string Spam;			// ESpamAction
		public string WelcomeMsg;
		public string WelcomeEnd;
		public string InvitedID;	// ulong
		public string InvitedName;
		public string LastPoke;		// ulong
		public string DCKick;		// ESpamAction (DcKick)
		public string TranslateLang;
		public string RandomDelay;  // Removed
		public string ClearMsg;

		public bool Cleverbot;
		public bool CleverbotInst;	// Removed
		public bool Translate;
		public bool Commands;
		public bool Welcome;
		public bool Lenny;			// Removed
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
		public bool IRC;			// Removed
		public bool Beta;
		public bool PostAnn;
		public bool FirstJoin;
		public bool Currency;
		public bool AutoLeave;

		public List<string> SetRules;

		public TimeoutSettings    Timeout;
		public CustomSettings     Custom;
		public AutoKickSettings   AutoKick;
		public DelaySettings      Delay;
		public WordFilterSettings WordFilter;
	}
}