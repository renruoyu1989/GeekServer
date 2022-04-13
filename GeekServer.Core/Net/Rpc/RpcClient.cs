using BeetleX.XRPC.Clients;
using BeetleX.XRPC.Packets;

namespace Geek.Server
{
    public class RpcClient
    {
        private static XRPCClient client;
        public static void Connect(string ip, int port)
        {
            client = new XRPCClient(ip, port);
            client.Options.LogToFile = true;
            client.Options.ParameterFormater = new JsonPacket();//default messagepack
        }

        public static T Create<T>()
        {
            return client.Create<T>();
        }

    }
}
