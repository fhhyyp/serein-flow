using Serein.Script.Node;

namespace Serein.Script
{
    public sealed class SereinSciptParserException : Exception
    {
        //public ASTNode Node { get; }
        public override string Message { get; }

        public SereinSciptParserException(ASTNode node,    string message)
        {
            //this.Node = node;
            Message = $"异常信息 : {message} ，代码在第{node.Row}行: {node.Code.Trim()}";
        }
    }

    public sealed class SereinSciptInterpreterExceptio : Exception
    {
        //public ASTNode Node { get; }
        public override string Message { get; }

        public SereinSciptInterpreterExceptio(ASTNode node,  string message)
        {
            //this.Node = node;
            Message = $"异常信息 : {message} ，代码在第{node.Row}行: {node.Code.Trim()}";
        }
        

        public override string StackTrace => string.Empty;
    }

}
