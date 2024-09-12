namespace Serein.Flow.NodeModel
{

    public class SingleFlipflopNode : NodeBase
    {
        public override object Execute(DynamicContext context)
        {
            throw new NotImplementedException("无法以非await/async的形式调用触发器");
        }

    }
}
