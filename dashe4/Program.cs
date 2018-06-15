using System;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;
using System.Security;
using Newtonsoft.Json;

namespace dashe4
{
    class Program
    {
        static void Main(string[] args)
        {
			// Create main bot
			var bot = new Kraxbot();

			//var settings = new Settings(null);
			//WriteJson(settings);
			
	        Console.ReadLine();
        }

	    private static void WriteJson(Settings settings)
	    {
			File.WriteAllText("settings.json", JsonConvert.SerializeObject(settings, Formatting.Indented));
	    }

	    private static void WriteBinary(Settings settings)
	    {
			var fs = new FileStream("settings.bin", FileMode.Create);
		    var formatter = new BinaryFormatter();
		    try
		    {
			    formatter.Serialize(fs, settings);
			    Console.WriteLine("Write OK");
		    }
		    catch (SerializationException e)
		    {
			    Console.WriteLine($"Write error: {e.Message}");
		    }
		    catch (SecurityException e)
		    {
				Console.WriteLine($"Security error: {e.Message}");
		    }
			fs.Close();
	    }
    }
}
