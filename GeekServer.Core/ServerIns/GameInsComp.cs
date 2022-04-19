using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Geek.Server
{
    public class GameInsComp : NoHotfixComponent { }

    public class GameInsCompAgent : FuncComponentAgent<GameInsComp>
    {

        static readonly NLog.Logger LOGGER = NLog.LogManager.GetCurrentClassLogger();

        public async Task OnRecevieMsg(long roleId, int msdId, byte[] msgBytes)
        {
            try
            {
                IMessage msg = MsgFactory.GetMsg(msdId);
                var handler = TcpHandlerFactory.GetHandler(msg.MsgId);
                LOGGER.Debug($"-------------get msg {msg.MsgId} {msg.GetType()}");

                if (handler == null)
                {
                    LOGGER.Error("找不到对应的handler " + msg.MsgId);
                    return;
                }

                //记录本地有哪些RoleId
                EntityMgr.LocalEntities.TryAdd(roleId, roleId);
                //TODO:并将信息存入Redis
                //

                handler.Time = DateTime.Now;
                //handler.Channel = ctx.Channel;
                handler.Msg = msg;
                if (handler is TcpCompHandler compHandler)
                {
                    var entityId = await compHandler.GetEntityId();
                    if (entityId != 0)
                    {
                        var agent = await EntityMgr.GetCompAgent(entityId, compHandler.CompAgentType);
                        if (agent != null)
                            _ = agent.Owner.Actor.SendAsync(compHandler.ActionAsync);
                        else
                            LOGGER.Error($"handler actor 为空 {msg.MsgId} {handler.GetType()}");
                    }
                    else
                    {
                        LOGGER.Error($"EntityId 为0 {msg.MsgId} {handler.GetType()} {roleId}");
                    }
                }
                else
                {
                    await handler.ActionAsync();
                }
            }
            catch (Exception e)
            {
                LOGGER.Error(e.ToString());
            }
        }
    }
}
