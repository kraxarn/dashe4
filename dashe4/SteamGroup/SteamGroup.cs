using SteamKit2;

namespace dashe4
{
    public class SteamGroup
    {
	    private readonly SteamClient client;

	    public SteamGroup(SteamClient client) => this.client = client;

	    public void AcknowledgeInvite(SteamID groupID, bool accept)
	    {
		    var invite = new ClientMsg<CMsgGroupInviteAction>((int) EMsg.ClientAcknowledgeClanInvite);

		    invite.Body.GroupID = groupID.ConvertToUInt64();
		    invite.Body.AcceptInvite = accept;

		    client.Send(invite);
		}

	    public void AcceptInvite(SteamID groupID) => AcknowledgeInvite(groupID, true);

	    public void DeclineInvite(SteamID groupID) => AcknowledgeInvite(groupID, true);

		public void InviteUser(SteamID userID, SteamID groupID)
	    {
		    var user = new ClientMsg<CMsgInviteUserToGroup>((int) EMsg.ClientInviteUserToClan);

		    user.Body.GroupID = groupID.ConvertToUInt64();
		    user.Body.Invitee = userID.ConvertToUInt64();
		    user.Body.UnknownInfo = true;

		    client.Send(user);
	    }
	}
}
