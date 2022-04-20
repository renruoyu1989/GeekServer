using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using Geek.Server;
using Newtonsoft.Json;

public enum ServerType
{
    None = 0,
    //网关服
    Gate = EntityType.GateInstance,
    //登录服
    Login = EntityType.LoginInstance,
    //游戏服
    Game = EntityType.GameInstnace,
    //中心服
    Center,
    //聊天服
    Chart,
    //充值服
    Recharge,
}

/// <summary>
/// 从Nacos获取的配置
/// </summary>
public class NacosSetting
{
    /// <summary>数据回存最大时间(秒)</summary>
    public int TimerSaveMax { get; set; }
    /// <summary>数据回存最小时间(秒)</summary>
    public int TimerSaveMin { get; set; }
    /// <summary>数据每秒操作上限</summary>
    public int DataFPS { get; set; }

    /// <summary>组件自动回收时间(分钟)</summary>
    public int CompRecycleTime { get; set; }
    /// <summary>actor自动回收时间(分钟)</summary>
    public int ActorRecycleTime { get; set; }
    /// <summary> 是否允许从文件中恢复 </summary>
    public bool RestoreFromFile { get; set; }

    /// <summary> mongoDB数据库名 </summary>
    public string MongoDB { get; set; }
    /// <summary> mongoDB登陆路径 </summary>
    public string MongoUrl { get; set; }
    /// <summary> 钉钉监控地址 </summary>
    public string MonitorUrl { get; set; }
    /// <summary> 钉钉监控key </summary>
    public string MonitorKey { get; set; }
  
    /// <summary> redis地址 </summary>
    public string RedisUrl { get; set; }
    /// <summary> http内部命名验证 </summary>
    public string HttpInnerCode { get; set; }
    /// <summary> http外部命令验证,可能提供给sdk方 </summary>
    public string HttpCode { get; set; }
    /// <summary> http指令路径 </summary>
    public string HttpUrl { get; set; }
    /// <summary> 使用libuv </summary>
    public bool UseLibuv { get; set; }

    /// <summary> 数据中心 </summary>
    public string DataCenter { get; set; }

    /// <summary>语言</summary>
    public string Language { get; set; }
}


public class Settings
{
    /// <summary>是否正常运行中(除开起服/关服准备)</summary>
    public volatile bool AppRunning;
    /// <summary>起服时间</summary>
    public DateTime StartServerTime { get; set; }
    /// <summary> 开发模式 </summary>
    public bool IsDebug { get; set; }
    /// <summary> 登录消息ID </summary>
    public int LoginMsgId { get; set; }

    /// <summary> 服务器id 从10000开始</summary>
    public int ServerId { get; set; }
    /// <summary> 服务器名</summary>
    public string ServerName { get; set; }

    /// <summary> 本机ip </summary>
    public string LocalIp { get; set; }
    /// <summary> tcp端口  </summary>
    public int TcpPort { get; set; }
    /// <summary> grpc端口  </summary>
    public int GrpcPort { get; set; }
    /// <summary> http端口 </summary>
    public int HttpPort { get; set; }


    /// <summary> Nacos地址 </summary>
    public string NacosUrl { get; set; }
    /// <summary> Nacos组 </summary>
    public string NacosGroup { get; set; }
    /// <summary> Nacos 通用配置ID </summary>
    public string NacosDataID { get; set; }

    /// <summary>
    /// 从Nacos获取的配置
    /// </summary>
    public NacosSetting Nacos { get; set; }
    #region Nacos Config
    /// <summary> mongoDB数据库名 </summary>
    public string MongoDB { get { return Nacos.MongoDB; } }
    /// <summary> mongoDB登陆路径 </summary>
    public string MongoUrl { get { return Nacos.MongoUrl; } }
    /// <summary>语言</summary>
    public string Language { get { return Nacos.Language; } }
    /// <summary> 钉钉监控地址 </summary>
    public string MonitorUrl { get { return Nacos.MonitorUrl; } }
    /// <summary> 钉钉监控key </summary>
    public string MonitorKey { get { return Nacos.MonitorKey; } }
    /// <summary> 数据中心 </summary>
    public string DataCenter { get { return Nacos.DataCenter; } }
    /// <summary> redis地址 </summary>
    public string RedisUrl { get { return Nacos.RedisUrl; } }


    /// <summary> http内部命名验证 </summary>
    public string HttpInnerCode { get { return Nacos.HttpInnerCode; } }
    /// <summary> http外部命令验证,可能提供给sdk方 </summary>
    public string HttpCode { get { return Nacos.HttpCode; } }
    /// <summary> http指令路径 </summary>
    public string HttpUrl { get { return Nacos.HttpUrl; } }
    /// <summary> 使用libuv </summary>
    public bool UseLibuv { get { return Nacos.UseLibuv; } }

    /// <summary>数据回存最大时间(秒)</summary>
    public int TimerSaveMax { get { return Nacos.TimerSaveMax; } }
    /// <summary>数据回存最小时间(秒)</summary>
    public int TimerSaveMin { get { return Nacos.TimerSaveMin; } }
    /// <summary>数据每秒操作上限</summary>
    public int DataFPS { get { return Nacos.DataFPS; } }

    /// <summary>组件自动回收时间(分钟)</summary>
    public int CompRecycleTime { get { return Nacos.CompRecycleTime; } }
    /// <summary>actor自动回收时间(分钟)</summary>
    public int ActorRecycleTime { get { return Nacos.ActorRecycleTime; } }

    /// <summary> 是否允许从文件中恢复 </summary>
    public bool RestoreFromFile { get { return Nacos.RestoreFromFile; } }

    #endregion


    static readonly NLog.Logger LOGGER = NLog.LogManager.GetCurrentClassLogger();
    public ServerType ServerType { get; private set; }
    public static Settings Ins { get; private set; }
    public static void Load(string configFilePath, ServerType serverType)
    {
        Ins = JsonConvert.DeserializeObject<Settings>(File.ReadAllText(configFilePath));
        Ins.ServerType = serverType;

#if DEBUG
        if (Ins.IsDebug)
        {
            string localIp = Dns
                .GetHostEntry(Dns.GetHostName())
                .AddressList
                .Where(address => address.AddressFamily == AddressFamily.InterNetwork)
                .FirstOrDefault()?
                .ToString();
            if (!string.IsNullOrEmpty(localIp))
            {
                int oldServerId = Ins.ServerId;
                var arr = localIp.Split('.');

                Ins.ServerId = int.Parse(arr[^1]) + (int.Parse(arr[^2]) % 10) * 1000 + Ins.TcpPort;//ip+端口
                LOGGER.Warn(string.Format("debug mode change serverId>{0}\nip:{1},port:{2},oldServerId:{3}", Ins.ServerId, localIp, Ins.TcpPort, oldServerId));
            }
            //Ins.Nacos.MongoDB += "_" + Ins.ServerId;
        }
#endif
    }
}
