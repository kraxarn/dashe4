using System;
using System.Net;
using System.Text;
using System.Web;
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

	    public void PrintCookies()
	    {
			Console.WriteLine($"sessionid: {SessionID}");
		    Console.WriteLine($"steamLogin: {SteamLogin}");
		    Console.WriteLine($"steamLoginSecure: {SteamLoginSecure}");
		}
    }
}
