﻿using Net461DllTest.Device;
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
        public FromWorkBenchViewModel(SiemensPlcDevice Device)
        {
            this.Device = Device;
        }
        private SiemensPlcDevice Device;

        public string Name { get; set; }

        public string GetDeviceInfo()
        {
            return Device?.ToString();
        }

        public void Trigger(OrderSignal signal,string spcaeNumber)
        {
            _ = Task.Run(() =>
            {
                Device.TriggerSignal(signal, spcaeNumber);
            });
        }


    }
}
