using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Threading.Channels;

namespace Serein.Proto.Modbus
{
    public class ModbusUdpClient : IModbusClient
    {
        /// <summary>
        /// 消息发送时触发的事件
        /// </summary>
        public Action<byte[]> OnTx { get; set; }

        /// <summary>
        /// 接收到消息时触发的事件
        /// </summary>
        public Action<byte[]> OnRx { get; set; }

        private readonly Channel<ModbusTcpRequest> _channel = Channel.CreateUnbounded<ModbusTcpRequest>();
        private readonly UdpClient _udpClient;
        private readonly IPEndPoint _remoteEndPoint;
        private readonly ConcurrentDictionary<ushort, TaskCompletionSource<byte[]>> _pendingRequests = new();
        private int _transactionId = 0;

#pragma warning disable CS8618 // 在退出构造函数时，不可为 null 的字段必须包含非 null 值。请考虑添加 "required" 修饰符或声明为可为 null。
        public ModbusUdpClient(string host, int port = 502)
#pragma warning restore CS8618 // 在退出构造函数时，不可为 null 的字段必须包含非 null 值。请考虑添加 "required" 修饰符或声明为可为 null。
        {
            _remoteEndPoint = new IPEndPoint(IPAddress.Parse(host), port);
            _udpClient = new UdpClient();
            _udpClient.Connect(_remoteEndPoint);

            _ = ProcessQueueAsync();
            _ = ReceiveLoopAsync();
        }

        #region 功能码封装
        public async Task<bool[]> ReadCoils(ushort startAddress, ushort quantity)
        {
            var pdu = BuildReadPdu(startAddress, quantity);
            var responsePdu = await SendAsync(ModbusFunctionCode.ReadCoils, pdu);
            return ParseDiscreteBits(responsePdu, quantity);
        }

        public async Task<bool[]> ReadDiscreteInputs(ushort startAddress, ushort quantity)
        {
            var pdu = BuildReadPdu(startAddress, quantity);
            var responsePdu = await SendAsync(ModbusFunctionCode.ReadDiscreteInputs, pdu);
            return ParseDiscreteBits(responsePdu, quantity);
        }

        public async Task<ushort[]> ReadHoldingRegisters(ushort startAddress, ushort quantity)
        {
            var pdu = BuildReadPdu(startAddress, quantity);
            var responsePdu = await SendAsync(ModbusFunctionCode.ReadHoldingRegisters, pdu);
            return ParseRegisters(responsePdu, quantity);
        }

        public async Task<ushort[]> ReadInputRegisters(ushort startAddress, ushort quantity)
        {
            var pdu = BuildReadPdu(startAddress, quantity);
            var responsePdu = await SendAsync(ModbusFunctionCode.ReadInputRegisters, pdu);
            return ParseRegisters(responsePdu, quantity);
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
            int byteCount = (values.Length + 7) / 8;
            byte[] data = new byte[byteCount];

            for (int i = 0; i < values.Length; i++)
            {
                if (values[i])
                    data[i / 8] |= (byte)(1 << (i % 8));
            }

            var pdu = new List<byte>
        {
            (byte)(startAddress >> 8),
            (byte)(startAddress & 0xFF),
            (byte)(values.Length >> 8),
            (byte)(values.Length & 0xFF),
            (byte)data.Length
        };
            pdu.AddRange(data);

            await SendAsync(ModbusFunctionCode.WriteMultipleCoils, pdu.ToArray());
        }

        public async Task WriteMultipleRegisters(ushort startAddress, ushort[] values)
        {
            var pdu = new List<byte>
        {
            (byte)(startAddress >> 8),
            (byte)(startAddress & 0xFF),
            (byte)(values.Length >> 8),
            (byte)(values.Length & 0xFF),
            (byte)(values.Length * 2)
        };

            foreach (var val in values)
            {
                pdu.Add((byte)(val >> 8));
                pdu.Add((byte)(val & 0xFF));
            }

            await SendAsync(ModbusFunctionCode.WriteMultipleRegister, pdu.ToArray());
        }
        #endregion

        #region 核心通信

        public Task<byte[]> SendAsync(ModbusFunctionCode functionCode, byte[] pdu)
        {
            int id = Interlocked.Increment(ref _transactionId);
            var transactionId = (ushort)(id % ushort.MaxValue);
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
        /// 处理发送队列的异步方法
        /// </summary>
        /// <returns></returns>
        private async Task ProcessQueueAsync()
        {
            while (true)
            {
                var request = await _channel.Reader.ReadAsync();
                if(request.PDU is null)
                {
                    request.Completion?.TrySetCanceled();
                    continue;
                }
                byte[] packet = BuildPacket(request.TransactionId, 0x01, (byte)request.FunctionCode, request.PDU);
                OnTx?.Invoke(packet);
                await _udpClient.SendAsync(packet, packet.Length);
            }
        }

        private async Task ReceiveLoopAsync()
        {
            while (true)
            {
                UdpReceiveResult result = await _udpClient.ReceiveAsync();
                var buffer = result.Buffer;

                if (buffer.Length < 6) continue;

                ushort transactionId = BinaryPrimitives.ReadUInt16BigEndian(buffer.AsSpan(0, 2));
                if (_pendingRequests.TryRemove(transactionId, out var tcs))
                {
                    OnRx?.Invoke(buffer);
                    var responsePdu = new ReadOnlySpan<byte>(buffer, 6, buffer.Length - 6).ToArray();

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
            }
        }

        private byte[] BuildPacket(ushort transactionId, byte unitId, byte functionCode, byte[] pduData)
        {
            int pduLength = 1 + pduData.Length;
            int totalLength = 7 + pduLength;

            Span<byte> packet = totalLength <= 256 ? stackalloc byte[totalLength] : new byte[totalLength];
            packet[0] = (byte)(transactionId >> 8);
            packet[1] = (byte)(transactionId);
            packet[2] = 0; packet[3] = 0;
            ushort length = (ushort)(pduLength + 1);
            packet[4] = (byte)(length >> 8);
            packet[5] = (byte)(length);
            packet[6] = unitId;
            packet[7] = functionCode;
            pduData.AsSpan().CopyTo(packet.Slice(8));
            return packet.ToArray();
        }
        #endregion

        private byte[] BuildReadPdu(ushort startAddress, ushort quantity)
        {
            byte[] buffer = new byte[4];
            BinaryPrimitives.WriteUInt16BigEndian(buffer.AsSpan(0, 2), startAddress);
            BinaryPrimitives.WriteUInt16BigEndian(buffer.AsSpan(2, 2), quantity);
            return buffer;
        }

        private bool[] ParseDiscreteBits(byte[] pdu, ushort count)
        {
            var result = new bool[count];
            int byteCount = pdu[2];
            int dataIndex = 3;

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
            int dataStart = 3;

            for (int i = 0; i < count; i++)
            {
                int offset = dataStart + i * 2;
                result[i] = (ushort)((pdu[offset] << 8) | pdu[offset + 1]);
            }
            return result;
        }

        public void Dispose()
        {
            foreach (var tcs in _pendingRequests.Values)
                tcs.TrySetCanceled();

            _udpClient?.Dispose();
        }
    }
}
