using System;
using System.Collections.Generic;
using System.Text;

namespace Geek.Server
{
    public static class SerializeTools
    {

        enum TestEnum
        {
            A,B,C
        }

        public static void Test()
        {
            TestEnum te = TestEnum.A;
            te.Read();

            List<TestEnum> list = new List<TestEnum>();
            list.Read();

            List<int> list2 = new List<int>();
            list2.Read();
        }


        public static void Read(this Enum self)
        {
            var a = (int)(object)self;
        }

        public static void Read<T>(this List<T> self) where T : struct, IConvertible
        {
            if (!typeof(T).IsEnum)
            {
                throw new ArgumentException("T must be an enumerated type");
            }
        }

        public static void Read(this List<int> self)
        {

        }

    }
}
