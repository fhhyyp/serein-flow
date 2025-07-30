using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Net.Sockets;
using System.Threading.Channels;


namespace Serein.Proto.Modbus
{
    /// <summary>
    /// Modbus TCP 客户端
    /// </summary>
    public class ModbusTcpClient : IModbusClient
    {
        /// <summary>
        /// 消息发送时触发的事件
        /// </summary>
        public Action<byte[]> OnTx { get; set; }

        /// <summary>
        /// 接收到消息时触发的事件
        /// </summary>
        public Action<byte[]> OnRx { get; set; }

        /// <summary>
        /// 消息通道
        /// </summary>
        private readonly Channel<ModbusTcpRequest> _channel = Channel.CreateUnbounded<ModbusTcpRequest>();
        /// <summary>
        /// TCP客户端
        /// </summary>
        private readonly TcpClient _tcpClient;
        /// <summary>
        /// TCP客户端的网络流，用于发送和接收数据
        /// </summary>
        private readonly NetworkStream _stream;
        /// <summary>
        /// 存储未完成请求的字典，键为事务ID，值为任务完成源
        /// </summary>
        private readonly ConcurrentDictionary<ushort, TaskCompletionSource<byte[]>> _pendingRequests = new();
        /// <summary>
        /// 事务ID计数器，用于生成唯一的事务ID
        /// </summary>
        private int _transactionId = 0;

#pragma warning disable CS8618 // 在退出构造函数时，不可为 null 的字段必须包含非 null 值。请考虑添加 "required" 修饰符或声明为可为 null。
        public ModbusTcpClient(string host, int port = 502)
#pragma warning restore CS8618 // 在退出构造函数时，不可为 null 的字段必须包含非 null 值。请考虑添加 "required" 修饰符或声明为可为 null。
        {
            _tcpClient = new TcpClient();
            _tcpClient.Connect(host, port);
            _stream = _tcpClient.GetStream();

            _ = ProcessQueueAsync(); // 启动后台消费者
            _ = ReceiveLoopAsync();  // 启动接收响应线程
        }

        #region 功能码封装

        /// <summary>
        /// 读取线圈状态
        /// </summary>
        /// <param name="startAddress"></param>
        /// <param name="quantity"></param>
        /// <returns></returns>
        public async Task<bool[]> ReadCoils(ushort startAddress, ushort quantity)
        {
            var pdu = BuildReadPdu(startAddress, quantity);
            var responsePdu = await SendAsync(ModbusFunctionCode.ReadCoils, pdu);
            return ParseDiscreteBits(responsePdu, quantity);
        }

        /// <summary>
        /// 读取离散输入状态
        /// </summary>
        /// <param name="startAddress"></param>
        /// <param name="quantity"></param>
        /// <returns></returns>
        public async Task<bool[]> ReadDiscreteInputs(ushort startAddress, ushort quantity)
        {
            var pdu = BuildReadPdu(startAddress, quantity);
            var responsePdu = await SendAsync(ModbusFunctionCode.ReadDiscreteInputs, pdu);
            return ParseDiscreteBits(responsePdu, quantity);
        }

        /// <summary>
        /// 读取保持寄存器
        /// </summary>
        /// <param name="startAddress"></param>
        /// <param name="quantity"></param>
        /// <returns></returns>
        public async Task<ushort[]> ReadHoldingRegisters(ushort startAddress, ushort quantity)
        {
            var pdu = BuildReadPdu(startAddress, quantity);
            var responsePdu = await SendAsync(ModbusFunctionCode.ReadHoldingRegisters, pdu);
            return ParseRegisters(responsePdu, quantity);
        }

        /// <summary>
        /// 读取输入寄存器
        /// </summary>
        /// <param name="startAddress"></param>
        /// <param name="quantity"></param>
        /// <returns></returns>
        public async Task<ushort[]> ReadInputRegisters(ushort startAddress, ushort quantity)
        {
            var pdu = BuildReadPdu(startAddress, quantity);
            var responsePdu = await SendAsync(ModbusFunctionCode.ReadInputRegisters, pdu);
            return ParseRegisters(responsePdu, quantity);
        }

        /// <summary>
        /// 写单个线圈
        /// </summary>
        /// <param name="address"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        public async Task WriteSingleCoil(ushort address, bool value)
        {
            var pdu = new byte[]
            {
            (byte)(address >> 8), // 地址高字节
            (byte)(address & 0xFF),  // 地址低字节
            value ? (byte)0xFF : (byte)0x00,  // 线圈值高字节，高电平为0xFF，低电平为00
            0x00 // 线圈值低字节
            };
            await SendAsync(ModbusFunctionCode.WriteSingleCoil, pdu);
        }

