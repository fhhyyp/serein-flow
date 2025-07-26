
namespace Serein.Library.Network.Modbus
{
    /// <summary>
    /// Modbus TCP 请求实体
    /// </summary>
    public class ModbusTcpRequest : ModbusRequest
    {
        public ushort TransactionId { get; set; }
    }

}