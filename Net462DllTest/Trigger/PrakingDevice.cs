using Net462DllTest.LogicControl;
using Serein.Library;
using Serein.Library.Utils.FlowTrigger;

namespace Net462DllTest.Trigger
{
    [AutoRegister]
    public class PrakingDevice : TaskFlowTrigger<ParkingCommand>
    {
    }

}
