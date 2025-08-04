using Serein.Library.Api;
using System.Collections.Generic;

namespace Serein.Library
{




    /// <summary>
    /// 流程画布
    /// </summary>
    [FlowDataProperty(ValuePath = NodeValuePath.Node)]
    public partial class FlowCanvasDetails
    {
        /// <summary>
        /// 流程画布的构造函数
        /// </summary>
        /// <param name="env"></param>
        public FlowCanvasDetails(IFlowEnvironment env)
        {
            Env = env;
        }
        /// <summary>
        /// 流程画布的运行环境
        /// </summary>

        public IFlowEnvironment Env { get; }

        /// <summary>
        /// 画布拥有的节点
        /// </summary>
        [DataInfo(IsProtection = false)]
        private List<IFlowNode> _nodes = [];
        //private System.Collections.ObjectModel.ObservableCollection<IFlowNode> _nodes = [];
        
        /// <summary>
        /// 画布公开的节点
        /// </summary>
        [DataInfo(IsProtection = false)]
        private List<IFlowNode> _publicNodes = [];

        /// <summary>
        /// 标识画布ID
        /// </summary>
        [DataInfo(IsProtection = false)]
        private string _guid;

        /// <summary>
        /// 画布名称
        /// </summary>
        [DataInfo(IsNotification = true)]
        private string _name;

        /// <summary>
        /// 画布宽度
        /// </summary>
        [DataInfo(IsNotification = true)]
        private double _width;

        /// <summary>
        /// 画布高度
        /// </summary>
        [DataInfo(IsNotification = true)]
        private double _height;

        /// <summary>
        /// 预览位置X
        /// </summary>
        [DataInfo(IsNotification = true)]
        private double _viewX;

        /// <summary>
        /// 预览位置Y
        /// </summary>
        [DataInfo(IsNotification = true)]
        private double _viewY;

        /// <summary>
        /// 缩放比例X
        /// </summary>
        [DataInfo(IsNotification = true)]
        private double _scaleX = 1;

        /// <summary>
        /// 缩放比例Y
        /// </summary>
        [DataInfo(IsNotification = true)]
        private double _scaleY = 1;

        /// <summary>
        /// 起始节点
        /// </summary>
        [DataInfo]
        private IFlowNode? _startNode;

    }


    public partial class FlowCanvasDetails
    {
    }


}
