using System;
using System.Threading.Tasks;
using SteamKit2;

namespace dashe4
{
	public class EventHandler
	{
		private readonly Kraxbot kraxbot;
		private readonly Command cmnd;

		private bool running;

		public EventHandler(Kraxbot bot)
		{
			kraxbot = bot;
			var manager = bot.Manager;
			running = true;

			cmnd = new Command(bot);

			manager.Subscribe<SteamClient.ConnectedCallback>(OnConnected);				// We connected
			manager.Subscribe<SteamClient.DisconnectedCallback>(OnDisconnected);		// We got disconnected

			manager.Subscribe<SteamUser.LoggedOnCallback>(OnLoggedOn);					// We logged on
			manager.Subscribe<SteamUser.LoggedOffCallback>(OnLoggedOff);				// We got logged off
			manager.Subscribe<SteamUser.AccountInfoCallback>(OnAccountInfo);			// We finished logging in
			manager.Subscribe<SteamUser.UpdateMachineAuthCallback>(OnMachineAuth);		// We logged in and can store it
			manager.Subscribe<SteamUser.LoginKeyCallback>(OnLoginKey);					// When we want to save our password

			manager.Subscribe<SteamFriends.FriendAddedCallback>(OnFriendAdded);			// Someone added us
			manager.Subscribe<SteamFriends.FriendMsgCallback>(OnFriendMsg);				// We got a PM
			manager.Subscribe<SteamFriends.ChatMsgCallback>(OnChatMsg);					// Someone sent a chat message
			manager.Subscribe<SteamFriends.ChatInviteCallback>(OnChatInvite);			// We got invited to a chat
			manager.Subscribe<SteamFriends.ChatEnterCallback>(OnChatEnter);				// We entered a chat
			manager.Subscribe<SteamFriends.ChatMemberInfoCallback>(OnChatMemberInfo);	// A user has left or entered a chat
			manager.Subscribe<SteamFriends.FriendsListCallback>(OnFriendsList);			// When we get our friends list

			Task.Run(() =>
			{
				while (running)
					manager.RunWaitCallbacks(TimeSpan.FromSeconds(1));
			});
		}

		#region EventHandler methods

		public void Stop() => running = false;

		#endregion

		#region SteamClient

		// When disconnected, attempt to reconnect
		private void OnDisconnected(SteamClient.DisconnectedCallback disconnectedCallback) => kraxbot.Connect();

		// When connected, login
		private void OnConnected(SteamClient.ConnectedCallback callback) => kraxbot.Login();

		#endregion

		#region SteamUser

		// When finished logging in, set us as online
		private void OnAccountInfo(SteamUser.AccountInfoCallback accountInfoCallback) =>
			kraxbot.SetPersonaState(EPersonaState.Online);

		private void OnLoggedOff(SteamUser.LoggedOffCallback loggedOffCallback) => Kraxbot.Log("OnLoggedOff");

		private void OnLoggedOn(SteamUser.LoggedOnCallback callback)
		{
			if (callback.Result != EResult.OK)
				Kraxbot.Error($"Login failed: {callback.Result}");

			// Save WebAPI stuff
			if (callback.Result == EResult.OK)
				kraxbot.UserNonce = callback.WebAPIUserNonce;
		}

		private void OnLoginKey(SteamUser.LoginKeyCallback callback)
		{
			// Save unique ID to use with SteamCommunity
			kraxbot.UniqueID = callback.UniqueID;

			// Login to Web
			kraxbot.LogOnToWeb();
		}

		private void OnMachineAuth(SteamUser.UpdateMachineAuthCallback obj) => Kraxbot.Log("OnMachineAuth");

		#endregion

		#region SteamFriends

		private void OnFriendsList(SteamFriends.FriendsListCallback callback)
	    {
		    foreach (var friend in callback.FriendList)
		    {
			    Kraxbot.Log($"{friend.SteamID}: {kraxbot.GetFriendPersonaName(friend.SteamID)}");
		    }

			kraxbot.JoinChatRoom(new SteamID(103582791438821937));
	    }

		private void OnChatMemberInfo(SteamFriends.ChatMemberInfoCallback obj) => Kraxbot.Log("OnChatMemberInfo");

		private void OnChatEnter(SteamFriends.ChatEnterCallback obj) => Kraxbot.Log("OnChatEnter");

		private void OnChatInvite(SteamFriends.ChatInviteCallback callback)
		{
			Kraxbot.Log("OnChatInvite");
			
			kraxbot.JoinChatRoom(callback.ChatRoomID);
		}

		private void OnChatMsg(SteamFriends.ChatMsgCallback callback)
		{
			Kraxbot.Log($"{kraxbot.GetFriendPersonaName(callback.ChatterID)} ({callback.ChatRoomID}): {callback.Message}");

			// TODO: We could launch this in another thread to let it do heavy stuff
			cmnd.Handle(callback.ChatRoomID, callback.ChatterID, callback.Message);
		}

		private void OnFriendMsg(SteamFriends.FriendMsgCallback callback)
		{
			Kraxbot.Log($"{kraxbot.GetFriendPersonaName(callback.Sender)}: {callback.Message}");
		}

		private void OnFriendAdded(SteamFriends.FriendAddedCallback obj) => Kraxbot.Log("OnFriendAdded");

		#endregion
	}
}
