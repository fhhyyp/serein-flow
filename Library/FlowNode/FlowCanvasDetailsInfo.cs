using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Serein.Library
{

    /// <summary>
    /// 画布信息
    /// </summary>
    public class FlowCanvasDetailsInfo
    {
        /// <summary>
        /// 起始节点Guid
        /// </summary>
        public string StartNode;

        /// <summary>
        /// 标识画布ID
        /// </summary>
        public string Guid;

        /// <summary>
        /// 画布名称
        /// </summary>
        public string Name;

        /// <summary>
        /// 画布宽度
        /// </summary>
        public double Width;

        /// <summary>
        /// 画布高度
        /// </summary>
        public double Height;

        /// <summary>
        /// 预览位置X
        /// </summary>
        public double ViewX;

        /// <summary>
        /// 预览位置Y
        /// </summary>
        public double ViewY;

        /// <summary>
        /// 缩放比例X
        /// </summary>
        public double ScaleX;

        /// <summary>
        /// 缩放比例Y
        /// </summary>
        public double ScaleY;
    }
}


