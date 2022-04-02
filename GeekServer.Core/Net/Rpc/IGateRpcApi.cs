namespace Geek.Server
{
    public interface IGateRpcApi
    {
        void OnReceive(long sessionId, int msgId, byte[] msg);

        void WriteAndFlush(long sessionId, NMessage msg);
    }
}
