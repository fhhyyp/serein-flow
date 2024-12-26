using IoTClient;
using IoTClient.Clients.PLC;
using IoTClient.Common.Enums;
using IoTClient.Enums;
using Net462DllTest.Enums;
using Net462DllTest.Model;
using Net462DllTest.Signal;
using Net462DllTest.Utils;
using Serein.Library;
using Serein.Library.Utils;
using Serein.Library.Utils.FlowTrigger;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;

namespace Net462DllTest.Trigger
{


    [AutoRegister]
    public class SiemensPlcDevice : TaskFlowTrigger<PlcVarName>
    {
        public SiemensClient Client { get; set; }
        public SiemensVersion Version { get; set; }
        public string IP { get; set; } = "127.0.0.1";
        public int Port { get; set; } = 102;
        public PlcState State { get; set; } = PlcState.PowerOff;
        public bool IsTimedRefresh { get; set; } = false; // 是否定时刷新
        public IGSModel<PlcVarName, PlcVarModel> Model { get; }


        private readonly object _lockObj = new object(); // 防止多次初始化读取任务
        private readonly ConcurrentBag<Task> TimedRefreshTask = new ConcurrentBag<Task>(); // 定时读取任务
        private readonly ConcurrentBag<PlcVarInfo> VarInfos = new ConcurrentBag<PlcVarInfo>(); // 所有变量信息
        private readonly ConcurrentBag<PlcVarInfo> OnRefreshs = new ConcurrentBag<PlcVarInfo>(); // 数据变更后需要通知触发器的变量信息
        private readonly ConcurrentBag<PlcVarInfo> OnChangeds = new ConcurrentBag<PlcVarInfo>(); // 读取读取后需要通知触发器的变量信息
        
        public SiemensPlcDevice(PlcVarModel model)
        {
            this.Model = new GSModel<PlcVarName, PlcVarModel>(model);
            LoadVarInfos();
        }

        /// <summary>
        /// 加载变量信息
        /// </summary>
        private void LoadVarInfos()
        {
            foreach (var property in typeof(PlcVarModel).GetProperties())
            {
                var attribute = property.GetCustomAttribute<BindValueAttribute>();
                if (attribute?.Value is PlcVarName varName)
                {
                    var varInfo = varName.ToVarInfo();
                    VarInfos.Add(varInfo);
                    if (varInfo.IsTimingRead) // 是否定时刷新
                    {
                        switch (varInfo.NotificationType)
                        {
                            case PlcVarInfo.OnNotificationType.OnRefresh:
                                OnRefreshs.Add(varInfo); // 变量读取时通知触发器的变量
                                break;
                            case PlcVarInfo.OnNotificationType.OnChanged:
                                OnChangeds.Add(varInfo); // 变量变更时才需要通知触发器的变量
                                break;
                        }
                    }
                }
            }
        }

        public void Init(SiemensVersion version, string ip, int port)
        {
            Client = new SiemensClient(version, ip, port);
            Version = version;
            IP = ip;
            Port = port;
            Client.Open();
        }

        public void Close()
        {
            Client?.Close();
            Client = null;
        }

        public void Write(PlcVarName varName, object value)
        {
            var varInfo = varName.ToVarInfo();
            if (this.State == PlcState.Runing)
            {
                if (varInfo.IsReadOnly)
                {
                    throw new Exception($"PLC变量{varInfo}当前禁止写入");
                }
                else
                {

                    Client.WriteVar(varInfo, value); // 尝试写入PLC
                    Model.Set(varName, value);
                    Console.WriteLine($"PLC变量{varInfo}写入数据：{value}");
                }
            }
            else
            {
                throw new Exception($"PLC处于非预期状态{this.State}");
            }
        }

        public object Read(PlcVarName varName)
        {
            var varInfo = varName.ToVarInfo();
            var result = Client.ReadVar(varInfo);// 尝试读取数据
            Model.Set(varName, result); // 缓存读取的数据
            return result;

        }

        public void BatchRefresh()
        {
            foreach(var varInfo in VarInfos)
            {
                Read(varInfo.Name); // 无条件批量读取
            }
        }


        /// <summary>
        /// 开启定时批量读取任务
        /// </summary>
        /// <returns></returns>
        public async Task OpenTimedRefreshAsync()
        {
            if (TimedRefreshTask.IsEmpty)
            {
                InitTimedRefreshTask();
            }
            IsTimedRefresh = true;
            await Task.WhenAll(TimedRefreshTask.ToArray());
        }

        /// <summary>
        /// 关闭定时任务
        /// </summary>
        public void CloseTimedRefresh() => IsTimedRefresh = false;

        /// <summary>
        /// 初始化定时刷新任务
        /// </summary>
        public void InitTimedRefreshTask()
        {
            Console.WriteLine("开始初始化刷新任务");
            lock (_lockObj)
            {
                foreach (var varInfo in OnChangeds)
                {
                    Console.WriteLine($"添加监视任务(OnChanged)：{varInfo.Name}");
                    ScheduleTask(varInfo);
                }
                foreach (var varInfo in OnRefreshs)
                {
                    Console.WriteLine($"添加监视任务(OnRefresh)：{varInfo.Name}");
                    ScheduleTask(varInfo);
                }
            }
        }

