using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Serein.Library.Network.Modbus
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
