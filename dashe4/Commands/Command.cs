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
using Newtonsoft.Json.Linq;
using SteamKit2;

namespace dashe4
{
    public class Command
    {
	    private readonly Kraxbot kraxbot;

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

			rng = new Random();

			cleverbots = new Dictionary<SteamID, CleverbotSession>();

		    lastMessage  = "";
		    lastUser     = default(SteamID);
		    lastChatroom = default(SteamID);
		    lastTime     = default(DateTime);
	    }

	    #region Classes

	    private class GameEntry
	    {
		    public string name;
		    public int    playtime_forever;

		    public string HoursPlayed => $"{Math.Round(playtime_forever / 60f)}";
			
		    [JsonConstructor]
		    private GameEntry(int appid, string name, int playtime_forever)
		    {
			    this.name = name;
			    this.playtime_forever = playtime_forever;
		    }
		}

	    private class RecentGameEntry
	    {
		    public readonly string Name;
		    public readonly int    PlayTimeRecently;

		    public string HoursPlayedRecently => $"{Math.Round(PlayTimeRecently / 60f)}";

		    [JsonConstructor]
		    private RecentGameEntry(int appid, string name, int playtime_2weeks, int playtime_forever)
		    {
			    Name = name;
			    PlayTimeRecently = playtime_2weeks;
		    }
		}

		#endregion
	    
	    #region Helpers

	    private void SendMessage(SteamID chatRoomID, string message) => kraxbot.SendChatRoomMessage(chatRoomID, message);

	    private void SendChatMessage(SteamID userID, string message) => kraxbot.SendChatMessage(userID, message);

	    private bool TryGet(string url, out string response)
		    => kraxbot.TryGet(url, out response);

	    private bool TryRequest(string url, NameValueCollection headers, string body, out string response)
		    => kraxbot.TryRequest(url, headers, body, out response);

	    private bool TryParseJson(string value, out dynamic result)
		    => kraxbot.TryParseJson(value, out result);

	    private bool TryGetJson(string url, out dynamic json)
		    => kraxbot.TryGetJson(url, out json);

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

					var datetime = new DateTime(1970, 1, 1).AddSeconds((int) result.data.datetime);

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
			
		    settings.Save();
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

	    private string FormatTime(TimeSpan time)
		    => $"{time.TotalMinutes:00}:{time.Seconds:00}";

	    private bool TryGetSpotifyToken(out string token)
	    {
		    token = null;

			// TODO: Probably always the same
		    var client = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{kraxbot.API.Spotify.ID}:{kraxbot.API.Spotify.Secret}"));

		    var headers = new NameValueCollection
		    {
			    { "Authorization", $"Basic {client}" }
		    };

			if (TryRequest("https://accounts.spotify.com/api/token", headers, "{ grant_type: 'client_credentials' }", out var response))
		    {
			    if (TryParseJson(response, out var json))
				    token = json.access_token;
		    }

		    return token != null;
	    }

	    private void WarnUser(string reason, SteamID userID, Settings chat)
	    {
		    var user = chat.Users.Single(u => u.SteamID == userID);
		    user.Warnings++;
			user.LastTime = DateTime.Now;
		    var warns = user.Warnings;

		    var chatRoomID = chat.ChatID;
		    var rank = chat.Users.Single(u => u.SteamID == kraxbot.SteamID).Rank;

			kraxbot.SendKraxMessage($"Warned {kraxbot.GetFriendPersonaName(userID)} ({warns}) because of {reason} in {chat.ChatName}");

		    switch (warns)
		    {
			    case 1:
				    SendMessage(chatRoomID, "This is your first warning");
				    break;

			    case 3 when rank != EClanPermission.Officer:
				    SendMessage(chatRoomID, rng.Next(20) == 1 ? @"¯\_(ツ)_/¯" : "Your own fault");
					user.Warnings = 0;

				    kraxbot.KickUser(chatRoomID, userID);
				    break;

				case 4:
					SendMessage(chatRoomID, "Warning, one more will get you banned!");
					break;

				case 5:
				    SendMessage(chatRoomID, rng.Next(10) == 1 ? @"¯\_(ツ)_/¯" : "Your own fault");
				    kraxbot.BanUser(chatRoomID, userID);
				    user.Warnings = 0;
				    break;

			    default:
				    SendMessage(chatRoomID, $"You now have {warns} warnings");
				    break;
		    }
		}

