using System;
using SteamKit2;

namespace dashe4
{
	public class UserInfo
	{
		public string Name;

		public ulong SteamID;

		public EClanPermission Rank;
		public EChatPermission Permission;

		public DateTime LastTime;
		public DateTime LastMessage;
		public DateTime LastJoin;
		public DateTime LastLeave;

		public int Disconnects;
		public int Warnings;
		public int SetWarnings;

		// TODO: USe this to determine if user is in chat
		public bool InChat;
	}
}