using Serein.Library.Entity;
using Serein.Library.Enums;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;

namespace Serein.Library.Api
{

    public class FlowEventArgs : EventArgs
    {
        public bool IsSucceed { get; protected set; } = true;
        public string ErrorTips { get; protected set; } = string.Empty;
    }


    public delegate void FlowRunCompleteHandler(FlowEventArgs eventArgs);


    /// <summary>
    /// 加载节点
    /// </summary>
    public delegate void LoadNodeHandler(LoadNodeEventArgs eventArgs);
    public class LoadNodeEventArgs : FlowEventArgs
    {
        public LoadNodeEventArgs(NodeInfo NodeInfo, MethodDetails MethodDetailss)
        {
            this.NodeInfo = NodeInfo;
            this.MethodDetailss = MethodDetailss;  
        }
        public NodeInfo NodeInfo { get; protected set; }
        public MethodDetails MethodDetailss { get; protected set; }
    }


    /// <summary>
    /// 加载DLL
    /// </summary>
    /// <param name="assembly"></param>
    public delegate void LoadDLLHandler(LoadDLLEventArgs eventArgs);
    public class LoadDLLEventArgs : FlowEventArgs
    {
        public LoadDLLEventArgs(Assembly Assembly, List<MethodDetails> MethodDetailss)
        {
            this.Assembly = Assembly;
            this.MethodDetailss = MethodDetailss;
        }
        public Assembly Assembly { get; protected set; }
        public List<MethodDetails> MethodDetailss { get; protected set; }
    }

    /// <summary>
    /// 运行环境节点连接发生了改变
    /// </summary>
    /// <param name="fromNodeGuid"></param>
    /// <param name="toNodeGuid"></param>
    /// <param name="connectionType"></param>
    public delegate void NodeConnectChangeHandler(NodeConnectChangeEventArgs eventArgs);
    public class NodeConnectChangeEventArgs : FlowEventArgs
    {
        public enum ChangeTypeEnum
        {
            Create,
            Remote,
        }
        public NodeConnectChangeEventArgs(string fromNodeGuid, string toNodeGuid, ConnectionType connectionType, ChangeTypeEnum changeType)
        {
            this.FromNodeGuid = fromNodeGuid;
            this.ToNodeGuid = toNodeGuid;
            this.ConnectionType = connectionType;
            this.ChangeType = changeType;
        }
        public string FromNodeGuid { get; protected set; }
        public string ToNodeGuid { get; protected set; }
        public ConnectionType ConnectionType { get; protected set; }
        public ChangeTypeEnum ChangeType { get; protected set; }
    }

    /// <summary>
    /// 添加了节点
    /// </summary>
    /// <param name="fromNodeGuid"></param>
    /// <param name="toNodeGuid"></param>
    /// <param name="connectionType"></param>
    public delegate void NodeCreateHandler(NodeCreateEventArgs eventArgs);
    public class NodeCreateEventArgs : FlowEventArgs
    {
        public NodeCreateEventArgs(object nodeModel)
        {
            this.NodeModel = nodeModel;
        }
        public object NodeModel { get; private set; }
    }



    public delegate void NodeRemoteHandler(NodeRemoteEventArgs eventArgs);
    public class NodeRemoteEventArgs : FlowEventArgs
    {
        public NodeRemoteEventArgs(string nodeGuid)
        {
            this.NodeGuid = nodeGuid;
        }
        public string NodeGuid { get; private set; }
    }


    public delegate void StartNodeChangeHandler(StartNodeChangeEventArgs eventArgs);


    public class StartNodeChangeEventArgs: FlowEventArgs
    {
        public StartNodeChangeEventArgs(string oldNodeGuid, string newNodeGuid)
        {
            this.OldNodeGuid = oldNodeGuid;
            this.NewNodeGuid = newNodeGuid; ;
        }
        public string OldNodeGuid {  get; private set; }
        public string NewNodeGuid {  get; private set; }
    }


    public interface IFlowEnvironment
    {
        event FlowRunCompleteHandler OnFlowRunComplete;
        event LoadNodeHandler OnLoadNode;
        event LoadDLLHandler OnDllLoad;
        event NodeConnectChangeHandler OnNodeConnectChange;
        event NodeCreateHandler OnNodeCreate;
        event NodeRemoteHandler OnNodeRemote;
        event StartNodeChangeHandler OnStartNodeChange;

        /// <summary>
        /// 保存当前项目
        /// </summary>
        /// <returns></returns>
        SereinOutputFileData SaveProject();
        /// <summary>
        /// 加载项目文件
        /// </summary>
        /// <param name="projectFile"></param>
        /// <param name="filePath"></param>
        void LoadProject(SereinOutputFileData projectFile, string filePath);
        /// <summary>
        /// 从文件中加载Dll
        /// </summary>
        /// <param name="dllPath"></param>
        void LoadDll(string dllPath);
        /// <summary>
        /// 清理加载的DLL（待更改）
        /// </summary>
        void ClearAll();
        /// <summary>
        /// 获取方法描述
        /// </summary>
        /// <param name="name"></param>
        /// <param name="md"></param>
        /// <returns></returns>
        bool TryGetMethodDetails(string methodName,out MethodDetails md);

        /// <summary>
        /// 开始运行
        /// </summary>
        Task StartAsync();
        /// <summary>
        /// 结束运行
        /// </summary>
        void Exit();

        /// <summary>
        /// 设置流程起点节点
        /// </summary>
        /// <param name="nodeGuid"></param>
        void SetStartNode(string nodeGuid);
        /// <summary>
        /// 在两个节点之间创建连接关系
        /// </summary>
        /// <param name="fromNodeGuid">起始节点Guid</param>
        /// <param name="toNodeGuid">目标节点Guid</param>
        /// <param name="connectionType">连接类型</param>
        void ConnectNode(string fromNodeGuid, string toNodeGuid, ConnectionType connectionType);
        /// <summary>
        /// 创建节点/区域/基础控件
        /// </summary>
        /// <param name="nodeBase">节点/区域/基础控件</param>
        /// <param name="methodDetails">节点绑定的方法说明（</param>
        void CreateNode(NodeControlType nodeBase, MethodDetails methodDetails = null);
        /// <summary>
        /// 移除两个节点之间的连接关系
        /// </summary>
        /// <param name="fromNodeGuid">起始节点</param>
        /// <param name="toNodeGuid">目标节点</param>
        /// <param name="connectionType">连接类型</param>
        void RemoteConnect(string fromNodeGuid, string toNodeGuid, ConnectionType connectionType);
        /// <summary>
        /// 移除节点/区域/基础控件
        /// </summary>
        /// <param name="nodeGuid">待移除的节点Guid</param>
        void RemoteNode(string nodeGuid);



    }

}
