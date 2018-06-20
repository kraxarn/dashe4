using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using System.Xml;
using CleverbotIO.Net;
using Newtonsoft.Json;
using SteamKit2;

namespace dashe4
{
    class Command
    {
	    private readonly Kraxbot kraxbot;

	    private readonly WebClient web;

	    private readonly Regex regexSplit3;

	    private readonly Random rng;

	    private string lastMessage;

	    private SteamID lastUser, lastChatroom;

	    private DateTime lastTime;

	    private readonly Dictionary<SteamID, CleverbotSession> cleverbots;

	    public Command(Kraxbot bot)
	    {
		    kraxbot = bot;

		    regexSplit3 = new Regex(".{3}");

			web = new WebClient();
			//web.Headers.Add("Mozilla/5.0 (Windows NT 6.1; Win64; x64; rv:54.0) Gecko/20100101 Firefox/54.0");

			rng = new Random();

			cleverbots = new Dictionary<SteamID, CleverbotSession>();

		    lastMessage  = "";
		    lastUser     = default(SteamID);
		    lastChatroom = default(SteamID);
		    lastTime     = default(DateTime);
	    }

	    #region Helpers

	    private void SendMessage(SteamID chatRoomID, string message) => kraxbot.SendChatRoomMessage(chatRoomID, message);

	    private bool TryGet(string url, out string response)
	    {
		    try
		    {
			    response = web.DownloadString(url);
			    return true;
		    }
		    catch (WebException e)
		    {
			    Kraxbot.Log($"Warning: Web request failed: {e.Message}");
			    response = null;
			    return false;
		    }

	    }

	    private bool TryRequest(string url, NameValueCollection headers, string body, out string response)
	    {
			var webRequest  = (HttpWebRequest) WebRequest.Create(url);
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
			    var webResponse = (HttpWebResponse) webRequest.GetResponse();
			    response = new StreamReader(webResponse.GetResponseStream()).ReadToEnd();
			    return true;
		    }
		    catch (Exception e)
		    {
			    Kraxbot.Log($"Warning: Web request failed: {e.Message}");
				response = null;
			    return false;
		    }
	    }

	    private bool TryParseJson(string value, out dynamic result)
	    {
		    result = JsonConvert.DeserializeObject(value);
		    return result != null;
	    }

	    private string GetImgurImage(string imageID)
	    {
		    var headers = new NameValueCollection
		    {
				{ "Authorization", $"Client-ID {kraxbot.API.Imgur}" }
		    };

			Console.WriteLine($"Debug:\tImageID: {imageID}");

		    if (TryRequest($"https://api.imgur.com/3/image/{imageID}", headers, null, out var response))
		    {
			    var nsfw    = "";
			    var animted = ", ";
			    var title   = "an image";

			    if (TryParseJson(response, out var result))
			    {
				    if ((bool)result.data.animated)
					    animted = " (animted), ";
				    if ((bool)result.data.nsfw)
					    nsfw = "NSFW";
				    if (result.data.title != null)
					    title = result.data.title;

					var datetime = new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc);
				    datetime.AddSeconds((int)result.data.datetime);

				    var views = regexSplit3.Replace(result.data.views.ToString(), "$0,");

				    return $"{title}{animted}{nsfw} with {views} views, uploaded {datetime}";
			    }
		    }

