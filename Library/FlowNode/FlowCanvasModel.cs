using Serein.Library.Api;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Serein.Library
{

    [NodeProperty(ValuePath = NodeValuePath.Node)]
    public partial class FlowCanvasModel
    {
        public FlowCanvasModel(IFlowEnvironment env)
        {
            Env = env;
        }
        public IFlowEnvironment Env { get; }

        [PropertyInfo(IsProtection = true)]
        private string _guid;

        [PropertyInfo(IsNotification = true)]
        private string _name;

        [PropertyInfo(IsNotification = true)]
        private double _width;

        [PropertyInfo(IsNotification = true)]
        private double _height;

        /// <summary>
        /// 预览位置X
        /// </summary>
        [PropertyInfo]
        private double _viewX ;

        /// <summary>
        /// 预览位置Y
        /// </summary>
        [PropertyInfo]
        private double _viewY ;

        /// <summary>
        /// 缩放比例X
        /// </summary>
        [PropertyInfo]
        private double _scaleX ;

        /// <summary>
        /// 缩放比例Y
        /// </summary>
        [PropertyInfo]
        private double _scaleY ;

    }
}
