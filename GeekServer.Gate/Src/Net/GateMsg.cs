using DotNetty.Transport.Channels;

namespace Geek.Server
{
    public class GateMsg
    {
        public int MsgId { get; set; }
        public byte[] Data { get; set; }
        public IChannel Channel { get; set; }
    }
}
