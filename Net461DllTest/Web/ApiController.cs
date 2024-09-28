using Net461DllTest.Device;
using Net461DllTest.Signal;
using Serein.Library.Attributes;
using Serein.Library.Web;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Net461DllTest.Web
{
    [AutoHosting]
    public class ApiController : ControllerBase
    {
        [AutoInjection] 
        public SiemensPlcDevice PlcDevice { get; set; }

        [WebApi(API.POST)]
        public dynamic Trigger([Url] string type, int value)
        {
            if (Enum.TryParse(type, out OrderSignal signal) && Enum.IsDefined(typeof(OrderSignal), signal))
            {
                Console.WriteLine($"外部触发 {signal} 信号，信号内容 ： {value} ");
                PlcDevice.TriggerSignal(signal, value);// 通过 Web Api 模拟外部输入信号
                return new { state = "succeed" };
            }
            return new { state = "fail" };
        }
    }

}