		/// <summary>
		/// Gets all games a user owns (free and paid)
		/// </summary>
		/// <param name="userID"> SteamID64 of user </param>
		/// <param name="sort"> Sort by total play time </param>
		/// <returns></returns>
	    private List<GameEntry> GetGamesForUser(ulong userID, bool sort = false)
	    {
		    if (TryGetJson($"http://api.steampowered.com/IPlayerService/GetOwnedGames/v1/?key={kraxbot.API.Steam}&include_appinfo=1&include_played_free_games=1&steamid={userID}", out var json))
		    {
			    if (json.response.games != null)
				{
					var list = ((JArray) json.response.games).ToObject<List<GameEntry>>();

					if (sort)
						list.Sort((a, b) => b.playtime_forever.CompareTo(a.playtime_forever));

					return list;
				}
			}

		    return new List<GameEntry>();
	    }

		/// <summary>
		/// Gets all games a user has recently played
		/// </summary>
		/// <param name="userID"> SteamID64 of user </param>
		/// <param name="sort"> Sort by recent play time </param>
		/// <returns></returns>
	    private List<RecentGameEntry> GetRecentGamesForUser(ulong userID, bool sort = false)
	    {
		    if (TryGetJson($"http://api.steampowered.com/IPlayerService/GetRecentlyPlayedGames/v1/?key={kraxbot.API.Steam}&steamid={userID}", out var json))
		    {
			    if (json.response.games != null)
			    {
				    var list = ((JArray)json.response.games).ToObject<List<RecentGameEntry>>();

				    if (sort)
					    list.Sort((a, b) => b.PlayTimeRecently.CompareTo(a.PlayTimeRecently));

				    return list;
			    }
		    }

		    return new List<RecentGameEntry>();
		}
	    
	    #endregion

	    public void HandleCleverbot(SteamID userID, string message)
	    {
		    if (message.Length < 3)
			    return;

		    if (!cleverbots.ContainsKey(userID))
		    {
				// Create session
				// TODO: Try-catch this
			    var api = kraxbot.API.CleverbotIO;
				cleverbots[userID] = CleverbotSession.NewSession(api.User, api.Key);
			    Kraxbot.Log($"[S] Created cleverbot session for user {kraxbot.GetFriendPersonaName(userID)}");
			}

			// TODO: Prob do this async and try-catch
		    var response = cleverbots[userID].Send(message);
		    Kraxbot.Log($"[F] Bot: {response}");
		    SendChatMessage(userID, response);
		}

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
			var userID64 = userID.ConvertToUInt64();
			
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

			// Check if user has entry in settings.User (Should always be)
			if (!settings.Users.Contains(user))
			{
				Kraxbot.Log("Warning: User does not exist in chatroom!");
				kraxbot.SendKraxMessage($"Warning: {userName} does not exist in {settings.ChatName}");

				// TODO: Return or create temporary user?
				return;
			}

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

			// Krax is always mod
			if (user.SteamID == kraxbot.KraxID)
				isMod = true;

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
									WarnUser("offensive word", userID, settings);
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
					Kraxbot.Log($"[S] Created cleverbot session for chat {settings.ChatName}");
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
					 * OS: 'uname -sr' ('/usr/lib/os-release')
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

					// Get openSUSE version
					if (File.Exists("/usr/lib/os-release"))
					{
						var all  = File.ReadAllLines("/usr/lib/os-release");
						var name = all.First(l => l.StartsWith("PRETTY_NAME"));
						osVer += $" ({name.Substring(13, name.Length - 14)})";
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

				else if (message == "!whatsnew")
				{
					if (TryGetJson("https://api.github.com/repos/KraXarN/dashe4/releases/latest", out var json))
						SendMessage(chatRoomID, $"What's new in {(string) json.tag_name}:\n{(string) json.body}");
				}
			}

			#endregion

			#region Cooldown commands

