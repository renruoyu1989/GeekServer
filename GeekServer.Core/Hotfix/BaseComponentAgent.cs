using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;

namespace Geek.Server
{
    public abstract class FuncComponentAgent<TComp> : BaseComponentAgent<TComp> where TComp : FuncComponent
    {
    }

    public abstract class BaseComponentAgent<TComp> : IComponentAgent where TComp : BaseComponent
    {
        static readonly NLog.Logger LOGGER = NLog.LogManager.GetCurrentClassLogger();
        public TComp Comp => (TComp)Owner;
        public BaseComponent Owner { get; set; }
        public long EntityId => Owner.EntityId;
        internal WorkerActor Actor => Owner.Actor;

        #region rpc
        public bool IsRemoting { get; set; } = false;

        /// <summary>
        /// TODO:处理notawait
        /// </summary>
        /// <param name="methodName"></param>
        /// <param name="genericParamCount"></param>
        /// <param name="argsType"></param>
        /// <param name="args"></param>
        /// <returns></returns>
        public Task CallRemote(string methodName, int genericParamCount, Type[] argsType, params object[] args)
        {
            _ = Actor.SendAsync(async () =>
            {
                Type self = this.GetType();
                var methodInfo = self.GetMethod(methodName, genericParamCount, argsType);
                var packet = GetRpcPacket(methodInfo, args);

                //通过entityid 获取 serverInfo
                var serverInfo = EntityID.GetServerInfo(EntityId);
                var res = await GrpcClient.Invoke(serverInfo, EntityId, packet);
                if (res < 0)
                    LOGGER.Error($"RPC调用失败:{res}");
            });
            return Task.CompletedTask;
        }

        public async Task<T> CallRemote<T>(string methodName, int genericParamCount, Type[] argsType, params object[] args)
        {
            return await Actor.SendAsync(async () =>
            {
                Type self = this.GetType();
                var methodInfo = self.GetMethod(methodName, genericParamCount, argsType);
                var packet = GetRpcPacket(methodInfo, args);

                //通过entityid 获取 serverInfo
                var serverInfo = EntityID.GetServerInfo(EntityId);
                var res =  await GrpcClient.Invoke<T>(serverInfo, EntityId, packet);
                if (res.Code < 0)
                    LOGGER.Error($"RPC调用失败:{res.Code}");
                return res.Result;
            });
        }

        private RPCPacket GetRpcPacket(MethodInfo methodInfo, object[] args)
        {
            Type self = this.GetType();

            var p = methodInfo.GetParameters();
            Type[] argTypes = new Type[p.Length];
            for (int i = 0; i < p.Length; i++)
                argTypes[i] = p[i].ParameterType;

            RPCPacket packet = new RPCPacket
            {
                CompAgent = self,
                MethodName = methodInfo.Name,
                Args = args,
                ArgTypes = argTypes,
                GenericArgs = methodInfo.GetGenericArguments()
            };
            return packet;
        }
        #endregion

        /// <summary>
        /// 等待waitComp的之前的逻辑先执行完再回到当前actor执行callback
        /// </summary>
        //public void AsyncBlockCall(IComponentAgent waitComp, Func<Task> callback, int timeOut = 10_000)
        //{
        //    waitComp.Owner.Actor.SendAsync(() => {
        //        Actor.SendAsync(callback, false, timeOut);
        //    }, false);
        //}

        /// <summary>
        /// waitComp调用waitCompFunc后 将结果带入callback回调到当前actor
        /// </summary>
        //public void AsyncBlockCall<T>(IComponentAgent waitComp, Func<Task<T>> waitCompFunc, Func<T, Task> callback, int timeOut = 10_000)
        //{
        //    waitComp.Owner.Actor.SendAsync(async () => {
        //        var ret = await waitCompFunc();
        //        _ = Actor.SendAsync(() => callback(ret), false, timeOut);
        //    }, false);
        //}

        /// <summary>
        /// 直接继承ComponentAgent<T>的可以通过此接口获取
        /// </summary>
        public async Task<TAgent> GetCompAgent<TAgent>() where TAgent : IComponentAgent
        {
            if (typeof(TAgent) == this.GetType())
                return Comp.GetAgentAs<TAgent>();
            return (TAgent)await EntityMgr.GetCompAgent(EntityId, typeof(TAgent));
        }

        public virtual Task Active()
        {
            return Task.CompletedTask;
        }

        public virtual Task Deactive()
        {
            return Task.CompletedTask;
        }

        /// <summary>延时单次回调</summary>
        public long DelayCall<TH>(long delay, Param param = null) where TH : ITimerHandler
        {
            if (HotfixMgr.IsFromHotfix(param))
            {
                LOGGER.Fatal($"不能添加hotfix工程的类型作为timer参数 DelayCall {typeof(TH)} {param.GetType()}");
                return -1;
            }
            return QuartzTimer.AddDelay(delay, EntityId, typeof(TH).FullName, param);
        }

        /// <summary>定时周期回调</summary>
        public long AddTimer<TH>(long delay, long period, Param param = null) where TH : ITimerHandler
        {
            if (HotfixMgr.IsFromHotfix(param))
            {
                LOGGER.Fatal($"不能添加hotfix工程的类型作为timer参数 AddTimer {typeof(TH)} {param.GetType()}");
                return -1;
            }
            return QuartzTimer.AddTimer(delay, period, EntityId, typeof(TH).FullName, param);
        }

        /// <summary>取消延时</summary>
        public void RemoveTimer(long id)
        {
            QuartzTimer.Remove(id);
        }

        /// <summary>取消延时</summary>
        public void RemoveTimer(List<long> list)
        {
            foreach (var id in list)
                QuartzTimer.Remove(id);
        }

        /// <summary>取消定时</summary>
        public void Unschedule(long id)
        {
            QuartzTimer.Remove(id);
        }

        /// <summary>取消定时</summary>
        public void Unschedule(List<long> list)
        {
            foreach (var id in list)
                QuartzTimer.Remove(id);
        }
    }
}