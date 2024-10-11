using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;

namespace Serein.Library.Entity
{
    /// <summary>
    /// 节点DLL依赖类，如果一个项目中引入了多个DLL，需要放置在同一个文件夹中
    /// </summary>
    public class NodeLibrary
    {
        /// <summary>
        /// 路径
        /// </summary>
        public string Path { get; set; }

        public Assembly Assembly { get; set; }
    }

}
