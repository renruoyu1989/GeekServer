using System;
using System.Threading.Tasks;


namespace Geek.Server
{
    public class GameLoop
    {
        static NLog.Logger LOGGER = NLog.LogManager.GetCurrentClassLogger();

        public static async Task Enter()
        {
            //开服时间设定
            try
            {

                await Start();
                Settings.Ins.StartServerTime = DateTime.Now;

                LOGGER.Warn("enter game loop...");
                Console.WriteLine("enter game loop...");

                int gcTime = 0;
                Settings.Ins.AppRunning = true;
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


        private static async Task Start()
        {
            await TcpServer.Start(Settings.Ins.TcpPort, Settings.Ins.UseLibuv);
            await HttpServer.Start(Settings.Ins.httpPort);
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

