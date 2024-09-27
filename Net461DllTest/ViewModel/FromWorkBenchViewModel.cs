using Net461DllTest.Device;
using Net461DllTest.Signal;
using Serein.Library.Attributes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Net461DllTest.ViewModel
{
    public class FromWorkBenchViewModel
    {
        [AutoInjection]
        public PlcDevice Device { get; set; }
        public string Name { get; set; }

        public string GetDeviceInfo()
        {
            if(Device is null)
            {
              return string.Empty;
            }
            return "PLC ID:" + Device.PlcId + "  - " + Device.MyData.Count.ToString();
        }


        public void Trigger(OrderSignal signal)
        {
            Device.TriggerSignal(signal, 0);
        }


    }
}
