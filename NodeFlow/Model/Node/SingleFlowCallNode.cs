using Newtonsoft.Json.Linq;
using Serein.Library;
using Serein.Library.Api;
using Serein.Script;
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
        /// 目标公开节点
        /// </summary>
        [PropertyInfo(IsNotification = true)]
        private string targetNodeGuid;

        /// <summary>
        /// 使用目标节点的参数（如果为true，则使用目标节点的入参，如果为false，则使用节点自定义入参）
        /// </summary>
        [PropertyInfo(IsNotification = true)]
        private bool _isShareParam  ;
    }



    /// <summary>
    /// 流程调用节点
    /// </summary>
    public partial class SingleFlowCallNode : NodeModelBase
    {
        /// <summary>
        /// 被调用的节点
        /// </summary>
        private IFlowNode targetNode;
        /// <summary>
        /// 缓存的方法信息
        /// </summary>
        public MethodDetails CacheMethodDetails { get; private set; }




        public SingleFlowCallNode(IFlowEnvironment environment) : base(environment)
        {

        }


        /// <summary>
        /// 重置接口节点
        /// </summary>
        public void ResetTargetNode()
        {
            if (targetNode is not null)
            {
                // 取消接口
                TargetNodeGuid = string.Empty;
            }
        }

        /// <summary>
        /// 设置接口节点
        /// </summary>
        /// <param name="nodeGuid"></param>
        public void SetTargetNode(string? nodeGuid)
        {
            if (nodeGuid is null || !Env.TryGetNodeModel(nodeGuid, out _))
            {
                return;
            }
            TargetNodeGuid = nodeGuid;
        }

        partial void OnTargetNodeGuidChanged(string value)
        {
            if (string.IsNullOrEmpty(value) || !Env.TryGetNodeModel(value, out targetNode))
            {
                // 取消设置接口节点
                targetNode.PropertyChanged -= TargetNode_PropertyChanged;
                this.MethodDetails = new MethodDetails();
            }
            else
            {
                //if (this.MethodDetails.ActingInstanceType.FullName.Equals())
                
                if(!this.IsShareParam 
                    && CacheMethodDetails is not null
                    && targetNode.MethodDetails is not null
                    && targetNode.MethodDetails.AssemblyName == CacheMethodDetails.AssemblyName
                    && targetNode.MethodDetails.MethodName == CacheMethodDetails.MethodName)
                {
                    this.MethodDetails = CacheMethodDetails;
                }
                else
                {
                    if (targetNode.MethodDetails is not null)
                    {
                        CacheMethodDetails = targetNode.MethodDetails.CloneOfNode(this); // 从目标节点复制一份
                        targetNode.PropertyChanged += TargetNode_PropertyChanged;
                        this.MethodDetails = CacheMethodDetails;
                    }
               
                }
                
            }
            OnPropertyChanged(nameof(MethodDetails));
        }

        partial void OnIsShareParamChanged(bool value)
        {
            if (targetNode is null || targetNode.MethodDetails is null)
            {
                return;
            }
            if (value)
            {
                CacheMethodDetails = targetNode.MethodDetails.CloneOfNode(this);
                this.MethodDetails = CacheMethodDetails;
            }
            else
            {

                if(targetNode.ControlType == NodeControlType.Script)
                {
                    // 脚本节点入参需不可编辑入参数量、入参名称
                    foreach (var item in CacheMethodDetails.ParameterDetailss)
                    {
                        item.IsParams = false;
                    }
                }
                this.MethodDetails = CacheMethodDetails;
            }

            OnPropertyChanged(nameof(MethodDetails));
        }

        private void TargetNode_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            // 如果不再公开
            if (sender is NodeModelBase node && !node.IsPublic)
            {
                foreach (ConnectionInvokeType ctType in NodeStaticConfig.ConnectionTypes)
                {
                    this.SuccessorNodes[ctType] = [];
                }
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
            CacheMethodDetails = this.MethodDetails; // 缓存
            string targetNodeGuid = nodeInfo.CustomData?.TargetNodeGuid ?? "";
            this.IsShareParam = nodeInfo.CustomData?.IsShareParam;
            if (Env.TryGetNodeModel(targetNodeGuid, out var targetNode))
            {
                TargetNodeGuid = targetNode.Guid;
                this.targetNode = targetNode;
            }
            else
            {
                SereinEnv.WriteLine(InfoType.ERROR, $"流程接口节点[{this.Guid}]无法找到对应的节点:{targetNodeGuid}");
            }

        }


        /*public override void Remove()
        {
            var tmp = this;
            targetNode = null;
            CacheMethodDetails = null;
        }
        */


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
            if (IsShareParam)
            {
                this.MethodDetails = targetNode.MethodDetails;
            }
            this.SuccessorNodes = targetNode.SuccessorNodes;

            FlowResult flowData = await (targetNode.ControlType switch
            {
                NodeControlType.Script => ((SingleScriptNode)targetNode).ExecutingAsync(this, context, token),
                _ => base.ExecutingAsync(context, token)
            });


            if (IsShareParam)
            {
                // 设置运行时上一节点

                // 此处代码与SereinFlow.Library.FlowNode.ParameterDetails
                // ToMethodArgData()方法中判断流程接口节点分支逻辑耦合
                // 不要轻易修改
                context.AddOrUpdate(targetNode, flowData);
                foreach (ConnectionInvokeType ctType in NodeStaticConfig.ConnectionTypes)
                {
                    if (this.SuccessorNodes[ctType] == null) continue;
                    foreach (var node in this.SuccessorNodes[ctType])
                    {
                        if (node.DebugSetting.IsEnable)
                        {

                            context.SetPreviousNode(node, this);
                        }
                    }
                }
            }


            return flowData;
        }



    }
}