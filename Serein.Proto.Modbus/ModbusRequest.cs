namespace Serein.Proto.Modbus
{
    public class ModbusRequest
    {
        /// <summary>
        /// 功能码
        /// </summary>
        public ModbusFunctionCode FunctionCode { get; set; }

        /// <summary>
        /// PDU (Protocol Data Unit) 数据，不包括从站地址和CRC
        /// </summary>
        public byte[]? PDU { get; set; }

        /// <summary>
        /// 异步任务完成源，用于等待响应
        /// </summary>
        public TaskCompletionSource<byte[]>? Completion { get; set; }
    }

}
