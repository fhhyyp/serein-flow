using Microsoft.Extensions.ObjectPool;
using Serein.Library.Api;
using Serein.Library.Utils;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Serein.Library
{
/*
    public class CallNodeLookup : IFlowCallTree
    {
        private static readonly string[] _keys = new[]
        {
            "Start",   // 0
            "Stop",    // 1
            "Reset",   // 2
            "Pause",   // 3
            "Resume",  // 4
            "Check",   // 5
            "Init",    // 6
            "Load",    // 7
            "Save",    // 8
            "Clear"    // 9
        };

        private static readonly CallNode[] _values = new CallNode[10];

        static CallNodeLookup()
        {
            *//*_values[0] = new CallNode("Start");
            _values[1] = new CallNode("Stop");
            _values[2] = new CallNode("Reset");
            _values[3] = new CallNode("Pause");
            _values[4] = new CallNode("Resume");
            _values[5] = new CallNode("Check");
            _values[6] = new CallNode("Init");
            _values[7] = new CallNode("Load");
            _values[8] = new CallNode("Save");
            _values[9] = new CallNode("Clear");*//*
        }

        // 最小冲突哈希函数（简单示例，固定键集有效）
        private static int PerfectHash(string key)
        {
            return key switch
            {
                "Start" => 0,
                "Stop" => 1,
                "Reset" => 2,
                "Pause" => 3,
                "Resume" => 4,
                "Check" => 5,
                "Init" => 6,
                "Load" => 7,
                "Save" => 8,
                "Clear" => 9,
                _ => -1
            };
        }

        public CallNode Get(string key)
        {
            int index = PerfectHash(key);
            if (index >= 0 && _keys[index] == key)
                return _values[index];
            return null;
        }
    }

  
*/

    public class FlowCallTree : IFlowCallTree
    {

        private readonly SortedDictionary<string, CallNode> _callNodes = new SortedDictionary<string,CallNode>();
        //private readonly Dictionary<string, CallNode> _callNodes = new Dictionary<string,CallNode>();

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

        public void AddCallNode(string nodeGuid, Action<IFlowContext> action)
        {
            var node = new CallNode(nodeGuid, action);
            _callNodes[nodeGuid] = node;
        }
        public void AddCallNode(string nodeGuid, Func<IFlowContext, Task> func)
        {
            var node = new CallNode(nodeGuid, func);
            _callNodes[nodeGuid] = node;
        }

        public CallNode Get(string key)
        {
            return _callNodes.TryGetValue(key, out CallNode callNode) ? callNode : null;
        }
    }






    public class CallNode
    {

        private Func<IFlowContext, Task> taskFunc;
        private Action<IFlowContext> action;

        public CallNode(string nodeGuid)
        {
            Guid = nodeGuid;
            Init();
        }
        public CallNode(string nodeGuid, Action<IFlowContext> action)
        {
            Guid = nodeGuid;
            this.action = action;
            Init();
        }

        public CallNode(string nodeGuid, Func<IFlowContext, Task> func)
        {
            Guid = nodeGuid;
            this.taskFunc = func;
            Init();
        }


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

        
        public void SetAction(Action<IFlowContext> action)
        {
            this.action = action;
        }
        public void SetAction(Func<IFlowContext, Task> taskFunc)
        {
            this.taskFunc = taskFunc;
        }


        /// <summary>
        /// 对应的节点
        /// </summary>
        public string Guid { get; }
        /// <summary>
        /// 不同分支的父节点（流程调用）
        /// </summary>
        //public Dictionary<ConnectionInvokeType, List<CallNode>> PreviousNodes { get; private set; } 

        /// <summary>
        /// 不同分支的子节点（流程调用）
        /// </summary>
        public Dictionary<ConnectionInvokeType, List<CallNode>> SuccessorNodes { get; private set; }
        public CallNode[][] ChildNodes { get; private set; } = new CallNode[][]
        {
            new CallNode[32],
            new CallNode[32],
            new CallNode[32],
            new CallNode[32]
        };

        public int GetCount(ConnectionInvokeType type) 
        {
            if (type == ConnectionInvokeType.Upstream) return UpstreamNodeCount;
            if (type == ConnectionInvokeType.IsSucceed) return IsSuccessorNodeCount;
            if (type == ConnectionInvokeType.IsFail) return IsFailNodeCount;
            if (type == ConnectionInvokeType.IsError) return IsErrorNodeCount;
            return 0;
        }

        public int UpstreamNodeCount { get; private set; } = 0;
        public int IsSuccessorNodeCount { get; private set; } = 0;
        public int IsFailNodeCount { get; private set; } = 0;
        public int IsErrorNodeCount { get; private set; } = 0;

        public CallNode AddChildNodeUpstream(CallNode callNode)
        {
            var connectionInvokeType = ConnectionInvokeType.Upstream;
            ChildNodes[(int)connectionInvokeType][UpstreamNodeCount++] = callNode;
            SuccessorNodes[connectionInvokeType].Add(callNode);
            return this;
        }

        public CallNode AddChildNodeSucceed(CallNode callNode)
        {
            ChildNodes[0][UpstreamNodeCount++] = callNode;

            var connectionInvokeType = ConnectionInvokeType.IsSucceed;
            ChildNodes[(int)connectionInvokeType][IsSuccessorNodeCount++] = callNode;
            SuccessorNodes[connectionInvokeType].Add(callNode);

            return this;
        }
        public CallNode AddChildNodeFail(CallNode callNode)
        {
            var connectionInvokeType = ConnectionInvokeType.IsFail; 
            ChildNodes[(int)connectionInvokeType][IsFailNodeCount++] = callNode;
            SuccessorNodes[connectionInvokeType].Add(callNode);

            return this;
        }
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
            if (action is not null)
            {
                action(context);
            }
            else if (taskFunc is not null)
            {
                await taskFunc(context);
            }
            else
            {
                throw new InvalidOperationException($"生成了错误的CallNode。【{Guid}】");
            }
        }

        private static readonly DefaultObjectPool<Stack<CallNode>> _stackPool = new DefaultObjectPool<Stack<CallNode>>(new DefaultPooledObjectPolicy<Stack<CallNode>>());


        /// <summary>
        /// 开始执行
        /// </summary>
        /// <param name="context"></param>
        /// <param name="token">流程运行</param>
        /// <returns></returns>
        public async Task<FlowResult> StartFlowAsync(IFlowContext context, CancellationToken token)
        {
            var stack = _stackPool.Get();
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
                    await currentNode.InvokeAsync(context, token);
                    if (context.NextOrientation == ConnectionInvokeType.None) // 没有手动设置时，进行自动设置
                    {
                        context.NextOrientation = ConnectionInvokeType.IsSucceed;
                    }
                }
                catch (Exception ex)
                {
                    flowResult = new FlowResult(currentNode.Guid, context);
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
                    //if (!ignodeState)
                    context.SetPreviousNode(nextNodes[index].Guid, currentNode.Guid);
                    stack.Push(nextNodes[index]);
                }

                // 然后将指上游分支的所有节点逆序推入栈中
                var upstreamNodes = currentNode.SuccessorNodes[ConnectionInvokeType.Upstream];
                for (int index = upstreamNodes.Count - 1; index >= 0; index--)
                {
                    context.SetPreviousNode(upstreamNodes[index].Guid, currentNode.Guid);
                    stack.Push(upstreamNodes[index]);
                }
                #endregion

                #region 执行完成后检查

                if (stack.Count == 0)
                {
                    _stackPool.Return(stack);
                    flowResult = context.GetFlowData(currentNode.Guid);
                    return flowResult;  // 说明流程到了终点
                }

                if (context.RunState == RunState.Completion)
                {

                    _stackPool.Return(stack);
                    context.Env.WriteLine(InfoType.INFO, $"流程执行到节点[{currentNode.Guid}]时提前结束，将返回当前执行结果。");
                    flowResult = context.GetFlowData(currentNode.Guid); _stackPool.Return(stack);
                    return flowResult; // 流程执行完成，返回结果
                }

                if (token.IsCancellationRequested)
                {
                    _stackPool.Return(stack);
                    throw new Exception($"流程执行到节点[{currentNode.Guid}]时被取消，未能获取到流程结果。");
                }


                #endregion
            }
          
        }
    }


    public interface IFlowCallTree
    {
        CallNode Get(string key);
    }

    /// <summary>
    /// 轻量级流程控制器
    /// </summary>
    public class LightweightFlowControl : IFlowControl
    {
        private readonly IFlowCallTree flowCallTree;
        private readonly IFlowEnvironment flowEnvironment;
        public static Serein.Library.Utils.ObjectPool<IFlowContext> FlowContextPool { get; set; }

        public ISereinIOC IOC => throw new NotImplementedException();

        public LightweightFlowControl(IFlowCallTree flowCallTree, IFlowEnvironment flowEnvironment)
        {
            this.flowCallTree = flowCallTree;
            this.flowEnvironment = flowEnvironment;
             FlowContextPool = new Utils.ObjectPool<IFlowContext>(() =>
             {
                 return new FlowContext(flowEnvironment);
             });
        }

        public Task<object> InvokeAsync(string apiGuid, Dictionary<string, object> dict)
        {
            throw new NotImplementedException();
        }

        public Task<TResult> InvokeAsync<TResult>(string apiGuid, Dictionary<string, object> dict)
        {
            throw new NotImplementedException();
        }


        //private readonly DefaultObjectPool<IDynamicContext> _stackPool = new DefaultObjectPool<IDynamicContext>(new DynamicContext(this));


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
        public async Task<bool> StartFlowAsync(string[] canvasGuids)
        {
            throw new NotImplementedException();
        }

        public Task<bool> ExitFlowAsync()
        {
            throw new NotImplementedException();
        }

        #region 无须实现

        public void ActivateFlipflopNode(string nodeGuid)
        {
            throw new NotImplementedException();
        }

        public void MonitorObjectNotification(string nodeGuid, object monitorData, MonitorObjectEventArgs.ObjSourceType sourceType)
        {
            throw new NotImplementedException();
        }



        public void TerminateFlipflopNode(string nodeGuid)
        {
            throw new NotImplementedException();
        }

        public void TriggerInterrupt(string nodeGuid, string expression, InterruptTriggerEventArgs.InterruptTriggerType type)
        {
            throw new NotImplementedException();
        }

        public void UseExternalIOC(ISereinIOC ioc)
        {
            throw new NotImplementedException();
        }

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
        public event LoadDllHandler DllLoad;
        public event ProjectLoadedHandler ProjectLoaded;
        public event ProjectSavingHandler ProjectSaving;
        public event NodeConnectChangeHandler NodeConnectChanged;
        public event CanvasCreateHandler CanvasCreated;
        public event CanvasRemoveHandler CanvasRemoved;
        public event NodeCreateHandler NodeCreated;
        public event NodeRemoveHandler NodeRemoved;
        public event NodePlaceHandler NodePlace;
        public event NodeTakeOutHandler NodeTakeOut;
        public event StartNodeChangeHandler StartNodeChanged;
        public event FlowRunCompleteHandler FlowRunComplete;
        public event MonitorObjectChangeHandler MonitorObjectChanged;
        public event NodeInterruptStateChangeHandler NodeInterruptStateChanged;
        public event ExpInterruptTriggerHandler InterruptTriggered;
        public event IOCMembersChangedHandler IOCMembersChanged;
        public event NodeLocatedHandler NodeLocated;
        public event EnvOutHandler EnvOutput;

        public void OnDllLoad(LoadDllEventArgs eventArgs)
        {
            DllLoad?.Invoke(eventArgs);
        }

        public void OnProjectLoaded(ProjectLoadedEventArgs eventArgs)
        {
            ProjectLoaded?.Invoke(eventArgs);
        }

        public void OnProjectSaving(ProjectSavingEventArgs eventArgs)
        {
            ProjectSaving?.Invoke(eventArgs);
        }

        public void OnNodeConnectChanged(NodeConnectChangeEventArgs eventArgs)
        {
            NodeConnectChanged?.Invoke(eventArgs);
        }

        public void OnCanvasCreated(CanvasCreateEventArgs eventArgs)
        {
            CanvasCreated?.Invoke(eventArgs);
        }

        public void OnCanvasRemoved(CanvasRemoveEventArgs eventArgs)
        {
            CanvasRemoved?.Invoke(eventArgs);
        }

        public void OnNodeCreated(NodeCreateEventArgs eventArgs)
        {
            NodeCreated?.Invoke(eventArgs);
        }

        public void OnNodeRemoved(NodeRemoveEventArgs eventArgs)
        {
            NodeRemoved?.Invoke(eventArgs);
        }

        public void OnNodePlace(NodePlaceEventArgs eventArgs)
        {
            NodePlace?.Invoke(eventArgs);
        }

        public void OnNodeTakeOut(NodeTakeOutEventArgs eventArgs)
        {
            NodeTakeOut?.Invoke(eventArgs);
        }

        public void OnStartNodeChanged(StartNodeChangeEventArgs eventArgs)
        {
            StartNodeChanged?.Invoke(eventArgs);
        }

        public void OnFlowRunComplete(FlowEventArgs eventArgs)
        {
            FlowRunComplete?.Invoke(eventArgs);
        }

        public void OnMonitorObjectChanged(MonitorObjectEventArgs eventArgs)
        {
            MonitorObjectChanged?.Invoke(eventArgs);
        }

        public void OnNodeInterruptStateChanged(NodeInterruptStateChangeEventArgs eventArgs)
        {
            NodeInterruptStateChanged?.Invoke(eventArgs);
        }

        public void OnInterruptTriggered(InterruptTriggerEventArgs eventArgs)
        {
            InterruptTriggered?.Invoke(eventArgs);
        }

        public void OnIOCMembersChanged(IOCMembersChangedEventArgs eventArgs)
        {
            IOCMembersChanged?.Invoke(eventArgs);
        }

        public void OnNodeLocated(NodeLocatedEventArgs eventArgs)
        {
            NodeLocated?.Invoke(eventArgs);
        }

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

        public LightweightFlowEnvironment(IFlowEnvironmentEvent lightweightFlowEnvironmentEvent)
        {
            this.Event = lightweightFlowEnvironmentEvent;
        }



        public void WriteLine(InfoType type, string message, InfoClass @class = InfoClass.Trivial)
        {
            Console.WriteLine(message);
        }


        public ISereinIOC IOC => throw new NotImplementedException();

        public IFlowEdit FlowEdit => throw new NotImplementedException();

        public IFlowControl FlowControl => throw new NotImplementedException();

        public IFlowEnvironmentEvent Event { get; private set; }

        public string EnvName => throw new NotImplementedException();

        public string ProjectFileLocation => throw new NotImplementedException();

        public bool _IsGlobalInterrupt => throw new NotImplementedException();

        public bool IsControlRemoteEnv => throw new NotImplementedException();

        public InfoClass InfoClass { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public RunState FlowState { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

        public IFlowEnvironment CurrentEnv => throw new NotImplementedException();

        public UIContextOperation UIContextOperation => throw new NotImplementedException();

       /* public Task<(bool, RemoteMsgUtil)> ConnectRemoteEnv(string addres, int port, string token)
        {
            throw new NotImplementedException();
        }*/

        public void ExitRemoteEnv()
        {
            throw new NotImplementedException();
        }

        public Task<FlowEnvInfo> GetEnvInfoAsync()
        {
            throw new NotImplementedException();
        }

        public Task<SereinProjectData> GetProjectInfoAsync()
        {
            throw new NotImplementedException();
        }

        public void LoadAllNativeLibraryOfRuning(string path, bool isRecurrence = true)
        {
            throw new NotImplementedException();
        }

        public void LoadLibrary(string dllPath)
        {
            throw new NotImplementedException();
        }

        public bool LoadNativeLibraryOfRuning(string file)
        {
            throw new NotImplementedException();
        }

        public void LoadProject(string filePath)
        {
            throw new NotImplementedException();
        }

        public Task LoadProjetAsync(string filePath)
        {
            throw new NotImplementedException();
        }

        public Task NotificationNodeValueChangeAsync(string nodeGuid, string path, object value)
        {
            throw new NotImplementedException();
        }

        public void SaveProject()
        {
            throw new NotImplementedException();
        }

        public void SetUIContextOperation(UIContextOperation uiContextOperation)
        {
            throw new NotImplementedException();
        }

        public Task StartRemoteServerAsync(int port = 7525)
        {
            throw new NotImplementedException();
        }

        public void StopRemoteServer()
        {
            throw new NotImplementedException();
        }

        public bool TryGetDelegateDetails(string assemblyName, string methodName, out DelegateDetails del)
        {
            throw new NotImplementedException();
        }

        public bool TryGetMethodDetailsInfo(string assemblyName, string methodName, out MethodDetailsInfo mdInfo)
        {
            throw new NotImplementedException();
        }

        public bool TryGetNodeModel(string nodeGuid, out IFlowNode nodeModel)
        {
            throw new NotImplementedException();
        }

        public bool TryUnloadLibrary(string assemblyFullName)
        {
            throw new NotImplementedException();
        }

       
    }
}
