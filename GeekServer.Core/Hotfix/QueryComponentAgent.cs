using MongoDB.Driver;

using System;
using System.Collections.Generic;
using System.Threading.Tasks;



namespace Geek.Server
{
    public abstract class QueryComponentAgent<TComp> : IComponentAgent where TComp : QueryComponent
    {

        static readonly NLog.Logger LOGGER = NLog.LogManager.GetCurrentClassLogger();

        public BaseComponent Owner { get; set; }
        internal WorkerActor Actor => Owner.Actor;
        protected TComp Comp => (TComp)Owner;
        public long EntityId => Owner.EntityId;

        #region rpc
        public bool IsRemoting { get; set; } = false;

        public Task CallRemote(string methodName, params object[] args)
        {
            _ = Actor.SendAsync(async () =>
            {
                Type self = this.GetType();
                var methodInfo = self.GetMethod(methodName);
                var packet = GetRpcPacket(methodName, args);

                //通过entityid 获取 serverInfo
                var serverInfo = EntityID.GetServerInfo(EntityId);
                var res = await GrpcClient.Invoke(serverInfo, EntityId, packet);
                if (res < 0)
                    LOGGER.Error($"RPC调用失败:{res}");
            });
            return Task.CompletedTask;
        }

        public async Task<T> CallRemote<T>(string methodName, params object[] args)
        {
            return await Actor.SendAsync(async () =>
            {
                Type self = this.GetType();
                var methodInfo = self.GetMethod(methodName);
                var packet = GetRpcPacket(methodName, args);

                //通过entityid 获取 serverInfo
                var serverInfo = EntityID.GetServerInfo(EntityId);
                var res = await GrpcClient.Invoke<T>(serverInfo, EntityId, packet);
                if (res.Code < 0)
                    LOGGER.Error($"RPC调用失败:{res.Code}");
                return res.Result;
            });
        }

        private RPCPacket GetRpcPacket(string methodName, object[] args)
        {
            Type self = this.GetType();
            var methodInfo = self.GetMethod(methodName);
            RPCPacket packet = new RPCPacket
            {
                CompAgent = self,
                MethodName = methodName,
                Args = args,
                GenericArgs = methodInfo.GetGenericArguments()
            };
            return packet;
        }
        #endregion


        public async Task Foreach<T>(IEnumerable<T> itor, Func<T, Task> dealFunc)
        {
            await Actor.SendAsync(async () => {
                foreach (var item in itor)
                {
                    await dealFunc(item);
                }
            });
        }

        public Task<List<T>> Copy<T>(IEnumerable<T> itor)
        {
            return Actor.SendAsync(() => {
                var list = new List<T>(itor);
                return list;
            });
        }

        public virtual Task Active()
        {
            return Task.CompletedTask;
        }

        public virtual Task Deactive()
        {
            return Task.CompletedTask;
        }

        public IMongoDatabase GetDB()
        {
            return Comp.GetDB();
        }

        public Task<T> LoadState<T>(long aId) where T : DBState, new()
        {
            return Comp.LoadState<T>(aId);
        }

        public Task SaveState<T>(T state) where T : DBState
        {
            return Comp.SaveState<T>(state);
        }

        /// <summary>
        /// 查询hash表
        /// </summary>
        public Task<long> QueryHash<T>(string key, string value, FilterDefinition<T> extraFilter) where T : DBState
        {
            return Comp.QueryHash<T>(key, value, extraFilter);
        }

        /// <summary>
        /// 模糊查询
        /// hashKeySearchPattern->abc
        /// </summary>
        public Task<List<T>> QueryHashKeys<T>(string key, string hashKeySearchPattern, int searchNum, FilterDefinition<T> extraFilter) where T : DBState
        {
            return Comp.QueryHashKeys<T>(key, hashKeySearchPattern, searchNum, extraFilter);
        }

        public Task UpdateField<T>(string key, string oldValue, string newValue) where T : DBState
        {
            return Comp.UpdateField<T>(key, oldValue, newValue);
        }
    }
}
