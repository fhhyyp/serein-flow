using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Serein.Proto.WebSocket
{

    public class ClassA : ISocketHandleModule
    {

    }
    public class ClassB : ISocketHandleModule
    {

    }
    public class ClassC : ISocketHandleModule
    {

    }
    internal class TestClass
    {
        public void Run()
        {
            SereinWebSocketService sereinWebSocketService = new SereinWebSocketService();
            sereinWebSocketService.AddHandleModule<ClassA>();
            sereinWebSocketService.AddHandleModule<ClassB>(() => new ClassB());
        }

       
    }
}