        /// <summary>
        /// 写单个寄存器
        /// </summary>
        /// <param name="address"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        public async Task WriteSingleRegister(ushort address, ushort value)
        {
            var pdu = new byte[]
            {
            (byte)(address >> 8), // 地址高字节
            (byte)(address & 0xFF), // 地址低字节
            (byte)(value >> 8), // 寄存器值高字节
            (byte)(value & 0xFF) // 寄存器值低字节
            };
            await SendAsync(ModbusFunctionCode.WriteSingleRegister, pdu);
        }

        /// <summary>
        /// 写多个线圈
        /// </summary>
        /// <param name="startAddress"></param>
        /// <param name="values"></param>
        /// <returns></returns>
        public async Task WriteMultipleCoils(ushort startAddress, bool[] values)
        {
            int byteCount = (values.Length + 7) / 8; // 计算需要的字节数
            byte[] data = new byte[byteCount];

            for (int i = 0; i < values.Length; i++)
            {
                if (values[i])
                    data[i / 8] |= (byte)(1 << (i % 8)); // 设置对应位
            }

            var pdu = new List<byte>
        {
            (byte)(startAddress >> 8), // 地址高字节
            (byte)(startAddress & 0xFF), // 地址低字节
            (byte)(values.Length >> 8),  // 数量高字节
            (byte)(values.Length & 0xFF), // 数量低字节
            (byte)data.Length // 字节数
        };

            pdu.AddRange(data);

            await SendAsync(ModbusFunctionCode.WriteMultipleCoils, pdu.ToArray());
        }

        /// <summary>
        /// 写多个寄存器
        /// </summary>
        /// <param name="startAddress"></param>
        /// <param name="values"></param>
        /// <returns></returns>
        public async Task WriteMultipleRegisters(ushort startAddress, ushort[] values)
        {
            var pdu = new List<byte>
    {
            (byte)(startAddress >> 8), // 地址高字节
            (byte)(startAddress & 0xFF), // 地址低字节
            (byte)(values.Length >> 8), // 数量高字节
            (byte)(values.Length & 0xFF), // 数量低字节
            (byte)(values.Length * 2) // 字节数
        };

            foreach (var val in values)
            {
                pdu.Add((byte)(val >> 8)); // 寄存器值高字节
                pdu.Add((byte)(val & 0xFF)); // 寄存器值低字节
            }

            await SendAsync(ModbusFunctionCode.WriteMultipleRegister, pdu.ToArray());
        }

        /// <summary>
        /// 构建读取PDU数据
        /// </summary>
        /// <param name="startAddress"></param>
        /// <param name="quantity"></param>
        /// <returns></returns>
        private byte[] BuildReadPdu(ushort startAddress, ushort quantity)
        {
            byte[] buffer = new byte[4];
            BinaryPrimitives.WriteUInt16BigEndian(buffer.AsSpan(0, 2), startAddress); // 起始地址高低字节
            BinaryPrimitives.WriteUInt16BigEndian(buffer.AsSpan(2, 2), quantity); // 读取数量高低字节
            return buffer;
        }


        /// <summary>
        /// 解析离散位数据
        /// </summary>
        /// <param name="pdu"></param>
        /// <param name="count"></param>
        /// <returns></returns>
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

        /// <summary>
        /// 解析寄存器数据
        /// </summary>
        /// <param name="pdu"></param>
        /// <param name="count"></param>
        /// <returns></returns>
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

        /// <summary>
        /// 处理消息队列，发送请求到服务器
        /// </summary>
        /// <returns></returns>
        private async Task ProcessQueueAsync()
        {
            while (_tcpClient.Connected)
            {
                var request = await _channel.Reader.ReadAsync();
                if (request.PDU is null)
                {
                    request.Completion?.TrySetCanceled();
                    continue;
                }
                byte[] packet = BuildPacket(request.TransactionId, 0x01, (byte)request.FunctionCode, request.PDU);
                OnTx?.Invoke(packet); // 触发发送日志
                await _stream.WriteAsync(packet, 0, packet.Length);
                await _stream.FlushAsync();
            }

        }

