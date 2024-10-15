using Serein.Library.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Serein.NodeFlow
{
    public static class NodeStaticConfig
    {
        /// <summary>
        /// 节点的命名空间
        /// </summary>
        public const string NodeSpaceName = $"{nameof(Serein)}.{nameof(Serein.NodeFlow)}.{nameof(Serein.NodeFlow.Model)}";

        public static readonly ConnectionType[] ConnectionTypes = [
                                                        ConnectionType.Upstream,
                                                        ConnectionType.IsSucceed,
                                                        ConnectionType.IsFail,
                                                        ConnectionType.IsError];
    }
}
