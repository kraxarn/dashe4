using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using SteamKit2;

namespace dashe4
{
    public class Kraxbot
    {
	    private readonly SteamClient     client;
	    private readonly SteamUser       user;
	    private readonly SteamFriends    friends;
	    private readonly SteamGroup      group;
	    private readonly SteamCommunity  community;
	    private readonly CallbackManager manager;

	    private readonly EventHandler eventHandler;
	    private readonly WebClient    web;

	    public readonly string Version;

	    private readonly Dictionary<ulong, Settings> chatrooms;

	    public CallbackManager Manager => manager;

	    public readonly SteamID KraxID;

	    public readonly APIKey API;

		// SteamCommunity
	    public uint UniqueID;
	    public string UserNonce;

	    /// <summary>
	    /// The bot's Steam ID
	    /// </summary>
	    public SteamID SteamID => client.SteamID;

	    public Kraxbot()
	    {
			// Vars
			// TODO: Get version from GitHub
		    Version   = "4.0.0-alpha.1";
			chatrooms = new Dictionary<ulong, Settings>();

		    UniqueID = 0;

			API = new APIKey();

			// Steam
			client  = new SteamClient();
			user    = client.GetHandler<SteamUser>();
			friends = client.GetHandler<SteamFriends>();
			manager = new CallbackManager(client);
			group   = new SteamGroup(client);
			web     = new WebClient();

			eventHandler = new EventHandler(this);

			KraxID = new SteamID(76561198024704964);

			// Welcome
			Log($"Welcome to Kraxbot {Version}");
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

	    public EPersonaState GetFriendPersonaState(SteamID steamID) => friends.GetFriendPersonaState(steamID);

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

	    public void SendKraxMessage(string message) => SendChatMessage(KraxID, message);

	    public void JoinChatRoom(SteamID chatRoomID) => friends.JoinChat(chatRoomID);

	    public void KickUser(SteamID chatRoomID, SteamID userID) => friends.KickChatMember(chatRoomID, userID);

	    public void BanUser(SteamID chatRoomID, SteamID userID) => friends.BanChatMember(chatRoomID, userID);

	    public void UnbanUser(SteamID chatRoomID, SteamID userID) => friends.UnbanChatMember(chatRoomID, userID);

	    public void LeaveChat(SteamID chatRoomID) => friends.LeaveChat(chatRoomID);

	    public void AddFriend(SteamID userID) => friends.AddFriend(userID);

	    public void RemoveFriend(SteamID userID) => friends.RemoveFriend(userID);

		public SteamFriends.ProfileInfoCallback GetProfileInfo(SteamID userID) 
			=> Task.Run(async () => await friends.RequestProfileInfo(userID)).Result;

	    public bool TryGetFriendDetails(SteamID userID, out FriendDetails friend) 
		    => eventHandler.TryGetFriendDetails(userID, out friend);

		/// <summary>
		/// Gets chatroom settings and adds them to chatroom list if needed
		/// </summary>
		// TODO: There could be a function that directly return settings without check
		// This would be used in chat messages (to speed it up) and then use this on joins
		public Settings GetChatRoomSettings(SteamID chatRoomID)
	    {
			// Shortcut
		    var chatID = chatRoomID.ConvertToUInt64();

			// Debug
			Log($"Debug: Requesting chatroom settings for {chatID}");

			// If there are settings, return them
		    if (chatrooms.ContainsKey(chatID))
			    return chatrooms[chatID];

		    // Otherwise, look in files
			// TODO: Move (most of) loading of settings to Settings.Load?
			// TODO: We need a way to parse 'legacy settings' to new ones
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
			    s = new Settings(chatID);
		    }

		    SaveSettingsToList(s);
		    return s;
	    }

	    private void SaveSettingsToList(Settings settings) => chatrooms[settings.ChatID] = settings;

	    public static string ExecuteProcess(string fileName, string arguments = null, int timeout = 3000)
	    {
			var startInfo = new ProcessStartInfo
			{
				UseShellExecute = false,
				RedirectStandardOutput = true,
				FileName = fileName
			};

		    if (arguments != null)
			    startInfo.Arguments = arguments;

		    var process = new Process
		    {
				StartInfo = startInfo
		    };

		    string output;

		    try
		    {
			    process.Start();
			    output = process.StandardOutput.ReadToEnd();
			    process.WaitForExit(timeout);
			}
		    catch (Exception e)
		    {
				Log($"Failed to execute '{fileName}': {e.Message}");
			    output = e.Message;
		    }

		    return output.Trim();
	    }

	    public bool TryGet(string url, out string response)
	    {
		    try
		    {
			    response = web.DownloadString(url);
			    return true;
		    }
		    catch (WebException e)
		    {
			    Log($"Warning: Web request failed: {e.Message}");
			    response = e.Message;
			    return false;
		    }

	    }

	    public bool TryRequest(string url, NameValueCollection headers, string body, out string response)
	    {
		    var webRequest = (HttpWebRequest)WebRequest.Create(url);
		    webRequest.Headers.Add(headers);
		    webRequest.ContentType = "application/json";

		    // See if we have a body
		    if (body != null)
		    {
			    var postData = $"body={body}";
			    var bytes = Encoding.UTF8.GetBytes(postData);
			    webRequest.Method = "POST";
			    webRequest.ContentType = "application/x-www-form-urlencoded";
			    webRequest.ContentLength = bytes.Length;
			    var stream = webRequest.GetRequestStream();
			    stream.Write(bytes, 0, bytes.Length);
			    stream.Close();
		    }

		    try
		    {
			    var webResponse = (HttpWebResponse)webRequest.GetResponse();
			    response = new StreamReader(webResponse.GetResponseStream()).ReadToEnd();
			    return true;
		    }
		    catch (Exception e)
		    {
			    Log($"Warning: Web request failed: {e.Message}");
			    response = e.Message;
			    return false;
		    }
	    }

	    public bool TryParseJson(string value, out dynamic result)
	    {
		    result = JsonConvert.DeserializeObject(value);
		    return result != null;
	    }

	    public bool TryGetJson(string url, out dynamic json)
	    {
		    json = null;

		    if (TryGet(url, out var response) && TryParseJson(response, out var result))
			    json = result;

		    return json != null;
	    }
	}
}