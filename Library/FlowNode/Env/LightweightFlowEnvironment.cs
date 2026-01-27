using Serein.Library.Api;
using Serein.Library.Utils;
using System;
using System.Threading.Tasks;

namespace Serein.Library
{

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
            Event = lightweightFlowEnvironmentEvent;
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
        public IFlowControl FlowControl { get; set; }
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

        public IFlowLibraryService FlowLibraryService => throw new NotImplementedException();

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
