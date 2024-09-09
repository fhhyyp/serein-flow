using IoTClient.Clients.PLC;
using IoTClient.Common.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MyDll
{
    internal class IoTClientTest
    {
        private void T()
        {
            SiemensClient client = new SiemensClient(SiemensVersion.S7_200Smart, "127.0.0.1", 102);

            //2、写操作
            //client.Write("Q1.3", true);
            //client.Write("V2205", (short)11);
            //client.Write("V2209", 33);
            //client.Write("V2305", "orderCode");             //写入字符串

            //3、读操作
            var value1 = client.ReadBoolean("Q1.3").Value;
            var value2 = client.ReadInt16("V2205").Value;
            var value3 = client.ReadInt32("V2209").Value;
            var value4 = client.ReadString("V2305").Value; //读取字符串

            //4、如果没有主动Open，则会每次读写操作的时候自动打开自动和关闭连接，这样会使读写效率大大减低。所以建议手动Open和Close。
            client.Open();

            //5、读写操作都会返回操作结果对象Result
            var result = client.ReadInt16("V2205");
            //5.1 读取是否成功（true或false）
            var isSucceed = result.IsSucceed;
            //5.2 读取失败的异常信息
            var errMsg = result.Err;
            //5.3 读取操作实际发送的请求报文
            var requst = result.Requst;
            //5.4 读取操作服务端响应的报文
            var response = result.Response;
            //5.5 读取到的值
            var value = result.Value;
        }
    }
}
