using Net462DllTest.LogicControl;
using Serein.Library.Attributes;
using Serein.Library.NodeFlow.Tool;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Net462DllTest.Trigger
{
    [AutoRegister]
    public class PrakingDevice : ChannelFlowTrigger<ParkingCommand>
    {
    }

}
