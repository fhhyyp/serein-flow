
using Net462DllTest.Signal;
using Net462DllTest.Trigger;
using Net462DllTest.ViewModel;
using Serein.Library.Api;
using Serein.Library.Attributes;
using Serein.Library.Enums;
using Serein.Library.Ex;
using Serein.Library.Framework.NodeFlow;
using Serein.Library.NodeFlow.Tool;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
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


        [NodeAction(NodeType.Flipflop, "等待车位调取命令",ReturnType=typeof(string))]
        public async Task<IFlipflopContext> GetPparkingSpace(ParkingCommand parkingCommand = ParkingCommand.GetPparkingSpace)
        {
            try
            {
                TriggerData triggerData = await PrakingDevice.CreateChannelWithTimeoutAsync(parkingCommand, TimeSpan.FromMinutes(120), 0);
                if (triggerData.Type == TriggerType.Overtime)
                {
                    throw new FlipflopException("超时取消");
                }
                if(triggerData.Value is string spaceNum)
                {
                    await Console.Out.WriteLineAsync("收到命令：调取车位，车位号"+ spaceNum);
                    return new FlipflopContext(FlipflopStateType.Succeed, spaceNum);
                }
                else
                {
                    throw new FlipflopException("并非车位号");
                }
              
            }
            catch (FlipflopException)
            {
                throw;
            }
            catch (Exception)
            {
                return new FlipflopContext(FlipflopStateType.Error);
            }
        }


        [NodeAction(NodeType.Action, "调取指定车位")]
        public void Storage(string spaceNum = "101")
        {
           if (PrakingDevice.TriggerSignal(ParkingCommand.GetPparkingSpace, spaceNum))
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
