using System;
using System.IO;
using System.Net;
using System.Text;
using System.Web;
using Newtonsoft.Json;
using SteamKit2;

namespace dashe4
{
    public class SteamCommunity
    {
	    public CookieContainer Cookies { get; }

	    private readonly SteamClient client;

		public string SessionID        { get; private set; }
	    public string SteamLogin       { get; private set; }
	    public string SteamLoginSecure { get; private set; }
		public bool   IsLoggedOn       { get; private set; }

		public SteamCommunity(SteamClient client)
	    {
			Cookies = new CookieContainer();
		    this.client = client;

		    SessionID = SteamLogin = SteamLoginSecure = null;
		    IsLoggedOn = false;
	    }

	    public bool LogOn(string uniqueID, string loginKey) => Authenticate(uniqueID, loginKey);

	    public bool LogOn(uint uniqueID, string loginKey) => LogOn(uniqueID.ToString(), loginKey);

	    public bool Authenticate(string uniqueID, string loginKey)
	    {
			// Check so they are valid
		    if (uniqueID == null || loginKey == null)
			    return false;

		    var sessionID = Convert.ToBase64String(Encoding.UTF8.GetBytes(uniqueID));

		    using (dynamic auth = WebAPI.GetInterface("ISteamUserAuth"))
		    {
				// Generate AES session key
			    var sessionKey = CryptoHelper.GenerateRandomBlock(32);

				// RSA encrypt it with public key
			    byte[] cryptedSessionKey;
			    using (var rsa = new RSACrypto(KeyDictionary.GetPublicKey(client.Universe)))
				    cryptedSessionKey = rsa.Encrypt(sessionKey);

				var key = new byte[20];
				Array.Copy(Encoding.ASCII.GetBytes(loginKey), key, loginKey.Length);

				// AES encrypt loginkey with session
			    var cryptedLoginKey = CryptoHelper.SymmetricEncrypt(key, sessionKey);

			    KeyValue authResult;

				// Get auth result
			    try
			    {
				    authResult = auth.AuthenticateUser(
					    steamid: client.SteamID.ConvertToUInt64(),
					    sessionkey: HttpUtility.UrlEncode(cryptedSessionKey),
					    encrypted_loginkey: HttpUtility.UrlEncode(cryptedLoginKey),
					    method: "POST",
					    secure: true
				    );
			    }
			    catch (Exception e)
			    {
				    Kraxbot.Log($"Warning: Failed to authenticate: {e.Message}");
				    return false;
			    }

			    var token       = authResult["token"].AsString();
			    var tokenSecure = authResult["tokensecure"].AsString();

				// Add cookies
				Cookies.Add(new Cookie("sessionid",        sessionID,   string.Empty, "steamcommunity.com"));
			    Cookies.Add(new Cookie("steamLogin",       token,       string.Empty, "steamcommunity.com"));
			    Cookies.Add(new Cookie("steamLoginSecure", tokenSecure, string.Empty, "steamcommunity.com"));

			    SessionID        = sessionID;
			    SteamLogin       = token;
			    SteamLoginSecure = tokenSecure;

			    IsLoggedOn = true;

			    return true;
		    }
	    }

	    public void PostComment(SteamID userID, string comment)
	    {
		    if (!IsLoggedOn)
			    return;

		    var postData = $"comment={comment}&count=6&sessionid={SessionID}";
		    var url = $"http://steamcommunity.com/comment/Profile/post/{userID.ConvertToUInt64()}/-1/";

		    var response = Request(url, postData);
		    Kraxbot.Log(response);
	    }

	    public void InviteToGroup(ulong groupID, ulong userID)
	    {
		    if (!IsLoggedOn)
			    return;

		    var postData = $"group={groupID}&invitee={userID}&sessionID={SessionID}&type=groupInvite";
		    const string url = "https://steamcommunity.com/actions/GroupInvite";

		    Kraxbot.Log($"InviteToGroup: {Request(url, postData)}");
		}

	    public int NumInvites
	    {
		    get
		    {
			    if (!IsLoggedOn)
				    return -1;

			    /*
				    *Slaps roof of line*
				    This code can throw so many exceptions
			     */
				dynamic obj = JsonConvert.DeserializeObject(Request("https://steamcommunity.com/actions/GetNotificationCounts", string.Empty));
			    return (int) obj["6"];
		    }
	    }

	    private string Request(string url, string postData)
	    {
		    var bytes = Encoding.UTF8.GetBytes(postData);

		    var request = (HttpWebRequest)WebRequest.Create(url);
		    request.KeepAlive = false;
		    request.Method = "POST";
		    request.ContentType = "application/x-www-form-urlencoded; charset=UTF-8";
		    request.ContentLength = bytes.Length;
		    request.CookieContainer = Cookies;

		    var stream = request.GetRequestStream();
		    stream.Write(bytes, 0, bytes.Length);
		    stream.Close();

		    var response = (HttpWebResponse) request.GetResponse();
		    return new StreamReader(response.GetResponseStream()).ReadToEnd();
	    }
    }
}
