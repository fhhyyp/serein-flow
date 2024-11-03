using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Serein.Library
{
    /// <summary>
    /// 方法描述信息
    /// </summary>
    public class MethodDetailsInfo
    {
        /// <summary>
        /// 属于哪个程序集
        /// </summary>
        public string AssemblyName { get; set; }

        /// <summary>
        /// 方法名称
        /// </summary>
        public string MethodName { get; set; }

        /// <summary>
        /// 节点类型
        /// </summary>
        public string NodeType { get; set; }

        /// <summary>
        /// 方法说明
        /// </summary>
        public string MethodAnotherName { get; set; }

        /// <summary>
        /// 参数内容
        /// </summary>

        public ParameterDetailsInfo[] ParameterDetailsInfos { get; set; }

        /// <summary>
        /// 可选参数信息
        /// </summary>
        public int IsParamsArgIndex { get; set; }

        /// <summary>
        /// 出参类型
        /// </summary>
        public string ReturnTypeFullName { get; set; }
    }

}
