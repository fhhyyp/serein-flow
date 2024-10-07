using Net462DllTest.Enums;
using Net462DllTest.Trigger;
using Serein.Library.Attributes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Net462DllTest.Model
{

    [AttributeUsage(AttributeTargets.Property)]
    public class PlcValueAttribute : Attribute
    {
        /// <summary>
        /// 变量类型
        /// </summary>
        public PlcVarName PlcVarEnum { get; }
        public PlcValueAttribute(PlcVarName plcVarEnum)
        {
            this.PlcVarEnum = plcVarEnum;
        }
    }


    /// <summary>
    /// PLC变量
    /// </summary>
    [AutoRegister]
    public class PlcVarModel
    {
        /// <summary>
        /// 车位号
        /// </summary>
        [BindValue(PlcVarName.SpaceNum)]
        public Int16 SpaceNum { get; set; }

        /// <summary>
        /// 上位机指令
        /// </summary>
        [BindValue(PlcVarName.CmdForPLC)]
        public Int16 CmdForPLC { get; set; }

        /// <summary>
        /// PLC当前存取车位号
        /// </summary>
        [BindValue(PlcVarName.DoingSpaceNum)]
        public Int16 DoingSpaceNum { get; set; }

        /// <summary>
        /// 下位机状态
        /// </summary>
        [BindValue(PlcVarName.PLCState)]
        public Int16 PLCState { get; set; }

        /// <summary>
        /// 门1正常待机车位号，存车完成地面车位0
        /// </summary>
        [BindValue(PlcVarName.Door1CurSpaceNum)]
        public Int16 Door1CurSpaceNum { get; set; }

        /// <summary>
        /// 门2正常待机车位号，存车完成地面车位0
        /// </summary>
        [BindValue(PlcVarName.Door2CurSpaceNum)]
        public Int16 Door2CurSpaceNum { get; set; }

        /// <summary>
        /// 下位机运行模式
        /// </summary>
        [BindValue(PlcVarName.PLCRunMode)]
        public Int16 PLCRunMode { get; set; }

        /// <summary>
        /// 执行的门号
        /// </summary>
        [BindValue(PlcVarName.DoorVar)]
        public Int16 DoorVar { get; set; }

        /// <summary>
        /// 门1是否开到位
        /// </summary>
        [BindValue(PlcVarName.IsDoor1OpenDone)]
        public bool IsDoor1OpenDone { get; set; }

        /// <summary>
        /// 门1是否关到位
        /// </summary>
        [BindValue(PlcVarName.IsDoor1ClosedDone)]
        public bool IsDoor1ClosedDone { get; set; }


        /// <summary>
        /// 门2是否开到位
        /// </summary>
        [BindValue(PlcVarName.IsDoor2OpenDone)]
        public bool IsDoor2OpenDone { get; set; }

        /// <summary>
        /// 门2是否关到位
        /// </summary>
        [BindValue(PlcVarName.IsDoor2ClosedDone)]
        public bool IsDoor2ClosedDone { get; set; }

        /// <summary>
        /// 通道1是否有车
        /// </summary>
        [BindValue(PlcVarName.HasCarInTone1)]
        public bool HasCarInTone1 { get; set; }

        /// <summary>
        /// 通道2是否有车
        /// </summary>
        [BindValue(PlcVarName.HasCarInTone2)]
        public bool HasCarInTone2 { get; set; }

        /// <summary>
        /// 下位机异常代码
        /// </summary>
        [BindValue(PlcVarName.ErrorCode)]
        public Int16 ErrorCode { get; set; }

        /// <summary>
        /// 2层以上的空板是否在待机
        /// </summary>
        [BindValue(PlcVarName.IsOver2FlowStanded)]
        public bool IsOver2FlowStanded { get; set; }

        /// <summary>
        /// 1号门指示灯
        /// </summary>
        [BindValue(PlcVarName.Gate1Light)]
        public bool Gate1Light { get; set; }

        /// <summary>
        /// 2号门指示灯
        /// </summary>
        [BindValue(PlcVarName.Gate2Light)]
        public bool Gate2Light { get; set; }
    }


}
