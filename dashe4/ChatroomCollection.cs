using System.Collections;
using System.Collections.Generic;
using System.IO;

namespace dashe4
{
	public class ChatroomCollection : IEnumerable<ulong>
	{
		private readonly HashSet<ulong> items;

		public ChatroomCollection()
		{
			items = new HashSet<ulong>();

			if (File.Exists("chatrooms.txt"))
			{
				foreach (var line in File.ReadAllLines("chatrooms.txt"))
				{
					if (ulong.TryParse(line, out var item))
						items.Add(item);
				}
			}
		}

		public void Add(ulong item)
		{
			if (items.Contains(item))
				return;

			items.Add(item);
			WriteToFile();
		}

		public void Remove(ulong item)
		{
			items.Remove(item);
			WriteToFile();
		}

		private async void WriteToFile()
		{
			using (var writer = new StreamWriter("chatrooms.txt"))
			{
				foreach (var item in items)
					await writer.WriteLineAsync($"{item}");
			}
		}

		IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

		public IEnumerator<ulong> GetEnumerator() => items.GetEnumerator();
	}
}