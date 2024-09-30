using Net461DllTest.Signal;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static Net461DllTest.Signal.PlcValueAttribute;

namespace Net461DllTest.Enums
{



    /// <summary>
    /// PLC变量
    /// </summary>
    public enum PlcVarEnum
    {
        None,
        /// <summary>
        /// 车位号
        /// </summary>
        [PlcValue(typeof(short), "V100", VarType.Writable)]
        SpaceNum,

        /// <summary>
        /// 上位机指令
        /// </summary>
        [PlcValue(typeof(short), "V102", VarType.Writable)]
        CmdForPLC,

        /// <summary>
        /// PLC当前存取车位号
        /// </summary>
        [PlcValue(typeof(short), "V110", VarType.ReadOnly)]
        DoingSpaceNum,

        /// <summary>
        /// 下位机状态
        /// </summary>
        [PlcValue(typeof(short), "V112", VarType.ReadOnly)]
        PLCState,

        /// <summary>
        /// 门1正常待机车位号，存车完成地面车位0
        /// </summary>
        [PlcValue(typeof(short), "V114", VarType.ReadOnly)]
        Door1CurSpaceNum,

        /// <summary>
        /// 门2正常待机车位号，存车完成地面车位0
        /// </summary>
        [PlcValue(typeof(short), "V124", VarType.ReadOnly)]
        Door2CurSpaceNum,

        /// <summary>
        /// 下位机运行模式
        /// </summary>
        [PlcValue(typeof(short), "V116", VarType.Writable)]
        PLCRunMode,

        /// <summary>
        /// 执行的门号
        /// </summary>
        [PlcValue(typeof(short), "V104", VarType.Writable)]
        DoorVar,

        /// <summary>
        /// 门1是否开到位
        /// </summary>
        [PlcValue(typeof(bool), "V207.0", VarType.ReadOnly)]
        IsDoor1OpenDone,

        /// <summary>
        /// 门1是否关到位
        /// </summary>
        [PlcValue(typeof(bool), "V207.1", VarType.ReadOnly)]
        IsDoor1ClosedDone,


        /// <summary>
        /// 门2是否开到位
        /// </summary>
        [PlcValue(typeof(bool), "V207.3", VarType.ReadOnly)]
        IsDoor2OpenDone,

        /// <summary>
        /// 门2是否关到位
        /// </summary>
        [PlcValue(typeof(bool), "V207.4", VarType.ReadOnly)]
        IsDoor2ClosedDone,

        /// <summary>
        /// 通道1是否有车
        /// </summary>
        [PlcValue(typeof(bool), "V284.7", VarType.ReadOnly)]
        HasCarInTone1,

        /// <summary>
        /// 通道2是否有车
        /// </summary>
        [PlcValue(typeof(bool), "V286.7", VarType.ReadOnly)]
        HasCarInTone2,

        /// <summary>
        /// 下位机异常代码
        /// </summary>
        [PlcValue(typeof(short), "V2", VarType.ReadOnly)]
        ErrorCode,

        /// <summary>
        /// 2层以上的空板是否在待机
        /// </summary>
        [PlcValue(typeof(bool), "V200.7", VarType.ReadOnly)]
        IsOver2FlowStanded,

        /// <summary>
        /// 1号门指示灯
        /// </summary>
        [PlcValue(typeof(bool), "Q17.0", VarType.ReadOnly)]
        Gate1Light,

        /// <summary>
        /// 2号门指示灯
        /// </summary>
        [PlcValue(typeof(bool), "Q17.3", VarType.ReadOnly)]
        Gate2Light,
    }
}