        /// <summary>
        /// 发送请求并等待响应
        /// </summary>
        /// <param name="functionCode">功能码</param>
        /// <param name="pdu">内容</param>
        /// <param name="timeout">超时时间</param>
        /// <param name="maxRetries">最大重发次数</param>
        /// <returns></returns>
        /// <exception cref="TimeoutException"></exception>
        public Task<byte[]> SendAsync(ModbusFunctionCode functionCode, byte[] pdu)
        {
            int id = Interlocked.Increment(ref _transactionId);
            var transactionId = (ushort)(id % ushort.MaxValue); // 0~65535 循环
            var tcs = new TaskCompletionSource<byte[]>(TaskCreationOptions.RunContinuationsAsynchronously);

            var request = new ModbusTcpRequest
            {
                TransactionId = transactionId,
                FunctionCode = functionCode,
                PDU = pdu,
                Completion = tcs
            };
            _pendingRequests[transactionId] = tcs;
            _channel.Writer.TryWrite(request);

            return tcs.Task;
        }

        /// <summary>
        /// 接收数据循环
        /// </summary>
        /// <returns></returns>
        private async Task ReceiveLoopAsync()
        {
            var buffer = new byte[1024];
            while (true)
            {
                int len = await _stream.ReadAsync(buffer, 0, buffer.Length).ConfigureAwait(false);
                //int len = await _stream.ReadAsync(buffer.AsMemory(0, buffer.Length)).ConfigureAwait(false);
                if (len == 0) return; // 连接关闭

                if (len < 6)
                {
                    Console.WriteLine("接收到的数据长度不足");
                    return;
                }



                ushort protocolId = BinaryPrimitives.ReadUInt16BigEndian(buffer.AsSpan(2, 2));
                if (protocolId != 0x0000)
                {
                    Console.WriteLine($"协议不匹配: {protocolId:X4}");
                    return;
                }

                ushort dataLength = BinaryPrimitives.ReadUInt16BigEndian(buffer.AsSpan(4, 2));
                // 检查数据长度是否合法
                if (dataLength > 253 || len < 6 + dataLength)
                {
                    Console.WriteLine($"数据长度异常: dataLength={dataLength}, 实际接收={len}");
                    return;
                }

                ushort transactionId = BinaryPrimitives.ReadUInt16BigEndian(buffer.AsSpan(0, 2));
                if (_pendingRequests.TryRemove(transactionId, out var tcs))
                {
                    var responsePdu = new ReadOnlySpan<byte>(buffer, 6, dataLength).ToArray();
                    if (OnRx is not null)
                    {
                        var packet = new ReadOnlySpan<byte>(buffer, 0, 6 + dataLength).ToArray();
                        OnRx?.Invoke(packet); // 触发接收日志
                    }

                    // 检查是否异常响应
                    if ((responsePdu[1] & 0x80) != 0)
                    {
                        byte exceptionCode = responsePdu[2];
                        tcs.SetException(new ModbusException(responsePdu[1], exceptionCode));
                    }
                    else
                    {
                        tcs.SetResult(responsePdu);
                    }
                }
                else
                {
                    Console.WriteLine($"未匹配到 TransactionId={transactionId} 的请求");
                }

            }
        }

        /// <summary>
        /// 构造 Modbus Tcp 报文
        /// </summary>
        /// <param name="transactionId"></param>
        /// <param name="unitId"></param>
        /// <param name="functionCode"></param>
        /// <param name="pduData"></param>
        /// <returns></returns>
        private byte[] BuildPacket(ushort transactionId, byte unitId, byte functionCode, byte[] pduData)
        {
            int pduLength = 1 + pduData.Length; // PDU 长度 = 功能码1字节 + 数据长度
            int totalLength = 7 + pduLength;    // MBAP头长度 = 7字节 + PDU长度

            Span<byte> packet = totalLength <= 256 ? stackalloc byte[totalLength] : new byte[totalLength];

            // 写入事务ID（大端序）
            packet[0] = (byte)(transactionId >> 8);
            packet[1] = (byte)(transactionId);

            // 协议ID（固定0x0000）
            packet[2] = 0;
            packet[3] = 0;

            // 长度（PDU长度 + 1字节UnitID）
            ushort length = (ushort)(pduLength + 1);
            packet[4] = (byte)(length >> 8);
            packet[5] = (byte)(length);

            // UnitID & 功能码
            packet[6] = unitId;
            packet[7] = functionCode;

            // 复制PDU数据
            pduData.AsSpan().CopyTo(packet.Slice(8));

            return packet.ToArray();
        }



        public void Dispose()
        {
            var tcs = _pendingRequests.Values.ToArray();
            foreach (var pending in tcs)
            {
                pending.TrySetCanceled();
            }
            _stream?.Close();
            _tcpClient?.Close();
        }
    }


}
