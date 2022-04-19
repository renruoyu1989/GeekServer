using Nacos.V2;
using Nacos.V2.Naming.Dtos;
using Nacos.V2.Naming.Event;
using Nacos.V2.Utils;
using Newtonsoft.Json;
using NLog;
using NLog.Config;
using NLog.LayoutRenderers;
using System;
using System.Threading.Tasks;


namespace Geek.Server
{

    public class LoginStartUp
    {
        static NLog.Logger LOGGER = NLog.LogManager.GetCurrentClassLogger();

        public static async Task Enter()
        {
            //开服时间设定
            try
            {
                var flag = await Start();
                if (!flag) return; //启动服务器失败

                Settings.Ins.StartServerTime = DateTime.Now;
                Settings.Ins.AppRunning = true;
                LOGGER.Warn("enter game loop...");
                Console.WriteLine("enter game loop...");

                int gcTime = 0;
                var random = new Random(Settings.Ins.ServerId + DateTime.Now.Millisecond);
                var gcGap = random.Next(600, 1000);//gc间隔随机
                while (Settings.Ins.AppRunning)
                {
                    //gc
                    gcTime += 1;
                    if (gcTime > gcGap)
                    {
                        gcTime = 0;
                        GC.Collect();
                    }
                    await Task.Delay(TimeSpan.FromSeconds(1));
                }
            }
            catch (Exception e)
            {
                Console.WriteLine("执行 Exception");
                LOGGER.Fatal(e.ToString());
            }

            Console.WriteLine("退出服务器，所有actor下线处理");
            LOGGER.Info("退出服务器，所有actor下线处理");
            Console.WriteLine("下线处理完毕");
            LOGGER.Info("下线处理完毕");
        }


        private static async Task<bool> Start()
        {
            try
            {
                Console.WriteLine("init NLog config...");
                LayoutRenderer.Register<NLogConfigurationLayoutRender>("logConfiguration");
                LogManager.Configuration = new XmlLoggingConfiguration("Config/NLog.config");
                LogManager.AutoShutdown = false;
                Settings.Load("Config/server_config.json", ServerType.Login);

                LOGGER.Warn("check restore data...");
                if (!FileBackUp.CheckRestoreFromFile())
                {
                    LOGGER.Error("check restore from file失败");
                    ExceptionMonitor.Report(ExceptionType.StartFailed, "check restore from file失败").Wait(TimeSpan.FromSeconds(10));
                    return false;
                }

                //拉取通用配置
                var nacosConfig = await NacosClient.Singleton.Get(Settings.Ins.NacosDataID);
                if (string.IsNullOrEmpty(nacosConfig))
                {
                    LOGGER.Error($"无法从Nacos服务器获取配置,DataId:{Settings.Ins.NacosDataID},Group:{Settings.Ins.NacosGroup}");
                    return false;
                }
                Settings.Ins.Nacos = JsonConvert.DeserializeObject<NacosSetting>(nacosConfig);

                //后面登录服务器考虑使用Http
                //await HttpServer.Start(Settings.Ins.HttpPort);

                //上报注册中心
                var config = await NacosClient.Singleton.GetMutableConfig();
                //上报注册中心
                Instance ins = new Instance
                {
                    Ip = Settings.Ins.LocalIp,
                    Port = config.GrpcPort,
                    InstanceId = config.ServerId.ToString()
                };
                await NacosClient.Singleton.RegisterInstance(ServiceManager.Login_Service, ins);

                //监听配置变化
                await NacosClient.Singleton.Subscribe(Settings.Ins.NacosDataID, Settings.Ins.NacosGroup, new ConfigListener());

                LOGGER.Info("regist components...");
                ComponentTools.RegistAll();

                //激活实列Entity
                await EntityMgr.GetCompAgent<GameInsCompAgent>(EntityType.Login);

                return true;
            }
            catch (Exception e)
            {
                LOGGER.Error($"启动服务器失败,异常:{e}");
                return false;
            }
        }

        class ConfigListener : Nacos.V2.IListener
        {
            public void ReceiveConfigInfo(string configInfo)
            {
                LOGGER.Error("ConfigChanged:" + configInfo);
                Settings.Ins.Nacos = JsonConvert.DeserializeObject<NacosSetting>(configInfo);
            }
        }

        /// <summary>
        /// 消息ID规则
        /// 判断消息类型：登录，游戏，聊天。。？？
        /// </summary>
        /// <param name="msgId"></param>
        /// <returns></returns>
        private static MsgType IDRule(int msgId)
        {
            return MsgType.Login;
        }

    }
}

