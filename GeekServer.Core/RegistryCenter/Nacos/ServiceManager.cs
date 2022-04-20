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

        private Dictionary<string, Dictionary<int, ServerInfo>> services = new Dictionary<string, Dictionary<int, ServerInfo>>();

        /// <summary>
        /// 选择一个负载最低且健康的服务器
        /// </summary>
        /// <param name="stype"></param>
        /// <returns></returns>
        public async Task<ServerInfo> Select(string serviceName)
        {
            var ins = await NacosClient.Singleton.SelectOneHealthyInstance(serviceName);
            if (ins != null)
            {
                ServerInfo info = new ServerInfo();
                info.Ip = ins.Ip;
                info.Port = ins.Port;
                info.ServerId = int.Parse(ins.InstanceId);
                return info;
            }
            LOGGER.Error($"SelectOneHealthyInstance失败:{serviceName}");
            return null;
        }

        public void UpdateInstances(string serviceName, List<Instance> instances)
        {
            lock (services)
            {
                services.TryGetValue(serviceName, out var srvDic);
                if (srvDic == null)
                {
                    srvDic = new Dictionary<int, ServerInfo>();
                    services.Add(serviceName, srvDic);
                }
                else
                {
                    srvDic.Clear();
                }
                foreach (var ins in instances)
                {
                    ServerInfo info = new ServerInfo();
                    info.Ip = ins.Ip;
                    info.Port = ins.Port;
                    info.ServerId = int.Parse(ins.InstanceId);
                    srvDic[info.ServerId] = info;
                }
            }
        }

        public async Task UpdateInstances(string serviceName)
        {
            var instances = await NacosClient.Singleton.GetAllInstances(serviceName);
            UpdateInstances(serviceName, instances);
        }


        public ServerInfo GetSeverInfo(string serviceName, int serverId)
        {
            lock (services)
            {
                if (services.ContainsKey(serviceName) && services[serviceName].ContainsKey(serverId))
                    return services[serviceName][serverId];
                LOGGER.Error($"GetSeverInfo失败:{serviceName}:{serverId}");
                return null;
            }
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
