using Net461DllTest.Data;
using Net461DllTest.Signal;
using Serein.Library.Attributes;
using Serein.Library.NodeFlow.Tool;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Net461DllTest.Device
{


    public class PlcDevice : ChannelFlowTrigger<OrderSignal>
    {
        [AutoInjection]
        public MyData MyData { get; set; }

        public PlcDevice()
        {
            PlcId = 114514 + 10000000 * new Random().Next(1, 9);
        }
        public int PlcId { get; set; }

        public void InitDevice(string ip, int port, string tips)
        {
            Write($"模拟设备初始化 :{Environment.NewLine}" +
                 $" ip :{ip}{Environment.NewLine}" +
                 $"port:{port}{Environment.NewLine}" +
                 $"tips:{tips}{Environment.NewLine}");
        }

        public void Write<T>(T value)
        {
            Console.WriteLine($"{value}");
        }
        public void Read<T>()
        {
            Console.WriteLine($"读取数据：... ");
        }
        public void Disconnect()
        {
            Console.WriteLine($"断开连接...");
        }
    }

}
