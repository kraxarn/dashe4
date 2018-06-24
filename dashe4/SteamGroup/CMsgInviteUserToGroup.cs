using System.IO;
using SteamKit2;
using SteamKit2.Internal;

namespace dashe4
{
    class CMsgInviteUserToGroup : ISteamSerializableMessage
    {
		// Who is being invited
	    public ulong Invitee;

		// Group to invite to
	    public ulong GroupID;

		// Unknown
	    public bool UnknownInfo;

	    public CMsgInviteUserToGroup()
	    {
		    Invitee = 0;
		    GroupID = 0;
		    UnknownInfo = true;
	    }

	    public EMsg GetEMsg() => EMsg.ClientInviteUserToClan;

		public void Serialize(Stream stream)
	    {
		    try
		    {
				var writer = new BinaryWriter(stream);
				writer.Write(Invitee);
				writer.Write(GroupID);
				writer.Write(UnknownInfo);
		    }
		    catch
		    {
			    throw new IOException();
		    }
	    }

	    public void Deserialize(Stream stream)
	    {
		    try
		    {
			    var reader  = new BinaryReader(stream);
			    Invitee     = reader.ReadUInt64();
			    GroupID     = reader.ReadUInt64();
			    UnknownInfo = reader.ReadBoolean();
		    }
		    catch
		    {
			    throw new IOException();
		    }
	    }
    }
}
