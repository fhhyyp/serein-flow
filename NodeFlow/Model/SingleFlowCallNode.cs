using Newtonsoft.Json.Linq;
using Serein.Library;
using Serein.Library.Api;
using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace Serein.NodeFlow.Model
{

    [NodeProperty(ValuePath = NodeValuePath.Node)]
    public partial class SingleFlowCallNode
    {
        
        

        /// <summary>
        /// 使用目标节点的参数（如果为true，则使用目标节点的入参，如果为false，则使用节点自定义入参）
        /// </summary>
        [PropertyInfo(IsNotification = true)]
        private bool _isShareParam = true ;
    }



    /// <summary>
    /// 流程调用节点
    /// </summary>
    public partial class SingleFlowCallNode : NodeModelBase
    {
        /// <summary>
        /// 接口节点
        /// </summary>
        private NodeModelBase targetNode;
        /// <summary>
        /// 接口节点Guid
        /// </summary>
        public string? TargetNodeGuid => targetNode?.Guid;


        public SingleFlowCallNode(IFlowEnvironment environment) : base(environment)
        {

        }

        /// <summary>
        /// 重置接口节点
        /// </summary>
        public void ResetTargetNode()
        {
            if(targetNode is not null)
            {
                // 取消接口
                targetNode.PropertyChanged -= TargetNode_PropertyChanged;
                this.MethodDetails = null;
                foreach (ConnectionInvokeType ctType in NodeStaticConfig.ConnectionTypes)
                {
                    this.SuccessorNodes[ctType] = new List<NodeModelBase>();
                }
            }
           
            
        }

        /// <summary>
        /// 设置接口节点（如果传入null，则视为取消设置）
        /// </summary>
        /// <param name="value"></param>
        public void SetTargetNode(NodeModelBase? value)
        {
            if( value is null)
            {
                return;
            }
            this.targetNode = value;
            tmpMethodDetails = targetNode.MethodDetails.CloneOfNode(this); // 从目标节点复制一份
            targetNode.PropertyChanged += TargetNode_PropertyChanged;
            this.MethodDetails = tmpMethodDetails;
            this.SuccessorNodes = targetNode.SuccessorNodes;
        }

        private MethodDetails tmpMethodDetails;
        partial void OnIsShareParamChanged(bool value)
        {
            if (targetNode is null)
            {
                return;
            }
            if (value)
            {
                tmpMethodDetails = this.MethodDetails;
                this.MethodDetails = targetNode.MethodDetails;
            }
            else
            {
                this.MethodDetails = tmpMethodDetails;
                OnPropertyChanged(nameof(MethodDetails));
            }
        }

        /*  partial void OnTargetNodeGuidChanged(string value)
          {
              var guid = value;
              if (string.IsNullOrEmpty(guid))
              {
                  targetNode = null;
                  return;
              }
              if (!Env.TryGetNodeModel(guid, out targetNode))
              {
                  SereinEnv.WriteLine(InfoType.ERROR, $"流程接口找不到节点{guid}");
                  return;
              }
              SetTargetNode(targetNode);
              //OnIsShareParamChanged(IsShareParam); // 更新参数状态
          }*/


        private void TargetNode_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            // 如果不再公开
            if (sender is NodeModelBase node && !node.IsPublic)
            {
                this.SuccessorNodes = [];
                targetNode.PropertyChanged -= TargetNode_PropertyChanged;
            }
        }


        /// <summary>
        /// 从节点Guid刷新实体
        /// </summary>
        /// <returns></returns>
        private bool UploadTargetNode()
        {
            if (targetNode is null)
            {
                if (string.IsNullOrWhiteSpace(TargetNodeGuid))
                {
                    return false;
                }

                if (!Env.TryGetNodeModel(TargetNodeGuid, out var targetNode) || targetNode is null)
                {
                    return false;
                }

                this.targetNode = targetNode;
            }
            return true;

        }

        /// <summary>
        /// 需要调用其它流程图中的某个节点
        /// </summary>
        /// <param name="context"></param>
        /// <param name="token"></param>
        /// <returns></returns>
        public override async Task<FlowResult> ExecutingAsync(IDynamicContext context, CancellationToken token)
        {
            if (!UploadTargetNode())
            {
                throw new ArgumentNullException();
            }
            return await base.ExecutingAsync(context, token); 
        }


        /// <summary>
        /// 保存全局变量的数据
        /// </summary>
        /// <param name="nodeInfo"></param>
        /// <returns></returns>
        public override NodeInfo SaveCustomData(NodeInfo nodeInfo)
        {
            dynamic data = new ExpandoObject();
            data.TargetNodeGuid = targetNode?.Guid; // 变量名称
            data.IsShareParam = IsShareParam;
            nodeInfo.CustomData = data;
            return nodeInfo;
        }

        /// <summary>
        /// 加载全局变量的数据
        /// </summary>
        /// <param name="nodeInfo"></param>
        public override void LoadCustomData(NodeInfo nodeInfo)
        {
            string targetNodeGuid = nodeInfo.CustomData?.TargetNodeGuid ?? "";
            this.IsShareParam = nodeInfo.CustomData?.IsShareParam;
            if (Env.TryGetNodeModel(targetNodeGuid, out var targetNode))
            {
                this.targetNode = targetNode;
            }
            else
            {
                SereinEnv.WriteLine(InfoType.ERROR, $"流程接口节点[{this.Guid}]无法找到对应的节点:{targetNodeGuid}");
            }
        }



    }
}