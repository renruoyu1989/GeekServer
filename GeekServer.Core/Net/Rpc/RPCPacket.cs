using BeetleX.Buffers;
using Newtonsoft.Json;
using System;
using System.Text;

namespace Geek.Server
{
    public class User
    {
        public string Name { get; set; }
        public int Age { get; set; }

        public User(string n, int a)
        {
            Name = n;
            Age = a;
        }
    }

    public class Arg
    {
        public Type ArgType { get; set; }
        public object Value { get; set; }
    }

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

        public Type[] GenericArgs { get; set; }

        public Type[] ArgTypes { get; set; }

        public object[] Args { get; set; }


        public void EncodeArg(object[] args)
        {
            for (int i = 0; i < args.Length; i++)
            {
                Arg arg = new Arg();
                arg.ArgType = args[i].GetType();
                arg.Value = args[i];
            }
        }

        public void DecodeArgs()
        {
            var temp = new object[Args.Length];
            for (int i = 0; i < Args.Length; i++)
            {
                temp[i] = JsonConvert.DeserializeObject("\"" + Args[i].ToString() + "\"", ArgTypes[i]);
            }
            Args = temp;
        }


    }

}
