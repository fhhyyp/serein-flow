using Serein.Script.Node;

namespace Serein.Script
{
    public sealed class SereinSciptException : Exception
    {
        //public ASTNode Node { get; }
        public override string Message { get; }

        public SereinSciptException(ASTNode node,    string message)
        {
            //this.Node = node;
            Message = $"异常信息 : {message} ，代码在第{node.Row}行: {node.Code.Trim()}";
        }
    }
}
