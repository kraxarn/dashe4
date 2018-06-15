using System;
using SteamKit2;

namespace dashe4
{
	public class UserInfo
	{
		public SteamID SteamID;

		public EClanPermission Rank;

		public EChatPermission Permission;

		public DateTime LastTime;

		public DateTime LastMessage;

		public int Disconnect, Warning;
	}
}