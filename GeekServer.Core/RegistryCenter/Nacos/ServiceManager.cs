using Nacos.V2;
using Nacos.V2.Naming.Dtos;
using Nacos.V2.Naming.Event;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Geek.Server
{
    public class ServiceManager
    {

        static NLog.Logger LOGGER = NLog.LogManager.GetCurrentClassLogger();

        public static ServiceManager Singleton = new ServiceManager();
        private ServiceManager() { }

        public const string Login_Service = "Login_Service";
        public const string Game_Service = "Game_Service";
        public const string Chart_Service = "Chart_Service";
        public const string Gate_Service = "Gate_Service";

        /// <summary>
        /// instanceId - RpcClient
        /// </summary>
        private Dictionary<string, RpcClient> services = new Dictionary<string, RpcClient>();

        /// <summary>
        /// 根据条件和负载均衡选择一个服务器
        /// </summary>
        /// <param name="stype"></param>
        /// <returns></returns>
        public Instance Select(string serviceName)
        {
            //从redis，根据用户id，获取当前处于哪个游戏服务器
            return default;
        }

        /// <summary>
        /// GameServer是有状态的，不能随意分配
        /// </summary>
        /// <param name="serviceName"></param>
        /// <param name="servers"></param>
        public void UpdateInstances(string serviceName, List<Instance> instances)
        {
            foreach (var ins in instances)
            {
                var instanceId = ins.Metadata["instanceId"];
                if (!services.ContainsKey(instanceId))
                {
                    //var client = RpcClient.Create(ins.Ip, ins.Port);
                    //services.Add(instanceId, client);
                }
            }
        }

        public async Task UpdateInstances(string serviceName)
        {
            var instances = await NacosClient.Singleton.GetAllInstances(serviceName);
            UpdateInstances(serviceName, instances);
        }

        public RpcClient GetClient(string instanceId)
        {
            services.TryGetValue(instanceId, out var res);
            return res;
        }

        public async Task Subscribe(string serviceName)
        {
            await Singleton.UpdateInstances(serviceName);
            await NacosClient.Singleton.Subscribe(serviceName, new NamingListerner());
        }

        class NamingListerner : Nacos.V2.IEventListener
        {
            public Task OnEvent(IEvent @event)
            {
                LOGGER.Info($"NamingListerner:Sever Changed");
                var insChangeEvt = @event as InstancesChangeEvent;
                if (insChangeEvt != null)
                    Singleton.UpdateInstances(insChangeEvt.ServiceName, insChangeEvt.Hosts);
                return Task.CompletedTask;
            }
        }

    }
}
