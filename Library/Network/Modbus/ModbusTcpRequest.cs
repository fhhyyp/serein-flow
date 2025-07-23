using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Serein.Library.Network.Modbus
{
    /// <summary>
    /// Modbus TCP 请求实体
    /// </summary>
    public class ModbusTcpRequest
    {
        /// <summary>
        /// 事务ID
        /// </summary>
        public ushort TransactionId { get; set; }
        /// <summary>
        /// 功能码
        /// </summary>
        public ModbusFunctionCode FunctionCode { get; set; }
        /// <summary>
        /// PDU 数据
        /// </summary>
        public byte[] PDU { get; set; }
        /// <summary>
        /// 请求的完成源，用于异步等待响应
        /// </summary>
        public TaskCompletionSource<byte[]> Completion { get; set; }
    }

}
