using IoTClient.Clients.PLC;
using IoTClient.Enums;
using Net462DllTest.Trigger;
using Net462DllTest.Signal;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using static Net462DllTest.Signal.PlcVarInfoAttribute;
using Serein.Library.Attributes;
using static Net462DllTest.Signal.PlcVarInfo;

namespace Net462DllTest.Enums
{



    /// <summary>
    /// PLC变量信息
    /// </summary>
    public enum PlcVarName
    {
        /// <summary>
        /// 车位号
        /// </summary>
        [PlcVarInfo(DataTypeEnum.Int16, "V100", OnNotificationType.OnChanged)]
        SpaceNum,

        /// <summary>
        /// 上位机指令
        /// </summary>
        [PlcVarInfo(DataTypeEnum.Int16, "V102", OnNotificationType.OnChanged)]
        CmdForPLC,

        /// <summary>
        /// PLC当前存取车位号
        /// </summary>
        [PlcVarInfo(DataTypeEnum.Int16, "V110", OnNotificationType.OnChanged)]
        DoingSpaceNum,

        /// <summary>
        /// 下位机状态
        /// </summary>
        [PlcVarInfo(DataTypeEnum.Int16, "V112", OnNotificationType.OnChanged)]
        PLCState,

        /// <summary>
        /// 门1正常待机车位号，存车完成地面车位0
        /// </summary>
        [PlcVarInfo(DataTypeEnum.Int16, "V114", OnNotificationType.OnChanged)]
        Door1CurSpaceNum,

        /// <summary>
        /// 门2正常待机车位号，存车完成地面车位0
        /// </summary>
        [PlcVarInfo(DataTypeEnum.Int16, "V124", OnNotificationType.OnChanged)]
        Door2CurSpaceNum,

        /// <summary>
        /// 下位机运行模式
        /// </summary>
        [PlcVarInfo(DataTypeEnum.Int16, "V116", OnNotificationType.OnChanged)]
        PLCRunMode,

        /// <summary>
        /// 执行的门号
        /// </summary>
        [PlcVarInfo(DataTypeEnum.Int16, "V104", OnNotificationType.OnChanged)]
        DoorVar,

        /// <summary>
        /// 门1是否开到位
        /// </summary>
        [PlcVarInfo(DataTypeEnum.Bool, "V207.0", OnNotificationType.OnChanged)]
        IsDoor1OpenDone,

        /// <summary>
        /// 门1是否关到位
        /// </summary>
        [PlcVarInfo(DataTypeEnum.Bool, "V207.1", OnNotificationType.OnChanged)]
        IsDoor1ClosedDone,


        /// <summary>
        /// 门2是否开到位
        /// </summary>
        [PlcVarInfo(DataTypeEnum.Bool, "V207.3", OnNotificationType.OnChanged)]
        IsDoor2OpenDone,

        /// <summary>
        /// 门2是否关到位
        /// </summary>
        [PlcVarInfo(DataTypeEnum.Bool, "V207.4", OnNotificationType.OnChanged)]
        IsDoor2ClosedDone,

        /// <summary>
        /// 通道1是否有车
        /// </summary>
        [PlcVarInfo(DataTypeEnum.Bool, "V284.7", OnNotificationType.OnChanged)]
        HasCarInTone1,

        /// <summary>
        /// 通道2是否有车
        /// </summary>
        [PlcVarInfo(DataTypeEnum.Bool, "V286.7", OnNotificationType.OnChanged)]
        HasCarInTone2,

        /// <summary>
        /// 下位机异常代码
        /// </summary>
        [PlcVarInfo(DataTypeEnum.Int16, "V2", OnNotificationType.OnChanged)]
        ErrorCode,

        /// <summary>
        /// 2层以上的空板是否在待机
        /// </summary>
        [PlcVarInfo(DataTypeEnum.Bool, "V200.7", OnNotificationType.OnChanged)]
        IsOver2FlowStanded,

        /// <summary>
        /// 1号门指示灯
        /// </summary>
        [PlcVarInfo(DataTypeEnum.Bool, "Q17.0", OnNotificationType.OnChanged)]
        Gate1Light,

        /// <summary>
        /// 2号门指示灯
        /// </summary>
        [PlcVarInfo(DataTypeEnum.Bool, "Q17.3", OnNotificationType.OnChanged)]
        Gate2Light,
    }

   



}
