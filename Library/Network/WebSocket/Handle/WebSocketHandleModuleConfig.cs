using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Serein.Library.Network.WebSocketCommunication.Handle
{
    /// <summary>
    /// 远程环境配置
    /// </summary>
    public class WebSocketHandleModuleConfig
    {
        /// <summary>
        /// 有关消息ID的 Json Key
        /// </summary>
        public string MsgIdJsonKey { get; set; }
        /// <summary>
        /// 有关消息主题的 Json Key
        /// </summary>
        public string ThemeJsonKey { get; set; }
        /// <summary>
        /// 有关数据的 Json Key
        /// </summary>
        public string DataJsonKey { get; set; }
    }

}
