using Net462DllTest.Trigger;
using Serein.Library;
using Serein.Library.Api;
using Serein.Library.Framework.NodeFlow;
using System;
using System.Threading.Tasks;

namespace Net462DllTest.LogicControl
{

    public enum ParkingCommand
    {
        GetPparkingSpace,
    }

    [AutoRegister]
    [DynamicFlow("[Parking]")]
    public class ParkingLogicControl
    {
        private readonly PrakingDevice PrakingDevice;

        public ParkingLogicControl(PrakingDevice PrakingDevice)
        {
            this.PrakingDevice = PrakingDevice;
        }


        [NodeAction(NodeType.Flipflop, "等待车位调取命令")]
        public async Task<IFlipflopContext<string>> GetPparkingSpace(ParkingCommand parkingCommand = ParkingCommand.GetPparkingSpace)
        {
            var result = await PrakingDevice.WaitTriggerAsync<string>(parkingCommand);
            await Console.Out.WriteLineAsync("收到命令：调取车位，车位号" + result.Value);
            return new FlipflopContext<string>(FlipflopStateType.Succeed, result.Value);
        }


        [NodeAction(NodeType.Action, "调取指定车位")]
        public async Task Storage(string spaceNum = "101")
        {
           if (await PrakingDevice.InvokeTriggerAsync(ParkingCommand.GetPparkingSpace, spaceNum))
            {
                Console.WriteLine("发送命令成功：调取车位" + spaceNum);

            }
            else
            {
                Console.WriteLine("发送命令失败：调取车位" + spaceNum);
            }
        }


    }
}
