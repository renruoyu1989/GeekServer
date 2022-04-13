using System;
using NLog.Config;
using System.Diagnostics;
using System.Threading.Tasks;
using NLog;
using NLog.LayoutRenderers;

namespace Geek.Server
{
    class Program
    {
        static readonly NLog.Logger LOGGER = NLog.LogManager.GetCurrentClassLogger();

        volatile static Task GameloopTask;
        volatile static Task ShutDownTask;

        static void Main(string[] args)
        {
            try
            {
                AppExitHandler.Init(HandleExit);
                GameloopTask = GateStartUp.Enter();
                GameloopTask.Wait();

                if (ShutDownTask != null)
                    ShutDownTask.Wait();
            }
            catch (Exception e)
            {
                if (Settings.Ins.AppRunning)
                    ExceptionMonitor.Report(ExceptionType.UnhandledException, $"{e}").Wait(TimeSpan.FromSeconds(10));
                else
                    ExceptionMonitor.Report(ExceptionType.StartFailed, $"{e}").Wait(TimeSpan.FromSeconds(10));
            }
        }

        private static bool IsExitCalled = false;

        private static void HandleExit()
        {
            if (IsExitCalled)
                return;
            IsExitCalled = true;
            LOGGER.Info("监听到退出程序消息");
            ShutDownTask = Task.Run(() =>
            {
                Settings.Ins.AppRunning = false;
                if (GameloopTask != null)
                    GameloopTask.Wait();

                LogManager.Shutdown();
                Console.WriteLine("退出程序");
                Process.GetCurrentProcess().Kill();
            });
            ShutDownTask.Wait();
        }
    }
}