			// xD
		    return "null";
	    }

	    private string FormatTime(int time)
	    {
		    var mins = 0;
		    while (time >= 60)
		    {
			    mins++;
			    time -= 60;
		    }
		    var secs = time;

		    var min = mins < 10 ? $"{mins}" : $"0{mins}";
		    var sec = mins < 10 ? $"{secs}" : $"0{secs}";

		    return $"{min}:{sec}";
	    }

	    private string FormatYTDuration(string duration)
	    {
		    var dur = XmlConvert.ToTimeSpan(duration);

		    var mins = dur.Minutes;
		    var secs = dur.Seconds;

		    mins += dur.Hours * 60;

		    var min = mins < 10 ? $"{mins}" : $"0{mins}";
		    var sec = mins < 10 ? $"{secs}" : $"0{secs}";

		    return $"{min}:{sec}";
		}

	    private string GetStringBetween(string token, string first, string second = null, bool lastSecond = false)
	    {
		    var from = token.IndexOf(first) + first.Length;
		    if (!string.IsNullOrEmpty(second))
		    {
			    var to = lastSecond ? token.LastIndexOf(second) : token.IndexOf(second);

				// Just to be safe
			    return to < from ? "null" : token.Substring(from, to - from);
		    }
		    return token.Substring(from);
	    }

	    private string GetStringBetween(string token, char first, char second) => GetStringBetween(token, first.ToString(), second.ToString());

	    private void KickUser(string reason, SteamID userID, SteamID chatRoomID)
	    {
		    var settings = kraxbot.GetChatRoomSettings(chatRoomID);
		    var rank = settings.Users.Single(u => u.SteamID == kraxbot.SteamID).Rank;

		    switch (settings.Spam)
		    {
				case ESpamAction.Kick:
					SendMessage(kraxbot.KraxID, $"Kicked {kraxbot.GetFriendPersonaName(userID)} because of {reason} in {settings.ChatName}");
					kraxbot.KickUser(chatRoomID, userID);
					break;

				case ESpamAction.Ban:
					SendMessage(kraxbot.KraxID, $"Banned {kraxbot.GetFriendPersonaName(userID)} because of {reason} in {settings.ChatName}");
					kraxbot.BanUser(chatRoomID, userID);
					break;

				case ESpamAction.Warn:
					var user = settings.Users.Single(u => u.SteamID == userID);
					user.Warnings++;
					user.LastTime = DateTime.Now;
					var warns = user.Warnings;
					SendMessage(kraxbot.KraxID, $"Warned ({warns}) {kraxbot.GetFriendPersonaName(userID)} because of {reason} in {settings.ChatName}");

					switch (warns)
					{
						case 1:
							SendMessage(chatRoomID, "This is your first warning");
							break;

						case 3:
							if (rank == EClanPermission.Officer)
								SendMessage(chatRoomID, "Warning, one more will get you banned!");
							else
							{
								SendMessage(chatRoomID, "Your own fault");
								user.Warnings = 0;
							}
							kraxbot.KickUser(chatRoomID, userID);
							break;

						case 5:
							SendMessage(chatRoomID, "Your own fault");
							kraxbot.BanUser(chatRoomID, userID);
							user.Warnings = 0;
							break;

						default:
							SendMessage(chatRoomID, $"You now have {warns} warnings");
							break;
					}

					break;
			}
	    }

	    private bool SearchUser(string keyword, List<UserInfo> users, out UserInfo user)
	    {
		    foreach (var u in users)
		    {
			    if (u.Name.ToLower().Contains(keyword.ToLower()) || keyword == $"{u.SteamID}")
			    {
				    user = u;
				    return true;
			    }
		    }

		    user = default(UserInfo);
		    return false;
	    }

	    private void ToggleSetting(string setting, string name, Settings settings)
	    {
		    if (string.IsNullOrEmpty(name))
			    name = setting;
				
			// TODO: We should prob test if property exists first
		    if ((bool) settings[setting])
		    {
			    settings[setting] = false;
				SendMessage(settings.ChatID, $"{name} is now disabled");
		    }
		    else
		    {
			    settings[setting] = true;
			    SendMessage(settings.ChatID, $"{name} is now enabled");
			}

			// TODO: Save here
		    //settings.Save();
	    }

	    private bool SetDelay(string delay, int time, Settings settings)
	    {
		    switch (delay)
		    {
				case "random":  settings.Delay.Random  = time; return true;
			    case "define":  settings.Delay.Define  = time; return true;
			    case "games":   settings.Delay.Games   = time; return true;
			    case "recents": settings.Delay.Recents = time; return true;
			    case "yt":      settings.Delay.Yt      = time; return true;

				default: return false;
			}
	    }
	    
	    #endregion

		public void Handle(SteamID chatRoomID, SteamID userID, string message)
		{
			/*
			 * TODO
			 * We could probably check msg[0] instead of message/message.StartsWith
			 */

			/*
			 * Variable changes from dashe3:
			 * chatter	-> user
			 * name		-> userName
			 * game		-> userGame
			 * permUser	-> userPermission
			 */

			#region Vars and stuff
			
			// Stuff used in various places
			message = message.Trim();
			var msg = message.Split(' ');

			// Get settings
			var settings = kraxbot.GetChatRoomSettings(chatRoomID);

			// Get ranks
			// TODO: Check if default
			var user = settings.Users.SingleOrDefault(u => u.SteamID == userID);
			var bot  = settings.Users.SingleOrDefault(u => u.SteamID == kraxbot.SteamID);

			// TODO: This may not work if we aren't friends
			var userName = kraxbot.GetFriendPersonaName(userID);
			var userGame = kraxbot.GetFriendPersonaState(userID);

			// Get one letter permission
			// TODO: These may be inaccurate
			var userPermission = '?';

			switch (user.Rank)
			{
				case EClanPermission.Anybody:   userPermission = 'G'; break;	// Guest
				case EClanPermission.Member:    userPermission = 'U'; break;	// User
				case EClanPermission.Moderator: userPermission = 'M'; break;	// Mod
				case EClanPermission.Officer:   userPermission = 'A'; break;	// Admin
				case EClanPermission.Owner:     userPermission = 'O'; break;	// Owner
			}

			Kraxbot.Log($"[C] [{settings.ChatName.Substring(0, 2)}] [{userPermission}] {userName}: {message}");

			// Check if user has entry in settings.User (Should be created when joining
			// TODO: This is sort of temp
			// TODO: We could create some temporary user here, just to avoid crashes
			if (!settings.Users.Contains(user))
				Kraxbot.Log("Warning: User does not exist in chatroom!");

			// Set when user last sent a message for spam protection
			user.LastMessage = DateTime.Now;

			// Check if user is mod
			var isMod = false;

			switch (user.Rank)
			{
				case EClanPermission.Moderator:
				case EClanPermission.Officer:
				case EClanPermission.Owner:
					isMod = true;
					break;
			}

			// Check if bot is mod
			var isBotMod = false;

			switch (bot.Rank)
			{
				case EClanPermission.Moderator:
				case EClanPermission.Officer:
				case EClanPermission.Owner:
					isBotMod = true;
					break;
			}

			// When someone chats, we want to reset disconnect
			// TODO: In dashe3, we also do a check here if it exists
			user.Disconnects = 0;

			// Memes
			if (user.SteamID == 76561197988712393)
			{
				if (rng.Next(10) == 1)
					message = DateTime.Now.ToShortTimeString();
			}

			#endregion

			#region Spam protection

			// We should check for spam
			// TODO: dashe3 still says the message, even if it can't kick
			if (isBotMod && settings.Spam != ESpamAction.None && !isMod)
			{
				// Duplicate messages
				if (message == lastMessage && userID == lastUser && chatRoomID == lastChatroom)
				{
					SendMessage(chatRoomID, $"Please {userName}, don't spam");
					message = "SpamDuplicate";
					KickUser("duplicate messages", userID, chatRoomID);
				}

				// Posting too fast
				else if (lastTime == DateTime.Now && userID == lastUser && chatRoomID == lastChatroom)
				{
					SendMessage(chatRoomID, $"Please {userName}, don't post too fast");
					message = "SpamTooFast";
					KickUser("posting too fast", userID, chatRoomID);
				}

				// Too long message
				else if (message.Length > 400)
				{
					SendMessage(chatRoomID, $"Please {userName}, don't post too long messages");
					message = "SpamTooLong";
					KickUser("too long message", userID, chatRoomID);
				}
			}

			#endregion

			#region Word filter

			if (settings.WordFilter.Enabled && !isMod)
			{
				foreach (var m in msg)
				{
					foreach (var word in settings.WordFilter.Filter)
					{
						if (m.ToLower() == word)
						{
							// Uh oh
							SendMessage(chatRoomID, $"Please {userName}, don't use any offensive words");
							switch (settings.WordFilter.Action)
							{
								case ESpamAction.Kick:
									kraxbot.KickUser(chatRoomID, userID);
									break;

								case ESpamAction.Ban:
									kraxbot.BanUser(chatRoomID, userID);
									break;

								case ESpamAction.Warn:
									// TODO
									break;
							}
						}
					}
				}
			}

			#endregion

			#region Cleverbot

			if (message.StartsWith('.') && message.Length > 3 && settings.Cleverbot)
			{
				// Cleverbot.IO
				if (!cleverbots.ContainsKey(chatRoomID))
				{
					// Create session
					// TODO: Try-catch this if it fails
					var p = kraxbot.API.CleverbotIO;
					cleverbots[chatRoomID] = CleverbotSession.NewSession(p.User, p.Key);
					Kraxbot.Log($"[S] Created cleverbot session for {settings.ChatName}");
				}

				// Ask it
				// TODO: Prob do this async
				// TODO: Also try-catch this
				var botResponse = cleverbots[chatRoomID].Send(message);
				Kraxbot.Log($"[F] Bot: {botResponse}");
				SendMessage(chatRoomID, botResponse);
			}

			#endregion

			#region Link handling

			// We split here to not resolve spam
			foreach (var url in message.Split(' '))
		    {
				// TODO: Handle links better, but for now, this works fine
				if (url.Length < 4)
					continue;

				#region Image links

			    var format = url.Substring(url.Length - 3);
			    if (format == "jpg" || format == "png" || format == "gif" || format == "bmp")
			    {
				    var headers = new NameValueCollection
				    {
					    { "Content-Type", "application/json" },
					    { "Ocp-Apim-Subscription-Key", kraxbot.API.ComputerVision }
					};

					if (TryRequest("https://westeurope.api.cognitive.microsoft.com/vision/v1.0/describe?maxCandidates=1", headers,
					    "{'url': '" + url + "'}",
					    out var response))
				    {
					    if (TryParseJson(response, out var result))
					    {
						    if (result.description.captions[0].confidence > 0.5f)
							    SendMessage(chatRoomID, $"{userName} posted {result.description.captions[0].text}");
						    else if (url.StartsWith("https://i.imgur.com/"))
							    SendMessage(chatRoomID, $"{userName} posted {GetImgurImage(GetStringBetween(url, ".com/", ".", true))}"); // url.Substring(19, url.LastIndexOf('.'))
						}
				    }

				    if (url.StartsWith("https://i.imgur.com/"))
					    SendMessage(chatRoomID, $"{userName} posted {GetImgurImage(GetStringBetween(url, ".com/", ".", true))}");
				}

			    #endregion
				    
				#region Twitch links
				
			    else if (url.StartsWith("https://www.twitch.tv/"))
			    {
				    if (TryGet($"https://api.twitch.tv/kraken/streams/{url.Substring(22)}?client_id={kraxbot.API.Twitch}", out var response))
				    {
					    if (TryParseJson(response, out var result))
					    {
						    if (result.error != null && (bool) result.error)
							    SendMessage(chatRoomID, $"Stream error {result.status}: {result.error}");
							else
							{
								if (result.stream == null)
									SendMessage(chatRoomID, "Stream is offline");
								else
								{
									var displayName = result.stream.channel.display_name;
									var game = result.stream.channel.game;
									// TODO: Check to see if this actually works
									var viewers = regexSplit3.Replace(result.stream.viewers.ToString(), "$0,");

									SendMessage(chatRoomID, $"{userName} posted {displayName} playing {game} with {viewers} viewers");
								}
						    }
					    }
				    }
			    }

				#endregion

			    #region Twitch clip links

				else if (url.StartsWith("https://clips.twitch.tv/"))
			    {
				    var headers = new NameValueCollection
				    {
					    { "Accept", "application/vnd.twitchtv.v5+json" },
					    { "Client-ID", kraxbot.API.Twitch }
				    };

					if (TryRequest($"https://api.twitch.tv/kraken/clips/{url.Substring(24)}", headers, null, out var response))
				    {
					    if (TryParseJson(response, out var result))
					    {
						    var title       = result.title;
						    var displayName = result.broadcaster.display_name;
						    var game        = result.game;
						    var views       = regexSplit3.Replace(result.views.ToString(), "$0,");
						    var duration    = FormatTime((int) result.duration);

							SendMessage(chatRoomID, $"{userName} posted {title} by {displayName} playing {game} with {views} views lasting {duration}");
					    }
				    }
			    }

				#endregion

				#region Imgur links

				else if (url.Contains("i.imgur.com"))
					SendMessage(chatRoomID, $"{userName} posted {GetImgurImage(GetStringBetween(url, ".com/", ".", true))}");

			    #endregion

				#region The rest

				else if (url.StartsWith("http"))
			    {
				    if (TryGet(url, out var response))
				    {
						// Improve this?
					    var title = GetStringBetween(GetStringBetween(response, "<title", "</title"), ">");
						//var titlePre = response.Substring(response.IndexOf("<title") + 6, response.IndexOf("</title>"));
						//var title = titlePre.Substring(titlePre.IndexOf('>') + 1);

						#region YouTube

						if (title.Contains("YouTube"))
					    {
						    string videoID;

						    videoID = url.StartsWith("https://youtu.be/")
							    ? url.Substring(17, 28)
							    : url.Substring(32, 43);

							if (string.IsNullOrEmpty(videoID))
								SendMessage(chatRoomID, "Error finding videoID");
							else
							{
								if (TryGet($"https://www.googleapis.com/youtube/v3/videos?id={videoID}&key={kraxbot.API.Google}&part=statistics,snippet,contentDetails", out var r))
								{
									if (TryParseJson(r, out var result))
									{
										var video = result.items[0];
										SendMessage(chatRoomID, $"{userName} posted {video.snippet.title} by {video.snippet.channelTitle} with {regexSplit3.Replace(video.statistics.viewCount, "$0,")} views, lasting {FormatYTDuration(video.contentDetails.duration)}");
									}
								}
							}
					    }

						#endregion

						#region Steam

						else if (title.Contains("on Steam"))
					    {
						    var appIDPre = url.Substring(url.IndexOf("/app/") + 5);
						    var appID = appIDPre.Substring(0, appIDPre.IndexOf('/'));

						    if (TryGet($"http://store.steampowered.com/api/appdetails/?appids={appID}", out var r))
						    {
							    if (TryParseJson(r, out var result))
							    {
								    result = result[appID].data;

								    var game      = result.name;
								    var developer = result.developers[0];
								    var price     = "Free";

								    if (!result.is_free)
									    price = $"{result.price_overview.final / 100} €";

									SendMessage(chatRoomID, $"{userName} posted {game} by {developer} ({price})");
							    }
						    }
						    else
								SendMessage(chatRoomID, $"{userName} posted {HttpUtility.HtmlDecode(title.Trim())}");
					    }

						#endregion

						#region Spotify

						else if (url.StartsWith("https://open.spotify.com/track/"))
					    {
							// TODO (Line 1086)
							SendMessage(chatRoomID, "Spotify_NotImplemented");
					    }

						#endregion

						#region Gfycat

					    else if (url.StartsWith("https://gfycat.com/"))
					    {
						    // TODO (Line 1106)
						    SendMessage(chatRoomID, "Gfycat_NotImplemented");
					    }

						#endregion

						#region GitHub

						else if (url.StartsWith("http://github.com/"))
					    {
						    if (TryGet($"https://api.github.com/repos/{url.Substring(19)}", out var r))
						    {
								if (TryParseJson(r, out var result))
									SendMessage(chatRoomID, $"{userName} posted {result.name} by {result.owner.login} with {result.subscriber_count} watchers and {result.stargazers_count} stars, written in {result.language}");
						    }
					    }

						#endregion

						#region The rest

						else if (title.Contains("Steam Community :: Screenshot"))
							SendMessage(chatRoomID, $"{userName} posted a screenshot from {response.Substring(response.IndexOf("This item is incompatible with") + 31, response.IndexOf(". Please see the"))}");
						else if (!title.Contains("Item Inventory"))
							SendMessage(chatRoomID, $"{userName} posted {HttpUtility.HtmlDecode(new Regex("/(\\r\\n|\\n|\\r)/gm").Replace(title.Trim(), ""))}");

						#endregion
					}
			    }

				#endregion
		    }

			#endregion

			#region Responses

			if (settings.Responses)
			{
				if (message.Contains("dance"))
					SendMessage(chatRoomID, "♫ ┌༼ຈل͜ຈ༽┘ ♪");
				else if (message.Contains("<3") && message.Contains(" bot"))
					SendMessage(chatRoomID, "<444444444");
				else if (message.ToLower() == "back")
					SendMessage(chatRoomID, "Welcome back");
				else if (message.Contains(" love ") && message.Contains(" bot"))
					SendMessage(chatRoomID, $"Thank you {userName}, I try my best <3");
				else if (message.Contains(" hate ") && message.Contains(" bot"))
					SendMessage(chatRoomID, ":c");
				else if (message == "ECH")
				{
					var echs = new[]
					{
						"ECH", "ECH", "ECH", "IM SICK", "BLEUCH", "NYECH", "IGH", "EYCH", 
						"JEC- CH", "AUGH", "EG", "DONT GGH AT ME", "GGGH", "GGGGGGGGH", "EUGH"
					};
					SendMessage(chatRoomID, echs[rng.Next(echs.Length)]);
				}
				else if (message.Contains(" bot") && rng.Next(100) < 5)
					SendMessage(chatRoomID, "Beep beep boop");
				else if (message == "!yes")
				{
					var words = new[] { "Yes", "yes", "No", "no", "Maybe", "idc", "YES", "NO" };
					SendMessage(chatRoomID, words[rng.Next(words.Length)]);
				}
			}

			#endregion

			#region Always on commands

			if (message == "!leave" && (isMod || userID == settings.InvitedID))
			{
				kraxbot.LeaveChat(chatRoomID);
				SendMessage(kraxbot.KraxID, $"Left {settings.ChatName} with request from {userName}");
				lastChatroom = chatRoomID;
			}

			#endregion

			#region Krax commands

			if (userID == kraxbot.KraxID)
			{
				if (message == "!timeout")
					SendMessage(chatRoomID, $"Current timeout value: {DateTime.Now}");
				else if (message == "!info")
				{
					/*
					 * dashe3 output:
					 * OS: os.type() os.release() os.arch()
					 * CPU: os.cpus()[0].model
					 * CPU load: os.loadavg()
					 * Up time: Math.round((os.uptime() / 60) / 60) hours (/24 days)
					 * Node version: process.version
					 *
					 * example output:
					 * OS: Linux 4.12.14-lp150.12.4-default x64
					 * CPU: Intel(R) Core(TM) i5-3570K CPU @ 3.40GHz
					 * CPU load: 0.0068359375, 0.0107421875, 0
					 * Up time: 24 hours (1 day)
					 * Node version: v8.10.0
					 *
					 * dashe4 output:
					 * OS: 'uname -sr'
					 * CPU load: '/proc/loadavg'
					 * Up time: '/proc/uptime[0]' ('/proc/uptime[1]' idle)
					 * .NET Core: 'dotnet --version'
					 */

					// Get OS version
					var osVer = "unknown";
					if (File.Exists("/proc/version"))
					{
						var all = File.ReadAllText("/proc/version").Split(' ');
						osVer = $"{all[0]} {all[2]}";
					}

					// Get average load
					var loadAvg = "unknown";
					if (File.Exists("/proc/loadavg"))
					{
						var all = File.ReadAllText("/proc/loadavg").Split(' ');
						loadAvg = $"1m: {all[0]}, 5m: {all[1]}, 10m: {all[2]}";
					}

					// Get up time
					var uptime = "unknown";
					if (File.Exists("/proc/uptime"))
					{
						var all  = File.ReadAllText("/proc/uptime").Split(' ');
						var time = new TimeSpan(0, 0, int.Parse(all[0]));
						var idle = new TimeSpan(0, 0, int.Parse(all[1]));

						uptime = $"{time} ({idle} idle)";
					}

					// Get .NET Core version
					var dotnetVer = Kraxbot.ExecuteProcess("dotnet", "--version");

					// Print
					SendMessage(chatRoomID, $"\nOS: {osVer}\nCPU load: {loadAvg}\nUp time: {uptime}\n.NET Core: {dotnetVer}");
				}
				else if (message.StartsWith("!permission "))
				{
					if (ulong.TryParse(message.Substring(12), out var search))
					{
						var found = settings.Users.SingleOrDefault(u => u.SteamID == search);

						SendMessage(chatRoomID, found == default(UserInfo)
							? "User not found"
							: $"{found.Permission}");
					}
					else
						SendMessage(chatRoomID, "Invalid SteamID");
				}
			}

			#endregion

			#region Admin commands

			if (isMod)
			{
				// Deprecation warnings
				if (message.StartsWith("!toggle spam"))
					SendMessage(chatRoomID, "Command not found. Try '!set spam'");
				else if (message.StartsWith("!toggle dc"))
					SendMessage(chatRoomID, "Command not found. Try '!set dc'");

				else if (message == "!clear")
					SendMessage(chatRoomID, $"\n \n \n \n \n \n \n \n \n \n \n \n \n \n \n \n \n \n \n \n \n \n \n \n \n \n \n \n \n \n \n \n \n \n \n \n \n \n \n \n \n \n \n \n \n \n \n \n \n \n \n \n \n \n \n \n \n \n {settings.ClearMsg}");

				else if (message.StartsWith("!warn "))
				{
					var search = message.Substring(6).ToLower();
					if (SearchUser(search, settings.Users, out var found))
					{
						switch (found.Warnings)
						{
							case 0:  SendMessage(chatRoomID, $"{found.Name} doesn't have any warnings");     break;
							case 1:  SendMessage(chatRoomID, $"{found.Name} has 1 warning");                 break;
							default: SendMessage(chatRoomID, $"{found.Name} has {found.Warnings} warnings"); break;
						}
					}
					else
						SendMessage(chatRoomID, "User not found");
				}

				else if (message == "!nowarn")
				{
					user.Warnings = 0;
					SendMessage(chatRoomID, "Warnings has been reset");
				}

				else if (message.StartsWith("!nowarn "))
				{
					var search = message.Substring(8);
					if (SearchUser(search, settings.Users, out var found))
					{
						found.Warnings = 0;
						SendMessage(chatRoomID, $"Warnings reset for {found.Name}");
					}
					else
						SendMessage(chatRoomID, "User not found");
				}

				else if (message == "!chatusers")
				{
					SendMessage(chatRoomID, $"There has been {settings.Users.Count} users in this chat");
				}

				else if (message == "!nodelay")
				{
					settings.Timeout = new Settings.TimeoutSettings();
					SendMessage(chatRoomID, "All delays reset");
				}

				else if (message.StartsWith("!toggle "))
				{
					var toggle = message.Substring(8);
					switch (toggle)
					{
						case "cleverbot": ToggleSetting("Cleverbot", null, settings); break;
						case "translate": ToggleSetting("Translate", null, settings); break;
						case "commands":  ToggleSetting("Commands",  null, settings); break;
						case "define":    ToggleSetting("Define",    null, settings); break;
						case "weather":   ToggleSetting("Weather",   null, settings); break;
						case "store":     ToggleSetting("Store",     null, settings); break;
						case "responses": ToggleSetting("Responses", null, settings); break;
						case "links":     ToggleSetting("Links",     null, settings); break;
						case "rules":     ToggleSetting("Rules",     null, settings); break;
						case "poke":      ToggleSetting("Poke",      null, settings); break;
						case "wiki":      ToggleSetting("Wiki",      null, settings); break;

						case "welcome":     ToggleSetting("Welcome",     "Welcome message",      settings); break;
						case "games":       ToggleSetting("Games",       "Games and recents",    settings); break;
						case "search":      ToggleSetting("Search",      "Search and YouTube",   settings); break;
						case "newapp":      ToggleSetting("NewApp",      "New app notification", settings); break;
						case "autowelcome": ToggleSetting("AutoWelcome", "Auto welcome message", settings); break;
						case "allstates":   ToggleSetting("AllStates",   "All state changes",    settings); break;
						case "allpoke":     ToggleSetting("AllPoke",     "All poke",             settings); break;

						case "Custom":
							settings.Custom.Enabled = !settings.Custom.Enabled;
							SendMessage(chatRoomID, settings.Custom.Enabled 
								? "Custom command is now enabled"
								: "Custom command is now disabled");
							break;

						case "autoleave" when userID == kraxbot.KraxID:
							ToggleSetting("AutoLeave", "Auto leave", settings);
							break;

						case "filter":
							settings.WordFilter.Enabled = !settings.WordFilter.Enabled;
							SendMessage(chatRoomID, settings.WordFilter.Enabled
								? "Word filter is now enabled"
								: "Word filter is now disabled");
							break;

						default:
							SendMessage(chatRoomID, "Toggle not found");
							break;

						// TODO: Save settings here
					}
				}

				else if (message.StartsWith("!setdelay "))
				{
					switch (msg.Length)
					{
						case 3 when int.TryParse(msg[2], out var delay):
							if (delay < 3)
								delay = 3;

							// Check if handled in SetDelay()
							if (SetDelay(msg[1], delay, settings))
							{
								SendMessage(chatRoomID, $"Delay of {msg[1]} has been set to {delay} seconds");
								// TODO: Save settings here
							}
							else
								SendMessage(chatRoomID, "Invalid parameter. Use random, define, games, recents or yt");

							break;

						case 3:
							SendMessage(chatRoomID, "Delay is not a number");
							break;

						default:
							SendMessage(chatRoomID, "Invalid amount of parameters");
							break;
					}
				}

				else if (message.StartsWith("!set "))
				{
					if (msg.Length == 3)
					{
						// TODO: Missing: lang
						// TODO: This could be moved to a method, like SetDelay
						if (msg[1] == "spam")
						{
							switch (msg[2])
							{
								case "ban":
									settings.Spam = ESpamAction.Ban;
									SendMessage(chatRoomID, "Spam will now ban");
									break;

								case "kick":
									settings.Spam = ESpamAction.Kick;
									SendMessage(chatRoomID, "Spam will now kick");
									break;

								case "warn":
									settings.Spam = ESpamAction.Warn;
									SendMessage(chatRoomID, "Spam will now warn. 3 warnings for kick (and 5 for ban when admin)");
									break;

								case "none":
									settings.Spam = ESpamAction.None;
									SendMessage(chatRoomID, "Spam will not be ignored");
									break;

								default:
									SendMessage(chatRoomID, "Unknown spam toggle. Use ban, kick, warn or none");
									break;
							}
						}
						else if (msg[1] == "dc")
						{
							// TODO: We would set count here, like '!set dc count 3'
							switch (msg[2])
							{
								case "kick":
									settings.DcKick = ESpamAction.Kick;
									SendMessage(chatRoomID, "Will now kick after 5 disconnections");
									break;

								case "warn":
									settings.DcKick = ESpamAction.Warn;
									SendMessage(chatRoomID, "Will now warn after 5 disconnections");
									break;

								case "none":
									settings.DcKick = ESpamAction.None;
									SendMessage(chatRoomID, "Will now ignore disconnections");
									break;

								default:
									SendMessage(chatRoomID, "Unknown dc toggle. Use kick, warn or none");
									break;
							}
						}
						else if (msg[1] == "autoban" || msg[1] == "autokick")
						{
							var search = message.Substring(msg[1] == "autoban" ? 13 : 14);

							if (search == "reset")
							{
								// Reset
								settings.AutoKick.Mode = ESpamAction.None;
								SendMessage(chatRoomID, "Auto kick/ban is now disabled");
							}
							else
							{
								if (SearchUser(search, settings.Users, out var found))
								{
									if (msg[1] == "autoban")
									{
										// Ban
										settings.AutoKick.Mode = ESpamAction.Ban;
										settings.AutoKick.User = found.SteamID;
										SendMessage(chatRoomID, $"{found.Name} will now be banned next time they join");
									}
									else
									{
										// Kick
										settings.AutoKick.Mode = ESpamAction.Kick;
										settings.AutoKick.User = found.SteamID;
										SendMessage(chatRoomID, $"{found.Name} will now be kicked next time they join");
									}
								}
								else
									SendMessage(chatRoomID, "No user found");
							}
						}
						else if (msg[1] == "filter")
						{
							switch (msg[2])
							{
								case "kick":
									settings.WordFilter.Action = ESpamAction.Kick;
									SendMessage(chatRoomID, "Word filter will now kick");
									break;

								case "ban":
									settings.WordFilter.Action = ESpamAction.Ban;
									SendMessage(chatRoomID, "Word filter will now ban");
									break;

								case "warn":
									settings.WordFilter.Action = ESpamAction.Warn;
									SendMessage(chatRoomID, "Word filter will now warn");
									break;

								case "list":
									SendMessage(chatRoomID, settings.WordFilter.Filter.Count > 0 
										? $"Words in filter: {string.Join(" ", settings.WordFilter.Filter)}"
										: "No words in filter to list");
									break;

								default:
									// Used as add:word or rem:word
									if (msg[2].StartsWith("add:"))
									{
										settings.WordFilter.Filter.Add(msg[2].Substring(4));
										SendMessage(chatRoomID, "Word added to list");
									}
									else if (msg[2].StartsWith("rem:"))
									{
										var w = msg[2].Substring(4);

										if (settings.WordFilter.Filter.Contains(w))
										{
											settings.WordFilter.Filter.Remove(w);
											SendMessage(chatRoomID, "Word removed from list");
										}
										else
											SendMessage(chatRoomID, "Word not found in list");
									}
									break;
							}
						}

						// TODO: Save settings here
					}
					else
						SendMessage(chatRoomID, "Invalid amount of parameters");
				}

				else if (message == "!load")
				{
					// TODO
					SendMessage(chatRoomID, "Error loading settings");
				}

				else if (message == "!save")
				{
					// TODO
					SendMessage(chatRoomID, "Error saving settings");
				}

				else if (message.StartsWith("!welcomemsg "))
				{
					var m = message.Substring(12);
					settings.WelcomeMsg = m;
					SendMessage(chatRoomID, $"Welcome message is now '{m} {userName} {settings.WelcomeEnd}'");
				}

				else if (message.StartsWith("!welcomeend "))
				{
					var m = message.Substring(12);
					settings.WelcomeEnd = m;
					SendMessage(chatRoomID, $"Welcome message is now '{settings.WelcomeMsg} {userName} {m}'");
				}

				else if (message.StartsWith("!clearmsg "))
				{
					var m = message.Substring(10);
					settings.ClearMsg = m;
					SendMessage(chatRoomID, "Clear message set");
				}

				else if (message.StartsWith("!count "))
				{
					var m = message.Substring(7);

					if (int.TryParse(m, out var num))
					{
						if (num >= 3 && num <= 20)
						{
							var i = 0;
							Task.Run(() =>
							{
								while (i < num)
								{
									SendMessage(chatRoomID, $"{num - i}");
									i++;
									Thread.Sleep(1000);
								}

								SendMessage(chatRoomID, "Done!");
							});
						}
						else
							SendMessage(chatRoomID, "Choose a number between 3 and 20");
					}
					else
						SendMessage(chatRoomID, "Not a number");
				}

				else if (message.StartsWith("!rule "))
				{
					if (msg.Length > 2)
					{
						switch (msg[1])
						{
							case "add":
								settings.SetRules.Add(message.Substring(10));
								SendMessage(chatRoomID, settings.Rules
									? "Rule added"
									: "Rule added (rules are disabled)");
								break;

							case "rm":
							case "remove":
								if (int.TryParse(msg[2], out var rule) && rule > 0)
								{
									// So rule 1 removes index 0
									rule--;

									if (settings.SetRules.Count >= rule)
									{
										settings.SetRules.RemoveAt(rule);
										SendMessage(chatRoomID, settings.Rules
											? "Rule removed"
											: "Rule removed (rules are disabled)");
									}
									else
										SendMessage(chatRoomID, "Rule not found");
								}
								else
									SendMessage(chatRoomID, "Not a rule number");

								break;

							case "cls":
							case "clear":
								settings.SetRules.Clear();
								SendMessage(chatRoomID, "Rules cleared");
								break;

							default:
								SendMessage(chatRoomID, "Invalid argument. Use add, remove or clear");
								break;
						}

						// TODO: Save settings
					}
					else
						SendMessage(chatRoomID, "Invalid amount of arguments");
				}

				else if (message.StartsWith("!kick"))
				{
					if (SearchUser(message.Substring(6), settings.Users, out var found))
						kraxbot.KickUser(chatRoomID, found.SteamID);
				}

				else if (message.StartsWith("!ban"))
				{
					if (SearchUser(message.Substring(5), settings.Users, out var found))
						kraxbot.BanUser(chatRoomID, found.SteamID);
				}

				else if (message.StartsWith("!unban"))
				{
					if (SearchUser(message.Substring(5), settings.Users, out var found))
					{
						kraxbot.UnbanUser(chatRoomID, found.SteamID);
						SendMessage(chatRoomID, $"Unbanned {found.Name}");
					}
				}
			}

			#endregion
	    }
	}
}