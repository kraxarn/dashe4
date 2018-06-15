using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using Newtonsoft.Json;
using SteamKit2;

namespace dashe4
{
    class Kraxbot
    {
	    private SteamClient     client;
	    private SteamUser       user;
	    private SteamFriends    friends;
	    private SteamGroup      group;
	    private SteamCommunity  community;
	    private CallbackManager manager;

	    private string         version;
	    private bool           running;
	    private List<Settings> chatrooms;

	    public CallbackManager Manager => manager;

	    public SteamID KraxID;

	    public APIKey API;

		// SteamCommunity
	    public uint UniqueID;
	    public string UserNonce;

	    public Kraxbot()
	    {
			// Vars
			// TODO: Get version from GitHub
		    version   = "4.0.0-alpha.1";
		    running   = true;
			chatrooms = new List<Settings>();

		    UniqueID = 0;

			API = new APIKey();

			// Steam
			client  = new SteamClient();
			user    = client.GetHandler<SteamUser>();
			friends = client.GetHandler<SteamFriends>();
			manager = new CallbackManager(client);
			group   = new SteamGroup(client);

			var events = new EventHandler(this);

			KraxID = new SteamID(76561198024704964);

			// Welcome
			Log($"Welcome to Kraxbot {version}");
		    client.Connect();

			// SteamCommunity
			community = new SteamCommunity(client);

			// Handle errors
			AppDomain.CurrentDomain.UnhandledException += OnUnhandledException;
	    }

	    private void OnUnhandledException(object sender, UnhandledExceptionEventArgs args)
	    {
			Error(args.ToString(), false);
	    }

	    public void Connect() => client.Connect();

	    public void Login()
	    {
		    var login = File.ReadAllLines("login.txt");

			user.LogOn(new SteamUser.LogOnDetails
			{
				Username = login[0],
				Password = login[1]
			});
	    }

	    public void SetPersonaState(EPersonaState state) => friends.SetPersonaState(state);

	    public string GetFriendPersonaName(SteamID steamID) => friends.GetFriendPersonaName(steamID);

	    public void LogOnToWeb()
	    {
		    if (UniqueID == 0)
		    {
				Log("Warning: Web login failed, uniqueID was not set");
			    return;
		    }

		    if (UserNonce == null)
		    {
			    Log("Warning: Web login failed, userNonce was not set");
			    return;
			}

		    var ok = community.Authenticate(UniqueID.ToString(), UserNonce);

		    if (ok)
				Log($"Web login ok, session: {community.SessionID}");
		    else
			    Log("Web login failed");

			// Post test comment
			PostComment(KraxID, "Comment from the future");

			community.PrintCookies();
	    }

		// If we want to change it later
		public static void Log(string message) => Console.WriteLine(message);

	    public static void Error(string message, bool close = true)
	    {
			Console.WriteLine($"Error: {message}");

		    if (!close)
			    return;

		    Console.ReadLine();
		    Environment.Exit(-1);
	    }

	    public void PostComment(SteamID userID, string comment)
	    {
		    if (!community.IsLoggedOn)
		    {
				Log("Warning: Trying to post comment without being logged in to Community");
			    return;
		    }
			
		    var postData = $"comment={comment}&count=6&sessionid={community.SessionID}";
			var url = $"http://steamcommunity.com/comment/Profile/post/{userID.ConvertToUInt64()}/-1/";
		    var bytes = Encoding.UTF8.GetBytes(postData);

			var request = (HttpWebRequest)WebRequest.Create(url);
		    request.KeepAlive = false;
		    request.Method = "POST";
		    request.ContentType = "application/x-www-form-urlencoded; charset=UTF-8";
		    request.ContentLength = bytes.Length;
		    request.CookieContainer = community.Cookies;

		    var stream = request.GetRequestStream();
		    stream.Write(bytes, 0, bytes.Length);
		    stream.Close();

		    var response = (HttpWebResponse)request.GetResponse();
		    Log(new StreamReader(response.GetResponseStream()).ReadToEnd());
			Log("Posted comment");
		}

	    public void SendChatMessage(SteamID userID, string message) =>
		    friends.SendChatMessage(userID, EChatEntryType.ChatMsg, message);

	    public void SendChatRoomMessage(SteamID chatRoomID, string message) =>
		    friends.SendChatRoomMessage(chatRoomID, EChatEntryType.ChatMsg, message);

	    public void JoinChatRoom(SteamID chatRoomID) => friends.JoinChat(chatRoomID);

		/// <summary>
		/// Gets chatroom settings and adds them to chatroom list if needed
		/// </summary>
	    public Settings GetChatRoomSettings(SteamID chatRoomID)
	    {
			// Shortcut
		    var chatID = chatRoomID.ConvertToUInt64();

			// Debug
			Log($"Debug: Requesting chatroom settings for {chatID}");

			// First we see if we can find it in chatrooms
		    var settings = chatrooms.SingleOrDefault(s => s.ChatID == chatID.ToString());

			// If we found it, return it, otherwise look in settings folder
		    if (settings == default(Settings))
		    {
				Log("Debug: Settings not found in chatrooms, looking in files");
			    Settings s;

			    if (File.Exists($"./settings/{chatID}.json"))
			    {
					Log("Debug: Settings found in file, saving to chatrooms");
					s = JsonConvert.DeserializeObject<Settings>($"./settings/{chatID}.json");
			    }
			    else
			    {
					Log("Debug: Settings not found. Creating new ones");
				    s = new Settings(chatID.ToString());
				}

				SaveSettingsToList(s);
			    return s;
		    }

		    Log("Debug: Settings found in chatrooms");
		    return settings;
	    }

		private void SaveSettingsToList(Settings settings) => chatrooms.Add(settings);
	}
}