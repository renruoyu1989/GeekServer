using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Geek.Server.Logic.Test
{
    public class TestCompAgent : FuncComponentAgent<TestComp>
    {
        public Task Test(string name)
        {
            Console.WriteLine($"hello:{name}");
            return Task.CompletedTask;
        }
    }
}
