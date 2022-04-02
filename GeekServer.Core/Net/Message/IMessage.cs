namespace Geek.Server
{


    public enum MsgType
    {
        Login,
        Game,
        Chart,
        Recharge,
        Center,
    }

    public interface IMessage
    {
        /// <summary>
        /// 每次请求的UniqueId
        /// </summary>
        int UniId { get; set; }

        int MsgId { get; }

        byte[] Serialize();

        void Deserialize(byte[] data);
    }
}