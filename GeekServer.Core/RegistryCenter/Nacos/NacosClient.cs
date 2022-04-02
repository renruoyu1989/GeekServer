using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Nacos.V2;
using Nacos.V2.DependencyInjection;
using Nacos.V2.Naming.Event;
using Nacos.V2.Utils;
using System;
using System.Threading.Tasks;

namespace Geek.Server
{
    public class NacosClient : SingletonTemplate<NacosClient>
    {
        static readonly NLog.Logger LOGGER = NLog.LogManager.GetCurrentClassLogger();

        private readonly INacosNamingService namingSvc;
        private readonly INacosConfigService configSvc;

        public NacosClient()
        {
            IServiceCollection services = new ServiceCollection();

            services.AddNacosV2Config(x =>
            {
                x.ServerAddresses = new System.Collections.Generic.List<string> { "http://localhost:8848/" };
                x.EndPoint = "";
                x.Namespace = "cs-test";

                /*x.UserName = "nacos";
                x.Password = "nacos";*/

                // swich to use http or rpc
                x.ConfigUseRpc = true;
            });

            services.AddLogging(builder => { builder.AddConsole(); });

            IServiceProvider serviceProvider = services.BuildServiceProvider();
            configSvc = serviceProvider.GetService<INacosConfigService>();
            namingSvc = serviceProvider.GetService<INacosNamingService>();
        }


        #region 配置中心

        public async Task<bool> Publish(string configId, string group, string content)
        {
            return await configSvc.PublishConfig(configId, group, content);
        }

        public async Task<string> Get(string configId, string group)
        {
            return await configSvc.GetConfig(configId, group, 10000L);
        }

        public async Task<bool> Remove(string configId, string group)
        {
            return await configSvc.RemoveConfig(configId, group);
        }


        public async Task Subscribe(string configId, string group)
        {
            var listener = new ConfigListener();
            await configSvc.AddListener(configId, group, listener);
        }


        class ConfigListener : Nacos.V2.IListener
        {
            public void ReceiveConfigInfo(string configInfo)
            {
            }
        }
        #endregion


        #region 注册发现
        protected virtual async Task RegisterInstance()
        {
            //GateWay_10001 Login_10001 Logic_10001
            var serviceName = $"reg-{Guid.NewGuid().ToString()}";
            var ip = "127.0.0.1";
            var port = 9999;
            await namingSvc.RegisterInstance(serviceName, ip, port);
        }

        protected virtual async Task DeregisterInstance()
        {
            var serviceName = $"reg-{Guid.NewGuid().ToString()}";
            var ip = "127.0.0.1";
            var port = 9999;
            await namingSvc.DeregisterInstance(serviceName, ip, port);
        }

        protected virtual async Task Subscribe()
        {
            var serviceName = $"sub-{Guid.NewGuid().ToString()}";
            var ip = "127.0.0.3";
            var port = 9999;
            var listerner = new NamingListerner();
            await namingSvc.Subscribe(serviceName, listerner);
            await namingSvc.RegisterInstance(serviceName, "127.0.0.4", 9999);
        }


        class NamingListerner : Nacos.V2.IEventListener
        {

            static readonly NLog.Logger LOGGER = NLog.LogManager.GetCurrentClassLogger();

            public async Task OnEvent(IEvent @event)
            {
                LOGGER.Info($"NamingListerner, {@event.ToJsonString()}");
                var instancesChangeEvent = @event as InstancesChangeEvent;
                if (instancesChangeEvent == null)
                {
                    return;
                }
                //await _serverRegister.CreateServerListener(instancesChangeEvent.ServiceName);
                //await _serverRegister.UpdateServer(instancesChangeEvent.ServiceName, instancesChangeEvent.Hosts);
            }
        }

        #endregion

    }
}
