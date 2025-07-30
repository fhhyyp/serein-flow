using System.Buffers.Binary;
using System.IO.Ports;

namespace Serein.Proto.Modbus
{


    public class ModbusRtuClient : IModbusClient
    {
        /// <summary>
        /// 消息发送时触发的事件
        /// </summary>
        public Action<byte[]> OnTx { get; set; }

        /// <summary>
        /// 接收到消息时触发的事件
        /// </summary>
        public Action<byte[]> OnRx { get; set; }


        private readonly SerialPort _serialPort;
        private readonly SemaphoreSlim _requestLock = new SemaphoreSlim(1, 1);
        private readonly byte _slaveId;

        private readonly CancellationTokenSource _cts = new();


#pragma warning disable CS8618 // 在退出构造函数时，不可为 null 的字段必须包含非 null 值。请考虑添加 "required" 修饰符或声明为可为 null。
        public ModbusRtuClient(string portName, int baudRate = 9600, Parity parity = Parity.None, int dataBits = 8, StopBits stopBits = StopBits.One, byte slaveId = 1)
#pragma warning restore CS8618 // 在退出构造函数时，不可为 null 的字段必须包含非 null 值。请考虑添加 "required" 修饰符或声明为可为 null。
        {
            _slaveId = slaveId;
            _serialPort = new SerialPort(portName, baudRate, parity, dataBits, stopBits)
            {
                ReadTimeout = 1000,
                WriteTimeout = 1000
            };
            _serialPort.Open();
            
        }

        #region 功能码封装

        public async Task<bool[]> ReadCoils(ushort startAddress, ushort quantity)
        {
            var pdu = BuildReadPdu(startAddress, quantity);
            var response = await SendAsync(ModbusFunctionCode.ReadCoils, pdu);
            return ParseDiscreteBits(response, quantity);
        }

        public async Task<bool[]> ReadDiscreteInputs(ushort startAddress, ushort quantity)
        {
            var pdu = BuildReadPdu(startAddress, quantity);
            var response = await SendAsync(ModbusFunctionCode.ReadDiscreteInputs, pdu);
            return ParseDiscreteBits(response, quantity);
        }

        public async Task<ushort[]> ReadHoldingRegisters(ushort startAddress, ushort quantity)
        {
            var pdu = BuildReadPdu(startAddress, quantity);
            var response = await SendAsync(ModbusFunctionCode.ReadHoldingRegisters, pdu);
            return ParseRegisters(response, quantity);
        }

        public async Task<ushort[]> ReadInputRegisters(ushort startAddress, ushort quantity)
        {
            var pdu = BuildReadPdu(startAddress, quantity);
            var response = await SendAsync(ModbusFunctionCode.ReadInputRegisters, pdu);
            return ParseRegisters(response, quantity);
        }

        public async Task WriteSingleCoil(ushort address, bool value)
        {
            var pdu = new byte[]
            {
                (byte)(address >> 8),
                (byte)(address & 0xFF),
                value ? (byte)0xFF : (byte)0x00,
                0x00
            };
            await SendAsync(ModbusFunctionCode.WriteSingleCoil, pdu);
        }

        public async Task WriteSingleRegister(ushort address, ushort value)
        {
            var pdu = new byte[]
            {
                (byte)(address >> 8),
                (byte)(address & 0xFF),
                (byte)(value >> 8),
                (byte)(value & 0xFF)
            };
            await SendAsync(ModbusFunctionCode.WriteSingleRegister, pdu);
        }


        public async Task WriteMultipleCoils(ushort startAddress, bool[] values)
        {
            if (values == null || values.Length == 0)
                throw new ArgumentException("values 不能为空");

            int byteCount = (values.Length + 7) / 8; // 需要多少字节
            byte[] coilData = new byte[byteCount];

            for (int i = 0; i < values.Length; i++)
            {
                if (values[i])
                    coilData[i / 8] |= (byte)(1 << (i % 8)); // 设置对应位
            }

            var pdu = new List<byte>
            {
                (byte)(startAddress >> 8),      // 起始地址高字节
                (byte)(startAddress & 0xFF),    // 起始地址低字节
                (byte)(values.Length >> 8),     // 数量高字节
                (byte)(values.Length & 0xFF),   // 数量低字节
                (byte)coilData.Length           // 数据字节数
            };
            pdu.AddRange(coilData);

            await SendAsync(ModbusFunctionCode.WriteMultipleCoils, pdu.ToArray());
        }

        public async Task WriteMultipleRegisters(ushort startAddress, ushort[] values)
        {
            if (values == null || values.Length == 0)
                throw new ArgumentException("values 不能为空");

            var arrlen = 5 + values.Length * 2;
            var pdu = new byte[arrlen];

            pdu[0] = (byte)(startAddress >> 8);      // 起始地址高字节
            pdu[1] = (byte)(startAddress & 0xFF);    // 起始地址低字节
            pdu[2] = (byte)(values.Length >> 8);     // 寄存器数量高字节
            pdu[3] = (byte)(values.Length & 0xFF);   // 寄存器数量低字节
            pdu[4] = (byte)(values.Length * 2);       // 数据字节数

            // 添加寄存器数据（每个寄存器 2 字节：高字节在前）
            var index = 5;
            foreach(var val in values)
            {
                pdu[index++] = (byte)(val >> 8);
                pdu[index++] = (byte)(val & 0xFF);

            }

           /* var pdu = new List<byte>
            {
                (byte)(startAddress >> 8),      // 起始地址高字节
                (byte)(startAddress & 0xFF),    // 起始地址低字节
                (byte)(values.Length >> 8),     // 寄存器数量高字节
                (byte)(values.Length & 0xFF),   // 寄存器数量低字节
                (byte)(values.Length * 2)       // 数据字节数
            };

         
            foreach (var val in values)
            {
                pdu.Add((byte)(val >> 8));
                pdu.Add((byte)(val & 0xFF));
            }*/

            await SendAsync(ModbusFunctionCode.WriteMultipleRegister, pdu);
        }

