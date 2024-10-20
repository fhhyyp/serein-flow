using IoTClient.Common.Enums;
using Net462DllTest.Enums;
using Net462DllTest.Model;
using Net462DllTest.Trigger;
using Serein.Library;
using Serein.Library.Api;
using Serein.Library.Framework.NodeFlow;
using System;
using System.Threading.Tasks;

namespace Net462DllTest.LogicControl
{
    [AutoRegister]
    [DynamicFlow("[SiemensPlc]")]
    public class PlcLogicControl 
    {
        public Guid HandleGuid { get; } = new Guid();

        private readonly SiemensPlcDevice MyPlc;
        private readonly PlcVarModelDataProxy plcVarModelDataProxy;

        public PlcLogicControl(SiemensPlcDevice MyPlc,
                               PlcVarModelDataProxy plcVarModelDataProxy)
        {
            this.MyPlc = MyPlc;
            this.plcVarModelDataProxy = plcVarModelDataProxy;
        }

        #region 初始化
        [NodeAction(NodeType.Loading)] // Loading 初始化完成已注入依赖项，可以开始逻辑上的操作
        public void Loading(IDynamicContext context)
        {
           

        }

        [NodeAction(NodeType.Exit)] // 流程结束时自动执行
        public void Exit(IDynamicContext context)
        {
            MyPlc.Close();
            MyPlc.CancelAllTasks();
        }

        #endregion

        #region 触发器节点

        [NodeAction(NodeType.Flipflop, "等待变量更新")]
        public async Task<IFlipflopContext<object>> WaitTask(PlcVarName varName = PlcVarName.ErrorCode)
        {
            try
            {
                var triggerData = await MyPlc.CreateTaskAsync<object>(varName);
                await Console.Out.WriteLineAsync($"PLC变量触发器[{varName}]传递数据：{triggerData}");
                return new FlipflopContext<object>(FlipflopStateType.Succeed, triggerData);
            }
            catch (Exception)
            {
                throw;
            }

        }

        #endregion

        #region 动作节点

        [NodeAction(NodeType.Action, "等待")]
        public async Task Delay(int ms = 5000)
        {
            await Console.Out.WriteLineAsync("开始等待");
            await Task.Delay(ms);
            await Console.Out.WriteLineAsync("不再等待");

        }


        [NodeAction(NodeType.Action, "PLC初始化")]
        public SiemensPlcDevice PlcInit(SiemensVersion version = SiemensVersion.None,
                                        string ip = "192.168.10.100",
                                        int port = 102)
        {
            //MyPlc.Model.Set(PlcVarName.DoorVar,(Int16)1);
            //MyPlc.Model.Get(PlcVarName.DoorVar);
            if (MyPlc.Client is null)
            {
                try
                {
                   MyPlc.Init(version, ip, port);
                   Console.WriteLine($"西门子PLC初始化成功[{version},{ip}:{port}]");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"西门子PLC[{version},{ip}:{port}]初始化异常:{ex.Message}");
                }
            }
            else
            {
                Console.WriteLine( $"西门子PLC已经初始化[{version},{ip}:{port}]");
            }
            return MyPlc;
        }

        [NodeAction(NodeType.Action, "设置PLC状态")]
        public SiemensPlcDevice SetState(PlcState state = PlcState.PowerOff)
        {
            var oldState = MyPlc.State;
            MyPlc.State = state;
            Console.WriteLine($"PLC状态从[{oldState}]转为[{state}]");
            return MyPlc;
        }


        [NodeAction(NodeType.Action, "PLC获取变量")]
        public object ReadVar(PlcVarName varName)
        {
            var result = MyPlc.Read(varName);
            Console.WriteLine($"获取变量成功：({varName})\t result = {result}");
            return result;
        }


        [NodeAction(NodeType.Action, "PLC写入变量")]
        public SiemensPlcDevice WriteVar(object value, PlcVarName varName)
        {
            MyPlc.Write(varName, value); // 新数据
            return MyPlc;
        }

        [NodeAction(NodeType.Action, "批量读取")]
        public PlcVarModelDataProxy BatchReadVar()
        {
            //MyPlc.BatchRefresh();
            return plcVarModelDataProxy;
        }


        [NodeAction(NodeType.Action, "开启定时刷新")]
        public void OpenTimedRefresh()
        {
            Task.Run(async () => await MyPlc.OpenTimedRefreshAsync());
        }

        [NodeAction(NodeType.Action, "关闭定时刷新")]
        public void CloseTimedRefresh()
        {
            MyPlc.CloseTimedRefresh();
        }


        #endregion
    }
}
