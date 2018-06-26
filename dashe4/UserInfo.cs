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

		/// <summary>
		/// Last time user got a warning?
		/// </summary>
		public DateTime LastTime;

		/// <summary>
		/// Last time the user sent a message in the chat
		/// </summary>
		public DateTime LastMessage;

		/// <summary>
		/// Last the they joined the chat
		/// </summary>
		public DateTime LastJoin;

		/// <summary>
		/// Last time they left the chat
		/// </summary>
		public DateTime LastLeave;

		public int Disconnects;
		public int Warnings;
		public int SetWarnings;

		// TODO: Use this to determine if user is in chat
		public bool InChat;
	}
}