        /// <summary>
        /// 定时读取PLC变量，并刷新Model的值
        /// </summary>
        /// <param name="varInfo"></param>
        private void ScheduleTask(PlcVarInfo varInfo)
        {
            var task = Task.Run(async () =>
            {
                var signal = varInfo.Name;
                object oldData;
                object newData;
                bool isNotification;
                while (true)
                {
                    await Task.Delay(varInfo.Interval);
                    if (!IsTimedRefresh || Client is null) break; 
                    
                    oldData = Model.Get(signal); // 暂存旧数据
                    newData = Read(signal); // 获取新数据
                    if (varInfo.NotificationType == PlcVarInfo.OnNotificationType.OnRefresh)
                    { 
                        isNotification = true; // 无条件触发通知
                    }
                    else
                    {
                        isNotification = !oldData.Equals(newData); // 变更时才会触发通知
                    }

                    if (isNotification)
                    {
                        Console.WriteLine($"VarName: {signal}\t\tOld Data: {oldData}\tNew Data: {newData}");
                        await InvokeTriggerAsync(signal, newData);
                    }
                  
                    
                }
            });
            TimedRefreshTask.Add(task);
        }

        public override string ToString()
        {
            return $"西门子Plc[{this.Version}-{this.IP}:{this.Port}]";
        }

    }




    /// <summary>
    /// 拓展方法
    /// </summary>
    public static class MyPlcExtension
    {
        /// <summary>
        /// 缓存变量信息
        /// </summary>
        private static readonly Dictionary<PlcVarName, PlcVarInfo> VarInfoDict = new Dictionary<PlcVarName, PlcVarInfo>();

        /// <summary>
        /// 获取变量信息
        /// </summary>
        /// <param name="plcVarEnum"></param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        public static PlcVarInfo ToVarInfo(this PlcVarName plcVarEnum)
        {
            if (VarInfoDict.ContainsKey(plcVarEnum))
            {
                return VarInfoDict[plcVarEnum];
            }
            var plcValue = EnumHelper.GetAttributeValue<PlcVarName, PlcVarInfoAttribute, PlcVarInfo>(plcVarEnum, attr => attr.Info)
                     ?? throw new Exception($"获取变量异常：{plcVarEnum}，没有标记PlcValueAttribute");
            if (string.IsNullOrEmpty(plcValue.Address))
            {
                throw new Exception($"获取变量异常：{plcVarEnum}，变量地址为空");
            }
            VarInfoDict.Add(plcVarEnum, plcValue);
            plcValue.Name = plcVarEnum;
            return plcValue;
        }

