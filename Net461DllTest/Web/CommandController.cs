﻿using Net461DllTest.Device;
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
    public class CommandController : ControllerBase
    {
        private SiemensPlcDevice PlcDevice;

        public CommandController(SiemensPlcDevice PlcDevice)
        {
            this.PlcDevice = PlcDevice;
        }


        /*
         * 类型 ：POST
         * url  :  http://127.0.0.1:8089/command/trigger?command=
         * body ：[JSON]
         * 
         *      {
         *          "value":0,
         *      }
         * 
         */
        [WebApi(API.POST)]
        public dynamic Trigger([Url] string command, int value)
        {
            if (Enum.TryParse(command, out CommandSignal signal) && Enum.IsDefined(typeof(CommandSignal), signal))
            {
                Console.WriteLine($"外部触发 {signal} 信号，信号内容 ： {value} ");
                PlcDevice.TriggerSignal(signal, value);// 通过 Web Api 模拟外部输入信号
                return new { state = "succeed" };
            }
            return new { state = "fail" };
        }
    }

}
