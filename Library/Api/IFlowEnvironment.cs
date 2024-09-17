using Serein.Library.Entity;
using Serein.Library.Enums;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;

namespace Serein.Library.Api
{
    #region 环境委托
    /// <summary>
    /// 流程运行完成
    /// </summary>
    /// <param name="eventArgs"></param>
    public delegate void FlowRunCompleteHandler(FlowEventArgs eventArgs);

    /// <summary>
    /// 项目加载完成
    /// </summary>
    public delegate void ProjectLoadedHandler(ProjectLoadedEventArgs eventArgs);

    /// <summary>
    /// 加载项目文件时成功加载了DLL文件
    /// </summary>
    public delegate void LoadDLLHandler(LoadDLLEventArgs eventArgs);

    /// <summary>
    /// 运行环境节点连接发生了改变
    /// </summary>
    /// <param name="fromNodeGuid"></param>
    /// <param name="toNodeGuid"></param>
    /// <param name="connectionType"></param>
    public delegate void NodeConnectChangeHandler(NodeConnectChangeEventArgs eventArgs);

    /// <summary>
    /// 环境中加载了一个节点
    /// </summary>
    /// <param name="fromNodeGuid"></param>
    /// <param name="toNodeGuid"></param>
    /// <param name="connectionType"></param>
    public delegate void NodeCreateHandler(NodeCreateEventArgs eventArgs);

    /// <summary>
    /// 环境中流程起始节点发生了改变
    /// </summary>
    /// <param name="eventArgs"></param>
    public delegate void StartNodeChangeHandler(StartNodeChangeEventArgs eventArgs); 
    #endregion

    #region 环境事件签名

    /// <summary>
    /// 流程事件签名基类
    /// </summary>
    public class FlowEventArgs : EventArgs
    {
        /// <summary>
        /// 是否完成
        /// </summary>
        public bool IsSucceed { get; protected set; } = true;
        /// <summary>
        /// 错误提示
        /// </summary>
        public string ErrorTips { get; protected set; } = string.Empty;
    }

    //public class LoadNodeEventArgs : FlowEventArgs
    //{
    //    public LoadNodeEventArgs(NodeInfo NodeInfo, MethodDetails MethodDetailss)
    //    {
    //        this.NodeInfo = NodeInfo;
    //        this.MethodDetailss = MethodDetailss;
    //    }
    //    /// <summary>
    //    /// 项目文件节点信息参数
    //    /// </summary>
    //    public NodeInfo NodeInfo { get; protected set; }
    //    /// <summary>
    //    /// 已加载在环境中的方法描述
    //    /// </summary>
    //    public MethodDetails MethodDetailss { get; protected set; }
    //}

    public class ProjectLoadedEventArgs : FlowEventArgs
    {
        public ProjectLoadedEventArgs()
        {
        }
    }

    public class LoadDLLEventArgs : FlowEventArgs
    {
        public LoadDLLEventArgs(Assembly Assembly, List<MethodDetails> MethodDetailss)
        {
            this.Assembly = Assembly;
            this.MethodDetailss = MethodDetailss;
        }
        /// <summary>
        /// 已加载了的程序集
        /// </summary>
        public Assembly Assembly { get; protected set; }
        /// <summary>
        /// dll文件中有效的流程方法描述
        /// </summary>
        public List<MethodDetails> MethodDetailss { get; protected set; }
    }


    public class NodeConnectChangeEventArgs : FlowEventArgs
    {
        /// <summary>
        /// 连接关系改变类型
        /// </summary>
        public enum ConnectChangeType
        {
            /// <summary>
            /// 创建
            /// </summary>
            Create,
            /// <summary>
            /// 移除
            /// </summary>
            Remote,
        }
        public NodeConnectChangeEventArgs(string fromNodeGuid, string toNodeGuid, ConnectionType connectionType, ConnectChangeType changeType)
        {
            this.FromNodeGuid = fromNodeGuid;
            this.ToNodeGuid = toNodeGuid;
            this.ConnectionType = connectionType;
            this.ChangeType = changeType;
        }
        /// <summary>
        /// 连接关系中始节点的Guid
        /// </summary>
        public string FromNodeGuid { get; protected set; }
        /// <summary>
        /// 连接关系中目标节点的Guid
        /// </summary>
        public string ToNodeGuid { get; protected set; }
        /// <summary>
        /// 连接类型
        /// </summary>
        public ConnectionType ConnectionType { get; protected set; }
        /// <summary>
        /// 表示此次需要在两个节点之间创建连接关系，或是移除连接关系
        /// </summary>
        public ConnectChangeType ChangeType { get; protected set; }
    }


    public class NodeCreateEventArgs : FlowEventArgs
    {
        public NodeCreateEventArgs(object nodeModel, Position position)
        {
            this.NodeModel = nodeModel;
            this.Position = position;
        }
        public NodeCreateEventArgs(object nodeModel, bool isAddInRegion, string regeionGuid)
        {
            this.NodeModel = nodeModel;
            this.RegeionGuid = regeionGuid;
            this.IsAddInRegion = isAddInRegion;
        }

        /// <summary>
        /// 节点Model对象，目前需要手动转换对应的类型
        /// </summary>
        public object NodeModel { get; private set; }
        public Position Position { get; private set; }
        public bool IsAddInRegion { get; private set; }
        public string RegeionGuid { get; private set; }
    }

    /// <summary>
    /// 环境中移除了一个节点
    /// </summary>
    /// <param name="eventArgs"></param>

    public delegate void NodeRemoteHandler(NodeRemoteEventArgs eventArgs);
    public class NodeRemoteEventArgs : FlowEventArgs
    {
        public NodeRemoteEventArgs(string nodeGuid)
        {
            this.NodeGuid = nodeGuid;
        }
        /// <summary>
        /// 被移除节点的Guid
        /// </summary>
        public string NodeGuid { get; private set; }
    }




    public class StartNodeChangeEventArgs : FlowEventArgs
    {
        public StartNodeChangeEventArgs(string oldNodeGuid, string newNodeGuid)
        {
            this.OldNodeGuid = oldNodeGuid;
            this.NewNodeGuid = newNodeGuid; ;
        }
        /// <summary>
        /// 原来的起始节点Guid
        /// </summary>
        public string OldNodeGuid { get; private set; }
        /// <summary>
        /// 新的起始节点Guid
        /// </summary>
        public string NewNodeGuid { get; private set; }
    }
    #endregion

    public interface IFlowEnvironment
    {

        event FlowRunCompleteHandler OnFlowRunComplete;
        event ProjectLoadedHandler OnProjectLoaded;
        event LoadDLLHandler OnDllLoad;
        event NodeConnectChangeHandler OnNodeConnectChange;
        event NodeCreateHandler OnNodeCreate;
        event NodeRemoteHandler OnNodeRemote;
        event StartNodeChangeHandler OnStartNodeChange;

        /// <summary>
        /// 保存当前项目
        /// </summary>
        /// <returns></returns>
        SereinProjectData SaveProject();
        /// <summary>
        /// 加载项目文件
        /// </summary>
        /// <param name="projectFile"></param>
        /// <param name="filePath"></param>
        void LoadProject(SereinProjectData projectFile, string filePath);
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
        void CreateNode(NodeControlType nodeBase, Position position, MethodDetails methodDetails = null);
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
