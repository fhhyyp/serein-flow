using Net461DllTest.Data;
using Net461DllTest.Device;
using Net461DllTest.Signal;
using Net461DllTest.Web;
using Serein.Library.Api;
using Serein.Library.Attributes;
using Serein.Library.Enums;
using Serein.Library.Ex;
using Serein.Library.Framework.NodeFlow;
using Serein.Library.NodeFlow.Tool;
using Serein.Library.Web;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Net461DllTest.Flow
{
    [DynamicFlow] 
    public class LogicControl
    {
        [AutoInjection] 
        public PlcDevice MyPlc { get; set; }


        #region 初始化、初始化完成以及退出的事件
        [NodeAction(NodeType.Init)] // Init ： 初始化事件，流程启动时执行
        public void Init(IDynamicContext context)
        {
            context.Env.IOC.Register<PlcDevice>(); // 注册Plc设备
            context.Env.IOC.Register<MyData>(); // 注册数据类
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
            MyPlc.Disconnect();
            MyPlc.CancelAllTasks();
        }

        #endregion

        #region 触发器

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
                MyPlc.MyData.Count += (int)triggerData.Value;
                return new FlipflopContext(FlipflopStateType.Succeed, MyPlc.MyData.Count);
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
            MyPlc.MyData.Count += number;
            return MyPlc;
        }

        [NodeAction(NodeType.Action, "重置计数")]
        public void 重置计数()
        {
            MyPlc.MyData.Count = 0;
        }

        [NodeAction(NodeType.Action, "触发信号")]
        public void 光电1信号触发(int data)
        {
            MyPlc.Write($"{MyPlc.PlcId.ToString("00000")} - 信号源[光电1] - 模拟写入 : {data}{Environment.NewLine}");
        }

        //[NodeAction(NodeType.Action, "触发光电2")]
        //public void 光电2信号触发(int data)
        //{
        //    MyPlc.Write($"{MyPlc.PlcId.ToString("00000")} - 信号源[光电2] - 模拟写入 : {data}{Environment.NewLine}");
        //}

        //[NodeAction(NodeType.Action, "触发光电3")]
        //public void 光电3信号触发(int data)
        //{
        //    MyPlc.Write($"{MyPlc.PlcId.ToString("00000")} - 信号源[光电3] - 模拟写入 : {data}{Environment.NewLine}");
        //}
        #endregion
    }
}
