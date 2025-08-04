using Serein.Library.Api;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Serein.Library
{
    /// <summary>
    /// 流程调用树，管理所有的调用节点
    /// </summary>
    public class FlowCallTree : IFlowCallTree
    {

        private readonly SortedDictionary<string, CallNode> _callNodes = new SortedDictionary<string,CallNode>();

        /// <inheritdoc/>
        public List<CallNode> StartNodes { get; set; }

        /// <inheritdoc/>
        public List<CallNode> GlobalFlipflopNodes { get; set; }


        /// <summary>
        /// 索引器，允许通过字符串索引访问CallNode
        /// </summary>
        /// <param name="index"></param>
        /// <returns></returns>
        public CallNode this[string index]
        {
            get
            {
                _callNodes.TryGetValue(index, out CallNode callNode);
                return callNode;
            }
            set
            {
                // 设置指定索引的值
                _callNodes.Add(index, value);
            }
        }

       

        /// <summary>
        /// 添加一个调用节点到流程调用树中
        /// </summary>
        /// <param name="nodeGuid"></param>
        /// <param name="action"></param>
        public void AddCallNode(string nodeGuid, Action<IFlowContext> action)
        {
            var node = new CallNode(nodeGuid, action);
            _callNodes[nodeGuid] = node;
        }

        /// <summary>
        /// 添加一个调用节点到流程调用树中，使用异步函数
        /// </summary>
        /// <param name="nodeGuid"></param>
        /// <param name="func"></param>
        public void AddCallNode(string nodeGuid, Func<IFlowContext, Task> func)
        {
            var node = new CallNode(nodeGuid, func);
            _callNodes[nodeGuid] = node;
        }

        /// <summary>
        /// 获取指定Key的CallNode，如果不存在则返回null
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        public CallNode Get(string key)
        {
            return _callNodes.TryGetValue(key, out CallNode callNode) ? callNode : null;
        }
    }
}
