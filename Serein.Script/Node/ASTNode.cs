using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Serein.Script.Node
{
    public abstract class ASTNode
    {
        public string Code { get; private set; }
        public int Row { get; private set; }
        public int StartIndex { get; private set; }
        public int Length { get; private set; }

        internal ASTNode SetTokenInfo(Token token)
        {
            Row = token.Row;
            StartIndex = token.StartIndex;
            Length = token.Length;
            Code = token.Code;
            return this;
        }
    }

}