        #endregion

        #region 核心通信

        public async Task<byte[]> SendAsync(ModbusFunctionCode functionCode, byte[] pdu)
        {
            await _requestLock.WaitAsync();
            try
            {
                // 构造 RTU 帧
                byte[] frame = BuildFrame(_slaveId, (byte)functionCode, pdu);
                OnTx?.Invoke(frame); // 触发发送日志
                await _serialPort.BaseStream.WriteAsync(frame, 0, frame.Length, _cts.Token);
                await _serialPort.BaseStream.FlushAsync(_cts.Token);

                // 接收响应
                var response = await ReceiveResponseAsync();
                OnRx?.Invoke(response); // 触发接收日志
                // 检查功能码是否异常响应
                if ((response[1] & 0x80) != 0)
                {
                    byte exceptionCode = response[2];
                    throw new ModbusException(response[1], exceptionCode);
                }


                return response;
            }
            finally
            {
                _requestLock.Release();
            } 
        }


        
        /// <summary>
        /// 接收响应
        /// </summary>
        private async Task<byte[]> ReceiveResponseAsync()
        {
            var buffer =  new byte[256];
            int offset = 0;

            while (true)
            {
                int read = await _serialPort.BaseStream.ReadAsync(buffer, offset, buffer.Length - offset, _cts.Token);
                offset += read;

                // 最小RTU帧：地址(1) + 功能码(1) + 数据(N) + CRC(2)
                if (offset >= 5)
                {
                    int frameLength = offset; 
                    if (!ValidateCrc(buffer, 0, frameLength))
                        throw new IOException("CRC 校验失败");

                    byte[] response = new byte[frameLength - 2];
                    Array.Copy(buffer, 0, response, 0, frameLength - 2);
                    return response;
                }
            }
        }

        private byte[] BuildFrame(byte slaveAddr, byte functionCode, byte[] pdu)
        {
            var frame = new byte[2 + pdu.Length + 2]; // 地址 + 功能码 + PDU + CRC
            frame[0] = slaveAddr;
            frame[1] = functionCode;
            Array.Copy(pdu, 0, frame, 2, pdu.Length);
            ushort crc = Crc16(frame, 0, frame.Length - 2);
            frame[frame.Length - 2] = (byte)(crc & 0xFF);
            frame[frame.Length - 1] = (byte)(crc >> 8);
            return frame;
        }

        private static bool ValidateCrc(byte[] buffer, int offset, int length)
        {
            ushort crcCalc = Crc16(buffer, offset, length - 2);
            ushort crcRecv = (ushort)(buffer[length - 2] | (buffer[length - 1] << 8));
            return crcCalc == crcRecv;
        }

        #endregion

        #region PDU与解析

        private byte[] BuildReadPdu(ushort startAddress, ushort quantity)
        {

            byte[] buffer = new byte[4];
            BinaryPrimitives.WriteUInt16BigEndian(buffer.AsSpan(0, 2), startAddress); // 起始地址高低字节
            BinaryPrimitives.WriteUInt16BigEndian(buffer.AsSpan(2, 2), quantity); // 读取数量高低字节
            return buffer;
        }

        private bool[] ParseDiscreteBits(byte[] pdu, ushort count)
        {
            int byteCount = pdu[2];        // 第2字节是后续的字节数量
            int dataIndex = 3;             // 数据从第3字节开始（0-based）

            var result = new bool[count];

            for (int i = 0, bytePos = 0, bitPos = 0; i < count; i++, bitPos++)
            {
                if (bitPos == 8)
                {
                    bitPos = 0;
                    bytePos++;
                }
                result[i] = ((pdu[dataIndex + bytePos] >> bitPos) & 0x01) != 0;
            }
            return result;
        }

        private ushort[] ParseRegisters(byte[] pdu, ushort count)
        {
            var result = new ushort[count];
            int dataStart = 3; // 数据从第3字节开始

            for (int i = 0; i < count; i++)
            {
                int offset = dataStart + i * 2;
                result[i] = (ushort)((pdu[offset] << 8) | pdu[offset + 1]);
            }

            return result;
        }

        #endregion

        #region CRC16
        private static ushort Crc16(byte[] data, int offset, int length)
        {
            const ushort polynomial = 0xA001;
            ushort crc = 0xFFFF;

            for (int i = offset; i < offset + length; i++)
            {
                crc ^= data[i];
                for (int j = 0; j < 8; j++)
                {
                    if ((crc & 0x0001) != 0)
                        crc = (ushort)((crc >> 1) ^ polynomial);
                    else
                        crc >>= 1;
                }
            }
            return crc;
        }
        #endregion

        public void Dispose()
        {
            _cts.Cancel();
            _serialPort?.Close();
        }


    }
}
