using System.IO;
using SteamKit2;
using SteamKit2.Internal;

namespace dashe4
{
    public class CMsgGroupInviteAction : ISteamSerializableMessage
    {
		// Group invited to
	    public ulong GroupID;

		// To accept or decline invite
	    public bool AcceptInvite;

	    public CMsgGroupInviteAction()
	    {
		    GroupID      = 0;
		    AcceptInvite = true;
	    }

	    public EMsg GetEMsg() => EMsg.ClientAcknowledgeClanInvite;

		public void Serialize(Stream stream)
	    {
		    try
		    {
			    var writer = new BinaryWriter(stream);
			    writer.Write(GroupID);
			    writer.Write(AcceptInvite);
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
			    var reader   = new BinaryReader(stream);
			    GroupID      = reader.ReadUInt64();
			    AcceptInvite = reader.ReadBoolean();
		    }
		    catch
		    {
				throw new IOException();
		    }
	    }
    }
}