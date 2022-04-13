using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Nacos.V2;
using Nacos.V2.DependencyInjection;
using Nacos.V2.Naming.Dtos;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
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
                x.ServerAddresses = new System.Collections.Generic.List<string> { Settings.Ins.NacosUrl };
                x.EndPoint = "";
                x.Namespace = "GeekServer";
                x.UserName = "nacos";
                x.Password = "nacos";
                // swich to use http or rpc
                x.ConfigUseRpc = true;
            });

            services.AddNacosV2Naming(x =>
            {
                x.ServerAddresses = new System.Collections.Generic.List<string> { Settings.Ins.NacosUrl };
                x.EndPoint = "";
                x.Namespace = "GeekServer";
                x.UserName = "nacos";
                x.Password = "nacos";
                // swich to use http or rpc
                x.NamingUseRpc = true;
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


        public async Task Subscribe(string configId, string group, Nacos.V2.IListener configListener=null)
        {
            if (configListener == null)
                configListener = new ConfigListener();
            await configSvc.AddListener(configId, group, configListener);
        }

        /// <summary>
        /// 默认监听器
        /// </summary>
        class ConfigListener : Nacos.V2.IListener
        {
            public void ReceiveConfigInfo(string configInfo)
            {
                if (string.IsNullOrEmpty(configInfo))
                    return;
                try
                {
                    LOGGER.Info("ConfigChanged:" + configInfo);
                    Settings.Ins.Nacos = JsonConvert.DeserializeObject<NacosSetting>(configInfo);
                }
                catch (Exception e)
                {
                    LOGGER.Error($"解析NacosConfig失败:{configInfo},{e}");
                }
            }
        }
        #endregion


        #region 注册发现
        public async Task RegisterInstance(string serviceName, string ip, int port)
        {
            await namingSvc.RegisterInstance(serviceName, Settings.Ins.NacosGroup, ip, port);
        }

        public  async Task DeregisterInstance(string serviceName, string ip, int port)
        {
            await namingSvc.DeregisterInstance(serviceName, Settings.Ins.NacosGroup, ip, port);
        }

        public  async Task Subscribe(string serviceName, Nacos.V2.IEventListener namingListerner)
        {
            await namingSvc.Subscribe(serviceName, Settings.Ins.NacosGroup, namingListerner);
        }

        public async Task<List<Instance>> GetAllInstances(string serviceName, bool subscribe=true)
        {
            return await namingSvc.GetAllInstances(serviceName, Settings.Ins.NacosGroup, subscribe);
        }
        #endregion

    }
}
