using IoTClient;
using IoTClient.Clients.PLC;
using IoTClient.Enums;
using Net462DllTest.Signal;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace Net462DllTest.Utils
{
    internal static class MyPlcExtension
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
        public static object ReadToPlcValue(this SiemensClient client,PlcVarInfo varInfo)
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

        public static void WriteToPlcValue(this SiemensClient client, PlcVarInfo varInfo ,object value)
        {
            if(client == null) throw new ArgumentNullException("client");
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
            if(!result.IsSucceed)
            {
                 throw new Exception(result.Err);
            }
        }

    }
}
