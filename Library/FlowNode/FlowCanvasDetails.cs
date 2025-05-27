using Serein.Library.Api;
using Serein.Library.FlowNode;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Serein.Library
{




    /// <summary>
    /// 流程画布
    /// </summary>
    [NodeProperty(ValuePath = NodeValuePath.Node)]
    public partial class FlowCanvasDetails
    {
        public FlowCanvasDetails(IFlowEnvironment env)
        {
            Env = env;
        }


        public IFlowEnvironment Env { get; }

        /// <summary>
        /// 画布拥有的节点
        /// </summary>
        [PropertyInfo(IsProtection = true)]
        private System.Collections.ObjectModel.ObservableCollection<NodeModelBase> _nodes = [];
        
        /// <summary>
        /// 画布公开的节点
        /// </summary>
        [PropertyInfo(IsProtection = true)]
        private System.Collections.ObjectModel.ObservableCollection<NodeModelBase> _publicNodes = [];

        /// <summary>
        /// 标识画布ID
        /// </summary>
        [PropertyInfo(IsProtection = false)]
        private string _guid;

        /// <summary>
        /// 画布名称
        /// </summary>
        [PropertyInfo(IsNotification = true)]
        private string _name;

        /// <summary>
        /// 画布宽度
        /// </summary>
        [PropertyInfo(IsNotification = true)]
        private double _width;

        /// <summary>
        /// 画布高度
        /// </summary>
        [PropertyInfo(IsNotification = true)]
        private double _height;

        /// <summary>
        /// 预览位置X
        /// </summary>
        [PropertyInfo(IsNotification = true)]
        private double _viewX;

        /// <summary>
        /// 预览位置Y
        /// </summary>
        [PropertyInfo(IsNotification = true)]
        private double _viewY;

        /// <summary>
        /// 缩放比例X
        /// </summary>
        [PropertyInfo(IsNotification = true)]
        private double _scaleX = 1;

        /// <summary>
        /// 缩放比例Y
        /// </summary>
        [PropertyInfo(IsNotification = true)]
        private double _scaleY = 1;


        /// <summary>
        /// 起始节点私有属性
        /// </summary>
        private string _startNode;

    }


    public partial class FlowCanvasDetails
    {


    }


}
