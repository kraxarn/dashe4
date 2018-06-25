using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
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
		private readonly Random  rng;

		private bool running;

		private readonly Dictionary<SteamID, FriendDetails> friends;

		private readonly Dictionary<SteamID, UserCooldowns>  users;
		private readonly Dictionary<SteamID, GroupCooldowns> groups;

		private readonly ChatroomCollection chatrooms;

		private SteamID  lastChatroom, lastInviter, lastFriendMessage;
		private DateTime lastFriendTime;

		private DateTime connectTime;

		public EventHandler(Kraxbot bot)
		{
			kraxbot = bot;
			var manager = bot.Manager;
			running = true;

			connectTime = DateTime.Now;

			cmnd = new Command(bot);
			rng  = new Random();

			friends = new Dictionary<SteamID, FriendDetails>();
			users   = new Dictionary<SteamID, UserCooldowns>();
			groups  = new Dictionary<SteamID, GroupCooldowns>();

			chatrooms = new ChatroomCollection();

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

		private string GetPlayerName(SteamID userID)
		{
			// TODO: See how often 'Unknown' gets returned
			// We could also use TryGetFriendDetails

			var name = kraxbot.GetFriendPersonaName(userID);
			return string.IsNullOrEmpty(name) ? "Unknown" : name;
		}

		private int GetPlayerLevel(ulong userID64)
		{
			var level = 0;
			
			/*
			 * TODO
			 * Like some other Steam Web API calls,
			 * this can be called using the methods
			 * provided by SteamKit2 instead of doing
			 * it like this. Before that though, try
			 * and make sure it works properly
			 */

			if (kraxbot.TryGetJson($"http://api.steampowered.com/IPlayerService/GetSteamLevel/v1/?key={kraxbot.API.Steam}&steamid={userID64}", out var json))
			{
				if (json.response.player_level != null)
					level = (int) json.response.player_level;
			}

			return level;
		}

		public IEnumerable<ulong> GetCurrentChatrooms()
			=> chatrooms;

		#endregion

		#region SteamClient

		// When disconnected, attempt to reconnect
		private void OnDisconnected(SteamClient.DisconnectedCallback disconnectedCallback) => kraxbot.Connect();

		// When connected, login
		private void OnConnected(SteamClient.ConnectedCallback callback) => kraxbot.Login();

		#endregion

		#region SteamUser

		// When finished logging in, set us as online
		private void OnAccountInfo(SteamUser.AccountInfoCallback accountInfoCallback) 
			=> kraxbot.SetPersonaState(EPersonaState.Online);

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
			
			// Join chatrooms again
			connectTime = DateTime.Now;

			foreach (var chatroom in chatrooms)
				kraxbot.JoinChatRoom(chatroom);
		}

		private void OnMachineAuth(SteamUser.UpdateMachineAuthCallback obj) => Kraxbot.Log("OnMachineAuth");

		#endregion

		#region SteamFriends

		private void OnFriendsList(SteamFriends.FriendsListCallback callback)
	    {
			// TODO: Auto-join chatrooms here?
	    }

		private void OnChatMemberInfo(SteamFriends.ChatMemberInfoCallback callback)
		{
			// TODO: We only care about state changes?
			if (callback.Type != EChatInfoType.StateChange)
				return;

			// Some vars
			var chatRoomID = callback.ChatRoomID;
			var info       = callback.StateChangeInfo;
			var state      = callback.StateChangeInfo.StateChange;

			var settings = kraxbot.GetChatRoomSettings(chatRoomID);

			var userID   = info.ChatterActedOn;
			var userName = GetPlayerName(userID);

			// TODO: Check for default properly
			var user = settings.Users.SingleOrDefault(u => u.SteamID == userID);

			// See what happened
			string message;

			switch (state)
			{
				case EChatMemberStateChange.Entered:      message = "Welcome";      break;	// Joined
				case EChatMemberStateChange.Left:         message = "Good bye";     break;	// Left
				case EChatMemberStateChange.Disconnected: message = "RIP";          break;	// Disconnected
				case EChatMemberStateChange.Kicked:       message = "Bye";          break;	// Kicked
				case EChatMemberStateChange.Banned:       message = "RIP in peace"; break;	// Banned

				case EChatMemberStateChange.VoiceSpeaking:     message = "Joined voice chat: "; break;	// Joined voice chat
				case EChatMemberStateChange.VoiceDoneSpeaking: message = "Left voice chat: ";   break;	// Left Voice chat

				default:
					message = $"Error ({state}):";
					break;
			}

			// Check if applied to bot
			if (userID == kraxbot.SteamID)
			{
				kraxbot.SendKraxMessage($"Got {state} from {settings.ChatName}");
				lastChatroom = chatRoomID;
				chatrooms.Remove(chatRoomID);
			}

			// User entered chat
			if (state == EChatMemberStateChange.Entered)
			{
				var member = callback.StateChangeInfo.MemberInfo;

				if (user == default(UserInfo))
				{
					// User doesn't exist, create
					settings.Users.Add(new UserInfo
					{
						Name       = GetPlayerName(userID),
						SteamID    = userID,
						Rank       = member.Details,
						Permission = member.Permissions
					});
				}
				else
				{
					// User already exists in list, just update values
					user.Name       = kraxbot.GetFriendPersonaName(userID);
					user.Rank       = member.Details;
					user.Permission = member.Permissions;
				}
			}

			// User left chat
			else if (state == EChatMemberStateChange.Left)
			{
				user.Disconnects = 0;
				user.LastLeave   = DateTime.Now;
			}

			// Fix for not counting Disconnects when DCKick is set to 'None'
			// When it's 2 it won't say anything anyway
			else if (state == EChatMemberStateChange.Disconnected && settings.DcKick == ESpamAction.None && user.Disconnects < 2)
				user.Disconnects++;

			// Check if we should auto kick/ban
			if (settings.AutoKick.Mode != ESpamAction.None && settings.AutoKick.User == userID)
			{
				switch (settings.AutoKick.Mode)
				{
					case ESpamAction.Ban:
						kraxbot.BanUser(chatRoomID, userID);
						kraxbot.SendKraxMessage($"Auto banned {userName} from {settings.ChatName}");
						break;

					case ESpamAction.Kick:
						kraxbot.KickUser(chatRoomID, userID);
						kraxbot.SendKraxMessage($"Auto kicked {userName} from {settings.ChatName}");
						break;

					// TODO: Add warning
				}
			}

			// Say welcome message
			if (settings.Welcome)
			{
				if (state == EChatMemberStateChange.Entered)
				{
					switch (user.Disconnects)
					{
						case 0:
							kraxbot.SendChatRoomMessage(chatRoomID, $"{settings.WelcomeMsg} {userName} {settings.WelcomeEnd}");
							break;

						case 1:
							kraxbot.SendChatRoomMessage(chatRoomID, $"Welcome back {userName}");
							break;
					}
				}
				else if (settings.AllStates && user.Disconnects == 0)
					kraxbot.SendChatRoomMessage(chatRoomID, $"{message} {userName}");
			}

			// Kick or warn user if disconnected enough times
			if (settings.DcKick != ESpamAction.None)
			{
				// If user disconnected, add to counter
				if (state == EChatMemberStateChange.Disconnected)
					user.Disconnects++;

				// Check disconnects and kick/warn
				// TODO: Make so you can set custom amount
				if (state == EChatMemberStateChange.Entered && user.Disconnects >= 5)
				{
					kraxbot.SendChatRoomMessage(chatRoomID, $"{userName}, please fix your connection");

					if (settings.DcKick == ESpamAction.Kick)
					{
						kraxbot.SendKraxMessage($"Kicked {userName} due to disconnecting in {settings.ChatName}");
						kraxbot.KickUser(chatRoomID, userID);
					}
					else if (settings.DcKick == ESpamAction.Warn)
					{
						// TODO: This could probably just be a method
						// TODO: We also set .Last in dashe3, why?
						user.Warnings++;
						
						if (user.Warnings == 1)
							kraxbot.SendChatRoomMessage(chatRoomID, "This is your first warning");
						else if (user.Warnings > 2)
						{
							kraxbot.SendChatRoomMessage(chatRoomID, rng.Next(10) == 0 ? "That's it >:c" : "Your own fault :/");
							user.Warnings = 0;
							kraxbot.KickUser(chatRoomID, userID);
						}
						else
						{
							kraxbot.SendChatRoomMessage(chatRoomID, $"You currently have {user.Warnings} warnings");
						}
					}

					user.Disconnects = 0;
				}
			}

			// Log state message
			Kraxbot.Log($"[C] [{settings.ChatName}] {message} {userName}");
			
			/*
			 * TODO
			 * We used to have a bot check here in dashe3,
			 * but since all those bots have died (xD),
			 * I don't think we need the check anymore
			 */

			// Check if inviter left
			if (userID == settings.InvitedID && settings.AutoLeave && state == EChatMemberStateChange.Left)
			{
				kraxbot.SendChatRoomMessage(chatRoomID, "Cya!");
				kraxbot.SendKraxMessage($"Left {settings.ChatName} because {userName} left the chat");
				kraxbot.LeaveChat(chatRoomID);
			}
		}

		private void OnChatEnter(SteamFriends.ChatEnterCallback callback)
		{
			// TODO: This is triggered on group event (sometimes?)!

			// TODO: This is probably not a good idea when auto-joining
			var connect = DateTime.Now - connectTime;

			if (connect.TotalSeconds > 5 && chatrooms.Contains(callback.ChatID))
			{
				Kraxbot.Log($"Warning: Entered {callback.ChatRoomName} twice ({connect.TotalMinutes}m ago)");
				return;
			}

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

			// Add to list of current chatrooms
			chatrooms.Add(callback.ChatID);

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
			// If message is empty, it's just a 'typing...'
			if (callback.Message.Trim().Length == 0)
				return;

			Kraxbot.Log($"{kraxbot.GetFriendPersonaName(callback.Sender)}: {callback.Message}");

			var message = callback.Message;
			var userID  = callback.Sender;

			// PM Spam check
			if (lastFriendTime == DateTime.Now && userID == lastFriendMessage)
			{
				// oh noes
				kraxbot.SendKraxMessage($"Removed {kraxbot.GetFriendPersonaName(userID)} because of spamming in PM");
				kraxbot.RemoveFriend(userID);
				return;
			}

			// Krax commands
			if (message.StartsWith('-') && userID == kraxbot.KraxID)
			{
				if (message == "-cur_cr")
				{
					var msg = "Chatrooms:";

					foreach (var chatroom in chatrooms)
					{
						var settings = kraxbot.GetChatRoomSettings(chatroom);
						var bot      = settings.Users.Single(u => u.SteamID == kraxbot.SteamID);

						msg += $"\n{settings.ChatName}\tsteam://friends/joinchat/{chatroom}\t{settings.InvitedName ?? "Unknown"}\t{settings.Users.Count} users\t{bot.Rank}";
					}

					kraxbot.SendKraxMessage(msg);
				}

				else if (message == "-get_ip")
				{
					using (var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.IP))
					{
						socket.Connect("0.0.0.0", 65530);
						var endPoint = socket.LocalEndPoint as IPEndPoint;
						kraxbot.SendKraxMessage($"IP: {endPoint?.Address}");
					}
				}

				else if (message.StartsWith("-leave "))
				{
					if (ulong.TryParse(message.Substring(7), out var chat))
						kraxbot.LeaveChat(chat);
					else
						kraxbot.SendKraxMessage($"'{message.Substring(7)}' is not a valid chatID");
				}

				else if (message.StartsWith("-join "))
				{
					if (ulong.TryParse(message.Substring(6), out var chat))
						kraxbot.JoinChatRoom(chat);
					else
						kraxbot.SendKraxMessage($"'{message.Substring(6)}' is not a valid chatID");
				}

				else if (message.StartsWith("-invite "))
				{
					if (ulong.TryParse(message.Substring(8), out var chat))
						kraxbot.InviteToChat(chat, kraxbot.KraxID);
					else
						kraxbot.SendKraxMessage($"'{message.Substring(8)}' is not a valid chatID");
				}

				else if (message == "-get_groups")
				{
					// TODO ?
				}
				else if (message == "-clean_groups")
				{
					// TODO ?
				}

				else if (message == "-get_friends")
				{
					// TODO ?
				}
				else if (message == "-clean_friends")
				{
					// TODO ?
				}

				else if (message == "-quit")
				{
					running = false;
					Environment.Exit(0);
				}

				else if (message == "-fortune")
					kraxbot.SendKraxMessage($"\n{Kraxbot.ExecuteProcess("fortune", "-n 200 -s")}");

				else if (message.StartsWith("-fortune "))
				{
					if (ulong.TryParse(message.Substring(9), out var chat))
					{
						var fortune = Kraxbot.ExecuteProcess("fortune", "-n 200 -s");
						kraxbot.SendChatRoomMessage(chat, $"\n{fortune}");
					}
					else
						kraxbot.SendKraxMessage($"'{message.Substring(9)}' is not a valid chatID");
				}

				else if (message == "-post_comment ")
					kraxbot.PostComment(kraxbot.KraxID, message.Substring(14));

				else if (message == "-login_community")
				{
					if (kraxbot.IsLoggedOnToWeb)
						kraxbot.SendKraxMessage("Already logged in to web");
					else
					{
						kraxbot.LogOnToWeb();
						kraxbot.SendKraxMessage("Logged in to web");
					}
				}

				else if (message == "-login_community_force")
				{
					kraxbot.LogOnToWeb();
					kraxbot.SendKraxMessage("Logged in to web");
				}
			}
			
			else if (message.StartsWith('!'))
				kraxbot.SendChatMessage(userID, "Sorry, commands only work in group chats");
			else
				cmnd.HandleCleverbot(userID, message.Trim());

			RegisterUserEvent(userID, UserEventType.Message);

			lastFriendTime    = DateTime.Now;
			lastFriendMessage = userID;
		}

		private void OnFriendAdded(SteamFriends.FriendAddedCallback callback)
		{
			// TODO: Does this only trigger once they have been added?

			var userID = callback.SteamID;

			if (GetPlayerLevel(userID) >= 10)
			{
				// Add user if level 10+
				kraxbot.AddFriend(userID);
				kraxbot.InviteUserToGroup(userID, new SteamID(103582791438821937));

				kraxbot.SendChatMessage(userID, "Hi, I'm KraxBot! Nice to meet you :3");
				kraxbot.SendChatMessage(userID, "You should add my creator if you haven't already, https://steamcommunity.com/id/kraxarn");
				kraxbot.SendChatMessage(userID, "You should also consider joining the Steam group if you want news and updates about me!");

				kraxbot.SendKraxMessage($"{callback.PersonaName} is now my friend");
				RegisterUserEvent(userID, UserEventType.Added);
			}
			else
			{
				// Othewise, ignore invite
				// TODO: Post comment
				kraxbot.RemoveFriend(userID);
			}
		}

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
