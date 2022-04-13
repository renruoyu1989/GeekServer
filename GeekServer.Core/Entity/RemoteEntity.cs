using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;

namespace Geek.Server
{

    public class RemoteEntity
    {
        //readonly static NLog.Logger LOGGER = NLog.LogManager.GetCurrentClassLogger();
        private readonly long entityId;
        private readonly int entityType;
        private readonly WorkerActor workerActor;
        private readonly ConcurrentDictionary<Type, BaseComponent> activeCompMap = new ConcurrentDictionary<Type, BaseComponent>();

        internal RemoteEntity(int entityType, long entityId)
        {
            this.entityId = entityId;
            this.entityType = entityType;
            workerActor = new WorkerActor() { entityType = entityType };
        }

        public async Task<T> GetCompAgent<T>() where T : BaseComponent
        {
            return (T)(await GetCompAgent(typeof(T)));
        }

        public async Task<IComponentAgent> GetCompAgent(Type compType)
        {
            var comp = await GetComponent(compType);
            var agent = comp.GetAgent(compType);
            agent.IsRemoting = true; //标记为远程组件
            return agent;
        }

        async Task<BaseComponent> GetComponent(Type compType)
        {
            if (activeCompMap.TryGetValue(compType, out var comp))
            {
                return comp;
            }
            else
            {
                long callChainId = workerActor.IsNeedEnqueue();
                if (callChainId < 0)
                    return ActiveComp(compType, workerActor);
                else
                    return await workerActor.Enqueue(() => ActiveComp(compType, workerActor), callChainId);
            }
        }

        private BaseComponent ActiveComp(Type compType, WorkerActor actor)
        {
            var got = activeCompMap.TryGetValue(compType, out var retComp);
            if (retComp == null)
                retComp = CompSetting.Singleton.NewComponent(actor, entityType, entityId, compType);
            if (retComp == null)//没有注册
                return null;
            if (!got)
                activeCompMap.TryAdd(compType, retComp);
            return retComp;
        }

    }
}
