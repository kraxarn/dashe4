using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Web;
using System.Xml;
using Newtonsoft.Json;
using SteamKit2;

namespace dashe4
{
    class Command
    {
	    private readonly Kraxbot kraxbot;
	    private WebClient web;

	    private Regex regexSplit3;

	    public Command(Kraxbot bot)
	    {
		    kraxbot = bot;

		    regexSplit3 = new Regex(".{3}");

			web = new WebClient();
			//web.Headers.Add("Mozilla/5.0 (Windows NT 6.1; Win64; x64; rv:54.0) Gecko/20100101 Firefox/54.0");
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

		#endregion

		public void Handle(SteamID chatRoomID, SteamID userID, string message)
		{
			// TODO: This may not work if we aren't friends
			var name = kraxbot.GetFriendPersonaName(userID);

			#region Link handling
		
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
							    SendMessage(chatRoomID, $"{name} posted {result.description.captions[0].text}");
						    else if (url.StartsWith("https://i.imgur.com/"))
							    SendMessage(chatRoomID, $"{name} posted {GetImgurImage(GetStringBetween(url, ".com/", ".", true))}"); // url.Substring(19, url.LastIndexOf('.'))
						}
				    }

				    if (url.StartsWith("https://i.imgur.com/"))
					    SendMessage(chatRoomID, $"{name} posted {GetImgurImage(GetStringBetween(url, ".com/", ".", true))}");
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

									SendMessage(chatRoomID, $"{name} posted {displayName} playing {game} with {viewers} viewers");
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

							SendMessage(chatRoomID, $"{name} posted {title} by {displayName} playing {game} with {views} views lasting {duration}");
					    }
				    }
			    }

				#endregion

				#region Imgur links

				else if (url.Contains("i.imgur.com"))
					SendMessage(chatRoomID, $"{name} posted {GetImgurImage(GetStringBetween(url, ".com/", ".", true))}");

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
										SendMessage(chatRoomID, $"{name} posted {video.snippet.title} by {video.snippet.channelTitle} with {regexSplit3.Replace(video.statistics.viewCount, "$0,")} views, lasting {FormatYTDuration(video.contentDetails.duration)}");
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

									SendMessage(chatRoomID, $"{name} posted {game} by {developer} ({price})");
							    }
						    }
						    else
								SendMessage(chatRoomID, $"{name} posted {HttpUtility.HtmlDecode(title.Trim())}");
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
									SendMessage(chatRoomID, $"{name} posted {result.name} by {result.owner.login} with {result.subscriber_count} watchers and {result.stargazers_count} stars, written in {result.language}");
						    }
					    }

						#endregion

						#region The rest

						else if (title.Contains("Steam Community :: Screenshot"))
							SendMessage(chatRoomID, $"{name} posted a screenshot from {response.Substring(response.IndexOf("This item is incompatible with") + 31, response.IndexOf(". Please see the"))}");
						else if (!title.Contains("Item Inventory"))
							SendMessage(chatRoomID, $"{name} posted {HttpUtility.HtmlDecode(new Regex("/(\\r\\n|\\n|\\r)/gm").Replace(title.Trim(), ""))}");

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