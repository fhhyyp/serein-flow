namespace Serein.Script.Node
{
    public class ExpressionNode : ASTNode
    {
        /// <summary>
        /// 对象成员（嵌套获取）
        /// </summary>
        public ASTNode Value { get; }

        public ExpressionNode(ASTNode value)
        {
            this.Value = value;
        }
    }
}
