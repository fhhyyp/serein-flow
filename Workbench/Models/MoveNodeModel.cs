using Serein.Library;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Serein.Workbench.Models
{
    /// <summary>
    /// 拖拽创建节点使用的数据
    /// </summary>
    internal class MoveNodeModel
    {
        public NodeControlType NodeControlType { get; set; }
        public MethodDetailsInfo MethodDetailsInfo { get; set; }
    }
}
