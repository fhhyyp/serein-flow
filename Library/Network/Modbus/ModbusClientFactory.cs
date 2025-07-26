using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Serein.Library.Network.Modbus
{
    public static class ModbusClientFactory
    {
        private static readonly char[] separator = new[] { ':' };

        /// <summary>
        /// 创建 Modbus 客户端实例
        /// </summary>
        /// <param name="connectionString">
        /// 连接字符串格式：
        /// TCP示例："tcp:192.168.1.100:502"
        /// UCP示例："ucp:192.168.1.100:502"
        /// RTU示例："rtu:COM3:9600:1" （格式：rtu:串口名:波特率:从站地址）
        /// </param>
        public static IModbusClient Create(string connectionString)
        {
            if (string.IsNullOrWhiteSpace(connectionString))
                throw new ArgumentException("connectionString 不能为空");
            var parts = connectionString.Split(separator, StringSplitOptions.RemoveEmptyEntries);
            //var parts = connectionString.Split(':',options: StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 2)
                throw new ArgumentException("connectionString 格式错误");

            var protocol = parts[0].ToLower();

            if (protocol == "tcp")
            {
                // tcp:host:port
                if (parts.Length < 3)
                    throw new ArgumentException("TCP格式应为 tcp:host:port");

                string host = parts[1];
                if (!int.TryParse(parts[2], out int port))
                    port = 502; // 默认端口

                return new ModbusTcpClient(host, port);
            }
            else if (protocol == "ucp")
            {
                // ucp:host:port
                if (parts.Length < 3)
                    throw new ArgumentException("TCP格式应为 tcp:host:port");

                string host = parts[1];
                if (!int.TryParse(parts[2], out int port))
                    port = 502; // 默认端口

                return new ModbusUdpClient(host, port);
            }
            else if (protocol == "rtu")
            {
                // rtu:portName:baudRate:slaveId
                if (parts.Length < 4)
                    throw new ArgumentException("RTU格式应为 rtu:portName:baudRate:slaveId");

                string portName = parts[1];
                if (!int.TryParse(parts[2], out int baudRate))
                    baudRate = 9600;

                if (!byte.TryParse(parts[3], out byte slaveId))
                    slaveId = 1;

                return new ModbusRtuClient(portName, baudRate, slaveId: slaveId);
            }
            else
            {
                throw new NotSupportedException($"不支持的协议类型: {protocol}");
            }
        }


        /// <summary>
        /// 创建 Modbus TCP 客户端
        /// </summary>
        /// <param name="host">服务器地址</param>
        /// <param name="port">端口，默认502</param>
        public static ModbusTcpClient CreateTcpClient(string host, int port = 502)
        {
            return new ModbusTcpClient(host, port);
        }

        /// <summary>
        /// 创建 Modbus TCP 客户端
        /// </summary>
        /// <param name="host">服务器地址</param>
        /// <param name="port">端口，默认502</param>
        public static ModbusUdpClient CreateUdpClient(string host, int port = 502)
        {
            return new ModbusUdpClient(host, port);
        }

        /// <summary>
        /// 创建 Modbus RTU 客户端
        /// </summary>
        /// <param name="portName">串口名，比如 "COM3"</param>
        /// <param name="baudRate">波特率，默认9600</param>
        /// <param name="parity">校验，默认None</param>
        /// <param name="dataBits">数据位，默认8</param>
        /// <param name="stopBits">停止位，默认1</param>
        /// <param name="slaveId">从站地址，默认1</param>
        public static ModbusRtuClient CreateRtuClient(string portName,
            int baudRate = 9600,
            Parity parity = Parity.None,
            int dataBits = 8,
            StopBits stopBits = StopBits.One,
            byte slaveId = 1)
        {
            return new ModbusRtuClient(portName, baudRate, parity, dataBits, stopBits, slaveId);
        }
    }
}
