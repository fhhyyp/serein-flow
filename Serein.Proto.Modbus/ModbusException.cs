namespace Serein.Proto.Modbus
{
    public class ModbusException : Exception
    {
        public byte FunctionCode { get; }
        public byte ExceptionCode { get; }

        public ModbusException(byte functionCode, byte exceptionCode) 
            : base($"Modbus异常码=0x{functionCode:X2}，0x{exceptionCode:X2}（{GetExceptionMessage(exceptionCode)})")
        {
            FunctionCode = functionCode;
            ExceptionCode = exceptionCode;
        }

        private static string GetExceptionMessage(byte code) => code switch
        {
            0x01 => "非法功能。确认功能码是否被目标设备支持；检查设备固件版本是否过低；修改主站请求为设备支持的功能码", // 功能码错误
            0x02 => "非法数据地址。检查主站请求的寄存器地址和长度是否越界；确保设备配置的寄存器数量正确", // 数据地址错误
            0x03 => "非法数据值。检查写入的数值是否在设备支持的范围内；核对协议文档中对应寄存器的取值要求", // 数据值错误
            0x04 => "从站设备故障。检查设备运行状态和日志；尝试重启设备；排查硬件或内部程序错误", // 从设备故障
            0x05 => "确认。主站需通过轮询或延时机制等待处理完成，再次查询结果", // 确认
            0x06 => "从站设备忙。增加请求重试延时；避免高频率发送编程指令", // 从设备忙
            0x08 => "存储奇偶性差错。尝试重新发送请求；如错误持续出现，检查存储器硬件或文件一致性", // 内存奇偶校验错误
            0x0A => "不可用网关路径。检查网关配置和负载；确认目标设备的网络连接可用性", // 网关路径不可用
            0x0B => "网关目标设备响应失败。检查目标设备是否在线；检查网关的路由配置与网络连接", // 网关目标设备未响应
            _ => $"未知错误" // 未知错误
        };
    }
}
