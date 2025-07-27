namespace Serein.Proto.Modbus
{
    /// <summary>
    /// Modbus 客户端通用接口 (TCP/RTU 通用)
    /// </summary>
    public interface IModbusClient : IDisposable
    {
        /// <summary>
        /// 报文发送时
        /// </summary>
        Action<byte[]> OnTx { get; set; }

        /// <summary>
        /// 接收到报文时
        /// </summary>
        Action<byte[]> OnRx { get; set; }

        /// <summary>
        /// 读取线圈状态 (0x01)
        /// </summary>
        Task<bool[]> ReadCoils(ushort startAddress, ushort quantity);

        /// <summary>
        /// 读取离散输入状态 (0x02)
        /// </summary>
        Task<bool[]> ReadDiscreteInputs(ushort startAddress, ushort quantity);

        /// <summary>
        /// 读取保持寄存器 (0x03)
        /// </summary>
        Task<ushort[]> ReadHoldingRegisters(ushort startAddress, ushort quantity);

        /// <summary>
        /// 读取输入寄存器 (0x04)
        /// </summary>
        Task<ushort[]> ReadInputRegisters(ushort startAddress, ushort quantity);

        /// <summary>
        /// 写单个线圈 (0x05)
        /// </summary>
        Task WriteSingleCoil(ushort address, bool value);

        /// <summary>
        /// 写单个寄存器 (0x06)
        /// </summary>
        Task WriteSingleRegister(ushort address, ushort value);

        /// <summary>
        /// 写多个线圈 (0x0F)
        /// </summary>
        Task WriteMultipleCoils(ushort startAddress, bool[] values);

        /// <summary>
        /// 写多个寄存器 (0x10)
        /// </summary>
        Task WriteMultipleRegisters(ushort startAddress, ushort[] values);
    }
}
