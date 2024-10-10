using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using System.Net.WebSockets;

namespace Serein.Library.Network.WebSocketCommunication
{
    [AttributeUsage(AttributeTargets.Method)]
    public sealed class AutoSocketHandleAttribute : Attribute
    {
        public string ThemeValue;
        //public Type DataType;
    }
    [AttributeUsage(AttributeTargets.Class)]
    public sealed class AutoSocketModuleAttribute : Attribute
    {
        public string JsonDataField;
        public string JsonThemeField;
    }

}
