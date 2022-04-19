using Nacos.V2.Naming.Dtos;
using Newtonsoft.Json;
using NLog;
using NLog.Config;
using NLog.LayoutRenderers;
using System;
using System.Threading.Tasks;


namespace Geek.Server
{
    public class GameStartUp
    {
        static NLog.Logger LOGGER = NLog.LogManager.GetCurrentClassLogger();

        public static async Task Enter()
        {
            //开服时间设定
            try
            {
                var flag = Start();
                if (!flag) return; //启动服务器失败

                LOGGER.Info("regist components...");
                ComponentTools.RegistAll();

                LOGGER.Info("load hotfix module...");
                await HotfixMgr.ReloadModule("");

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


        private static bool Start()
        {
            try
            {
                Console.WriteLine("init NLog config...");
                LayoutRenderer.Register<NLogConfigurationLayoutRender>("logConfiguration");
                LogManager.Configuration = new XmlLoggingConfiguration("Config/NLog.config");
                LogManager.AutoShutdown = false;
                Settings.Load("Config/server_config.json", ServerType.Game);

                LOGGER.Warn("check restore data...");
                if (!FileBackUp.CheckRestoreFromFile())
                {
                    LOGGER.Error("check restore from file失败");
                    ExceptionMonitor.Report(ExceptionType.StartFailed, "check restore from file失败").Wait(TimeSpan.FromSeconds(10));
                    return false;
                }
                return true;
            }
            catch (Exception e)
            {
                LOGGER.Error($"启动服务器失败,异常:{e}");
                return false;
            }
        }

    }
}

