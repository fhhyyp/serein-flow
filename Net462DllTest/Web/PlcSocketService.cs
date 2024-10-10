using IoTClient.Common.Enums;
using Net462DllTest.Enums;
using Net462DllTest.Model;
using Net462DllTest.Trigger;
using Serein.Library.Api;
using Serein.Library.Attributes;
using Serein.Library.Enums;
using Serein.Library.Ex;
using Serein.Library.Framework.NodeFlow;
using Serein.Library.Network.WebSocketCommunication;
using Serein.Library.NodeFlow.Tool;
using Serein.Library.Web;
using System;
using System.Threading.Tasks;

namespace Net462DllTest.Web
{

    [DynamicFlow("[PlcSocketService]")]
    [AutoRegister]
    [AutoSocketModule(JsonThemeField = "theme", JsonDataField = "data")]
    public class PlcSocketService : ISocketControlBase
    {
        public Guid HandleGuid { get; } = new Guid();

        private readonly SiemensPlcDevice MyPlc;
        private readonly PlcVarModelDataProxy plcVarModelDataProxy;

        public PlcSocketService(SiemensPlcDevice MyPlc,
                                      PlcVarModelDataProxy plcVarModelDataProxy)
        {
            this.MyPlc = MyPlc;
            this.plcVarModelDataProxy = plcVarModelDataProxy;
        }

        #region 初始化、初始化完成以及退出的事件
        [NodeAction(NodeType.Init)]
        public void Init(IDynamicContext context)
        {
            context.Env.IOC.Register<WebSocketServer>();
            context.Env.IOC.Register<WebSocketClient>();

            context.Env.IOC.Register<IRouter, Router>();
            context.Env.IOC.Register<WebApiServer>();


        }

        [NodeAction(NodeType.Loading)] // Loading 初始化完成已注入依赖项，可以开始逻辑上的操作
        public void Loading(IDynamicContext context)
        {
            // 注册控制器
            context.Env.IOC.Run<IRouter, WebApiServer>((router, apiServer) => {
                router.AddHandle(typeof(FlowController));
                apiServer.Start("http://*:8089/"); // 开启 Web Api 服务
            });

            context.Env.IOC.Run<WebSocketServer>(async (socketServer) => {
                socketServer.MsgHandleHelper.AddModule(this, (ex, recover) =>
                {
                    recover(new
                    {
                        ex = ex.Message,
                        storehouseInfo = ex.StackTrace
                    });

                });
                await socketServer.StartAsync("http://localhost:5005/"); // 开启 Web Socket 监听
            });
            context.Env.IOC.Run<WebSocketClient>(async client => {
                await client.ConnectAsync("ws://localhost:5005/"); // 连接到服务器
            });
        }

        [NodeAction(NodeType.Exit)] // 流程结束时自动执行
        public void Exit(IDynamicContext context)
        {
            context.Env.IOC.Run<WebApiServer>((apiServer) =>
            {
                apiServer?.Stop(); // 关闭 Web 服务

            });
            context.Env.IOC.Run<WebSocketServer>((socketServer) =>
            {
                socketServer?.Stop(); // 关闭 Web 服务
            });
            MyPlc.Close();
            MyPlc.CancelAllTasks();
        }

        #endregion


        [NodeAction(NodeType.Action, "等待")]
        public async Task Delay(int ms = 5000)
        {
            await Console.Out.WriteLineAsync("开始等待");
            await Task.Delay(ms);
            await Console.Out.WriteLineAsync("不再等待");

        }

        [AutoSocketHandle(IsReturnValue = false)]
        public SiemensPlcDevice PlcInit(SiemensVersion version = SiemensVersion.None,
                                        string ip = "192.168.10.100",
                                        int port = 102)
        {
            MyPlc.Model.Set(PlcVarName.DoorVar, (Int16)1);
            MyPlc.Model.Get(PlcVarName.DoorVar);
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
                Console.WriteLine($"西门子PLC已经初始化[{version},{ip}:{port}]");
            }
            return MyPlc;
        }

        [AutoSocketHandle(IsReturnValue = false)]
        public SiemensPlcDevice SetState(PlcState state = PlcState.PowerOff)
        {
            var oldState = MyPlc.State;
            MyPlc.State = state;
            Console.WriteLine($"PLC状态从[{oldState}]转为[{state}]");
            return MyPlc;
        }

        [AutoSocketHandle]
        public object ReadVar(PlcVarName varName)
        {
            var result = MyPlc.Read(varName);
            Console.WriteLine($"获取变量成功：({varName})\t result = {result}");
            return result;
        }

        [AutoSocketHandle(IsReturnValue = false)]
        public SiemensPlcDevice WriteVar(object value, PlcVarName varName)
        {
            MyPlc.Write(varName, value); // 新数据
            return MyPlc;
        }

        public PlcVarModelDataProxy BatchReadVar()
        {
            MyPlc.BatchRefresh();
            return plcVarModelDataProxy;
        }

        public void OpenTimedRefresh()
        {
            Task.Run(async () => await MyPlc.OpenTimedRefreshAsync());
        }

        public void CloseTimedRefresh()
        {
            MyPlc.CloseTimedRefresh();
        }

    }
}
