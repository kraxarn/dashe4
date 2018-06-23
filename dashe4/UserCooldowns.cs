using System;

namespace dashe4
{
	public class UserCooldowns
	{
		public DateTime LastMessage, LastInvite, Added, Last;

		public UserCooldowns() => LastMessage = LastInvite = Added = Last = DateTime.Now;
	}
}