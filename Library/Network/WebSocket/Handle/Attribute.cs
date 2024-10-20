using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Serein.Library.Network.WebSocketCommunication.Handle
{
    /// <summary>
    /// 表示参数可以为空(Net462不能使用NutNull的情况）
    /// </summary>
    public sealed class NeedfulAttribute : Attribute
    {
    }
}
