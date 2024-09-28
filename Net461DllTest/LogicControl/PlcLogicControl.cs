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
using System.ComponentModel;
using System.Reflection;
using System.Threading.Tasks;

namespace Net461DllTest.LogicControl
{
    [DynamicFlow] 
    public class PlcLogicControl
    {
        [AutoInjection] 
        public SiemensPlcDevice MyPlc { get; set; }




        #region 初始化、初始化完成以及退出的事件
        [NodeAction(NodeType.Init)] // Init ： 初始化事件，流程启动时执行
        public void Init(IDynamicContext context)
        {
            context.Env.IOC.Register<SiemensPlcDevice>(); // 注册Plc设备
            context.Env.IOC.Register<WebServer>(); // 注册Web服务
            // // 注册控制器
            context.Env.IOC.Run<IRouter>(router => {
                router.RegisterController(typeof(ApiController));
            });
        }

        [NodeAction(NodeType.Loading)] // Loading 初始化完成已注入依赖项，可以开始逻辑上的操作
        public void Loading(IDynamicContext context)
        {
            context.Env.IOC.Run<WebServer>((web) =>
            {
                web.Start("http://*:8089/"); // 开启 Web 服务
            });
        }

        [NodeAction(NodeType.Exit)] // 流程结束时自动执行
        public void Exit(IDynamicContext context)
        {
            MyPlc.ResetDevice();
            MyPlc.CancelAllTasks();
        }

        #endregion

        #region 触发器节点

        [NodeAction(NodeType.Flipflop, "等待信号触发", ReturnType = typeof(int))]
        public async Task<IFlipflopContext> WaitTask(OrderSignal order = OrderSignal.Command_1)
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
        public object ReadVar([BindConvertor(typeof(PlcVarEnum), typeof(PlcVarConvertor))] PlcVarInfo varInfo)
        {
            var result = MyPlc.Read(varInfo);
            Console.WriteLine($"获取变量成功：({varInfo})\t result = {result}");
            return result;
        }

        [NodeAction(NodeType.Action, "PLC写入变量")]
        public SiemensPlcDevice WriteVar2(object value, [BindConvertor(typeof(PlcVarEnum), typeof(PlcVarConvertor))] PlcVarInfo varInfo)
        {
            
            if (MyPlc.State == PlcState.Runing)
            {
                if (!varInfo.IsProtected)
                {
                    MyPlc.Write(varInfo, value);
                    Console.WriteLine($"PLC变量{varInfo}写入数据：{value}");
                }
                else
                {
                    Console.WriteLine($"PLC变量{varInfo}当前禁止写入");
                }
            }
            else
            {
                Console.WriteLine($"PLC处于非预期状态{MyPlc.State}");
            }
            return MyPlc;
        }


        /// <summary>
        /// 转换器，用于将枚举转为自定义特性中的数据
        /// </summary>
        public class PlcVarConvertor: IEnumConvertor<PlcVarEnum, PlcVarInfo>
        {
            public PlcVarInfo Convertor(PlcVarEnum plcVarValue)
            {
                if (plcVarValue == PlcVarEnum.None)
                {
                    throw new Exception("非预期枚举值");
                }
                var plcValue = EnumHelper.GetBoundValue<PlcVarEnum, PlcValueAttribute, PlcVarInfo>(plcVarValue, attr => attr.PlcInfo)
                         ?? throw new Exception($"获取变量异常：{plcVarValue}，没有标记PlcValueAttribute");
                if (string.IsNullOrEmpty(plcValue.VarAddress))
                {
                    throw new Exception($"获取变量异常：{plcVarValue}，变量地址为空");
                }
                return plcValue;
            }

        }

        #endregion
    }
}
