using System.IO;

namespace dashe4
{
	public class UserKeyPair
	{
		public readonly string User;
		public readonly string Key;

		public UserKeyPair(string user, string key)
		{
			User = user;
			Key  = key;
		}
	}

	public class IdSecretPair
	{
		public readonly string ID;
		public readonly string Secret;

		public IdSecretPair(string id, string secret)
		{
			ID     = id;
			Secret = secret;
		}
	}

	public class ApiKey
	{
		public readonly UserKeyPair CleverbotIo;

		public readonly IdSecretPair Spotify, DeviantArt;

		public readonly string Cleverbot, Google, Steam, OpenWeatherMap;

		public readonly string Trello, ComputerVision, Twitch, Imgur;

		public ApiKey()
		{
			var lines = File.ReadAllLines("apikeys.txt");
			
			CleverbotIo = new UserKeyPair(lines[1],   lines[3]);
			Spotify     = new IdSecretPair(lines[6],  lines[8]);
			DeviantArt  = new IdSecretPair(lines[11], lines[14]);

			Cleverbot = lines[16];
			Google    = lines[19];
			Steam     = lines[22];
			Trello    = lines[28];
			Twitch    = lines[34];
			Imgur     = lines[37];

			OpenWeatherMap = lines[25];
			ComputerVision = lines[31];
		}
	}
}
