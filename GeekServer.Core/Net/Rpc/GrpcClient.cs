using Google.Protobuf;
using Grpc.Core;
using Newtonsoft.Json;
using NLog;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Geek.Server
{
    public class GrpcClient
    {

        private static readonly Logger LOGGER = LogManager.GetCurrentClassLogger();

        private static readonly ConcurrentDictionary<int, Channel> clientDic = new ConcurrentDictionary<int, Channel>();

        public static Task Showdown()
        {
            var taskList = new List<Task>();
            foreach (var kv in clientDic)
                taskList.Add(kv.Value.ShutdownAsync());
            return Task.WhenAll(taskList);
        }

        public static async Task<int> Invoke(ServerInfo serverInfo, long entityId, RPCPacket packet)
        {
            int serverId = serverInfo.ServerId;
            LOGGER.Debug($"发起grpc请求 目标serverId：{serverId} entityId：{entityId} CompAgent:{packet.CompAgent.Name} MethodName:{packet.MethodName}");
            var res = await Invoke<object>(serverInfo, entityId, packet);
            if(!GrpcRes.IsOK(res.Code))
                LOGGER.Error($"目标serverId：{serverId} entityId：{entityId} CompAgent:{packet.CompAgent.Name} MethodName:{packet.MethodName} 调用失败{res.Code}");
            return res.Code;
        }

        public static async Task<GrpcRes<T>> Invoke<T>(ServerInfo serverInfo, long entityId, RPCPacket packet)
        {
            int serverId = serverInfo.ServerId;
            LOGGER.Debug($"发起grpc请求 目标serverId：{serverId} entityId：{entityId} CompAgent:{packet.CompAgent.Name} MethodName:{packet.MethodName}");
            // 创建通道成本高昂。 重用 gRPC 调用的通道可提高性能。
            // 通道和从该通道创建的客户端可由多个线程安全使用。
            // 从通道创建的客户端可同时进行多个调用。
            if (!clientDic.TryGetValue(serverId, out var channel) || channel.State == ChannelState.Shutdown)
            {
                if (serverInfo == null)
                    return PackResult<T>(GrpcCode.TARGET_SERVER_CONFIG_NOT_FOUND);

                channel = new Channel($"{serverInfo.Ip}:{serverInfo.Port}", ChannelCredentials.Insecure);

                clientDic.TryRemove(serverId, out _);
                clientDic.TryAdd(serverId, channel);
            }

            // gRPC 客户端是使用通道创建的。 gRPC 客户端是轻型对象，无需缓存或重用。
            Inner.InnerClient client = new Inner.InnerClient(channel);

            //if (!await ServerInfoUtils.IsAlive(serverId))
            //    return PackResult<T>(GrpcCode.APP_NOT_RUNNING);

            var rpcMsg = new RpcMessage
            {
                EntityId = entityId,
                Data = ByteString.CopyFrom(RpcFormatter.Serialize(packet)),
                ServerId = Settings.Ins.ServerId
            };

            RpcReply reply;
            try
            {
                reply = await client.InvokeAsync(rpcMsg, deadline: DateTime.UtcNow.AddSeconds(400));
            }
            catch (RpcException e)
            {
                if (e.StatusCode == StatusCode.Unavailable)
                {
                    try
                    {
                        await Task.Delay(50);
                        reply = await client.InvokeAsync(rpcMsg, deadline: DateTime.UtcNow.AddSeconds(4));
                    }
                    catch (RpcException e1)
                    {
                        LOGGER.Error($"grpc调用异常 重发无效 {serverId} entityId：{entityId} CompAgent:{packet.CompAgent.Name} MethodName:{packet.MethodName} e:{e1.Message}");
                        return PackResult<T>(GrpcCode.GRPC_CALL_EXCEPTION);
                    }
                }
                else
                {
                    LOGGER.Error($"grpc调用异常 {serverId} entityId：{entityId} CompAgent:{packet.CompAgent.Name} MethodName:{packet.MethodName} e:{e.Message}");
                    return PackResult<T>(GrpcCode.GRPC_CALL_EXCEPTION);
                }
            }

            GrpcCode code =(GrpcCode) reply.Code;
            if (reply.Data == null)
                return PackResult<T>(code);

            T res = RpcFormatter.Deserialize<T>(reply.Data.ToByteArray());
            return PackResult<T>(code, res);
        }

        private static GrpcRes<T> PackResult<T>(GrpcCode code, T msg)
        {
            return new GrpcRes<T>((int)code, msg);
        }

        private static GrpcRes<T> PackResult<T>(GrpcCode code)
        {
            return new GrpcRes<T>((int)code, null);
        }
    }
}
