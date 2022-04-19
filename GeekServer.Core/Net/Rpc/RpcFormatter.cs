using Newtonsoft.Json;

namespace Geek.Server
{

    /// <summary>
    /// 暂时只支持JSON
    /// </summary>
    public class RpcFormatter
    {
        public enum Format
        {
            JSON,
            MESSAGE_PACK
        }

        public static Format Formatter = Format.JSON;

        public static byte[] Serialize(object obj)
        {
            var json = JsonConvert.SerializeObject(obj);
            return System.Text.Encoding.UTF8.GetBytes(json);
        }

        public static object Deserialize(byte[] data)
        {
            string json = System.Text.Encoding.UTF8.GetString(data);
            return JsonConvert.DeserializeObject<RPCPacket>(json);
        }

        public static T Deserialize<T>(byte[] data)
        {
            string json = System.Text.Encoding.UTF8.GetString(data);
            return JsonConvert.DeserializeObject<T>(json);
        }

    }
}
