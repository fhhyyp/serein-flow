using Microsoft.VisualBasic;
using Serein.Library.Api;
using Serein.Library.Utils;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Serein.Library.FlowNode
{


    /*public class FlowControl
    {
        private DynamicFlowTemplate dynamicFlowTemplate;
        private LightweightEnvironment environment;
        public FlowControl(DynamicFlowTemplate dynamicFlowTemplate,
                           LightweightEnvironment environment)
        {
            var callTree = new CallTree()
            callTree.SetMainNode("node1_guid");

            callTree.AddCallNode("node1_guid", (context) => DynamicFlowTemplate.Method(context, ...));
            callTree.AddCallNode("node2_guid", (context) => DynamicFlowTemplate.Method(context, ...));
            callTree.AddCallNode("node3_guid", (context) => DynamicFlowTemplate.Method(context, ...));
            callTree.AddCallNode("node4_guid", (context) => DynamicFlowTemplate.Method(context, ...));

            callTree["node1_guid"].AddFlowUpstream("node2_guid");
            callTree["node1_guid"].AddFlowSucceed("node2_guid");
            callTree["node2_guid"].AddFlowSucceed("node3_guid");
            callTree["node2_guid"].AddFlowError("node4_guid");

            LightweightEnvironment.LoadProject(callTree);
        }
    }*/


    /*public class DynamicFlowTemplate
    {
        private readonly class1_type class1_instance;

        public DynamicFlowTemplate(class1_type class_instance){
            this.class1_instance = class1_instance;
        }

        [Description("node1_guid")]
        [Description("assembly_name.class_name.method_name")]
        private [methodReturnType / void] NodeMethod_[method_name](IDynamicContext context, type1 param1, type2 param2)
        {
            class1_instance.method(params);
            context.SetData("node_guid", null);
        }

        [Description("node2_guid")]
        [Description("assembly_name.class_name.method_name")]
        private [methodReturnType] NodeMethod_[method_name](IDynamicContext context, type1 param1)
        {
            param1 = context.GetData("node1_guid") as type1;
            var result = class1_instance.method(params, param1);
            context.SetData("node2_guid", result);
        }


        [Description("node3_guid")]
        public [methodReturnType] FlowCall_[method_name](IDynamicContext context, type1 param1, type2 param2)
        {
            param1 = context.GetData("node3_guid") as type1;
            var result = class1_instance.method(params, param1);
            context.SetData("node3_guid", result);
        }
    }
    */



    







    public class CallTree
    {
        public CallTree(string canvasGuid, string startNodeGuid)
        {
            _canvas_guid = canvasGuid;
            _start_node_guid = startNodeGuid;
        }


        private readonly ConcurrentDictionary<string, CallNode> _callNodes = new ConcurrentDictionary<string,CallNode>();
        private readonly string _start_node_guid;
        private readonly string _canvas_guid ;
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
                _callNodes.AddOrUpdate(index, value, (key, oldValue) => value);
            }
        }

        public void AddCallNode(string nodeGuid, Action<IDynamicContext> action)
        {
            var node = new CallNode(this, nodeGuid, action);
            _callNodes[nodeGuid] = node;
        }
        public void AddCallNode(string nodeGuid, Func<IDynamicContext, Task> func)
        {
            var node = new CallNode(this, nodeGuid, func);
            _callNodes[nodeGuid] = node;
        }

    }



    public class CallNode
    {

        private readonly Func<IDynamicContext, Task> func;
        private readonly CallTree callTree;
        private readonly Action<IDynamicContext> action;

        public CallNode(CallTree callTree, string nodeGuid, Action<IDynamicContext> action)
        {
            this.callTree = callTree;
            Guid = nodeGuid;
            this.action = action;
        }

        public CallNode(CallTree callTree, string nodeGuid, Func<IDynamicContext, Task> func)
        {
            Guid = nodeGuid;
            this.func = func;
            Init();
        }

        private void Init()
        {
            PreviousNodes = new Dictionary<ConnectionInvokeType, List<CallNode>>();
            SuccessorNodes = new Dictionary<ConnectionInvokeType, List<CallNode>>();
            foreach (ConnectionInvokeType ctType in NodeStaticConfig.ConnectionTypes)
            {
                PreviousNodes[ctType] = new List<CallNode>();
                SuccessorNodes[ctType] = new List<CallNode>();
            }
        }


        /// <summary>
        /// 对应的节点
        /// </summary>
        public string Guid { get; }
        /// <summary>
        /// 不同分支的父节点（流程调用）
        /// </summary>
        public Dictionary<ConnectionInvokeType, List<CallNode>> PreviousNodes { get; private set; } 

        /// <summary>
        /// 不同分支的子节点（流程调用）
        /// </summary>
        public Dictionary<ConnectionInvokeType, List<CallNode>> SuccessorNodes { get; private set; } 


        public CallNode AddChildNodeUpstream(string nodeGuid)
        {
            var connectionInvokeType = ConnectionInvokeType.Upstream;
            PreviousNodes[connectionInvokeType].Add(callTree[nodeGuid]);
            return this;
        }
        public CallNode AddChildNodeSucceed(string nodeGuid)
        {
            var connectionInvokeType = ConnectionInvokeType.IsSucceed;
            PreviousNodes[connectionInvokeType].Add(callTree[nodeGuid]);
            return this;
        }
        public CallNode AddChildNodeFail(string nodeGuid)
        {
            var connectionInvokeType = ConnectionInvokeType.IsFail;
            PreviousNodes[connectionInvokeType].Add(callTree[nodeGuid]);
            return this;
        }
        public CallNode AddChildNodeError(string nodeGuid)
        {
            var connectionInvokeType = ConnectionInvokeType.IsError;
            PreviousNodes[connectionInvokeType].Add(callTree[nodeGuid]);
            return this;
        }


        /// <summary>
        /// 调用  
        /// </summary>
        /// <param name="context"></param>
        /// <param name="token"></param>
        /// <returns></returns>
        /// <exception cref="InvalidOperationException"></exception>
        public async Task InvokeAsync(IDynamicContext context, CancellationToken token)
        {
            if (token.IsCancellationRequested)
            {
                return;
            }
            if (action is not null)
            {
                action(context);
            }
            else if (func is not null)
            {
                await func(context);
            }
            else
            {
                throw new InvalidOperationException($"生成了错误的CallNode。【{Guid}】");
            }
        }

        /// <summary>
        /// 开始执行
        /// </summary>
        /// <param name="context"></param>
        /// <param name="token">流程运行</param>
        /// <returns></returns>
        public async Task<FlowResult> StartFlowAsync(IDynamicContext context, CancellationToken token)
        {
            Stack<CallNode> stack = new Stack<CallNode>();
            HashSet<CallNode> processedNodes = new HashSet<CallNode>(); // 用于记录已处理上游节点的节点
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
                context.AddOrUpdate(currentNode.Guid, flowResult); // 上下文中更新数据

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
                    return flowResult;  // 说明流程到了终点
                }

                if (context.RunState == RunState.Completion)
                {
                    context.Env.WriteLine(InfoType.INFO, $"流程执行到节点[{currentNode.Guid}]时提前结束，将返回当前执行结果。");
                    return flowResult; // 流程执行完成，返回结果
                }

                if (token.IsCancellationRequested)
                {
                    throw new Exception($"流程执行到节点[{currentNode.Guid}]时被取消，未能获取到流程结果。");
                }


                #endregion
            }
        }
    }

    /// <summary>
    /// 轻量级流程控制器
    /// </summary>
    public class LightweightFlowControl : IFlowControl
    {
        private readonly Dictionary<string, CallTree> callTree = new Dictionary<string, CallTree>();

        public LightweightFlowControl()
        {
        }

        public Task<object> InvokeAsync(string apiGuid, Dictionary<string, object> dict)
        {
            throw new NotImplementedException();
        }

        public Task<TResult> InvokeAsync<TResult>(string apiGuid, Dictionary<string, object> dict)
        {
            throw new NotImplementedException();
        }

        public Task<bool> StartFlowFromSelectNodeAsync(string startNodeGuid)
        {
            throw new NotImplementedException();
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
        public void WriteLine(InfoType type, string message, InfoClass @class = InfoClass.Trivial)
        {
            Console.WriteLine(message);
        }


        public ISereinIOC IOC => throw new NotImplementedException();

        public IFlowEdit FlowEdit => throw new NotImplementedException();

        public IFlowControl FlowControl => throw new NotImplementedException();

        public IFlowEnvironmentEvent Event => throw new NotImplementedException();

        public string EnvName => throw new NotImplementedException();

        public string ProjectFileLocation => throw new NotImplementedException();

        public bool IsGlobalInterrupt => throw new NotImplementedException();

        public bool IsControlRemoteEnv => throw new NotImplementedException();

        public InfoClass InfoClass { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public RunState FlowState { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

        public IFlowEnvironment CurrentEnv => throw new NotImplementedException();

        public UIContextOperation UIContextOperation => throw new NotImplementedException();

        public Task<(bool, RemoteMsgUtil)> ConnectRemoteEnv(string addres, int port, string token)
        {
            throw new NotImplementedException();
        }

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