			if (settings.Commands || isMod)
			{
				if (message.StartsWith("!poke"))
				{
					if (settings.AllPoke || isMod)
					{
						if (SearchUser(message.Substring(6), settings.Users, out var found))
						{
							if (found.SteamID == settings.LastPoke)
								SendMessage(chatRoomID, $"You have already poked {found.Name}");
							else if (found.SteamID == kraxbot.KraxID || found.SteamID == kraxbot.SteamID)
								SendMessage(chatRoomID, "Invalid target. Use !krax if you want to poke Kraxie");
							else
							{
								SendChatMessage(found.SteamID, $"Hey you! {found.Name} poked you in {settings.ChatName}");
								SendMessage(chatRoomID, $"Poked {found.Name}");
								settings.LastPoke = found.SteamID;
							}
						}
						else
							SendMessage(chatRoomID, "No user found");
					}
				}

				else if (message == "!random")
				{
					if (isMod || settings.Timeout.Random < DateTime.Now)
					{
						// TODO: Remake this to not take the same user twice?

						// Don't select bot
						// TODO: We may need to create a new list (prob not though?)
						var users = settings.Users.Where(u => u.SteamID != kraxbot.SteamID).ToArray();
						var random = users[rng.Next(users.Length)];

						// Print it
						SendMessage(chatRoomID, rng.Next(100) <= 5
							? $"{random.Name} wins a free cookie"
							: $"{random.Name} wins");

						settings.Timeout.Random = DateTime.Now + TimeSpan.FromSeconds(settings.Delay.Random);
					}
					else
						SendMessage(chatRoomID, $"This command is disabled for {FormatTime(settings.Timeout.Random - DateTime.Now)}");
				}

				else if (message == "!rangame")
				{
					if (TryGet($"http://api.steampowered.com/IPlayerService/GetOwnedGames/v1/?key={kraxbot.API.Steam}&include_appinfo=1&include_played_free_games=1&steamid={userID64}", out var response))
					{
						if (TryParseJson(response, out var json))
						{
							JArray games = json.response.games;
							var ranGame  = json.response.games[rng.Next(games.Count)];

							var gameID   = (int) ranGame.appid;
							var gameName = (string) ranGame.name;
							var gameTime = Math.Round((int) ranGame.playtime_forever / 60f);

							if (gameTime <= 1)
								SendMessage(chatRoomID, $"Why don't you try out {gameName}? You haven't played it yet. steam://install/{gameID}");
							else if (gameTime <= 10)
								SendMessage(chatRoomID, $"Why don't you try out {gameName}? You have only played it for {gameTime} hours. steam://install/{gameID}");
							else
								SendMessage(chatRoomID, $"Why don't you try out {gameName}? You have played it for {gameTime} hours though. steam://install/{gameID}");
						}
					}
				}

				else if (message == "!games")
				{
					if (isMod || settings.Timeout.Games < DateTime.Now)
					{
						var games = GetGamesForUser(userID64, true);

						SendMessage(chatRoomID, $"You have {games.Count} games");

						if (games.Count >= 5)
						{
							var gamestr = "";

							for (var i = 0; i < 5; i++)
								gamestr += $"\n{i + 1}: {games[i].name} ({games[i].HoursPlayed} hours played)";

							SendMessage(chatRoomID, gamestr);
						}
						else
							SendMessage(chatRoomID, "You don't have enough games to show most played");

						settings.Timeout.Games = DateTime.Now + TimeSpan.FromSeconds(settings.Delay.Random);
					}
					else
						SendMessage(chatRoomID, $"This command is disabled for {FormatTime(settings.Timeout.Games - DateTime.Now)}");
				}

				else if (message == "!recents")
				{
					if (isMod || settings.Timeout.Games < DateTime.Now)
					{
						var games = GetRecentGamesForUser(userID64, true);
						var max = games.Count;

						if (max > 5)
							max = 5;
							
						if (max > 0)
						{
							var gamestr = $"You have played {max} games recently";

							for (var i = 0; i < max; i++)
								gamestr += $"\n{i + 1}: {games[i].Name} ({games[i].HoursPlayedRecently} hours played recently)";

							SendMessage(chatRoomID, gamestr);
						}
						else
							SendMessage(chatRoomID, "You haven't played any games recently");

						settings.Timeout.Games = DateTime.Now + TimeSpan.FromSeconds(settings.Delay.Random);
					}
					else
						SendMessage(chatRoomID, $"This command is disabled for {FormatTime(settings.Timeout.Games - DateTime.Now)}");
				}

				else if (message.StartsWith("!define ") && (settings.Define || isMod))
				{
					if (isMod || settings.Timeout.Define < DateTime.Now)
					{
						if (TryGet($"http://api.urbandictionary.com/v0/define?term={message.Substring(8)}",
							out var response))
						{
							if (TryParseJson(response, out var json))
							{
								if ((string) json.result_type == "no_results")
									SendMessage(chatRoomID, "No results found");
								else
								{
									var def = ((string) json.list[0].definition).Replace('\n', ' ');

									SendMessage(chatRoomID, def.Length < 500
										? $"{json.list[0].word} is {def}"
										: $"{json.list[0].word} is {def.Substring(0, 500)}...");

									if (isMod)
									{
										if (json.list[0].example != null)
											SendMessage(chatRoomID,
												$"Example: {((string) json.list[0].example).Replace('\n', ' ')}");

										var likes = (int) json.list[0].thumbs_up;
										var dislikes = (int) json.list[0].thumbs_down;
										var total = likes + dislikes;

										SendMessage(chatRoomID,
											$"Rating: {(float) likes / total * 100}% positive ({likes}/{total})");
									}
								}
							}
						}

						settings.Timeout.Define = DateTime.Now + TimeSpan.FromSeconds(settings.Delay.Define);
					}
					else
						SendMessage(chatRoomID, $"This command is disabled for {FormatTime(settings.Timeout.Define - DateTime.Now)}");
				}

				else if (message.StartsWith("!wiki ") && (settings.Wiki || isMod))
				{
					if (isMod || settings.Timeout.Define < DateTime.Now)
					{
						// TODO: Post both defs in the same message

						if (TryGet($"http://en.wikipedia.org/w/api.php?action=opensearch&format=json&search={message.Substring(6)}", out var response))
						{
							if (TryParseJson(response, out var json))
							{
								if (json[1][0] != null)
								{
									SendMessage(chatRoomID, $"{json[1][0]}: {json[2][0]}");

									if (isMod && json[1][1])
										SendMessage(chatRoomID, $"{json[1][1]}: {json[2][1]}");
								}
								else
									SendMessage(chatRoomID, "No results found");
							}
						}

						settings.Timeout.Define = DateTime.Now + TimeSpan.FromSeconds(settings.Delay.Define);
					}
					else
						SendMessage(chatRoomID, $"This command is disabled for {FormatTime(settings.Timeout.Define - DateTime.Now)}");
				}

				else if (message.StartsWith("!yt ") && (settings.Search || isMod))
				{
					if (isMod || settings.Timeout.Search < DateTime.Now)
					{
						if (TryGet($"https://www.googleapis.com/youtube/v3/search?part=snippet&q={message.Substring(4)}&type=video&key={kraxbot.API.Google}", out var response))
						{
							if (TryParseJson(response, out var json))
							{
								if (json.items[0].snippet != null)
								{
									var max     = isMod ? 3 : 1;
									var results = "";

									for (var i = 0; i < max; i++)
									{
										if (json.items[i] != null)
											results += $"\n{json.items[i].snippet.title} ({json.items[i].snippet.channelTitle}):  https://youtu.be/{json.items[i].videoId}";
									}

									SendMessage(chatRoomID, $"Results:{results}");
								}
								else
									SendMessage(chatRoomID, "No results found");
							}
						}

						settings.Timeout.Search = DateTime.Now + TimeSpan.FromSeconds(settings.Delay.Search);
					}
					else
						SendMessage(chatRoomID, $"This command is disabled for {FormatTime(settings.Timeout.Search - DateTime.Now)}");
				}

				else if (message.StartsWith("!search ") && (settings.Search || isMod))
				{
					if (isMod || settings.Timeout.Search < DateTime.Now)
					{
						if (TryGet( $"https://www.googleapis.com/customsearch/v1?cx=004114719084244063804%3Ah7bxvwhveyw&key={kraxbot.API.Google}&q={message.Substring(8)}", out var response))
						{
							if (TryParseJson(response, out var json))
							{
								var max = isMod ? 3 : 1;

								var info     = json.searchInformation;
								JArray items = json.items;

								if (items != null && items.Count >= max)
								{
									var results = $"Found {info.formattedTotalResults} results in {info.formattedSearchTime} seconds";

									if (info.spelling != null)
										results += $"\nDid you mean {json.spelling.correctedQuery}?";

									for (var i = 0; i < max; i++)
									{
										if (items[i] != null)
											results += $"\n{json.items[i].title}: {json.items[i].link}";
									}

									SendMessage(chatRoomID, results);
								}
								else
									SendMessage(chatRoomID, "No results found");
							}
						}

						settings.Timeout.Search = DateTime.Now + TimeSpan.FromSeconds(settings.Delay.Search);
					}
					else
						SendMessage(chatRoomID, $"This command is disabled for {FormatTime(settings.Timeout.Search - DateTime.Now)}");
				}

				else if (message.StartsWith("!music ") && (settings.Search || isMod))
				{
					if (isMod || settings.Timeout.Search < DateTime.Now)
					{
						if (TryGetSpotifyToken(out var token))
						{
							var headers = new NameValueCollection
							{
								{ "Authorization", $"Bearer {token}" },
								{ "Accept", "application/json" }
							};

							if (TryRequest($"https://api.spotify.com/v1/search?type=track,artist&limit=3&q={message.Substring(7)}", headers, null, out var response))
							{
								if (TryParseJson(response, out var json))
								{
									var max      = isMod ? 3 : 1;
									JArray items = json.tracks.items;

									if (items.Count >= max)
									{
										var results = $"Found {json.tracks.total} tracks";

										for (var i = 0; i < max; i++)
										{
											var item = json.tracks.items[i];
											results += $"\n{item.name} by {item.artists[0].name}: {item.external_urls.spotify}";
										}

										SendMessage(chatRoomID, results);
									}

								}
								else
									SendMessage(chatRoomID, "Bug found! (Failed to parse json)");
							}
							else
								SendMessage(chatRoomID, "Bug found! (Failed to get response)");
						}
						else
							SendMessage(chatRoomID, "Bug found! (Failed to get token)");

						settings.Timeout.Search = DateTime.Now + TimeSpan.FromSeconds(settings.Delay.Search);
					}
					else
						SendMessage(chatRoomID, $"This command is disabled for {FormatTime(settings.Timeout.Search - DateTime.Now)}");
				}

				else if (message.StartsWith("/r/") || message.StartsWith("!r "))
				{
					if (TryGet($"https://www.reddit.com/r/{message.Substring(3)}/about/.json?count=3&show=3", out var response))
					{
						if (TryParseJson(response, out var json))
						{
							// TODO: This may crash if there are less than 3 posts total
							var results = "Top posts:";
							for (var i = 0; i < 3; i++)
							{
								var item = json.data.children[i].data;
								results += $"\n{i + 1}. {item.title} ({item.score / 1000f * 10}k): {item.url}";
							}

							SendMessage(chatRoomID, results);
						}
					}
				}
			}

