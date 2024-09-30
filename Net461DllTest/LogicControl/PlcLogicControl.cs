using IoTClient.Clients.PLC;
using IoTClient.Common.Enums;
using Net461DllTest.Device;
using Net461DllTest.Enums;
using Net461DllTest.Signal;
using Net461DllTest.Web;
using Serein.Library.Api;
using Serein.Library.Attributes;
using Serein.Library.Enums;
using Serein.Library.Ex;
using Serein.Library.Framework.NodeFlow;
using Serein.Library.NodeFlow.Tool;
using Serein.Library.Utils;
using Serein.Library.Web;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Reflection;
using System.Threading.Tasks;

namespace Net461DllTest.LogicControl
{
    [AutoRegister]
    [DynamicFlow] 
    public class PlcLogicControl
    {
        private readonly SiemensPlcDevice MyPlc;

        public PlcLogicControl(SiemensPlcDevice MyPlc)
        {
            this.MyPlc = MyPlc;
           
            
        }

        #region 初始化、初始化完成以及退出的事件
        [NodeAction(NodeType.Init)] // Init ： 初始化事件，流程启动时执行
        public void Init(IDynamicContext context)
        {
            context.Env.IOC.Register<IRouter, Router>();
            context.Env.IOC.Register<WebServer>();
           
        }

        [NodeAction(NodeType.Loading)] // Loading 初始化完成已注入依赖项，可以开始逻辑上的操作
        public void Loading(IDynamicContext context)
        {
            // 注册控制器
            context.Env.IOC.Run<IRouter, WebServer>((router, web) => {
                try
                {
                    router.RegisterController(typeof(CommandController));
                    web.Start("http://*:8089/"); // 开启 Web 服务
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex);
                }
            });
        }

        [NodeAction(NodeType.Exit)] // 流程结束时自动执行
        public void Exit(IDynamicContext context)
        {
            context.Env.IOC.Run<WebServer>((web) =>
            {
                web?.Stop(); // 关闭 Web 服务
            });
            MyPlc.ResetDevice();
            MyPlc.CancelAllTasks();
        }

        #endregion

        #region 触发器节点

        [NodeAction(NodeType.Flipflop, "等待信号触发", ReturnType = typeof(int))]
        public async Task<IFlipflopContext> WaitTask(CommandSignal order = CommandSignal.Command_1)
        {
            try
            {
                TriggerData triggerData = await MyPlc.CreateChannelWithTimeoutAsync(order, TimeSpan.FromMinutes(5), 0);
                if (triggerData.Type == TriggerType.Overtime)
                {
                    throw new FlipflopException("超时取消");
                }
                //int.TryParse(triggerData.Value.ToString(),out int result);
                return new FlipflopContext(FlipflopStateType.Succeed, triggerData.Value);
            }
            catch (FlipflopException)
            {
                throw;
            }
            catch (Exception)
            {
                return new FlipflopContext(FlipflopStateType.Error);
            }

        }

        #endregion

        #region 动作节点

        [NodeAction(NodeType.Action, "初始化")]
        public SiemensPlcDevice PlcInit(SiemensVersion version = SiemensVersion.None,
                                        string ip = "192.168.10.100",
                                        int port = 102)
        {
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
            if(MyPlc.Client != null)
            {
                var oldState = MyPlc.State;
                MyPlc.State = state;
                Console.WriteLine($"PLC状态从[{oldState}]转为[{state}]");
                return MyPlc;
            }
            else
            {
                Console.WriteLine($"PLC尚未初始化");
                return MyPlc;
            }
        }

        [NodeAction(NodeType.Action, "PLC获取变量")]
        public object ReadVar(PlcVarEnum plcVarEnum)
        {
            var varInfo = ToVarInfo(plcVarEnum);
            var result = MyPlc.Read(varInfo);
            Console.WriteLine($"获取变量成功：({varInfo})\t result = {result}");
            return result;
        }

        [NodeAction(NodeType.Action, "PLC写入变量")]
        public SiemensPlcDevice WriteVar2(object value, PlcVarEnum plcVarEnum)
        {
            var varInfo = ToVarInfo(plcVarEnum);
            if (MyPlc.State == PlcState.Runing)
            {
                if (varInfo.IsProtected)
                {
                    Console.WriteLine($"PLC变量{varInfo}当前禁止写入");
                }
                else
                {
                    MyPlc.Write(varInfo, value);
                    Console.WriteLine($"PLC变量{varInfo}写入数据：{value}");
                }
            }
            else
            {
                Console.WriteLine($"PLC处于非预期状态{MyPlc.State}");
            }
            return MyPlc;
        }

        /// <summary>
        /// 缓存变量信息
        /// </summary>
        private readonly Dictionary<PlcVarEnum, PlcVarInfo> VarInfoDict = new Dictionary<PlcVarEnum, PlcVarInfo>();
        
        private PlcVarInfo ToVarInfo(PlcVarEnum plcVarEnum)
        {
            if (VarInfoDict.ContainsKey(plcVarEnum))
            {
                return VarInfoDict[plcVarEnum];
            }
            if (plcVarEnum == PlcVarEnum.None)
            {
                throw new Exception("非预期枚举值");
            }
            var plcValue = EnumHelper.GetBoundValue<PlcVarEnum, PlcValueAttribute, PlcVarInfo>(plcVarEnum, attr => attr.PlcInfo)
                     ?? throw new Exception($"获取变量异常：{plcVarEnum}，没有标记PlcValueAttribute");
            if (string.IsNullOrEmpty(plcValue.VarAddress))
            {
                throw new Exception($"获取变量异常：{plcVarEnum}，变量地址为空");
            }
            VarInfoDict.Add(plcVarEnum, plcValue);
            return plcValue;
        }

        #endregion
    }
}
