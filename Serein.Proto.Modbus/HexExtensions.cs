namespace Serein.Proto.Modbus
{
    public static class HexExtensions
    {
        public static string ToHexString(this byte[] data, string separator = " ")
        {
            if (data == null) return string.Empty;
            return BitConverter.ToString(data).Replace("-", separator);
        }
    }
}
