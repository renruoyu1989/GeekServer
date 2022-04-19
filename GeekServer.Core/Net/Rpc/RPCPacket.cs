using System;

namespace Geek.Server
{

    /// <summary>
    /// 通过反射调用
    /// </summary>
    public class RPCPacket
    {
        /// <summary>
        /// 是否无需等待
        /// </summary>
        public bool NotAwait { get; set; }

        /// <summary>
        /// 名字 泛型参数数量 参数类型[]
        /// </summary>
        public string MethodName { get; set; }

        public Type CompAgent { get; set; }

        public object[] Args { get; set; }

        public Type[] GenericArgs { get; set; }
    }

}
