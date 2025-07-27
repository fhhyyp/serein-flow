using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using System.Net.WebSockets;
using Serein.Library;



namespace Serein.Proto.WebSocket.Attributes
{

    /// <summary>
    /// 使用 DataKey 整体数据
    /// </summary>
    [AttributeUsage(AttributeTargets.Parameter)]
    public sealed class UseDataAttribute : Attribute
    {
    }


}
