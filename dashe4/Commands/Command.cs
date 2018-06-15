using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Web;
using System.Xml;
using Cleverbot.Net;
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
				case Settings.SpamAction.Kick:
					SendMessage(kraxbot.KraxID, $"Kicked {kraxbot.GetFriendPersonaName(userID)} because of {reason} in {settings.ChatName}");
					kraxbot.KickUser(chatRoomID, userID);
					break;

				case Settings.SpamAction.Ban:
					SendMessage(kraxbot.KraxID, $"Banned {kraxbot.GetFriendPersonaName(userID)} because of {reason} in {settings.ChatName}");
					kraxbot.BanUser(chatRoomID, userID);
					break;

				case Settings.SpamAction.Warn:
					var user = settings.Users.Single(u => u.SteamID == userID);
					user.Warning++;
					user.LastTime = DateTime.Now;
					var warns = user.Warning;
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
								user.Warning = 0;
							}
							kraxbot.KickUser(chatRoomID, userID);
							break;

						case 5:
							SendMessage(chatRoomID, "Your own fault");
							kraxbot.BanUser(chatRoomID, userID);
							user.Warning = 0;
							break;

						default:
							SendMessage(chatRoomID, $"You now have {warns} warnings");
							break;
					}

					break;
			}
	    }
	    
	    #endregion

		public void Handle(SteamID chatRoomID, SteamID userID, string message)
		{
			/*
			 * Variable changes from dashe3:
			 * chatter	-> user
			 * name		-> userName
			 * game		-> userGame
			 * permUser	-> userPermission
			 */

			#region Vars and stuff
			
			// Stuff used in various places
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

			Kraxbot.Log($"[C] [{settings.ChatName.Substring(0, 2)}] []");

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
			user.Disconnect = 0;

			// Memes
			if (user.SteamID.ConvertToUInt64() == 76561197988712393)
			{
				if (rng.Next(10) == 1)
					message = DateTime.Now.ToShortTimeString();
			}

			#endregion

			#region Spam protection

			// We should check for spam
			// TODO: dashe3 still says the message, even if it can't kick
			if (isBotMod && settings.Spam != Settings.SpamAction.None && !isMod)
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
								case Settings.SpamAction.Kick:
									kraxbot.KickUser(chatRoomID, userID);
									break;

								case Settings.SpamAction.Ban:
									kraxbot.BanUser(chatRoomID, userID);
									break;

								case Settings.SpamAction.Warn:
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

			#endregion
	    }
	}
}