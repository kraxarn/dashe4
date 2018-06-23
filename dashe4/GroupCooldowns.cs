using System;

namespace dashe4
{
	public class GroupCooldowns
	{
		public DateTime LastInvite, Joined;

		public GroupCooldowns()
			=> LastInvite = Joined = DateTime.Now;
	}
}