using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Serein.Library.Network.Modbus
{
    /// <summary>
    /// Modbus 功能码枚举
    /// </summary>
    public enum ModbusFunctionCode
    {
        /// <summary>
        /// 读线圈
        /// </summary>
        ReadCoils = 0x01,
        /// <summary>
        /// 读离散输入
        /// </summary>
        ReadDiscreteInputs = 0x02,
        /// <summary>
        /// 读保持寄存器
        /// </summary>
        ReadHoldingRegisters = 0x03,
        /// <summary>
        /// 读输入寄存器
        /// </summary>
        ReadInputRegisters = 0x04,
        /// <summary>
        /// 写单个线圈
        /// </summary>
        WriteSingleCoil = 0x05,
        /// <summary>
        /// 写单个寄存器
        /// </summary>
        WriteSingleRegister = 0x06,
        /// <summary>
        /// 写多个线圈
        /// </summary>
        WriteMultipleCoils = 0x0F,
        /// <summary>
        /// 写多个寄存器
        /// </summary>
        WriteMultipleRegister = 0x10,
    }
}
