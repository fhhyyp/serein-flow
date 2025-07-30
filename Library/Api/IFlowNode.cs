using System.Collections.Generic;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;

namespace Serein.Library.Api
{

    /// <summary>
    /// 流程节点
    /// </summary>
    public interface IFlowNode : INotifyPropertyChanged, ISereinFlow
    {
        /// <summary>
        /// 节点持有的运行环境
        /// </summary>
        IFlowEnvironment Env {  get; set; }

        /// <summary>
        /// 节点唯一标识
        /// </summary>
        string Guid { get; set; }

        /// <summary>
        /// 节点的类型
        /// </summary>
        NodeControlType ControlType { get; set; }

        /// <summary>
        /// 节点所在画布
        /// </summary>
        FlowCanvasDetails CanvasDetails {  get; set; }

        /// <summary>
        /// 节点位置
        /// </summary>
        PositionOfUI Position { get; set; }

        /// <summary>
        /// 节点显示名称
        /// </summary>
        string DisplayName { get; set; }

        /// <summary>
        /// 是否作为公开的节点，用以“流程接口”节点调用
        /// </summary>
        bool IsPublic { get; set; }

        /// <summary>
        /// 是否为基础节点，指示节点创建中的行为
        /// </summary>
        bool IsBase { get; } 

        /// <summary>
        /// 最多可以放置几个节点，当该节点具有容器功能时，用来指示其容器的行为。
        /// </summary>
        int MaxChildrenCount { get;}

        /// <summary>
        /// 调试器
        /// </summary>
        NodeDebugSetting DebugSetting { get; set; }

        /// <summary>
        /// 节点方法描述，包含入参数据
        /// </summary>
        MethodDetails MethodDetails { get; set; }

        /// <summary>
        /// 前继节点集合
        /// </summary>
        Dictionary<ConnectionInvokeType, List<IFlowNode>> PreviousNodes { get;}
        /// <summary>
        /// 后继节点集合
        /// </summary>
        Dictionary<ConnectionInvokeType, List<IFlowNode>> SuccessorNodes { get; set; }

        /// <summary>
        /// 需要该节点返回结果作为入参参数的节点集合
        /// </summary>
        Dictionary<ConnectionArgSourceType, List<IFlowNode>> NeedResultNodes { get;  }

        /// <summary>
        /// 当该节点放置在某个具有容器行为的节点时，该值指示其容器节点
        /// </summary>
        IFlowNode ContainerNode { get; set; }

        /// <summary>
        /// 当该节点具备容器行为时，该集合包含其容器中的节点
        /// </summary>
        List<IFlowNode> ChildrenNode { get; }

        /// <summary>
        /// 节点创建时的行为
        /// </summary>
        void OnCreating();

        /// <summary>
        /// 节点保存时如若需要保存自定义数据，可通过该方法进行控制保存逻辑
        /// </summary>
        /// <param name="nodeInfo"></param>
        /// <returns></returns>
        NodeInfo SaveCustomData(NodeInfo nodeInfo);

        /// <summary>
        /// 节点从信息创建后需要加载自定义数据时，可通过该方法进行控制加载逻辑
        /// </summary>
        /// <param name="nodeInfo"></param>
        void LoadCustomData(NodeInfo nodeInfo);

        /// <summary>
        /// 节点执行方法
        /// </summary>
        /// <param name="context"></param>
        /// <param name="token"></param>
        /// <returns></returns>
        Task<FlowResult> ExecutingAsync(IFlowContext context, CancellationToken token);

        /// <summary>
        /// 以该节点开始执行流程，通常用于流程的入口节点。
        /// </summary>
        /// <param name="context"></param>
        /// <param name="token"></param>
        /// <returns></returns>
        Task<FlowResult> StartFlowAsync(IFlowContext context, CancellationToken token);
    }
}
