using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Geek.Server
{
    public class MsgDispatcher
    {

        static readonly NLog.Logger LOGGER = NLog.LogManager.GetCurrentClassLogger();

        private static readonly WorkerActor worker = new WorkerActor(100);

        private static long sessionId = 100;

        /// <summary>
        /// 通过redis分布式锁，将玩家锁定在某一个网关服（发布,订阅）
        /// 首先从本地寻找，是否存在目标服务器
        /// 不存在则让nacos选择一个负载最低的
        /// </summary>
        /// <param name="msg"></param>
        /// <returns></returns>
        public static Task Dispatch(GateMsg msg)
        {
            _ = worker.SendAsync(() =>
            {
                //根据消息id，进行分发：游戏服，登录服，聊天服
                if (Settings.Ins.LoginMsgId == msg.MsgId)
                {
                    _ = HandleLoginMsg(msg);
                }
                else
                {
                    _ = HandleGameMsg(msg);
                }
            });
            return Task.CompletedTask;
        }

        /// <summary>
        /// //TODO:如果有多个网关服务器，需要Redis分布式锁
        /// </summary>
        /// <param name="msg"></param>
        /// <returns></returns>
        public static async Task HandleLoginMsg(GateMsg msg)
        {
            var serverInfo = await ServiceManager.Singleton.Select(EntityType.LoginInstance.ToString());
            long id = EntityID.GetID(EntityType.LoginInstance, serverInfo.ServerId);
            var loginComp = await EntityMgr.GetCompAgent<LoginInsCompAgent>(id);
            var res = await loginComp.Login(msg.Data);
            //登录成功之后再记录session
            if (res.Success)
            {
                Session session = new Session
                {
                    Id = Interlocked.Increment(ref sessionId),
                    Channel = msg.Channel,
                    //Sign = reqLogin.handToken,
                    Time = DateTime.UtcNow,
                };
                SessionManager.Add(session);

                //分发到游戏服进行后续的登录流程(激活角色)
            }
            //回包给客户端
            //SessionUtils.WriteAndFlush(msg.Channel, res.ResLogin);
        }

        public static async Task HandleGameMsg(GateMsg msg)
        {
            //检查是否已经登录
            var session = SessionManager.Get(msg.Channel);
            if (session != null)
            {
                ServerInfo serverInfo;
                if (session.GameServerId <= 0)
                    serverInfo = await ServiceManager.Singleton.Select(EntityType.GameInstnace.ToString());
                else
                    serverInfo = ServiceManager.Singleton.GetSeverInfo(EntityType.GameInstnace.ToString(), session.GameServerId);
                if (serverInfo != null)
                {
                    session.GameServerId = serverInfo.ServerId;
                    long id = EntityID.GetID(EntityType.GameInstnace, serverInfo.ServerId);
                    var comp = await EntityMgr.GetCompAgent<GameInsCompAgent>(id);
                    _ = comp.OnRecevieMsg(session.RoleId, msg.MsgId, msg.Data);
                }
                else
                {
                    //选择服务器失败，直接断开链接
                    await msg.Channel.CloseAsync();
                }
            }
            else
            {
                LOGGER.Error($"你还未登录{msg.MsgId}");
            }
        }

    }
}
