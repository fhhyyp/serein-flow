using Serein.Library;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Serein.Workbench.Avalonia.Api
{
    internal interface INodeControl
    {
        /// <summary>
        /// 对应的节点实体
        /// </summary>
        NodeModelBase NodeModelBase { get; }

        /// <summary>
        /// 初始化使用的方法，设置节点实体
        /// </summary>
        /// <param name="nodeModel"></param>
        void SetNodeModel(NodeModelBase nodeModel);
    }
}