        /// <summary>
        /// 读取PLC
        /// </summary>
        public static object ReadVar(this SiemensClient client, PlcVarInfo varInfo)
        {
            if (client is null)
            {
                throw new ArgumentNullException($"PLC尚未初始化");
            }
            object resultvalue;
            switch (varInfo.DataType)
            {
                case DataTypeEnum.String:
                    var resultString = client.ReadString(varInfo.Address);
                    if (!resultString.IsSucceed) throw new Exception(resultString.Err);
                    resultvalue = resultString.Value;
                    break;
                case DataTypeEnum.Bool:
                    var resultBool = client.ReadBoolean(varInfo.Address);
                    if (!resultBool.IsSucceed) throw new Exception(resultBool.Err);
                    resultvalue = resultBool.Value;
                    break;
                case DataTypeEnum.Float:
                    var resultFloat = client.ReadFloat(varInfo.Address);
                    if (!resultFloat.IsSucceed) throw new Exception(resultFloat.Err);
                    resultvalue = resultFloat.Value;
                    break;
                case DataTypeEnum.Double:
                    var resultDouble = client.ReadDouble(varInfo.Address);
                    if (!resultDouble.IsSucceed) throw new Exception(resultDouble.Err);
                    resultvalue = resultDouble.Value;
                    break;
                case DataTypeEnum.Byte:
                    var resultByte = client.ReadByte(varInfo.Address);
                    if (!resultByte.IsSucceed) throw new Exception(resultByte.Err);
                    resultvalue = resultByte.Value;
                    break;
                case DataTypeEnum.Int16:
                    var resultInt16 = client.ReadInt16(varInfo.Address);
                    if (!resultInt16.IsSucceed) throw new Exception(resultInt16.Err);
                    resultvalue = resultInt16.Value;
                    break;
                case DataTypeEnum.UInt16:
                    var resultUint16 = client.ReadUInt16(varInfo.Address);
                    if (!resultUint16.IsSucceed) throw new Exception(resultUint16.Err);
                    resultvalue = resultUint16.Value;
                    break;
                case DataTypeEnum.Int32:
                    var resultInt32 = client.ReadInt32(varInfo.Address);
                    if (!resultInt32.IsSucceed) throw new Exception(resultInt32.Err);
                    resultvalue = resultInt32.Value;
                    break;
                case DataTypeEnum.UInt32:
                    var resultUInt32 = client.ReadUInt32(varInfo.Address);
                    if (!resultUInt32.IsSucceed) throw new Exception(resultUInt32.Err);
                    resultvalue = resultUInt32.Value;
                    break;
                case DataTypeEnum.Int64:
                    var resultInt64 = client.ReadInt64(varInfo.Address);
                    if (!resultInt64.IsSucceed) throw new Exception(resultInt64.Err);
                    resultvalue = resultInt64.Value;
                    break;
                case DataTypeEnum.UInt64:
                    var resultUInt64 = client.ReadUInt64(varInfo.Address);
                    if (!resultUInt64.IsSucceed) throw new Exception(resultUInt64.Err);
                    resultvalue = resultUInt64.Value;
                    break;
                default:
                    throw new NotImplementedException($"变量为指定数据类型，或是非预期的数据类型{varInfo}");
            }
            return resultvalue;
        }
        /// <summary>
        /// 转换数据类型，写入PLC
        /// </summary>
        public static object WriteVar(this SiemensClient client, PlcVarInfo varInfo, object value)
        {
            if (client is null)
            {
                throw new ArgumentNullException($"PLC尚未初始化");
            }
            DataTypeEnum dataType = varInfo.DataType;
            object convertValue;
            Result result;
            switch (dataType)
            {
                case DataTypeEnum.String:
                    var @string = value.ToString();
                    convertValue = @string;
                    result = client.Write(varInfo.Address, @string);
                    break;
                case DataTypeEnum.Bool:
                    var @bool = bool.Parse(value.ToString());
                    convertValue = @bool;
                    result = client.Write(varInfo.Address, @bool);
                    break;
                case DataTypeEnum.Float:
                    var @float = float.Parse(value.ToString());
                    convertValue = @float;
                    result = client.Write(varInfo.Address, @float);
                    break;
                case DataTypeEnum.Double:
                    var @double = double.Parse(value.ToString());
                    convertValue = @double;
                    result = client.Write(varInfo.Address, @double);
                    break;
                case DataTypeEnum.Byte:
                    var @byte = byte.Parse(value.ToString());
                    convertValue = @byte;
                    result = client.Write(varInfo.Address, @byte);
                    break;
                case DataTypeEnum.Int16:
                    var @short = short.Parse(value.ToString());
                    convertValue = @short;
                    result = client.Write(varInfo.Address, @short);
                    break;
                case DataTypeEnum.UInt16:
                    var @ushort = ushort.Parse(value.ToString());
                    convertValue = @ushort;
                    result = client.Write(varInfo.Address, @ushort);
                    break;
                case DataTypeEnum.Int32:
                    var @int = int.Parse(value.ToString());
                    convertValue = @int;
                    result = client.Write(varInfo.Address, @int);
                    break;
                case DataTypeEnum.UInt32:
                    var @uint = uint.Parse(value.ToString());
                    convertValue = @uint;
                    result = client.Write(varInfo.Address, @uint);
                    break;
                case DataTypeEnum.Int64:
                    var @long = long.Parse(value.ToString());
                    convertValue = @long;
                    result = client.Write(varInfo.Address, @long);
                    break;
                case DataTypeEnum.UInt64:
                    var @ulong = ulong.Parse(value.ToString());
                    convertValue = @ulong;
                    result = client.Write(varInfo.Address, @ulong);
                    break;
                default:
                    throw new NotImplementedException($"变量为指定数据类型，或是非预期的数据类型{varInfo}");
            }

            if (result.IsSucceed)
            {
                return convertValue;
            }
            else
            {
                throw new Exception(result.Err);
            }
        }

       


        /// <summary>
        /// 类型转换
        /// </summary>
        /// <param name="dataType"></param>
        /// <returns></returns>
        /// <exception cref="NotImplementedException"></exception>
        //public static Type ToDataType(this DataTypeEnum dataType)
        //{
        //    Type plcDataType;
        //    switch (dataType)
        //    {
        //        case DataTypeEnum.String:
        //            plcDataType = typeof(string);
        //            break;
        //        case DataTypeEnum.Bool:
        //            plcDataType = typeof(bool);
        //            break;
        //        case DataTypeEnum.Float:
        //            plcDataType = typeof(float);
        //            break;
        //        case DataTypeEnum.Double:
        //            plcDataType = typeof(double);
        //            break;
        //        case DataTypeEnum.Byte:
        //            plcDataType = typeof(byte);
        //            break;
        //        case DataTypeEnum.Int16:
        //            plcDataType = typeof(short);
        //            break;
        //        case DataTypeEnum.UInt16:
        //            plcDataType = typeof(ushort);
        //            break;
        //        case DataTypeEnum.Int32:
        //            plcDataType = typeof(int);
        //            break;
        //        case DataTypeEnum.UInt32:
        //            plcDataType = typeof(uint);
        //            break;
        //        case DataTypeEnum.Int64:
        //            plcDataType = typeof(long);
        //            break;
        //        case DataTypeEnum.UInt64:
        //            plcDataType = typeof(ulong);
        //            break;
        //        default:
        //            throw new NotImplementedException();
        //    }
        //    return plcDataType;
        //}

    }
   

}
