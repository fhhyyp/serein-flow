using Serein.Library.Api;
using Serein.Library.Enums;
using Serein.Library.Attributes;
using Serein.Library.Core.NodeFlow;
using Serein.Library.Core.NodeFlow.Tool;
using static MyDll.PlcDevice;
namespace MyDll
{
    # region Web Api 层
    //public class ApiController: ControllerBase
    //{
    //    [AutoInjection]
    //    public required PlcDevice PLCDevice { get; set; }

    //    // example => http://127.0.0.1:8089/api/trigger?type=超宽光电信号&value=网络触发
    //    [ApiPost]
    //    public dynamic Trigger([IsUrlData] string type, [IsUrlData]string value)
    //    {
    //        if (Enum.TryParse(type, out SignalType result) && Enum.IsDefined(typeof(SignalType), result))
    //        {
    //            PLCDevice.TriggerSignal(result, value);// 通过 Web Api 模拟外部输入信号
    //            return new  {state = "succeed" };
    //        }
    //        return new { state = "fail" };
    //    }


    //}
    #endregion

    #region 设备层

    public class PlcDevice : TcsSignal<SignalType>
    {
        public int Count; 
        public enum SignalType
        {
            光电1,
            光电2,
            光电3,
            光电4
        }

        public void InitDevice(string ip,int port, string tips)
        {
            Write($"模拟设备初始化 :{Environment.NewLine}" +
                 $" ip :{ip}{Environment.NewLine}" +
                 $"port:{port}{Environment.NewLine}" +
                 $"tips:{tips}{Environment.NewLine}");
        }

        public void Write<T>(T value)
        {
            Console.WriteLine($"{value}");
        }
        public void Read<T>()
        {
            Console.WriteLine($"读取数据：... ");
        }
        public void Disconnect()
        { 
            Console.WriteLine($"断开连接...");
        }
    }

    #endregion

    #region 逻辑控制层
    [DynamicFlow]
    public class LogicControl
    {
        [AutoInjection]
        public PlcDevice MyPlc { get; set; }


        #region 初始化、初始化完成以及退出的事件
        [NodeAction(NodeType.Init)]
        public void Init(IDynamicContext context)
        {
            context.SereinIoc.Register<PlcDevice>();
        }

        [NodeAction(NodeType.Loading)]
        public void Loading(IDynamicContext context)
        {
            #region 初始化Web Api、Db

            // 初始化完成，已注入依赖项，可以开始逻辑上的操作
            /*context.ServiceContainer.Run<WebServer>((web) =>
            {
                // 启动 Web （先启动，再注册控制器）
                web.Start("http://*:8089/", context.ServiceContainer);
                web.RegisterAutoController<ApiController>();
            });*/

            /*dynamicContext.ServiceContainer.Run<AppConfig>((config) =>
            {
                // 配置数据库连接
                var host = config.Get<string>["127.0.0.1"];
                var port = config.Get<string>[3306];
                var dbName = config.Get<string>["system"];
                var account = config.Get<int>["sa"];
                var password = config.Get<string>["123456"];
                DBSync.SecondaryConnect(SqlSugar.DbType.MySql, host, port, dbName, account, password);
            });*/
            #endregion

            #region 模拟信号触发
            //var MainCts = context.ServiceContainer.CreateServiceInstance<NodeRunTcs>();
            //async Task action(string signalTypeName)
            //{
            //    Random random = new();
            //    Enum.TryParse(signalTypeName, out SignalType triggerType);
            //    while (MainCts != null && !MainCts.IsCancellationRequested)
            //    {
            //        int waitSec = 2000;
            //        await Task.Delay(waitSec);
            //        MyPlc.TriggerSignal(triggerType, MyPlc.Count);
            //    }
            //}
            //var tasks = typeof(SignalType).GetFields().Select(it => action(it.Name)).ToArray();
            //Task.WhenAll(tasks);
            #endregion

            Console.WriteLine("初始化完成");
        }

        [NodeAction(NodeType.Exit)]
        public void Exit(IDynamicContext context)
        {
            MyPlc.Disconnect();
            MyPlc.CancelTask();
        }

        #endregion

        #region 触发器

        [NodeAction(NodeType.Flipflop, "等待信号触发")]
        public async Task<IFlipflopContext> WaitTask(SignalType triggerType = SignalType.光电1)
        {
            /*if (!Enum.TryParse(triggerValue, out SignalType triggerType) && Enum.IsDefined(typeof(SignalType), triggerType))
            {
                return new FlipflopContext();
            }*/

            try
                {
                var tcs = MyPlc.CreateTcs(triggerType);
                var result = await tcs.Task;
                return new FlipflopContext(FlowStateType.Succeed, MyPlc.Count);
            }
            catch (Exception ex)
            {
                // await Console.Out.WriteLineAsync($"取消等待信号[{triggerType}]");
                return new FlipflopContext(FlowStateType.Error);
            }

        }

    
        #endregion

        #region 动作

        [NodeAction(NodeType.Action, "初始化")]
        public PlcDevice PlcInit(string ip = "192.168.1.1",
                                 int port = 6688,
                                 string tips = "测试")
        {
            MyPlc.InitDevice(ip, port, tips);
            return MyPlc;
        }


        [NodeAction(NodeType.Action, "自增")]
        public PlcDevice 自增(int number = 1)
        {
            MyPlc.Count += number;
            return MyPlc;
        }


        [NodeAction(NodeType.Action, "模拟循环触发")]
        public void 模拟循环触发(IDynamicContext context, 
                                int time = 200,
                                int count = 5, 
                                SignalType signal = SignalType.光电1)
        {
            Action action = () =>
            {
                MyPlc.TriggerSignal(signal, count);
            };
            _ = context.CreateTimingTask(action, time, count);
        }
        [NodeAction(NodeType.Action, "重置计数")]
        public void 重置计数()
        {
            MyPlc.Count = 0;
        }

        [NodeAction(NodeType.Action, "触发光电1")]
        public void 光电1信号触发(int data)
        {
            MyPlc.Write($"信号源[光电1] - 模拟写入 : {data}{Environment.NewLine}");
        }

        [NodeAction(NodeType.Action, "触发光电2")]
        public void 光电2信号触发(int data)
        {
            MyPlc.Write($"信号源[光电2] - 模拟写入 : {data}{Environment.NewLine}");
        }

        [NodeAction(NodeType.Action, "触发光电3")]
        public void 光电3信号触发(int data)
        {
            MyPlc.Write($"信号源[光电3] - 模拟写入 : {data}{Environment.NewLine}");
        } 
        #endregion
    }
    #endregion 

}
