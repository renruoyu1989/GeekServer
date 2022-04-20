using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Geek.Server
{
    public class EntityMgr
    {
        //[EntityType-RemoteEntity]远程的实体，只会存储一个，这种实体组件的state为null
        static readonly ConcurrentDictionary<int, RemoteEntity> remoteEntityMap = new ConcurrentDictionary<int, RemoteEntity>();
        static readonly ConcurrentDictionary<long, Entity> entityMap = new ConcurrentDictionary<long, Entity>();
        static readonly ConcurrentDictionary<long, DateTime> activeTimeMap = new ConcurrentDictionary<long, DateTime>();

        /// <summary>
        /// 本地实体
        /// </summary>
        public static ConcurrentDictionary<long, long> LocalEntities = new ConcurrentDictionary<long, long>();

        //public static Func<long, int> ID2Type { get; set; }
        //public static Func<int, long> Type2ID { get; set; }
        //public static Func<long, ServerInfo> GetServerInfo { get; set; }


        readonly static ConcurrentDictionary<long, WorkerActor> lifeActorDic = new ConcurrentDictionary<long, WorkerActor>();
        private static WorkerActor GetLifeActor(long entityId)
        {
            lifeActorDic.TryGetValue(entityId, out var actor);
            lock (lifeActorDic)
            {
                lifeActorDic.TryGetValue(entityId, out actor);
                if (actor == null)
                {
                    actor = new WorkerActor();
                    lifeActorDic.TryAdd(entityId, actor);
                }
            }
            return actor;
        }
        
        public static async Task<T> GetCompAgent<T>(long entityId) where T : IComponentAgent
        {
            return (T)await GetCompAgent(entityId, typeof(T));
        }

        /// <summary>
        /// entityType请传入EntityType枚举类型
        /// </summary>
        public static Task<T> GetCompAgent<T>(EntityType entityType) where T : IComponentAgent
        {
            var id = EntityID.GetID(entityType);
            return GetCompAgent<T>(id);
        }

        internal static async Task<IComponentAgent> GetCompAgent(long entityId, Type compAgentType)
        {
            var type = compAgentType.BaseType;
            if (type.IsGenericType)
            {
                //Agent是不能被二次继承的,所以直接取泛型参数是安全的(优化：可在解析dll的时候缓存)
                return await GetCompAgentByCompType(entityId, type.GenericTypeArguments[0]);
            }
            return null;
        }

        public static async Task<IComponentAgent> GetCompAgentByCompType(long entityId, Type compType)
        {
            if (IsRemoteEntity(entityId))
            {
                //var entityType = ID2Type(entityId);
                var entityType = EntityID.GetEntityTypeFromID(entityId);

                remoteEntityMap.TryGetValue(entityType, out RemoteEntity entity);
                if (entity == null)
                {
                    lock (remoteEntityMap)
                    {
                        remoteEntityMap.TryGetValue(entityType, out entity);
                        if (entity == null)
                        {
                            entity = new RemoteEntity(entityType, entityId);
                            remoteEntityMap.TryAdd(entityType, entity);
                        }
                    }
                }
                return await entity.GetCompAgent(compType);
            }
            else
            {
                var entity = await GetOrNewEntity(entityId);
                return await entity.GetCompAgent(compType);
            }
        }


        /// <summary>
        /// 是否为远程实体
        /// </summary>
        /// <param name="entityId"></param>
        /// <returns></returns>
        public static bool IsRemoteEntity(long entityId)
        {
            int etype = EntityID.GetEntityTypeFromID(entityId);
            if ((int)Settings.Ins.ServerType == etype)
                return false;
            return !LocalEntities.ContainsKey(entityId);
        }

        public static Task<bool> IsCompActive(long entityId, Type compType)
        {
            entityMap.TryGetValue(entityId, out var entity);
            if (entity == null)
                return Task.FromResult(false);
            return entity.IsCompActive(compType);
        }

        /// <summary>
        /// 删档，删除entity所有StateComponent的数据
        /// </summary>
        public static async Task DeleteEntity(long entityId)
        {
            var entity = await GetOrNewEntity(entityId);
            await entity.Die();
        }

        /// <summary>
        /// 自动回收开关，离线玩家自动回收内存
        /// </summary>
        public static async Task ToggleAutoRecycle(long entityId, bool enable)
        {
            var entity = await GetOrNewEntity(entityId);
            entity.AutoRecyleEnable = enable;
        }

        /// <summary>
        /// 自动跨天，默认true
        /// </summary>
        public static async Task ToggleAutoCrossDay(long entityId, bool enable)
        {
            var entity = await GetOrNewEntity(entityId);
            entity.AutoCrossDay = enable;
        }

        /// <summary>
        /// 激活所有开启自动激活的actor及其组件
        /// </summary>
        public static async Task CompleteActiveTask()
        {
            var taskList = new List<Task>();
            var list = CompSetting.Singleton.AutoActiveEntityList;
            foreach(var type in list)
            {
                //var id = Type2ID(type);
                var id = EntityID.GetEntityIdFromType(type);
                var entity = await GetOrNewEntity(id);
                var task = entity.ActiveAutoActiveComps();
                taskList.Add(task);
            }
            await Task.WhenAll(taskList);
        }

        internal static Task RemoveEntity(long id)
        {
            return GetLifeActor(id).SendAsync(() =>
            {
                entityMap.TryRemove(id, out _);
                activeTimeMap.TryRemove(id, out _);
            });
        }

        public static void ClearEntityAgent()
        {
            foreach(var kv in entityMap)
            {
                var entity = kv.Value;
                entity.ClearAllCompsAgent();
            }
        }

        public static bool IsEntityLoaded(long entityId)
        {
            return entityMap.ContainsKey(entityId);
        }

        internal static Entity GetEntity(long entityId)
        {
            entityMap.TryGetValue(entityId, out var entity);
            return entity;
        }

        internal static async Task<Entity> GetOrNewEntity(long entityId)
        {
            var now = DateTime.Now;
            float threshold = Settings.Ins.ActorRecycleTime * 0.7f;
            if (activeTimeMap.TryGetValue(entityId, out var activeTime)
                && (now - activeTime).TotalMinutes < threshold
                && entityMap.TryGetValue(entityId, out var entity))
            {
                activeTimeMap[entityId] = DateTime.Now;
                return entity;
            }

            return await GetLifeActor(entityId).SendAsync(async () =>
            {
                entityMap.TryGetValue(entityId, out entity);
                if (entity == null)
                {
                    //var type = ID2Type(entityId);
                    var type = EntityID.GetEntityTypeFromID(entityId);
                    entity = new Entity(type, entityId);
                    entityMap.TryAdd(entityId, entity);
                    await entity.InitListener();
                }
                activeTimeMap[entityId] = DateTime.Now;
                return entity;
            });
        }

        /// <summary>
        /// 仅仅在主线程调用
        /// </summary>
        internal static async Task CheckIdle()
        {
            foreach (var kv in entityMap)
            {
                var entityId = kv.Key;
                var entity = kv.Value;
                //Actor回收
                if (!entity.AutoRecyleEnable)
                    return;

                await entity.CheckIdle();
                if ((DateTime.Now - activeTimeMap[entityId]).TotalMinutes > Settings.Ins.ActorRecycleTime)
                {
                    await GetLifeActor(entityId).SendAsync(async () =>
                    {
                        if (activeTimeMap.ContainsKey(entityId)
                        && (DateTime.Now - activeTimeMap[entityId]).TotalMinutes > Settings.Ins.ActorRecycleTime)
                        {
                            if (await entity.ReadyToDeactive())
                            {
                                entityMap.TryRemove(entityId, out var act);
                                activeTimeMap.TryRemove(entityId, out _);
                                await act.Deactive();
                            }
                        }
                    });
                }
            }
        }

        /// <summary>
        /// 所有自动跨天Enitty跨天
        /// </summary>
        public static async Task CrossDay(int openServerDay)
        {
            foreach (var kk in entityMap)
            {
                var entity = kk.Value;
                if (!entity.AutoCrossDay)
                    continue;
                await entity.CrossDay(openServerDay);
            }
        }

        /// <summary>
        /// 指定实体跨天
        /// </summary>
        public static async Task CrossDay(long entityId, int openServerDay)
        {
            entityMap.TryGetValue(entityId, out var entity);
            if (entity != null)
                await entity.CrossDay(openServerDay);
        }

        /// <summary>
        /// 关服前等待所有任务执行完成
        /// </summary>
        public static Task CompleteAllTask()
        {
            var taskList = new List<Task>();
            foreach(var kv in entityMap)
            {
                var entity = kv.Value;
                taskList.Add(entity.CompleteAllTask());
            }
            return Task.WhenAll(taskList);
        }
    }
}
