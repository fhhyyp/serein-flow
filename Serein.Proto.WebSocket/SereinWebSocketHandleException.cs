using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;

namespace Serein.Proto.WebSocket
{
    /// <summary>
    /// WebSocket处理异常
    /// </summary>
    [Serializable]
    internal sealed class SereinWebSocketHandleException : Exception
    {
        public SereinWebSocketHandleException()
        {
        }

        public SereinWebSocketHandleException(string message)
            : base(message)
        {
        }

        public SereinWebSocketHandleException(string message, Exception innerException)
            : base(message, innerException)
        {
        }

        private SereinWebSocketHandleException(SerializationInfo info, StreamingContext context): base(info, context)
        {
        }
    }
}
