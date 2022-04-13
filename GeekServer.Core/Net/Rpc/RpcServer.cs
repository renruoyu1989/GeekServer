using BeetleX.XRPC;
using BeetleX.XRPC.Packets;
using System.Reflection;

namespace Geek.Server
{
    public class RpcServer
    {
        private static XRPCServer server;

        public static void Start(int port, Assembly assembly)
        {
            server = new XRPCServer();
            server.ServerOptions.LogLevel = BeetleX.EventArgs.LogType.All;
            server.ServerOptions.DefaultListen.Port = port;
            server.RPCOptions.ParameterFormater = new JsonPacket();//default messagepack

            server.Register(assembly);
            server.Open();
        }
    }
}
