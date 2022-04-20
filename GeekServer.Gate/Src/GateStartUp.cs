using Nacos.V2;
using Nacos.V2.Naming.Dtos;
using Nacos.V2.Naming.Event;
using Nacos.V2.Utils;
using Newtonsoft.Json;
using NLog;
using NLog.Config;
using NLog.LayoutRenderers;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;


namespace Geek.Server
{
    public class GateStartUp
    {
        static NLog.Logger LOGGER = NLog.LogManager.GetCurrentClassLogger();


        public static async Task Enter()
        {
            //object[] objs = new object[2];
            //objs[0] = 1000;
            //objs[1] = new byte[] { 1, 2, 3, 4 };
            //var json = JsonConvert.SerializeObject(new byte[] { 1, 2, 3, 4 });

            //var b = JsonConvert.DeserializeObject<byte[]>(json);

            //var json1 = JsonConvert.SerializeObject(objs);
            //var objs2 = JsonConvert.DeserializeObject<object[]>(json1);

            ////var b2 = JsonConvert.DeserializeObject<byte[]>("\"" + objs2[1].ToString() + "\"");
            //var b2 = JsonConvert.DeserializeObject<byte[]>(objs2[1].ToString());


            //Console.Read();

            //开服时间设定
            try
            {
                var flag = await Start();
                if (!flag) return; //启动服务器失败

                RegistComps();

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
                Settings.Load("Config/server_config.json", ServerType.Gate);

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
                //设置登录消息ID
                Settings.Ins.LoginMsgId = 111101;

                //上报注册中心
                var config = await NacosClient.Singleton.GetMutableConfig();
                Instance ins = new Instance
                {
                    Ip = Settings.Ins.LocalIp,
                    Port = config.GrpcPort,
                    InstanceId = config.ServerId.ToString()
                };
                //开启TCP服务
                var handlerList = new List<Type>
                {
                    typeof(GateServerEncoder),
                    typeof(GateServerDecoder),
                    typeof(GateServerHandler)
                };
                await TcpServer.Start(Settings.Ins.TcpPort, Settings.Ins.UseLibuv, handlerList);
                await NacosClient.Singleton.RegisterInstance(EntityType.GateInstance.ToString(), ins);

                //监听配置变化
                await NacosClient.Singleton.Subscribe(Settings.Ins.NacosDataID, Settings.Ins.NacosGroup);
                //监听各种服务(Login,Game,Chart.....)
                await ServiceManager.Singleton.Subscribe(EntityType.LoginInstance.ToString());
                await ServiceManager.Singleton.Subscribe(EntityType.GameInstnace.ToString());
                return true;
            }
            catch (Exception e)
            {
                LOGGER.Error($"启动服务器失败,异常:{e}");
                return false;
            }
        }


        public static void RegistComps()
        {
            CompSetting.Singleton.RegistComp<LoginInsComp>((int)EntityType.LoginInstance, true);
            CompSetting.Singleton.RegistComp<GameInsComp>((int)EntityType.GameInstnace, true);
        }

    }
}

