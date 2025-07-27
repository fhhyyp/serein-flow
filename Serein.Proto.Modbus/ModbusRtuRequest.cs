namespace Serein.Proto.Modbus
{
    /// <summary>
    /// Modbus RTU 请求实体（串口模式下无效）
    /// </summary>
    public sealed class ModbusRtuRequest : ModbusRequest
    {
        /// <summary>
        /// 从站地址（1~247）
        /// </summary>
        public byte SlaveAddress { get; set; }
    }

}