			#endregion

			#region User commands

			if (settings.Commands || isMod)
			{
				if (message.StartsWith("!rannum"))
					SendMessage(chatRoomID, "Command not found. Did you mean '!roll'?");

				else if (message == "!help")
					SendMessage(chatRoomID, "Check https://web.kraxarn.com/bot/docs/ for how to use all commands");

				else if (message == "!check")
					SendMessage(chatRoomID, $"Settings for this chat can be found at https://web.kraxarn.com/bot/settings/?id={chatRoomID}");

				else if (message == "!bday")
				{
					if (TryGet($"http://api.steampowered.com/ISteamUser/GetPlayerSummaries/v0002/?key={kraxbot.API.Steam}&steamids={userID}", out var response))
					{
						if (TryParseJson(response, out var json))
						{
							if (json.response.players[0].timecreated != null)
							{
								var date = new DateTime(1970, 1, 1).AddSeconds((double) json.response.players[0].timecreated);
								var age  = DateTime.Now.Year - date.Year;

								SendMessage(chatRoomID, age == 1
									? $"{userName}'s cake day is {date} + (Account created {date.Year} and 1 year old"
									: $"{userName}'s cake day is {date} + (Account created {date.Year} and {age} years old");
							}
						}
					}
				}

				else if (message == "!users")
				{
					int guests, users, mods, admins;
					guests = users = mods = admins = 0;
					var owner = "no ";

					foreach (var u in settings.Users)
					{
						switch (u.Rank)
						{
							case EClanPermission.Anybody: guests++; break;
							case EClanPermission.Member:  users++;  break;
							case EClanPermission.Moderator: mods++; break;
							case EClanPermission.Officer: admins++; break;
							case EClanPermission.Owner: owner = ""; break;
						}
					}

					SendMessage(chatRoomID, $"{settings.Users.Count} people are currently in chat, where {guests} are guests, {users} are users, {mods} are mods, {admins} are admins and {owner}owner");
				}

				else if (message == "!afk")
				{
					// TODO: Check Users.InChat

					var afks = 0;
					
					foreach (var u in settings.Users)
					{
						if (u.LastMessage <= DateTime.Now - TimeSpan.FromMinutes(30))
							afks++;
					}
					
					SendMessage(chatRoomID, $"{afks} out of {settings.Users} users are idle in the chat");
				}

				else if (message == "!invited")
				{
					var invitedName = string.IsNullOrEmpty(settings.InvitedName) ? "Unknown" : settings.InvitedName;
					SendMessage(chatRoomID, $"{invitedName} invited me to this chat");
				}

				else if (message == "!servertime")
				{
					if (TryGetJson("http://api.steampowered.com/ISteamWebAPIUtil/GetServerInfo/v0001/", out var json))
						SendMessage(chatRoomID, $"Current time on Steam's server is {json.servertimestring}");
				}

				else if (message == "!today")
				{
					var date = DateTime.Now;
					if (TryGetJson($"http://numbersapi.com/{date.Month}/{date.Day}/date?json", out var json))
						SendMessage(chatRoomID, json.text);
				}

				else if (message == "!nameof ")
				{
					if (int.TryParse(message.Substring(8), out var gameID))
					{
						// This can take a while, so we launch it in another thread
						// TODO: We could cache the applist
						Task.Run(() =>
						{
							var gameName = "";

							if (TryGetJson("http://api.steampowered.com/ISteamApps/GetAppList/v2/", out var json))
							{
								foreach (var app in json.applist.apps)
								{
									if ((int) app.appid != gameID)
										continue;

									gameName = (string) app.name;
									break;
								}

								SendMessage(chatRoomID, string.IsNullOrEmpty(gameName) 
									? $"Nothing found for {gameID}" 
									: $"Name for {gameID} is {gameName}");
							}
						});
					}
				}

				else if (message == "!apps")
				{
					// TODO: Same as !nameof

					Task.Run(() =>
					{
						if (TryGetJson("http://api.steampowered.com/ISteamApps/GetAppList/v2/", out var json))
							SendMessage(chatRoomID, $"There are currently {((JArray) json.applist.apps).Count} games and apps on Steam");
					});
				}

				else if (message == "!lastdown")
				{
					if (TryGetJson($"http://api.steampowered.com/ISteamUser/GetPlayerSummaries/v0002/?key={kraxbot.API.Steam}&steamids=76561198107682909", out var json))
					{
						var date = new DateTime(1970, 1, 1).AddSeconds((int) json.response.players[0].lastlogoff);
						SendMessage(chatRoomID, $"Steam's last downtime was {date}");
					}
				}

				else if (message.StartsWith("!rps "))
				{
					var num = rng.Next(3);
					var sel = char.Parse(msg[1].Substring(0, 1));
					var win = "Invalid selection. Use r, p, or s";

					char bsel;
					switch (num)
					{
						case 0:  bsel = 'r'; break;
						case 1:  bsel = 'p'; break;
						default: bsel = 's'; break;
					}

					switch (sel)
					{
						case 'r':
							switch (bsel)
							{
								case 'r': win = "Rock: Tied! :)";       break;
								case 'p': win = "Paper: I win! :D";     break;
								case 's': win = "Scissor: You win! :/"; break;
							}
							break;

						case 'p':
							switch (bsel)
							{
								case 'r': win = "Rock: You win! :/";  break;
								case 'p': win = "Paper: Tied! :)";    break;
								case 's': win = "Scissor: I win! :D"; break;
							}
							break;

						case 's':
							switch (bsel)
							{
								case 'r': win = "Rock: I win! :D";   break;
								case 'p': win = "Paper: You win :/"; break;
								case 's': win = "Scissor: Tied! :)"; break;
							}
							break;
					}

					SendMessage(chatRoomID, win);
				}

				else if (message == "!name")
				{
					if (kraxbot.TryGetFriendDetails(userID, out var info) && !string.IsNullOrEmpty(info.GameName))
					{
						SendMessage(chatRoomID, Equals(info.GameServerIP, IPAddress.None)
							? $"{userName} playing {info.GameName} ({user.Rank})"
							: $"{userName} playing {info.GameName} on {info.GameServerIP} ({user.Rank})");
					}
					else
						SendMessage(chatRoomID, $"{userName} ({user.Rank})");
				}

				else if (message == "!ver")
					SendMessage(chatRoomID, $"KraxBot {kraxbot.Version} powered by .NET Core {Kraxbot.ExecuteProcess("dotnet", "--version")} by Kraxie / kraxarn");

				else if (message == "!id")
					SendMessage(chatRoomID, $"{userName}'s SteamID is {userID}");

				else if (message == "!chatid")
					SendMessage(chatRoomID, $"This chat's SteamID is {chatRoomID}");

				else if (message.StartsWith("!8ball"))
				{
					var words = new[]
					{
						"It is certain", "It is decidedly so", "Without a doubt", 
						"Yes definitely", "You may rely on it", "As I see it, yes", 
						"Most likely", "Outlook good", "Yes", "Signs point to yes", 
						"Reply hazy try again", "Ask again later", "Better not tell you now", 
						"Cannot predict now", "Concentrate and ask again", "Do not count on it", 
						"My reply is no", "My sources say no", "Outlook not so good", 
						"Very doubtful"
					};
					SendMessage(chatRoomID, words[rng.Next(words.Length)]);
				}

				else if (message == "!time")
					SendMessage(chatRoomID, $"{DateTime.Now.TimeOfDay}");

				else if (message.StartsWith("!roll"))
				{
					if (msg.Length != 2 || !int.TryParse(msg[1], out var max))
						max = 100;

					SendMessage(chatRoomID, $"Your number is {rng.Next(max) + 1}");
				}

				else if (message == "!updated")
				{
					// TODO: Get the latest release from GitHub instead
				}

				else if (message == "!api")
				{
					if (TryParseJson($"http://api.steampowered.com/ISteamUser/GetPlayerSummaries/v0002/?key={kraxbot.API.Steam}&steamids={userID}", out var json))
					{
						// TODO: When does this fail?
						if (!string.IsNullOrEmpty(json.response.players[0].personaname as string))
						{
							var info = json.response.players[0];
							SendMessage(chatRoomID, $"[{info.loccountrycode}] {info.realname} / {info.personaname}");
						}
					}
				}

				else if (message.StartsWith("!math "))
				{
					if (TryGet($"http://api.mathjs.org/v1/?expr={Uri.EscapeDataString(message.Substring(6))}", out var response))
					{
						if (!response.StartsWith("Error") && !response.StartsWith("TimeoutError"))
							SendMessage(chatRoomID, $"= {response}");
					}
				}

				else if (message.StartsWith("!players "))
				{
					// BUG: This ignores what the user is playing

					var search = message.Substring(9).ToLower();
					
					var gameName = "";
					var gameID   = 0;

					// TODO: This could also be cached
					Task.Run(() =>
					{
						if (TryParseJson("http://api.steampowered.com/ISteamApps/GetAppList/v2", out var tempJson))
						{
							var applist = (JArray) tempJson.applist.apps;

							foreach (dynamic app in applist)
							{
								var name = (string) app.name;
								if (name.ToLower().Contains(search) && !name.Contains("Trailer") && !name.Contains("DLC"))
								{
									gameName = name;
									gameID = (int) app.appid;
									break;
								}
							}

							if (gameID == 0)
								SendMessage(chatRoomID, "No results found");
							else
							{
								if (TryGetJson($"http://api.steampowered.com/ISteamUserStats/GetNumberOfCurrentPlayers/v1/?appid={gameID}", out var json))
								{
									SendMessage(chatRoomID, $"There are currently {json.response.player_count} people playing {gameName}");
								}
							}
						}
					});
				}

				else if (message.StartsWith("!weather ") && settings.Weather)
				{
					var search = message.Substring(9);

					if (TryGetJson($"http://api.openweathermap.org/data/2.5/weather?units=metric&appid={kraxbot.API.OpenWeatherMap}&q={search}", out var json))
					{
						if (string.IsNullOrEmpty(json.messageas as string))
						{
							var updated = new DateTime(1970, 1, 1).AddSeconds((int) json.dt);
							var time    = DateTime.Now - updated;

							SendMessage(chatRoomID, $"The weather in {json.name} is {json.weather[0].main}, {Math.Round(json.main.temp)}ºC with wind at {Math.Round(json.wind.speed)} m/s and {json.clouds.all}% clouds (Updated {time} ago)");
						}
					}
				}
				
				else if (message.StartsWith("!krax") && settings.Poke)
				{
					var send = message.Length > 6 ? message.Substring(6) : null;

					var state = kraxbot.GetFriendPersonaState(kraxbot.KraxID);

					switch (state)
					{
						case EPersonaState.Offline:
							SendMessage(chatRoomID, "Sorry, she's offline right now");
							break;

						case EPersonaState.Busy:
							SendMessage(chatRoomID, "Sorry, she's busy right now");
							break;

						case EPersonaState.Away when send == null:
							SendMessage(chatRoomID, "She's away, but I poked her anyway");
							SendChatMessage(kraxbot.KraxID, $"{userName} poked you in {settings.ChatName}");
							break;

						case EPersonaState.Away:
							SendMessage(chatRoomID, "She's away, but I sent her your message anyway");
							SendChatMessage(kraxbot.KraxID, $"{userName} poked you with '{send}' in {settings.ChatName}");
							break;

						case EPersonaState.Snooze when send == null:
							SendMessage(chatRoomID, "She's on snooze, but I poked her anyway");
							SendChatMessage(kraxbot.KraxID, $"{userName} poked you in {settings.ChatName}");
							break;

						case EPersonaState.Snooze:
							SendMessage(chatRoomID, "She's on snooze, but I sent her your message anyway");
							SendChatMessage(kraxbot.KraxID, $"{userName} poked you with '{send}' in {settings.ChatName}");
							break;

						default:
							if (send == null)
							{
								SendMessage(chatRoomID, "Poked her");
								SendChatMessage(kraxbot.KraxID, $"{userName} poked you in {settings.ChatName}");
							}
							else
							{
								SendMessage(chatRoomID, "Send her your message");
								SendChatMessage(kraxbot.KraxID, $"{userName} poked you with '{send}' in {settings.ChatName}");
							}
							break;
					}
				}

				else if (message == "!rules" && settings.Rules)
				{
					string rules;

					if (settings.SetRules.Count > 0)
					{
						var i = 0;
						rules = "Rules:";

						foreach (var rule in settings.SetRules)
							rules += $"\n{++i}. {rule}";
					}
					else
						rules = "Default rules: \n1. No begging for stuff \n2. No spamming \n3. Use common sense \n4. The decisions of mods and admins are final \n5. Don't spam the bot's commands \n(You can change these with the !rule command)";

					SendMessage(chatRoomID, rules);
				}
			}

			#endregion

			#region Update vars

			lastMessage  = message;
			lastUser     = userID;
			lastChatroom = chatRoomID;
			lastTime     = DateTime.Now;

			#endregion
		}
	}
}