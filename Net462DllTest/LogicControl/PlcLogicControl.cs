using IoTClient.Clients.PLC;
using IoTClient.Common.Enums;
using Net462DllTest.Enums;
using Net462DllTest.Model;
using Net462DllTest.Signal;
using Net462DllTest.Trigger;
using Net462DllTest.Web;
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

namespace Net462DllTest.LogicControl
{
    [AttributeUsage(AttributeTargets.Class)]
    public sealed class AutoSocketAttribute : Attribute
    {
        public string BusinessField;
    }




    [AutoRegister]
    [DynamicFlow("[SiemensPlc]")] 
    public class PlcLogicControl
    {
        private readonly SiemensPlcDevice MyPlc;
        private readonly PlcVarModelDataProxy plcVarModelDataProxy;

        public PlcLogicControl(SiemensPlcDevice MyPlc,
                                      PlcVarModelDataProxy plcVarModelDataProxy)
        {
            this.MyPlc = MyPlc;
            this.plcVarModelDataProxy = plcVarModelDataProxy;
        }

        #region 初始化、初始化完成以及退出的事件
        [NodeAction(NodeType.Init)]
        public void Init(IDynamicContext context)
        {
            context.Env.IOC.Register<IRouter, Router>();
            context.Env.IOC.Register<WebServer>();

            //context.Env.IOC.Register<SocketServer>();
            //context.Env.IOC.Register<SocketClient>();
        }

        [NodeAction(NodeType.Loading)] // Loading 初始化完成已注入依赖项，可以开始逻辑上的操作
        public void Loading(IDynamicContext context)
        {
            // 注册控制器
            context.Env.IOC.Run<IRouter, WebServer>((router, web) => {
                router.RegisterController(typeof(CommandController));
                web.Start("http://*:8089/"); // 开启 Web 服务
            });

            //context.Env.IOC.Run<SocketServer>(server => {
            //    server.Start(5000); // 开启 Socket 监听
            //});
        }

        [NodeAction(NodeType.Exit)] // 流程结束时自动执行
        public void Exit(IDynamicContext context)
        {
            context.Env.IOC.Run<WebServer>((web) =>
            {
                web?.Stop(); // 关闭 Web 服务
            });
            MyPlc.Close();
            MyPlc.CancelAllTasks();
        }

        #endregion

        #region 触发器节点

        [NodeAction(NodeType.Flipflop, "等待变量更新", ReturnType = typeof(int))]
        public async Task<IFlipflopContext> WaitTask(PlcVarName varName = PlcVarName.ErrorCode)
        {
            try
            {
                TriggerData triggerData = await MyPlc.CreateChannelWithTimeoutAsync(varName, TimeSpan.FromMinutes(120), 0);
                if (triggerData.Type == TriggerType.Overtime)
                {
                    throw new FlipflopException("超时取消");
                }
                await Console.Out.WriteLineAsync($"PLC变量触发器[{varName}]传递数据：{triggerData.Value}");
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
            //MyPlc.Model.Set(PlcVarName.DoorVar,1);
            //MyPlc.Model.Value.SpaceNum = 1;
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
        public void BatchReadVar()
        {
            MyPlc.BatchRefresh();
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
