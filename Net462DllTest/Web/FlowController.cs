
using Net462DllTest.Enums;
using Net462DllTest.Signal;
using Net462DllTest.Trigger;
using Serein.Library.Utils;
using Serein.Library.Web;
using System;

namespace Net462DllTest.Web
{
    [AutoHosting]
    public class FlowController : ControllerBase
    {
        private readonly SiemensPlcDevice plcDevice;
        private readonly ViewManagement viewManagement;

        public FlowController(SiemensPlcDevice plcDevice, ViewManagement viewManagement)
        {
            this.plcDevice = plcDevice;
            this.viewManagement = viewManagement;

        }

        /*
         * 类型 ：POST
         * url  :  http://127.0.0.1:8089/flow/plcop?var=
         * url  :  http://127.0.0.1:8089/flow/plcop?var=SpaceNum
         * body ：[JSON]
         * 
         *      {
         *          "value":0,
         *      }
         */
        [WebApi(ApiType.POST)]
        public dynamic PlcOp([Url] string var, int value)
        {
            if (EnumHelper.TryConvertEnum<PlcVarName>(var, out var signal))
            {
                Console.WriteLine($"外部触发 {signal} 信号，信号内容 ： {value} ");
                plcDevice.Trigger(signal, value);// 通过 Web Api 模拟外部输入信号
                return new { state = "succeed" };
            }
            else
            {
                return new { state = "fail" };
            }
          
        }
        /*
        * 类型 ：POST
        * url  :  http://127.0.0.1:8089/flow/trigger?command=
        * url  :  http://127.0.0.1:8089/flow/trigger?command=Command_1
        * body ：[JSON]
        * 
        *      {
        *          "value":0,
        *      }
        */
        [WebApi(ApiType.POST)]
        public dynamic Trigger([Url] string command, int value)
        {
            if (EnumHelper.TryConvertEnum<CommandSignal>(command, out var signal))
            {
                Console.WriteLine($"外部触发 {signal} 信号，信号内容 ： {value} ");
                viewManagement.Trigger(signal, value);// 通过 Web Api 模拟外部输入信号
                return new { state = "succeed" };
            }
            else
            {
                return new { state = "fail" };
            }

        }
    }


     

     
}
