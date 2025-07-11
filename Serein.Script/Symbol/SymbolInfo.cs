using Serein.Script.Node;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Serein.Script.Symbol
{
    public enum SymbolType 
    {
        Identifier,
        FunctionReturn,
    }


    /// <summary>
    /// 符号信息
    /// </summary>
    public class SymbolInfo
    {
        /// <summary>
        /// 符号名称
        /// </summary>
        public string Name;

        /// <summary>
        /// 对应类型
        /// </summary>
        public Type Type;

        /// <summary>
        /// 节点
        /// </summary>
        public ASTNode Node;
    }
}
