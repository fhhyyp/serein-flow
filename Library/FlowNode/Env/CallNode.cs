using Serein.Library.Api;
using Serein.Library.Utils;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Serein.Library
{
    /// <summary>
    /// 调用节点，代表一个流程中的调用点，可以是一个Action或一个异步函数。
    /// </summary>

    public class CallNode
    {

        private Func<IFlowContext, Task> taskFunc;
        private Action<IFlowContext> action;

        /// <summary>
        /// 创建一个新的调用节点，使用指定的节点Guid。
        /// </summary>
        /// <param name="nodeGuid"></param>
        public CallNode(string nodeGuid)
        {
            Guid = nodeGuid;
            Init();
        }

        /// <summary>
        /// 创建一个新的调用节点，使用指定的节点Guid和Action。
        /// </summary>
        /// <param name="nodeGuid"></param>
        /// <param name="action"></param>
        public CallNode(string nodeGuid, Action<IFlowContext> action)
        {
            Guid = nodeGuid;
            this.action = action;
            Init();
        }

        /// <summary>
        /// 创建一个新的调用节点，使用指定的节点Guid和异步函数。
        /// </summary>
        /// <param name="nodeGuid"></param>
        /// <param name="func"></param>
        public CallNode(string nodeGuid, Func<IFlowContext, Task> func)
        {
            Guid = nodeGuid;
            this.taskFunc = func;
            Init();
        }

        /// <summary>
        /// 初始化调用节点，设置默认的子节点和后继节点字典。
        /// </summary>
        private void Init()
        {
            //PreviousNodes = new Dictionary<ConnectionInvokeType, List<CallNode>>();
            SuccessorNodes = new Dictionary<ConnectionInvokeType, List<CallNode>>();
            foreach (ConnectionInvokeType ctType in NodeStaticConfig.ConnectionTypes)
            {
                //PreviousNodes[ctType] = new List<CallNode>();
                SuccessorNodes[ctType] = new List<CallNode>();
            }
        }


        private enum ActionType
        {
            Action,
            Task,
        }
        private ActionType actionType = ActionType.Action;

        /// <summary>
        /// 设置调用节点的Action，表示该节点执行一个同步操作。
        /// </summary>
        /// <param name="action"></param>
        public void SetAction(Action<IFlowContext> action)
        {
            this.action = action;
            actionType = ActionType.Action;
        }

        /// <summary>
        /// 设置调用节点的异步函数，表示该节点执行一个异步操作。
        /// </summary>
        /// <param name="taskFunc"></param>
        public void SetAction(Func<IFlowContext, Task> taskFunc)
        {
            this.taskFunc = taskFunc;
            actionType = ActionType.Task;
        }


        /// <summary>
        /// 对应的节点
        /// </summary>
        public string Guid { get; }

#if false

        /// <summary>
        /// 不同分支的父节点（流程调用）
        /// </summary>
        public Dictionary<ConnectionInvokeType, List<CallNode>> PreviousNodes { get; private set; }

#endif
        /// <summary>
        /// 不同分支的子节点（流程调用）
        /// </summary>
        public Dictionary<ConnectionInvokeType, List<CallNode>> SuccessorNodes { get; private set; }

        /// <summary>
        /// 子节点数组，分为四个分支：上游、成功、失败、错误，每个分支最多支持16个子节点。
        /// </summary>
        public CallNode[][] ChildNodes { get; private set; } = new CallNode[][]
        {
            new CallNode[MaxChildNodeCount],
            new CallNode[MaxChildNodeCount],
            new CallNode[MaxChildNodeCount],
            new CallNode[MaxChildNodeCount]
        };
        private const int MaxChildNodeCount = 16; // 每个分支最多支持16个子节点


        /// <summary>
        /// 获取指定类型的子节点数量。
        /// </summary>
        /// <param name="type"></param>
        /// <returns></returns>
        public int GetCount(ConnectionInvokeType type) 
        {
            if (type == ConnectionInvokeType.Upstream) return UpstreamNodeCount;
            if (type == ConnectionInvokeType.IsSucceed) return IsSuccessorNodeCount;
            if (type == ConnectionInvokeType.IsFail) return IsFailNodeCount;
            if (type == ConnectionInvokeType.IsError) return IsErrorNodeCount;
            return 0;
        }

        /// <summary>
        /// 获取当前节点的子节点数量。
        /// </summary>
        public int UpstreamNodeCount { get; private set; } = 0;

        /// <summary>
        /// 获取当前节点的成功后继子节点数量。
        /// </summary>
        public int IsSuccessorNodeCount { get; private set; } = 0;
        /// <summary>
        /// 获取当前节点的失败后继子节点数量。
        /// </summary>
        public int IsFailNodeCount { get; private set; } = 0;
        /// <summary>
        /// 获取当前节点的错误后继子节点数量。
        /// </summary>
        public int IsErrorNodeCount { get; private set; } = 0;

        /// <summary>
        /// 添加一个上游子节点到当前节点。
        /// </summary>
        /// <param name="callNode"></param>
        /// <returns></returns>
        public CallNode AddChildNodeUpstream(CallNode callNode)
        {
            var connectionInvokeType = ConnectionInvokeType.Upstream;
            ChildNodes[(int)connectionInvokeType][UpstreamNodeCount++] = callNode;
            SuccessorNodes[connectionInvokeType].Add(callNode);
            return this;
        }

        /// <summary>
        /// 添加一个成功后继子节点到当前节点。
        /// </summary>
        /// <param name="callNode"></param>
        /// <returns></returns>
        public CallNode AddChildNodeSucceed(CallNode callNode)
        {
            ChildNodes[0][UpstreamNodeCount++] = callNode;

            var connectionInvokeType = ConnectionInvokeType.IsSucceed;
            ChildNodes[(int)connectionInvokeType][IsSuccessorNodeCount++] = callNode;
            SuccessorNodes[connectionInvokeType].Add(callNode);

            return this;
        }
        /// <summary>
        /// 添加一个失败后继子节点到当前节点。
        /// </summary>
        /// <param name="callNode"></param>
        /// <returns></returns>
        public CallNode AddChildNodeFail(CallNode callNode)
        {
            var connectionInvokeType = ConnectionInvokeType.IsFail; 
            ChildNodes[(int)connectionInvokeType][IsFailNodeCount++] = callNode;
            SuccessorNodes[connectionInvokeType].Add(callNode);

            return this;
        }

        /// <summary>
        /// 添加一个错误后继子节点到当前节点。
        /// </summary>
        /// <param name="callNode"></param>
        /// <returns></returns>
        public CallNode AddChildNodeError(CallNode callNode)
        {
            var connectionInvokeType = ConnectionInvokeType.IsError;
            ChildNodes[(int)connectionInvokeType][IsErrorNodeCount++] = callNode;
            SuccessorNodes[connectionInvokeType].Add(callNode);
            return this;
        }


        /// <summary>
        /// 调用  
        /// </summary>
        /// <param name="context"></param>
        /// <param name="token"></param>
        /// <returns></returns>
        /// <exception cref="InvalidOperationException"></exception>
        public async Task InvokeAsync(IFlowContext context, CancellationToken token)
        {
            if (token.IsCancellationRequested)
            {
                return;
            }
            if (actionType == ActionType.Action)
            {
                action.Invoke(context);
            }
            else if (actionType == ActionType.Task)
            {
                await taskFunc.Invoke(context);
            }
            else
            {
                throw new InvalidOperationException($"生成了错误的CallNode。【{Guid}】");
            }
        }

        private static readonly ObjectPool<Stack<CallNode>> _stackPool = new ObjectPool<Stack<CallNode>>(() => new Stack<CallNode>());


        /// <summary>
        /// 开始执行
        /// </summary>
        /// <param name="context"></param>
        /// <param name="token">流程运行</param>
        /// <returns></returns>
        public async Task<FlowResult> StartFlowAsync(IFlowContext context, CancellationToken token)
        {
            var stack = _stackPool.Allocate();
            stack.Push(this);
            while (true)
            {
                if (token.IsCancellationRequested)
                {
                    throw new Exception($"流程执行被取消，未能获取到流程结果。");
                }

                #region 执行相关
                // 从栈中弹出一个节点作为当前节点进行处理
                var currentNode = stack.Pop();
                context.NextOrientation = ConnectionInvokeType.None; // 重置上下文状态
                FlowResult flowResult = null;
                try
                {
                    context.NextOrientation = ConnectionInvokeType.IsSucceed; // 默认执行成功
                    await currentNode.InvokeAsync(context, token);
                }
                catch (Exception ex)
                {
                    flowResult = FlowResult.Fail(currentNode.Guid, context, ex.Message);
                    context.Env.WriteLine(InfoType.ERROR, $"节点[{currentNode}]异常：" + ex);
                    context.NextOrientation = ConnectionInvokeType.IsError;
                    context.ExceptionOfRuning = ex;
                }
                #endregion

                #region 执行完成时更新栈
                // 首先将指定类别后继分支的所有节点逆序推入栈中
                var nextNodes = currentNode.SuccessorNodes[context.NextOrientation];
                for (int index = nextNodes.Count - 1; index >= 0; index--)
                {
                    var node = nextNodes[index];
                    context.SetPreviousNode(node.Guid, currentNode.Guid);
                    stack.Push(node);
                }

                // 然后将指上游分支的所有节点逆序推入栈中
                var upstreamNodes = currentNode.SuccessorNodes[ConnectionInvokeType.Upstream];
                for (int index = upstreamNodes.Count - 1; index >= 0; index--)
                {
                    var node = upstreamNodes[index];
                    context.SetPreviousNode(node.Guid, currentNode.Guid);
                    stack.Push(node);
                }
                #endregion

                #region 执行完成后检查

                if (stack.Count == 0)
                {
                    _stackPool.Free(stack);
                    flowResult = context.GetFlowData(currentNode.Guid);
                    return flowResult;  // 说明流程到了终点
                }

                if (context.RunState == RunState.Completion)
                {

                    _stackPool.Free(stack);
                    context.Env.WriteLine(InfoType.INFO, $"流程执行到节点[{currentNode.Guid}]时提前结束，将返回当前执行结果。");
                    flowResult = context.GetFlowData(currentNode.Guid);
                    return flowResult; // 流程执行完成，返回结果
                }

                if (token.IsCancellationRequested)
                {
                    _stackPool.Free(stack);
                    throw new Exception($"流程执行到节点[{currentNode.Guid}]时被取消，未能获取到流程结果。");
                }


                #endregion
            }
          
        }
    }
}
