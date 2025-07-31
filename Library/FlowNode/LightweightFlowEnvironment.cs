using Serein.Library.Api;
using Serein.Library.Utils;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Serein.Library
{

    /// <summary>
    /// 流程调用树，管理所有的调用节点
    /// </summary>
    public class FlowCallTree : IFlowCallTree
    {

        private readonly SortedDictionary<string, CallNode> _callNodes = new SortedDictionary<string,CallNode>();

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


    /// <summary>
    /// 流程调用树接口，提供获取CallNode的方法。
    /// </summary>
    public interface IFlowCallTree
    {
        /// <summary>
        /// 获取指定Key的CallNode，如果不存在则返回null。
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        CallNode Get(string key);
    }

    /// <summary>
    /// 轻量级流程控制器
    /// </summary>
    public class LightweightFlowControl : IFlowControl
    {
        private readonly IFlowCallTree flowCallTree;
        private readonly IFlowEnvironment flowEnvironment;

        /// <summary>
        /// 轻量级流程上下文池，使用对象池模式来管理流程上下文的创建和回收。
        /// </summary>
        public static Serein.Library.Utils.ObjectPool<IFlowContext> FlowContextPool { get; set; }

        /// <summary>
        /// 单例IOC容器，用于依赖注入和服务定位。
        /// </summary>
        public ISereinIOC IOC => throw new NotImplementedException();

        /// <summary>
        /// 轻量级流程控制器构造函数，接受流程调用树和流程环境作为参数。
        /// </summary>
        /// <param name="flowCallTree"></param>
        /// <param name="flowEnvironment"></param>
        public LightweightFlowControl(IFlowCallTree flowCallTree, IFlowEnvironment flowEnvironment)
        {
            this.flowCallTree = flowCallTree;
            this.flowEnvironment = flowEnvironment;
             FlowContextPool = new Utils.ObjectPool<IFlowContext>(() =>
             {
                 return new FlowContext(flowEnvironment);
             });
        }

        /// <inheritdoc/>
        public Task<object> InvokeAsync(string apiGuid, Dictionary<string, object> dict)
        {
            throw new NotImplementedException();
        }
        /// <inheritdoc/>
        public Task<TResult> InvokeAsync<TResult>(string apiGuid, Dictionary<string, object> dict)
        {
            throw new NotImplementedException();
        }


        //private readonly DefaultObjectPool<IDynamicContext> _stackPool = new DefaultObjectPool<IDynamicContext>(new DynamicContext(this));

        /// <inheritdoc/>
        public async Task<TResult> StartFlowAsync<TResult>(string startNodeGuid)
        {
            IFlowContext context = Serein.Library.LightweightFlowControl.FlowContextPool.Allocate();
            CancellationTokenSource cts = new CancellationTokenSource();
            FlowResult flowResult;
#if DEBUG
            flowResult = await BenchmarkHelpers.BenchmarkAsync(async () =>
            {
                var node = flowCallTree.Get(startNodeGuid);
                var flowResult = await node.StartFlowAsync(context, cts.Token);
                return flowResult;
            });
#else
            var node = flowCallTree.Get(startNodeGuid);
            try
            {
                flowResult = await node.StartFlowAsync(context, cts.Token);
            }
            catch (global::System.Exception)
            {
                throw;
            }
            finally
            {
                context.Reset();
                FlowContextPool.Free(context);
            }
#endif

            cts?.Cancel();
            cts?.Dispose();
            if (flowResult.Value is TResult result)
            {
                return result;
            }
            else if (flowResult is FlowResult && flowResult is TResult result2)
            {
                return result2;
            }
            else
            {
                throw new ArgumentNullException($"类型转换失败，流程返回数据与泛型不匹配，当前返回类型为[{flowResult.Value.GetType().FullName}]。");
            }
        }

        /// <inheritdoc/>
        public  Task<bool> StartFlowAsync(string[] canvasGuids)
        {
            throw new NotImplementedException();
        }

        /// <inheritdoc/>
        public Task<bool> ExitFlowAsync()
        {
            throw new NotImplementedException();
        }

        #region 无须实现
        /// <inheritdoc/>
        public void ActivateFlipflopNode(string nodeGuid)
        {
            throw new NotImplementedException();
        }
        /// <inheritdoc/>
        public void MonitorObjectNotification(string nodeGuid, object monitorData, MonitorObjectEventArgs.ObjSourceType sourceType)
        {
            throw new NotImplementedException();
        }


        /// <inheritdoc/>
        public void TerminateFlipflopNode(string nodeGuid)
        {
            throw new NotImplementedException();
        }
        /// <inheritdoc/>
        public void TriggerInterrupt(string nodeGuid, string expression, InterruptTriggerEventArgs.InterruptTriggerType type)
        {
            throw new NotImplementedException();
        }
        /// <inheritdoc/>
        public void UseExternalIOC(ISereinIOC ioc)
        {
            throw new NotImplementedException();
        }
        /// <inheritdoc/>
        public void UseExternalIOC(ISereinIOC ioc, Action<ISereinIOC> setDefultMemberOnReset = null)
        {
            throw new NotImplementedException();
        }
        #endregion
    }


    /// <summary>
    /// 轻量级流程环境事件实现
    /// </summary>
    public class LightweightFlowEnvironmentEvent : IFlowEnvironmentEvent
    {
        /// <inheritdoc/>
        public event LoadDllHandler DllLoad;
        /// <inheritdoc/>
        public event ProjectLoadedHandler ProjectLoaded;
        /// <inheritdoc/>
        public event ProjectSavingHandler ProjectSaving;
        /// <inheritdoc/>
        public event NodeConnectChangeHandler NodeConnectChanged;
        /// <inheritdoc/>
        public event CanvasCreateHandler CanvasCreated;
        /// <inheritdoc/>
        public event CanvasRemoveHandler CanvasRemoved;
        /// <inheritdoc/>
        public event NodeCreateHandler NodeCreated;
        /// <inheritdoc/>
        public event NodeRemoveHandler NodeRemoved;
        /// <inheritdoc/>
        public event NodePlaceHandler NodePlace;
        /// <inheritdoc/>
        public event NodeTakeOutHandler NodeTakeOut;
        /// <inheritdoc/>
        public event StartNodeChangeHandler StartNodeChanged;
        /// <inheritdoc/>
        public event FlowRunCompleteHandler FlowRunComplete;
        /// <inheritdoc/>
        public event MonitorObjectChangeHandler MonitorObjectChanged;
        /// <inheritdoc/>
        public event NodeInterruptStateChangeHandler NodeInterruptStateChanged;
        /// <inheritdoc/>
        public event ExpInterruptTriggerHandler InterruptTriggered;
        /// <inheritdoc/>
        public event IOCMembersChangedHandler IOCMembersChanged;
        /// <inheritdoc/>
        public event NodeLocatedHandler NodeLocated;
        /// <inheritdoc/>
        public event EnvOutHandler EnvOutput;
        /// <inheritdoc/>
        public void OnDllLoad(LoadDllEventArgs eventArgs)
        {
            DllLoad?.Invoke(eventArgs);
        }
        /// <inheritdoc/>
        public void OnProjectLoaded(ProjectLoadedEventArgs eventArgs)
        {
            ProjectLoaded?.Invoke(eventArgs);
        }
        /// <inheritdoc/>
        public void OnProjectSaving(ProjectSavingEventArgs eventArgs)
        {
            ProjectSaving?.Invoke(eventArgs);
        }
        /// <inheritdoc/>
        public void OnNodeConnectChanged(NodeConnectChangeEventArgs eventArgs)
        {
            NodeConnectChanged?.Invoke(eventArgs);
        }
        /// <inheritdoc/>
        public void OnCanvasCreated(CanvasCreateEventArgs eventArgs)
        {
            CanvasCreated?.Invoke(eventArgs);
        }
        /// <inheritdoc/>
        public void OnCanvasRemoved(CanvasRemoveEventArgs eventArgs)
        {
            CanvasRemoved?.Invoke(eventArgs);
        }
        /// <inheritdoc/>
        public void OnNodeCreated(NodeCreateEventArgs eventArgs)
        {
            NodeCreated?.Invoke(eventArgs);
        }
        /// <inheritdoc/>
        public void OnNodeRemoved(NodeRemoveEventArgs eventArgs)
        {
            NodeRemoved?.Invoke(eventArgs);
        }
        /// <inheritdoc/>
        public void OnNodePlace(NodePlaceEventArgs eventArgs)
        {
            NodePlace?.Invoke(eventArgs);
        }
        /// <inheritdoc/>
        public void OnNodeTakeOut(NodeTakeOutEventArgs eventArgs)
        {
            NodeTakeOut?.Invoke(eventArgs);
        }
        /// <inheritdoc/>
        public void OnStartNodeChanged(StartNodeChangeEventArgs eventArgs)
        {
            StartNodeChanged?.Invoke(eventArgs);
        }
        /// <inheritdoc/>
        public void OnFlowRunComplete(FlowEventArgs eventArgs)
        {
            FlowRunComplete?.Invoke(eventArgs);
        }
        /// <inheritdoc/>
        public void OnMonitorObjectChanged(MonitorObjectEventArgs eventArgs)
        {
            MonitorObjectChanged?.Invoke(eventArgs);
        }
        /// <inheritdoc/>
        public void OnNodeInterruptStateChanged(NodeInterruptStateChangeEventArgs eventArgs)
        {
            NodeInterruptStateChanged?.Invoke(eventArgs);
        }
        /// <inheritdoc/>
        public void OnInterruptTriggered(InterruptTriggerEventArgs eventArgs)
        {
            InterruptTriggered?.Invoke(eventArgs);
        }
        /// <inheritdoc/>
        public void OnIOCMembersChanged(IOCMembersChangedEventArgs eventArgs)
        {
            IOCMembersChanged?.Invoke(eventArgs);
        }
        /// <inheritdoc/>
        public void OnNodeLocated(NodeLocatedEventArgs eventArgs)
        {
            NodeLocated?.Invoke(eventArgs);
        }
        /// <inheritdoc/>
        public void OnEnvOutput(InfoType type, string value)
        {
            EnvOutput?.Invoke(type, value);
        }

    }

    /// <summary>
    /// 轻量级流程环境实现
    /// </summary>
    public class LightweightFlowEnvironment : IFlowEnvironment
    {
        /// <summary>
        /// 轻量级流程环境构造函数，接受一个流程环境事件接口。
        /// </summary>
        /// <param name="lightweightFlowEnvironmentEvent"></param>
        public LightweightFlowEnvironment(IFlowEnvironmentEvent lightweightFlowEnvironmentEvent)
        {
            this.Event = lightweightFlowEnvironmentEvent;
        }
        /// <inheritdoc/>

        public void WriteLine(InfoType type, string message, InfoClass @class = InfoClass.Debug)
        {
            Console.WriteLine(message);
        }

        /// <inheritdoc/>
        public ISereinIOC IOC => throw new NotImplementedException();
        /// <inheritdoc/>
        public IFlowEdit FlowEdit => throw new NotImplementedException();
        /// <inheritdoc/>
        public IFlowControl FlowControl => throw new NotImplementedException();
        /// <inheritdoc/>
        public IFlowEnvironmentEvent Event { get; private set; }
        /// <inheritdoc/>
        public string EnvName => throw new NotImplementedException();
        /// <inheritdoc/>
        public string ProjectFileLocation => throw new NotImplementedException();
        /// <inheritdoc/>
        public bool _IsGlobalInterrupt => throw new NotImplementedException();
        /// <inheritdoc/>
        public bool IsControlRemoteEnv => throw new NotImplementedException();
        /// <inheritdoc/>
        public InfoClass InfoClass { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        /// <inheritdoc/>
        public RunState FlowState { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        /// <inheritdoc/>
        public IFlowEnvironment CurrentEnv => throw new NotImplementedException();
        /// <inheritdoc/>
        public UIContextOperation UIContextOperation => throw new NotImplementedException();

        /* public Task<(bool, RemoteMsgUtil)> ConnectRemoteEnv(string addres, int port, string token)
         {
             throw new NotImplementedException();
         }*/
        /// <inheritdoc/>
        public void ExitRemoteEnv()
        {
            throw new NotImplementedException();
        }
        /// <inheritdoc/>
        public Task<FlowEnvInfo> GetEnvInfoAsync()
        {
            throw new NotImplementedException();
        }
        /// <inheritdoc/>
        public SereinProjectData GetProjectInfoAsync()
        {
            throw new NotImplementedException();
        }
        /// <inheritdoc/>
        public void LoadAllNativeLibraryOfRuning(string path, bool isRecurrence = true)
        {
            throw new NotImplementedException();
        }
        /// <inheritdoc/>
        public void LoadLibrary(string dllPath)
        {
            throw new NotImplementedException();
        }
        /// <inheritdoc/>
        public bool LoadNativeLibraryOfRuning(string file)
        {
            throw new NotImplementedException();
        }
        /// <inheritdoc/>
        public void LoadProject(string filePath)
        {
            throw new NotImplementedException();
        }
        /// <inheritdoc/>
        public Task LoadProjetAsync(string filePath)
        {
            throw new NotImplementedException();
        }
        /// <inheritdoc/>
        public Task NotificationNodeValueChangeAsync(string nodeGuid, string path, object value)
        {
            throw new NotImplementedException();
        }
        /// <inheritdoc/>
        public void SaveProject()
        {
            throw new NotImplementedException();
        }
        /// <inheritdoc/>
        public void SetUIContextOperation(UIContextOperation uiContextOperation)
        {
            throw new NotImplementedException();
        }
        /// <inheritdoc/>
        public Task StartRemoteServerAsync(int port = 7525)
        {
            throw new NotImplementedException();
        }
        /// <inheritdoc/>
        public void StopRemoteServer()
        {
            throw new NotImplementedException();
        }
        /// <inheritdoc/>
        public bool TryGetDelegateDetails(string assemblyName, string methodName, out DelegateDetails del)
        {
            throw new NotImplementedException();
        }
        /// <inheritdoc/>
        public bool TryGetMethodDetailsInfo(string assemblyName, string methodName, out MethodDetailsInfo mdInfo)
        {
            throw new NotImplementedException();
        }
        /// <inheritdoc/>
        public bool TryGetNodeModel(string nodeGuid, out IFlowNode nodeModel)
        {
            throw new NotImplementedException();
        }
        /// <inheritdoc/>
        public bool TryUnloadLibrary(string assemblyFullName)
        {
            throw new NotImplementedException();
        }

       
    }
}
