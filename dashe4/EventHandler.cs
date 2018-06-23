using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json;
using SteamKit2;

namespace dashe4
{
	public class EventHandler
	{
		private enum UserEventType  { Message, Invite, Added }
		private enum GroupEventType { Invite,  Joined        }

		private readonly Kraxbot kraxbot;
		private readonly Command cmnd;

		private bool running;

		private readonly Dictionary<SteamID, FriendDetails> friends;

		private readonly Dictionary<SteamID, UserCooldowns>  users;
		private readonly Dictionary<SteamID, GroupCooldowns> groups;

		private SteamID tempInvitedID;
		private string  tempInvitedName, tempChatName;

		private SteamID lastChatroom, lastInviter;

		public EventHandler(Kraxbot bot)
		{
			kraxbot = bot;
			var manager = bot.Manager;
			running = true;

			cmnd = new Command(bot);

			friends = new Dictionary<SteamID, FriendDetails>();
			users   = new Dictionary<SteamID, UserCooldowns>();
			groups  = new Dictionary<SteamID, GroupCooldowns>();

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
			manager.Subscribe<SteamFriends.PersonaStateCallback>(OnPersonaState);		// Friend changes persona state

			Task.Run(() =>
			{
				while (running)
					manager.RunWaitCallbacks(TimeSpan.FromSeconds(1));
			});
		}

		#region EventHandler methods

		public void Stop() => running = false;

		public bool TryGetFriendDetails(SteamID userID, out FriendDetails friend)
		{
			if (friends.ContainsKey(userID))
			{
				friend = friends[userID];
				return true;
			}

			friend = default(FriendDetails);
			return false;
		}

		private void RegisterUserEvent(SteamID userID, UserEventType type)
		{
			if (users.ContainsKey(userID))
			{
				users[userID].Last = DateTime.Now;

				switch (type)
				{
					case UserEventType.Message:
						users[userID].LastMessage = DateTime.Now;
						break;

					case UserEventType.Invite:
						users[userID].LastInvite = DateTime.Now;
						break;

					case UserEventType.Added:
						users[userID].Added = DateTime.Now;
						break;
				}
			}
			else
			{
				// All cooldowns are set to now anyway
				users[userID] = new UserCooldowns();
			}

			// Save users list
			var json = JsonConvert.SerializeObject(users);
			File.WriteAllText("./users.json", json);
		}

		private void RegisterGroupEvent(SteamID chatRoomID, GroupEventType type)
		{
			// Similar to RegisterUserEvent

			if (groups.ContainsKey(chatRoomID))
			{
				switch (type)
				{
					case GroupEventType.Invite:
						groups[chatRoomID].LastInvite = DateTime.Now;
						break;

					case GroupEventType.Joined:
						groups[chatRoomID].Joined = DateTime.Now;
						break;
				}
			}
			else
			{
				groups[chatRoomID] = new GroupCooldowns();
			}

			var json = JsonConvert.SerializeObject(groups);
			File.WriteAllText("./groups.json", json);
		}

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
			kraxbot.JoinChatRoom(new SteamID(103582791438821937));
	    }

		private void OnChatMemberInfo(SteamFriends.ChatMemberInfoCallback obj) => Kraxbot.Log("OnChatMemberInfo");

		private void OnChatEnter(SteamFriends.ChatEnterCallback callback)
		{
			var settings = kraxbot.GetChatRoomSettings(callback.ChatID);

			// Fill settings with stuff
			settings.ChatName = callback.ChatRoomName;

			// Add users
			foreach (var member in callback.ChatMembers)
			{
				var user = settings.Users.SingleOrDefault(u => u.SteamID == member.SteamID);

				if (user == default(UserInfo))
				{
					// User doesn't exist, create
					settings.Users.Add(new UserInfo
					{
						Name       = kraxbot.GetFriendPersonaName(member.SteamID),
						SteamID    = member.SteamID,
						Rank       = member.Details,
						Permission = member.Permissions
					});
				}
				else
				{
					// User already exists in list, just update values
					user.Name       = kraxbot.GetFriendPersonaName(member.SteamID);
					user.Rank       = member.Details;
					user.Permission = member.Permissions;
				}
			}

			Kraxbot.Log($"Joined {callback.ChatRoomName} with invite from {settings.InvitedName}");
		}

		private void OnChatInvite(SteamFriends.ChatInviteCallback callback)
		{
			// TODO: Is this needed?
			if (callback.InvitedID != kraxbot.SteamID)
				return;

			if (string.IsNullOrEmpty(callback.ChatRoomName))
			{
				kraxbot.SendChatMessage(callback.FriendChatID, "Sorry, I can't (currently) join multi-user chats");
				return;
			}

			var userID   = callback.FriendChatID;
			var userName = kraxbot.GetFriendPersonaName(callback.FriendChatID);

			if (userID != kraxbot.KraxID && (callback.ChatRoomID == lastChatroom || userID == lastInviter))
			{
				Kraxbot.Log($"Got invite to {callback.ChatRoomName} from {userName}");
				kraxbot.JoinChatRoom(callback.ChatRoomID);

				// TODO: Are these used?
				tempInvitedID   = userID;
				tempInvitedName = userName;
				tempChatName    = callback.ChatRoomName;

				lastInviter = userID;

				RegisterUserEvent(userID, UserEventType.Invite);
				RegisterGroupEvent(callback.ChatRoomID, GroupEventType.Invite);

				// Update names
				var settings = kraxbot.GetChatRoomSettings(callback.ChatRoomID);
				settings.InvitedID = callback.FriendChatID;
				if (kraxbot.TryGetFriendDetails(userID, out var friend))
					settings.InvitedName = friend.Name;
			}
			else
			{
				Kraxbot.Log($"Got invited to recent chat from {userName}, declined");
				kraxbot.SendChatMessage(userID, "Sorry, I can't enter this chat. This is either because I recently left it or because you are spamming invites to chats.");
			}
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

		private void OnPersonaState(SteamFriends.PersonaStateCallback callback)
		{
			/*
			 * If we already have the user saved, just update (all) values
			 * Otherwise, create a new entry for them
			 */
			if (friends.ContainsKey(callback.FriendID))
				friends[callback.FriendID].UpdateValues(callback);
			else
				friends[callback.FriendID] = new FriendDetails(callback);
		}

		#endregion
	}
}
