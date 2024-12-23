using Net462DllTest.LogicControl;
using Serein.Library;

namespace Net462DllTest.Trigger
{
    [AutoRegister]
    public class PrakingDevice : TaskFlowTrigger<ParkingCommand>
    {
    }

}
