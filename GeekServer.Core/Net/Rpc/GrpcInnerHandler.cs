using Google.Protobuf;
using Grpc.Core;
using NLog;
using System;
using System.Threading.Tasks;

namespace Geek.Server
{
    /// <summary>
    /// grpc最初处理消息的类
    /// </summary>
    public class GrpcInnerHandler : Inner.InnerBase
    {

        private static readonly Logger LOGGER = LogManager.GetCurrentClassLogger();

        public override async Task<RpcReply> Invoke(RpcMessage request, ServerCallContext context)
        {
            if (!Settings.Ins.AppRunning)
                return new RpcReply { Code = (int)GrpcCode.APP_NOT_RUNNING };

            //解析参数
            var packet = RpcFormatter.Deserialize<RPCPacket>(request.Data.ToByteArray());
            LOGGER.Debug($"收到Grpc请求 目标serverId：{request.ServerId} entityId：{request.EntityId} CompAgent:{packet.CompAgent.Name} MethodName:{packet.MethodName}");
            GrpcRes res;
            try
            {
                res = await ReflectionCall(request.EntityId, packet);
            }
            catch (Exception e)
            {
                LOGGER.Error($"grpc handler调用异常 目标serverId：{request.ServerId} entityId：{request.EntityId} CompAgent:{packet.CompAgent.Name} MethodName:{packet.MethodName} {e}");
                return ErrorMsg(GrpcCode.HANDLER_EXCEPTION);
            }

            LOGGER.Debug($"处理完成Grpc请求 目标serverId：{request.ServerId} entityId：{request.EntityId} CompAgent:{packet.CompAgent.Name} MethodName:{packet.MethodName}");

            var reply = new RpcReply { Code = res.Code };
            if (res.Res != null)
            {
                reply.Data = ByteString.CopyFrom(RpcFormatter.Serialize(res.Res));
            }
            return reply;
        }



        private Task<GrpcRes> CallComp(long entityId, RPCPacket packet)
        {
            int reqMsgId = 0;
            GrpcBaseHandler handler = HotfixMgr.GetHandler<GrpcBaseHandler>(reqMsgId);
            //if (handler == null)
            //    return ErrorMsg(GrpcCode.HANDLER_NOT_FOUND);
            //msg.Deserialize(request.Data.ToByteArray());
            //handler.Msg = msg;
            //handler.EntityId = request.ActorId;
            //handler.ServerId = request.ServerId;




            return default;
        }


        public async Task<GrpcRes> ReflectionCall(long entityId, RPCPacket packet)
        {
            var comp = await EntityMgr.GetCompAgent(entityId, packet.CompAgent);
            if (comp == null)
            {
                LOGGER.Error($"目标服务器找不到entityId：{entityId} CompAgent：{packet.CompAgent.Name}");
                throw new Exception($"目标服务器找不到Comp：{entityId}");
            }
            //入队调用
            return await comp.Owner.Actor.SendAsync(async () =>
            {
                var methodInfo = comp.GetType().GetMethod(packet.MethodName);
                //泛型函数
                if (methodInfo.IsGenericMethod)
                    methodInfo = methodInfo.MakeGenericMethod(packet.GenericArgs);
                var task = (Task)methodInfo.Invoke(comp, packet.Args);
                await task;
                //Agent上的函数只有可能是Task或Task<T>
                if (methodInfo.ReturnType.Equals(typeof(Task)))
                {
                    return GrpcRes.Create(null);
                }
                else
                {
                    var resultProperty = task.GetType().GetProperty("Result");
                    var val = resultProperty.GetValue(task);
                    return GrpcRes.Create(val);
                }
            });
        }

        private static RpcReply ErrorMsg(GrpcCode code)
        {
            return new RpcReply { Code = (int)code };
        }

    }
}
