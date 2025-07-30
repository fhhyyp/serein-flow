using Serein.Library;
using Serein.Library.Api;
using System.Dynamic;

namespace Serein.NodeFlow.Model.Nodes
{

    
    /// <summary>
    /// Expression Operation - 表达式操作
    /// </summary>
    [FlowDataProperty(ValuePath = NodeValuePath.Node, IsNodeImp = true)]
    public partial class SingleGlobalDataNode : NodeModelBase
    {
        /// <summary>
        /// 全局数据的Key名称
        /// </summary>
        [DataInfo(IsNotification = true)] 
        private string _keyName;

    }
     
    /// <summary>
    /// 全局数据节点
    /// </summary>
    public partial class SingleGlobalDataNode : NodeModelBase, INodeContainer
    {
        string INodeContainer.Guid => this.Guid; 

        /// <summary>
        /// 全局数据节点是基础节点
        /// </summary>
        public override bool IsBase => true;
        /// <summary>
        /// 数据源只允许放置1个节点。
        /// </summary>
        public override int MaxChildrenCount => 1;

        /// <summary>
        /// 全局数据节点，允许放置一个数据源节点，通常是[Action]或[Script]节点，用于获取全局数据。
        /// </summary>
        /// <param name="environment"></param>
        public SingleGlobalDataNode(IFlowEnvironment environment) : base(environment)
        {
        }

        /// <summary>
        /// 数据来源的节点
        /// </summary>
        public IFlowNode? DataNode { get; private set; } = null;

        /// <summary>
        /// 有节点被放置
        /// </summary>
        /// <param name="nodeModel"></param>
        /// <returns></returns>
        public bool PlaceNode(IFlowNode nodeModel)
        {
            if(nodeModel.ControlType  is not (NodeControlType.Action or NodeControlType.Script))
            {
                SereinEnv.WriteLine(InfoType.INFO, "放置在全局数据必须是有返回值的[Action]节点，[Script]节点。");
                return false;
            }
            if (nodeModel.MethodDetails?.ReturnType is null 
                || nodeModel.MethodDetails.ReturnType == typeof(void))
            {
                SereinEnv.WriteLine(InfoType.INFO, "放置在全局数据必须是有返回值的[Action]节点，[Script]节点。");
                return false;
            }

            if(DataNode is null)
            {
                // 放置节点
                nodeModel.ContainerNode = this;  
                ChildrenNode.Add(nodeModel);
                DataNode = nodeModel;

                MethodDetails.IsAsync = nodeModel.MethodDetails.IsAsync;
                MethodDetails.ReturnType = nodeModel.MethodDetails.ReturnType;
                return true;
            }
            else if (DataNode.Guid != nodeModel.Guid)
            {
                Env.FlowEdit.TakeOutNodeToContainer(DataNode.CanvasDetails.Guid, DataNode.Guid);
                Env.FlowEdit.PlaceNodeToContainer(this.CanvasDetails.Guid, nodeModel.Guid, this.Guid);
                return false;
            }
            return false;
            
        }

        /// <summary>
        /// 从容器中取出节点
        /// </summary>
        /// <param name="nodeModel"></param>
        /// <returns></returns>
        public bool TakeOutNode(IFlowNode nodeModel)
        {
            if (ChildrenNode.Contains(nodeModel))
            {
                ChildrenNode.Remove(nodeModel);
                nodeModel.ContainerNode = null;
                DataNode = null;
                return true;
            }
            else
            {
                return false;
            }
            
        }

        /// <summary>
        /// 从容器中取出所有节点
        /// </summary>
        public void TakeOutAll()
        {
            foreach (var nodeModel in ChildrenNode) 
            {
                nodeModel.Env.FlowEdit.TakeOutNodeToContainer(nodeModel.CanvasDetails.Guid, nodeModel.Guid);
            }
            DataNode = null;
        }



        /// <summary>
        /// 设置全局数据
        /// </summary>
        /// <param name="context"></param>
        /// <param name="token"></param>
        /// <returns></returns>
        public override async Task<FlowResult> ExecutingAsync(IFlowContext context, CancellationToken token)
        {
            if (token.IsCancellationRequested) return FlowResult.Fail(this.Guid, context, "流程已通过token取消");
            if (string.IsNullOrEmpty(KeyName))
            {
                context.NextOrientation = ConnectionInvokeType.IsError;
                return FlowResult.Fail(this.Guid, context, $"全局数据的KeyName不能为空[{this.Guid}]");
            }
            if (DataNode is null)
            {
                context.NextOrientation = ConnectionInvokeType.IsError;
                return FlowResult.Fail(this.Guid, context, $"全局数据节点没有设置数据来源[{this.Guid}]");
            }


            var result = await DataNode.ExecutingAsync(context, token);
            if (result.IsSuccess)
            {
                SereinEnv.AddOrUpdateFlowGlobalData(KeyName, result.Value);
                return result;
            }
            else
            {
                return FlowResult.Fail(this.Guid, context, $"全局数据节点[{this.Guid}]执行失败，原因：{result.Message}。");
            }
        }
        
        /// <summary>
        /// 保存全局变量的数据
        /// </summary>
        /// <param name="nodeInfo"></param>
        /// <returns></returns>
        public override NodeInfo SaveCustomData(NodeInfo nodeInfo)
        {
            dynamic data = new ExpandoObject();
            data.KeyName = KeyName; // 变量名称

            nodeInfo.CustomData = data;
            return nodeInfo;
        }

        /// <summary>
        /// 加载全局变量的数据
        /// </summary>
        /// <param name="nodeInfo"></param>
        public override void LoadCustomData(NodeInfo nodeInfo)
        {
            KeyName = nodeInfo.CustomData?.KeyName;
        }

       /* /// <summary>
        /// 需要移除数据节点
        /// </summary>
        public override void Remove()
        {
            if (DataNode is null) {
                return;
            }
            // 移除数据节点
            _ = this.Env.RemoveNodeAsync(DataNode.CanvasDetails.Guid, DataNode.Guid);
        }*/

    }
}
