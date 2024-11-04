using Serein.Library.Api;
using Serein.Library.Utils;
using System.Collections.Concurrent;

namespace Serein.Library.Core
{

    /// <summary>
    /// 动态流程上下文
    /// </summary>
    public class DynamicContext : IDynamicContext
    {
        /// <summary>
        /// 动态流程上下文
        /// </summary>
        /// <param name="flowEnvironment"></param>
        public DynamicContext(IFlowEnvironment flowEnvironment)
        {
            Env = flowEnvironment;
            RunState = RunState.Running;
        }

        /// <summary>
        /// 运行环境
        /// </summary>
        public IFlowEnvironment Env { get; }

        /// <summary>
        /// 运行状态
        /// </summary>
        public RunState RunState { get; set; } = RunState.NoStart;

        /// <summary>
        /// 用来在当前流程上下文间传递数据
        /// </summary>
        public Dictionary<string, object> ContextShareData { get; } = new Dictionary<string, object>();

        /// <summary>
        /// 当前节点执行完成后，设置该属性，让运行环境判断接下来要执行哪个分支的节点。
        /// </summary>
        public ConnectionInvokeType NextOrientation { get; set; }

        /// <summary>
        /// 运行时异常信息
        /// </summary>
        public Exception ExceptionOfRuning { get; set; }

        /// <summary>
        /// 每个流程上下文分别存放节点的当前数据
        /// </summary>
        private readonly ConcurrentDictionary<string, object?> dictNodeFlowData = new ConcurrentDictionary<string, object?>();

        /// <summary>
        /// 每个流程上下文存储运行时节点的调用关系
        /// </summary>
        private readonly ConcurrentDictionary<NodeModelBase, NodeModelBase> dictPreviousNodes = new ConcurrentDictionary<NodeModelBase, NodeModelBase>();

        /// <summary>
        /// 设置运行时上一节点
        /// </summary>
        /// <param name="currentNodeModel">当前节点</param>
        /// <param name="PreviousNode">上一节点</param>
        public void SetPreviousNode(NodeModelBase currentNodeModel, NodeModelBase PreviousNode)
        {
            dictPreviousNodes.AddOrUpdate(currentNodeModel, (_) => PreviousNode, (_, _) => PreviousNode);
        }

        /// <summary>
        /// 获取当前节点的运行时上一节点
        /// </summary>
        /// <param name="currentNodeModel"></param>
        /// <returns></returns>
        public NodeModelBase GetPreviousNode(NodeModelBase currentNodeModel)
        {
            if (dictPreviousNodes.TryGetValue(currentNodeModel, out var node))
            {
                return node;
            }
            else
            {
                return null;
            }
        }


        /// <summary>
        /// 获取节点当前数据
        /// </summary>
        /// <param name="nodeGuid">节点</param>
        /// <returns></returns>
        public object? GetFlowData(string nodeGuid)
        {
            if (dictNodeFlowData.TryGetValue(nodeGuid, out var data))
            {
                return data;
            }
            else
            {
                return null;
            }
        }

        /// <summary>
        /// 添加或更新当前节点数据
        /// </summary>
        /// <param name="nodeGuid">节点</param>
        /// <param name="flowData">新的数据</param>
        public void AddOrUpdate(string nodeGuid, object? flowData)
        {
            // this.dictNodeFlowData.TryGetValue(nodeGuid, out var oldFlowData);
            dictNodeFlowData.AddOrUpdate(nodeGuid, _ => flowData, (_, _) => flowData);
        }

        /// <summary>
        /// 上一节点数据透传到下一节点
        /// </summary>
        /// <param name="nodeModel"></param>
        public object? TransmissionData(NodeModelBase nodeModel)
        {
            if (dictPreviousNodes.TryGetValue(nodeModel, out var previousNode)) // 首先获取当前节点的上一节点
            {
                if (dictNodeFlowData.TryGetValue(previousNode.Guid, out var data)) // 其次获取上一节点的数据
                {
                    return data;
                    //AddOrUpdate(nodeModel.Guid, data); // 然后作为当前节点的数据记录在上下文中
                }
            }
            return null;
        }


        /// <summary>
        /// 结束流程
        /// </summary>
        public void Exit()
        {
            foreach (var nodeObj in dictNodeFlowData.Values)
            {
                if (nodeObj is not null)
                {
                    if (typeof(IDisposable).IsAssignableFrom(nodeObj?.GetType()) && nodeObj is IDisposable disposable)
                    {
                        disposable?.Dispose();
                    }
                }
                else 
                {

                }
            }
            foreach (var nodeObj in ContextShareData.Values)
            {
                if (nodeObj is null)
                {
                    continue;
                }
                else
                {
                    if (typeof(IDisposable).IsAssignableFrom(nodeObj?.GetType()) && nodeObj is IDisposable disposable)
                    {
                        disposable?.Dispose();
                    }
                }
            }
            this.dictNodeFlowData?.Clear();
            this.ContextShareData?.Clear();
            RunState = RunState.Completion;
        }


        private void Dispose(ref IDictionary<string, object>  keyValuePairs)
        {
            foreach (var nodeObj in keyValuePairs.Values)
            {
                if (nodeObj is null)
                {
                    continue;
                }

                if (nodeObj is IDisposable disposable) /* typeof(IDisposable).IsAssignableFrom(nodeObj?.GetType()) &&*/
                {
                    disposable?.Dispose();
                }
                else if (nodeObj is IDictionary<string, object> tmpDict)
                {
                    Dispose(ref tmpDict);
                }
                else if (nodeObj is ICollection<object> tmpList)
                {
                    Dispose(ref tmpList);
                }
                else if (nodeObj is IList<object> tmpList2)
                {
                    Dispose(ref tmpList2);
                }
            }
            keyValuePairs.Clear();
        }
        private void Dispose(ref ICollection<object> list)
        {
            foreach (var nodeObj in list)
            {
                if (nodeObj is null)
                {
                    continue;
                }

                if (nodeObj is IDisposable disposable) /* typeof(IDisposable).IsAssignableFrom(nodeObj?.GetType()) &&*/
                {
                    disposable?.Dispose();
                }
                else if (nodeObj is IDictionary<string, object> tmpDict)
                {
                    Dispose(ref tmpDict);
                }
                else if (nodeObj is ICollection<object> tmpList)
                {
                    Dispose(ref tmpList);
                }
                else if (nodeObj is IList<object> tmpList2)
                {
                    Dispose(ref tmpList2);
                }
            }

            list.Clear();
        }
        private void Dispose(ref IList<object> list)
        {
            foreach (var nodeObj in list)
            {
                if (nodeObj is null)
                {
                    continue;
                }

                if (nodeObj is IDisposable disposable) /* typeof(IDisposable).IsAssignableFrom(nodeObj?.GetType()) &&*/
                {
                    disposable?.Dispose();
                }
                else if (nodeObj is IDictionary<string, object> tmpDict)
                {
                    Dispose(ref tmpDict);
                }
                else if (nodeObj is ICollection<object> tmpList)
                {
                    Dispose(ref tmpList);
                }
                else if (nodeObj is IList<object> tmpList2)
                {
                    Dispose(ref tmpList2);
                }
            }

            list.Clear();
        }
    }
}
