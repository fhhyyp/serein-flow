using Serein.Library.Api;
using Serein.Library.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Serein.Library
{

    /// <summary>
    /// 不提供流程操作能力，仅提供容器功能
    /// </summary>
    public class ContainerFlowEnvironment : IFlowEnvironment, ISereinIOC
    {
        /// <summary>
        /// 本地运行环境缓存的持久化实例
        /// </summary>
        private Dictionary<string, object> PersistennceInstance { get; } = new Dictionary<string, object>();
        public ContainerFlowEnvironment()
        {
            
        }

        private ISereinIOC sereinIOC => this;
        public ISereinIOC IOC => sereinIOC;

        public string EnvName => throw new NotImplementedException();
        public string ProjectFileLocation => throw new NotImplementedException();

        public bool IsGlobalInterrupt => throw new NotImplementedException();

        public bool IsControlRemoteEnv => throw new NotImplementedException();

        public InfoClass InfoClass { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public RunState FlowState { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public RunState FlipFlopState { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

        public IFlowEnvironment CurrentEnv => this;

        public UIContextOperation UIContextOperation { get; set; }
        public NodeMVVMManagement NodeMVVMManagement { get; set; }

        /// <summary>
        /// 设置在UI线程操作的线程上下文
        /// </summary>
        /// <param name="uiContextOperation"></param>
        public void SetUIContextOperation(UIContextOperation uiContextOperation)
        {
            this.UIContextOperation = uiContextOperation;
        }

        public void ActivateFlipflopNode(string nodeGuid)
        {
            throw new NotImplementedException();
        }

        public Task<bool> ChangeParameter(string nodeGuid, bool isAdd, int paramIndex)
        {
            throw new NotImplementedException();
        }

        public Task<bool> ConnectArgSourceNodeAsync(string fromNodeGuid, string toNodeGuid, JunctionType fromNodeJunctionType, JunctionType toNodeJunctionType, ConnectionArgSourceType argSourceType, int argIndex)
        {
            throw new NotImplementedException();
        }

        public Task<bool> ConnectInvokeNodeAsync(string fromNodeGuid, string toNodeGuid, JunctionType fromNodeJunctionType, JunctionType toNodeJunctionType, ConnectionInvokeType invokeType)
        {
            throw new NotImplementedException();
        }

        public Task<(bool, RemoteMsgUtil)> ConnectRemoteEnv(string addres, int port, string token)
        {
            throw new NotImplementedException();
        }

        public Task<NodeInfo> CreateNodeAsync(NodeControlType nodeType, PositionOfUI position, MethodDetailsInfo methodDetailsInfo = null)
        {
            throw new NotImplementedException();
        }

        public Task<bool> ExitFlowAsync()
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

        public Task<object> InvokeNodeAsync(IDynamicContext context, string nodeGuid)
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

        public Task LoadNodeInfosAsync(List<NodeInfo> nodeInfos)
        {
            throw new NotImplementedException();
        }

        public void LoadProject(FlowEnvInfo flowEnvInfo, string filePath)
        {
            throw new NotImplementedException();
        }

        public void MonitorObjectNotification(string nodeGuid, object monitorData, MonitorObjectEventArgs.ObjSourceType sourceType)
        {
            throw new NotImplementedException();
        }

        public void MoveNode(string nodeGuid, double x, double y)
        {
            throw new NotImplementedException();
        }

        public void NodeLocated(string nodeGuid)
        {
            throw new NotImplementedException();
        }

        public Task NotificationNodeValueChangeAsync(string nodeGuid, string path, object value)
        {
            throw new NotImplementedException();
        }

        public Task<bool> PlaceNodeToContainerAsync(string nodeGuid, string containerNodeGuid)
        {
            throw new NotImplementedException();
        }

        public Task<bool> RemoveConnectArgSourceAsync(string fromNodeGuid, string toNodeGuid, int argIndex)
        {
            throw new NotImplementedException();
        }

        public Task<bool> RemoveConnectInvokeAsync(string fromNodeGuid, string toNodeGuid, ConnectionInvokeType connectionType)
        {
            throw new NotImplementedException();
        }

        public Task<bool> RemoveNodeAsync(string nodeGuid)
        {
            throw new NotImplementedException();
        }

        public void SaveProject()
        {
            throw new NotImplementedException();
        }

        public Task<bool> SetConnectPriorityInvoke(string fromNodeGuid, string toNodeGuid, ConnectionInvokeType connectionType)
        {
            throw new NotImplementedException();
        }

        public Task<string> SetStartNodeAsync(string nodeGuid)
        {
            throw new NotImplementedException();
        }

        public Task<bool> StartAsyncInSelectNode(string startNodeGuid)
        {
            throw new NotImplementedException();
        }

        public Task<bool> StartFlowAsync()
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

        public Task<bool> TakeOutNodeToContainerAsync(string nodeGuid)
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

        public bool TryGetDelegateDetails(string assemblyName, string methodName, out DelegateDetails del)
        {
            throw new NotImplementedException();
        }

        public bool TryGetMethodDetailsInfo(string assemblyName, string methodName, out MethodDetailsInfo mdInfo)
        {
            throw new NotImplementedException();
        }

        public bool TryUnloadLibrary(string assemblyFullName)
        {
            throw new NotImplementedException();
        }

        public void WriteLine(InfoType type, string message, InfoClass @class = InfoClass.Trivial)
        {
            throw new NotImplementedException();
        }



        #region IOC容器相关
        ISereinIOC ISereinIOC.Reset()
        {
            sereinIOC.Reset();
            lock (PersistennceInstance)
            {
                foreach (var kvp in PersistennceInstance)
                {
                    IOC.RegisterPersistennceInstance(kvp.Key, kvp.Value);
                }
            } // 重置后重新登记
            return this;
        }

        ISereinIOC ISereinIOC.Register(Type type, params object[] parameters)
        {
            sereinIOC.Register(type, parameters);
            return this;
        }

        ISereinIOC ISereinIOC.Register<T>(params object[] parameters)
        {
            sereinIOC.Register<T>(parameters);
            return this;
        }

        ISereinIOC ISereinIOC.Register<TService, TImplementation>(params object[] parameters)
        {
            sereinIOC.Register<TService, TImplementation>(parameters);
            return this;
        }

        //T ISereinIOC.GetOrRegisterInstantiate<T>()
        //{
        //    return sereinIOC.GetOrRegisterInstantiate<T>();

        //}

        //object ISereinIOC.GetOrRegisterInstantiate(Type type)
        //{
        //    return sereinIOC.GetOrRegisterInstantiate(type);
        //}

        object ISereinIOC.Get(Type type)
        {
            return sereinIOC.Get(type);

        }
        T ISereinIOC.Get<T>()
        {
            return (T)sereinIOC.Get(typeof(T));
        }
        T ISereinIOC.Get<T>(string key)
        {
            return sereinIOC.Get<T>(key);
        }


        bool ISereinIOC.RegisterPersistennceInstance(string key, object instance)
        {
            if (PersistennceInstance.ContainsKey(key))
            {
                return false;
            }
            PersistennceInstance.Add(key, instance); // 记录需要持久化的实例
            return sereinIOC.RegisterPersistennceInstance(key, instance);
        }

        bool ISereinIOC.RegisterInstance(string key, object instance)
        {
            return sereinIOC.RegisterInstance(key, instance);
        }


        object ISereinIOC.Instantiate(Type type)
        {
            return sereinIOC.Instantiate(type);
        }
        T ISereinIOC.Instantiate<T>()
        {
            return sereinIOC.Instantiate<T>();
        }
        ISereinIOC ISereinIOC.Build()
        {
            sereinIOC.Build();
            return this;
        }

        ISereinIOC ISereinIOC.Run<T>(Action<T> action)
        {
            sereinIOC.Run(action);
            return this;
        }

        ISereinIOC ISereinIOC.Run<T1, T2>(Action<T1, T2> action)
        {
            sereinIOC.Run(action);
            return this;
        }

        ISereinIOC ISereinIOC.Run<T1, T2, T3>(Action<T1, T2, T3> action)
        {
            sereinIOC.Run(action);
            return this;
        }

        ISereinIOC ISereinIOC.Run<T1, T2, T3, T4>(Action<T1, T2, T3, T4> action)
        {
            sereinIOC.Run(action);
            return this;
        }

        ISereinIOC ISereinIOC.Run<T1, T2, T3, T4, T5>(Action<T1, T2, T3, T4, T5> action)
        {
            sereinIOC.Run(action);
            return this;
        }

        ISereinIOC ISereinIOC.Run<T1, T2, T3, T4, T5, T6>(Action<T1, T2, T3, T4, T5, T6> action)
        {
            sereinIOC.Run(action);
            return this;
        }

        ISereinIOC ISereinIOC.Run<T1, T2, T3, T4, T5, T6, T7>(Action<T1, T2, T3, T4, T5, T6, T7> action)
        {
            sereinIOC.Run(action);
            return this;
        }

        ISereinIOC ISereinIOC.Run<T1, T2, T3, T4, T5, T6, T7, T8>(Action<T1, T2, T3, T4, T5, T6, T7, T8> action)
        {
            sereinIOC.Run(action);
            return this;
        }
        #endregion


    }
}
