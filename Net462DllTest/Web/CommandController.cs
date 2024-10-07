
using Net462DllTest.Enums;
using Net462DllTest.Signal;
using Net462DllTest.Trigger;
using Serein.Library.Attributes;
using Serein.Library.Utils;
using Serein.Library.Web;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Net462DllTest.Web
{
    [AutoHosting]
    public class CommandController : ControllerBase
    {
        private readonly SiemensPlcDevice plcDevice;

        public CommandController(SiemensPlcDevice plcDevice)
        {
            this.plcDevice = plcDevice;
        }

        /*
         * 类型 ：POST
         * url  :  http://127.0.0.1:8089/command/trigger?command=
         * body ：[JSON]
         * 
         *      {
         *          "value":0,
         *      }
         */
        [WebApi(API.POST)]
        public dynamic Trigger([Url] string var, int value)
        {
            if (EnumHelper.TryConvertEnum<PlcVarName>(var,out var signal))
            {
                Console.WriteLine($"外部触发 {signal} 信号，信号内容 ： {value} ");
                plcDevice.TriggerSignal(signal, value);// 通过 Web Api 模拟外部输入信号
                return new { state = "succeed" };
            }
            else
            {
                return new { state = "fail" };
            }
          
        }
    }


     

     
}
