using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;

namespace Serein.Library
{
    /// <summary>
    /// 节点DLL依赖类，如果一个项目中引入了多个DLL，需要放置在同一个文件夹中
    /// </summary>
    public class NodeLibrary
    {
        /// <summary>
        /// 文件名
        /// </summary>
        public string FileName { get; set; }

        /// <summary>
        /// 路径
        /// </summary>
        public string FilePath { get; set; }

        /// <summary>
        /// 依赖类的名称
        /// </summary>
        public string FullName{ get; set; }

        /// <summary>
        /// 对应的程序集
        /// </summary>
        public Assembly Assembly { get; set; }
    }

}
