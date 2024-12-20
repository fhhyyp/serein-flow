using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Serein.Script.Node
{
    /// <summary>
    /// 程序入口
    /// </summary>
    public class ProgramNode : ASTNode
    {
        public List<ASTNode> Statements { get; }

        public ProgramNode(List<ASTNode> statements)
        {
            Statements = statements;
        }
    }

}
