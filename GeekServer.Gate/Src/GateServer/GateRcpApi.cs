using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Geek.Server
{
    public class GateRcpApi : IGateRpcApi
    {
        public void OnReceive(long sessionId, int msgId, byte[] msg)
        {
            throw new NotImplementedException();
            //根据sessionid，分发给客户端
        }

        public void WriteAndFlush(long sessionId, NMessage msg)
        {
            //throw new NotImplementedException();


        }
    }
}
