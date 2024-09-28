using IoTClient.Clients.PLC;
using Net461DllTest.Enums;
using Net461DllTest.Signal;
using Net461DllTest.Utils;
using Serein.Library.NodeFlow.Tool;
using System;

namespace Net461DllTest.Device
{

    /// <summary>
    /// 官方文档：如果没有主动Open，则会每次读写操作的时候自动打开自动和关闭连接，这样会使读写效率大大减低。所以建议手动Open和Close。
    /// </summary>
    public class SiemensPlcDevice : ChannelFlowTrigger<OrderSignal>
    {
        public SiemensClient Client { get; set; }

        public IoTClient.Common.Enums.SiemensVersion Version { get; set; }
        public string IP { get; set; }
        public int Port { get; set; }
        public PlcState State { get; set; } = PlcState.PowerOff;


        public void Init(IoTClient.Common.Enums.SiemensVersion version,string ip, int port)
        {
            Client = new SiemensClient(version, ip, port);
            Version = version;
            IP = ip;
            Port = port;
        }

        public void ResetDevice()
        {
            Client?.Close();
            Client = null;
        }

        public void Write(PlcVarInfo plcValue, object value)
        {
            try
            {
                Client.WriteToPlcValue(plcValue, value);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"写入出错:{this}{plcValue}。{ex.Message}");
                throw;
            }
        }
        public object Read(PlcVarInfo plcValue)
        {
            try
            {
                return Client.ReadToPlcValue(plcValue);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"读取出错:{this}{plcValue}。{ex.Message}");
                throw;
            }
            
        }

        public override string ToString()
        {
            return $"西门子Plc[{this.Version}-{this.IP}:{this.Port}]";
        }
    }

}
