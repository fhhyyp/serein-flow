using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Serein.Library.Network.Mqtt
{
    internal interface IMqttServer
    {
        void Staer();

        void Stop();

        void HandleMsg(string msg);

        void AddHandleConfig();

    }
}
