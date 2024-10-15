﻿using Serein.Library.Api;
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
        /// 全局触发器CTS
        /// </summary>
        public const string FlipFlopCtsName = "<>.FlowFlipFlopCts";
        /// <summary>
        /// 流程运行CTS
        /// </summary>
        public const string FlowRungCtsName = "<>.FlowRungCtsName";


        /// <summary>
        /// 节点的命名空间
        /// </summary>
        //public const string NodeSpaceName = $"{nameof(Serein)}.{nameof(Serein.NodeFlow)}.{nameof(Serein.NodeFlow.Model)}";
        public const string NodeSpaceName = "Serein.NodeFlow.Model";


        /// <summary>
        /// 节点连接关系种类
        /// </summary>
        public static readonly ConnectionType[] ConnectionTypes = new ConnectionType[]
        {
             ConnectionType.Upstream,
              ConnectionType.IsSucceed,
              ConnectionType.IsFail,
              ConnectionType.IsError,
        };
    }
}