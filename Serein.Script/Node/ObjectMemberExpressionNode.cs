namespace Serein.Script.Node
{
    public class ObjectMemberExpressionNode : ASTNode
    {
        /// <summary>
        /// 对象成员（嵌套获取）
        /// </summary>
        public ASTNode Value { get; }

        public ObjectMemberExpressionNode(ASTNode value)
        {
            this.Value = value;
        }
    }
}
