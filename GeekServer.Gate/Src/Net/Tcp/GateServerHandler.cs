using System;
using System.Threading;
using DotNetty.Transport.Channels;

namespace Geek.Server
{
    public class GateServerHandler : SimpleChannelInboundHandler<GateMsg>
    {
        static readonly NLog.Logger LOGGER = NLog.LogManager.GetCurrentClassLogger();

        protected override void ChannelRead0(IChannelHandlerContext ctx, GateMsg msg)
        {
            if (!Settings.Ins.AppRunning)
                return;

            IEventLoop group = ctx.Channel.EventLoop;
            group.Execute(() =>
            {
                try
                {
                    msg.Channel = ctx.Channel;
                    _ = MsgDispatcher.Dispatch(msg);
                }
                catch (Exception e)
                {
                    LOGGER.Error(e.ToString());
                }
            });
        }

        public override void ChannelActive(IChannelHandlerContext context)
        {
            LOGGER.Info("{} 连接成功.", context.Channel.RemoteAddress.ToString());
        }

        public override async void ChannelInactive(IChannelHandlerContext ctx)
        {
            LOGGER.Info("{} 断开连接.", ctx.Channel.RemoteAddress.ToString());
            var session = ctx.Channel.GetAttribute(SessionManager.SESSION).Get();
            try
            {
                await SessionManager.Remove(session);
            }
            catch (Exception e)
            {
                LOGGER.Error(e.ToString());
            }
        }

        public override void ExceptionCaught(IChannelHandlerContext context, Exception exception)
        {
            LOGGER.Error(exception.ToString());
            context.CloseAsync();
        }
    }
}
