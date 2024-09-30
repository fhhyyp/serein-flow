using IoTClient;
using IoTClient.Clients.PLC;
using IoTClient.Enums;
using Net461DllTest.Enums;
using Net461DllTest.Signal;
using Serein.Library.Attributes;
using Serein.Library.NodeFlow.Tool;
using System;

namespace Net461DllTest.Device
{

    /// <summary>
    /// 官方文档：如果没有主动Open，则会每次读写操作的时候自动打开自动和关闭连接，这样会使读写效率大大减低。所以建议手动Open和Close。
    /// </summary>
    [AutoRegister]
    public class SiemensPlcDevice : ChannelFlowTrigger<CommandSignal>
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

    /// <summary>
    /// PLC方法
    /// </summary>
    public static class PlcExtension
    {
        public static DataTypeEnum ToDataTypeEnum(this PlcVarInfo varInfo)
        {
            Type dataType = varInfo.DataType;
            DataTypeEnum plcDataType;
            switch (dataType)
            {
                case Type _ when dataType == typeof(string):
                    plcDataType = DataTypeEnum.String;
                    break;
                case Type _ when dataType == typeof(char):
                    plcDataType = DataTypeEnum.String;
                    break;
                case Type _ when dataType == typeof(bool):
                    plcDataType = DataTypeEnum.Bool;
                    break;
                case Type _ when dataType == typeof(float):
                    plcDataType = DataTypeEnum.Float;
                    break;
                case Type _ when dataType == typeof(double):
                    plcDataType = DataTypeEnum.Double;
                    break;
                case Type _ when dataType == typeof(byte):
                    plcDataType = DataTypeEnum.Byte;
                    break;
                case Type _ when dataType == typeof(short):
                    plcDataType = DataTypeEnum.Int16;
                    break;
                case Type _ when dataType == typeof(ushort):
                    plcDataType = DataTypeEnum.UInt16;
                    break;
                case Type _ when dataType == typeof(int):
                    plcDataType = DataTypeEnum.Int32;
                    break;
                case Type _ when dataType == typeof(uint):
                    plcDataType = DataTypeEnum.UInt32;
                    break;
                case Type _ when dataType == typeof(long):
                    plcDataType = DataTypeEnum.Int64;
                    break;
                case Type _ when dataType == typeof(ulong):
                    plcDataType = DataTypeEnum.UInt64;
                    break;
                default:
                    plcDataType = DataTypeEnum.None;
                    break;
            }


            return plcDataType;
        }

        /// <summary>
        /// 读取设备的值
        /// </summary>
        /// <param name="client"></param>
        /// <param name="varInfo"></param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        public static object ReadToPlcValue(this SiemensClient client, PlcVarInfo varInfo)
        {
            Type dataType = varInfo.DataType;
            object resultvalue;
            if (dataType == typeof(string))
            {
                var result = client.ReadString(varInfo.VarAddress);
                if (!result.IsSucceed) throw new Exception(result.Err);
                resultvalue = result.Value;
            }
            else if (dataType == typeof(char))
            {
                var result = client.ReadString(varInfo.VarAddress);
                if (!result.IsSucceed) throw new Exception(result.Err);
                resultvalue = result.Value;
            }
            else if (dataType == typeof(bool))
            {
                var result = client.ReadBoolean(varInfo.VarAddress);
                if (!result.IsSucceed) throw new Exception(result.Err);
                resultvalue = result.Value;
            }
            else if (dataType == typeof(float))
            {
                var result = client.ReadFloat(varInfo.VarAddress);
                if (!result.IsSucceed) throw new Exception(result.Err);
                resultvalue = result.Value;
            }
            else if (dataType == typeof(double))
            {
                var result = client.ReadDouble(varInfo.VarAddress);
                if (!result.IsSucceed) throw new Exception(result.Err);
                resultvalue = result.Value;
            }
            else if (dataType == typeof(byte))
            {
                var result = client.ReadByte(varInfo.VarAddress);
                if (!result.IsSucceed) throw new Exception(result.Err);
                resultvalue = result.Value;
            }
            else if (dataType == typeof(short))
            {
                var result = client.ReadInt16(varInfo.VarAddress);
                if (!result.IsSucceed) throw new Exception(result.Err);
                resultvalue = result.Value;
            }
            else if (dataType == typeof(ushort))
            {
                var result = client.ReadUInt16(varInfo.VarAddress);
                if (!result.IsSucceed) throw new Exception(result.Err);
                resultvalue = result.Value;
            }
            else if (dataType == typeof(int))
            {
                var result = client.ReadInt32(varInfo.VarAddress);
                if (!result.IsSucceed) throw new Exception(result.Err);
                resultvalue = result.Value;
            }
            else if (dataType == typeof(uint))
            {
                var result = client.ReadUInt32(varInfo.VarAddress);
                if (!result.IsSucceed) throw new Exception(result.Err);
                resultvalue = result.Value;
            }
            else if (dataType == typeof(long))
            {
                var result = client.ReadInt64(varInfo.VarAddress);
                if (!result.IsSucceed) throw new Exception(result.Err);
                resultvalue = result.Value;
            }
            else if (dataType == typeof(ulong))
            {
                var result = client.ReadUInt64(varInfo.VarAddress);
                if (!result.IsSucceed) throw new Exception(result.Err);
                resultvalue = result.Value;
            }
            else
            {
                resultvalue = default;
            }
            return resultvalue;
        }

        public static void WriteToPlcValue(this SiemensClient client, PlcVarInfo varInfo, object value)
        {
            if (client == null) throw new ArgumentNullException("client");
            Type dataType = varInfo.DataType;
            Result result = null;
            if (dataType == typeof(string))
            {
                result = client.Write(varInfo.VarAddress, value.ToString());
            }
            else if (dataType == typeof(char))
            {
                result = client.Write(varInfo.VarAddress, value.ToString());
            }
            else if (dataType == typeof(bool))
            {
                var @bool = bool.Parse(value.ToString());
                result = client.Write(varInfo.VarAddress, @bool);
            }
            else if (dataType == typeof(float))
            {
                var @float = float.Parse(value.ToString());
                result = client.Write(varInfo.VarAddress, @float);
            }
            else if (dataType == typeof(double))
            {
                var @double = double.Parse(value.ToString());
                result = client.Write(varInfo.VarAddress, @double);
            }
            else if (dataType == typeof(byte))
            {
                var @byte = byte.Parse(value.ToString());
                result = client.Write(varInfo.VarAddress, @byte);
            }
            else if (dataType == typeof(short))
            {
                var @short = short.Parse(value.ToString());
                result = client.Write(varInfo.VarAddress, @short);
            }
            else if (dataType == typeof(ushort))
            {
                var @ushort = ushort.Parse(value.ToString());
                result = client.Write(varInfo.VarAddress, @ushort);
            }
            else if (dataType == typeof(int))
            {
                var @int = int.Parse(value.ToString());
                result = client.Write(varInfo.VarAddress, @int);
            }
            else if (dataType == typeof(uint))
            {
                var @uint = uint.Parse(value.ToString());
                result = client.Write(varInfo.VarAddress, @uint);
            }
            else if (dataType == typeof(long))
            {
                var @long = long.Parse(value.ToString());
                result = client.Write(varInfo.VarAddress, @long);
            }
            else if (dataType == typeof(ulong))
            {
                var @ulong = ulong.Parse(value.ToString());
                result = client.Write(varInfo.VarAddress, @ulong);
            }
            if (result is null)
            {
                throw new Exception($"未定义的数据类型");
            }
            if (!result.IsSucceed)
            {
                throw new Exception(result.Err);
            }
        }

    }
}
