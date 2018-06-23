using System;
using System.Net;
using SteamKit2;

namespace dashe4
{
	public class FriendDetails
	{
		public SteamID FriendID;

		public EPersonaState State;

		public EPersonaStateFlag StateFlags;
		
		public uint GameServerPort;
		public uint QueryPort;

		public GameID GameID;
		
		public string GameName;
		public string Name;

		public IPAddress GameServerIP;

		public DateTime LastLogOff;
		public DateTime LastLogOn;
		
		public FriendDetails(SteamFriends.PersonaStateCallback callback) 
			=> UpdateValues(callback);

		public void UpdateValues(SteamFriends.PersonaStateCallback callback)
		{
			Name       = callback.Name;
			FriendID   = callback.FriendID;
			State      = callback.State;
			StateFlags = callback.StateFlags;

			GameID         = callback.GameID;
			GameName       = callback.GameName;
			GameServerIP   = callback.GameServerIP;
			GameServerPort = callback.GameServerPort;
			QueryPort      = callback.QueryPort;

			LastLogOff = callback.LastLogOff;
			LastLogOn  = callback.LastLogOn;
		}
	}
}