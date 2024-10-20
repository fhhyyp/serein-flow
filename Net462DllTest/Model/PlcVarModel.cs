using Net462DllTest.Enums;
using Serein.Library;
using System;

namespace Net462DllTest.Model
{
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



    /// <summary>
    /// 数据代理，防止View修改
    /// </summary>
    [AutoRegister]
    public class PlcVarModelDataProxy
    {
        private readonly PlcVarModel plcVarModel;
        public PlcVarModelDataProxy(PlcVarModel plcVarModel)
        {
            this.plcVarModel = plcVarModel;
        }
        /// <summary>
        /// 车位号
        /// </summary>
        public Int16 SpaceNum { get => plcVarModel.SpaceNum;  }

        /// <summary>
        /// 上位机指令
        /// </summary>
        public Int16 CmdForPLC { get => plcVarModel.CmdForPLC; }

        /// <summary>
        /// PLC当前存取车位号
        /// </summary>
        public Int16 DoingSpaceNum { get => plcVarModel.DoingSpaceNum; }

        /// <summary>
        /// 下位机状态
        /// </summary>
        public Int16 PLCState { get => plcVarModel.PLCState; }

        /// <summary>
        /// 门1正常待机车位号，存车完成地面车位0
        /// </summary>
        public Int16 Door1CurSpaceNum { get => plcVarModel.Door1CurSpaceNum; }

        /// <summary>
        /// 门2正常待机车位号，存车完成地面车位0
        /// </summary>
        public Int16 Door2CurSpaceNum { get => plcVarModel.Door2CurSpaceNum; }

        /// <summary>
        /// 下位机运行模式
        /// </summary>
        public Int16 PLCRunMode { get => plcVarModel.PLCRunMode; }

        /// <summary>
        /// 执行的门号
        /// </summary>
        public Int16 DoorVar { get => plcVarModel.DoorVar; }

        /// <summary>
        /// 门1是否开到位
        /// </summary>
        public bool IsDoor1OpenDone { get => plcVarModel.IsDoor1OpenDone; }

        /// <summary>
        /// 门1是否关到位
        /// </summary>
        public bool IsDoor1ClosedDone { get => plcVarModel.IsDoor1ClosedDone; }


        /// <summary>
        /// 门2是否开到位
        /// </summary>
        public bool IsDoor2OpenDone { get => plcVarModel.IsDoor2OpenDone; }

        /// <summary>
        /// 门2是否关到位
        /// </summary>
        public bool IsDoor2ClosedDone { get => plcVarModel.IsDoor2ClosedDone; }

        /// <summary>
        /// 通道1是否有车
        /// </summary>
        public bool HasCarInTone1 { get => plcVarModel.HasCarInTone1; }

        /// <summary>
        /// 通道2是否有车
        /// </summary>
        public bool HasCarInTone2 { get => plcVarModel.HasCarInTone2; }

        /// <summary>
        /// 下位机异常代码
        /// </summary>
        public Int16 ErrorCode { get => plcVarModel.ErrorCode; }

        /// <summary>
        /// 2层以上的空板是否在待机
        /// </summary>
        public bool IsOver2FlowStanded { get => plcVarModel.IsOver2FlowStanded; }

        /// <summary>
        /// 1号门指示灯
        /// </summary>
        public bool Gate1Light { get => plcVarModel.Gate1Light; }

        /// <summary>
        /// 2号门指示灯
        /// </summary>
        public bool Gate2Light { get => plcVarModel.Gate2Light; }
    }


}
