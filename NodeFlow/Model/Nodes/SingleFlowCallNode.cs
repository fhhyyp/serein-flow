using Serein.Library;
using Serein.Library.Api;
using Serein.NodeFlow.Services;
using System.Dynamic;
using System.Reflection;

namespace Serein.NodeFlow.Model.Nodes
{

    [FlowDataProperty(ValuePath = NodeValuePath.Node, IsNodeImp = true)]
    public partial class SingleFlowCallNode
    {
        /// <summary>
        /// 目标公开节点
        /// </summary>
        [DataInfo(IsNotification = true)]
        private string targetNodeGuid = string.Empty;

        /// <summary>
        /// 使用目标节点的参数（如果为true，则使用目标节点的入参，如果为false，则使用节点自定义入参）
        /// </summary>
        [DataInfo(IsNotification = true)]
        private bool _isShareParam ;

        /// <summary>
        /// 接口全局名称
        /// </summary>
        [DataInfo(IsNotification = true)]
        private string _apiGlobalName = string.Empty;
    }



    /// <summary>
    /// 流程调用节点
    /// </summary>
    public partial class SingleFlowCallNode : NodeModelBase
    {
        /// <summary>
        /// 被调用的节点
        /// </summary>
        public IFlowNode TargetNode { get; private set; }
        /// <summary>
        /// 缓存的方法信息
        /// </summary>
        public MethodDetails CacheMethodDetails { get; private set; }

        /// <summary>
        /// 流程接口节点
        /// </summary>
        /// <param name="environment"></param>
        public SingleFlowCallNode(IFlowEnvironment environment) : base(environment)
        {

        }


        /// <summary>
        /// 重置接口节点
        /// </summary>
        public void ResetTargetNode()
        {
            if (TargetNode is not null)
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


        /// <summary>
        /// 目标节点发生改变
        /// </summary>
        /// <param name="value"></param>
        partial void OnTargetNodeGuidChanged(string value)
        {
            if (string.IsNullOrEmpty(value) || !Env.TryGetNodeModel(value, out  var targetNode))
            {
                // 取消设置接口节点
                TargetNode.PropertyChanged -= TargetNode_PropertyChanged;  // 不再监听目标属通知
                TargetNode = null;
                this.ApiGlobalName = "";
                this.MethodDetails = new MethodDetails();
            }
            else
            {
                TargetNode = targetNode;
                if (!this.IsShareParam  // 不共享参数 
                    && TargetNode.MethodDetails is not null // 目标节点有方法描述
                     && CacheMethodDetails is not null // 存在缓存
                    && TargetNode.MethodDetails.AssemblyName == CacheMethodDetails.AssemblyName   // 与缓存中一致
                    && TargetNode.MethodDetails.MethodName == CacheMethodDetails.MethodName) // 与缓存中一致
                {
                    this.MethodDetails = CacheMethodDetails; // 直接使用缓存
                    this.ApiGlobalName = GetApiInvokeName(this); // 生成新的接口名称
                }
                else
                {
                    if (TargetNode.MethodDetails is not null) // // 目标节点有方法描述
                    {
                        CacheMethodDetails = TargetNode.MethodDetails.CloneOfNode(this); // 从目标节点复制一份到缓存中
                        TargetNode.PropertyChanged += TargetNode_PropertyChanged; // 监听目标属性通知（“IsPublic”属性）
                        this.MethodDetails = CacheMethodDetails; // 设置流程接口节点的方法描述为目标节点的方法描述（共享参数更改）
                        this.ApiGlobalName = GetApiInvokeName(this); // 生成新的接口名称
                    }
               
                }
                
            }
            OnPropertyChanged(nameof(MethodDetails)); // 通知控件，MethodDetails属性发生改变
        }

        partial void OnIsShareParamChanged(bool value)
        {
            if (TargetNode is null || TargetNode.MethodDetails is null)
            {
                return;
            }
            if (value)
            {
                // 不再与目标节点共享参数，而是拷贝新的实体，缓存起来，自己单独使用
                CacheMethodDetails = TargetNode.MethodDetails.CloneOfNode(this);
                this.MethodDetails = CacheMethodDetails;
            }
            else
            {

                if(TargetNode.ControlType == NodeControlType.Script)
                {
                    // 限制脚本节点对于入参数量、入参名称的修改
                    foreach (var item in CacheMethodDetails.ParameterDetailss)
                    {
                        item.IsParams = false;
                    }
                }
                // 与目标节点共享参数
                if(CacheMethodDetails is null)
                {
                    CacheMethodDetails = TargetNode.MethodDetails; // 防御性代码，几乎不可能触发
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
                TargetNode.PropertyChanged -= TargetNode_PropertyChanged;
            }
        }


        /// <summary>
        /// 从节点Guid刷新实体
        /// </summary>
        /// <returns></returns>
        private bool UploadTargetNode()
        {
            if (TargetNode is null)
            {
                if (string.IsNullOrWhiteSpace(TargetNodeGuid))
                {
                    return false;
                }

                if (!Env.TryGetNodeModel(TargetNodeGuid, out var targetNode) || targetNode is null)
                {
                    return false;
                }

                this.TargetNode = targetNode;
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
            data.TargetNodeGuid = TargetNode?.Guid; // 变量名称
            data.IsShareParam = IsShareParam;
            data.ApiGlobalName = ApiGlobalName;
            nodeInfo.CustomData = data;
            return nodeInfo;
        }

        private static Dictionary<string, int> ApiInvokeNameCache = new Dictionary<string, int>();
        private static int getApiInvokeNameCount = 0;
        private static string GetApiInvokeName(SingleFlowCallNode node, string apiName)
        {
            if (ApiInvokeNameCache.ContainsKey(apiName))
            {
                var count = ApiInvokeNameCache[apiName];
                count++;
                ApiInvokeNameCache[apiName] = count;
                return $"{apiName}{count}";
            }
            else
            {
                ApiInvokeNameCache[apiName] = 0;
                return $"{apiName}";
            }
        }

        /// <summary>
        /// 获取流程接口节点的名称
        /// </summary>
        /// <param name="node"></param>
        /// <returns></returns>
        public static string GetApiInvokeName(SingleFlowCallNode node)
        {
            if(node.TargetNode is null)
            {
                return GetApiInvokeName(node, "ApiInvoke"); // 如果没有目标节点，则返回默认名称
            }
            var md = node.TargetNode.MethodDetails;
            if (md is null)
            {
                var apiName = $"{node.TargetNode.ControlType}";
                return GetApiInvokeName(node, apiName);
            }
            else
            {
                /*var tempName = node.MethodDetails.MethodName;
                var index = node.MethodDetails.MethodName.IndexOf('(');
                var methodName = tempName[..(index - 1)];
                return GetApiInvokeName(node, methodName);*/
                IFlowLibraryService service = node.Env.IOC.Get<IFlowLibraryService>();
                if (service.TryGetMethodInfo(md.AssemblyName, md.MethodName, out var methodInfo))
                {
                    
                    var apiName = $"{methodInfo.Name}";
                    return GetApiInvokeName(node, apiName);
                }
                else
                {
                    var apiName = $"{node.TargetNode.ControlType}";
                    return GetApiInvokeName(node, apiName);
                }
            }
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
                this.TargetNode = targetNode;

                if(this.IsShareParam == false)
                {
                    foreach (var item in nodeInfo.ParameterData)
                    {
                        
                    }
                }

                this.ApiGlobalName = nodeInfo.CustomData?.ApiGlobalName ?? GetApiInvokeName(this);
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
        public override async Task<FlowResult> ExecutingAsync(IFlowContext context, CancellationToken token)
        {
            if (!UploadTargetNode())
            {
                throw new ArgumentNullException();
            }
            if (IsShareParam)
            {
                this.MethodDetails = TargetNode.MethodDetails;
            }
            this.SuccessorNodes = TargetNode.SuccessorNodes;

            FlowResult flowData = await (TargetNode.ControlType switch
            {
                NodeControlType.Script => ((SingleScriptNode)TargetNode).ExecutingAsync(this, context, token),
                _ => base.ExecutingAsync(context, token)
            });

            // 对于目标节点的后续节点，如果入参参数来源指定为它（目标节点）时，就需要从上下文中根据它的Guid获取流程数据
            context.AddOrUpdateFlowData(TargetNode.Guid, flowData); 
            if (IsShareParam)
            {
                // 设置运行时上一节点

                // 此处代码与SereinFlow.Library.FlowNode.ParameterDetails
                // ToMethodArgData()方法中判断流程接口节点分支逻辑耦合
                // 不要轻易修改
                foreach (ConnectionInvokeType ctType in NodeStaticConfig.ConnectionTypes)
                {
                    if (this.SuccessorNodes[ctType] == null) continue;
                    foreach (var node in this.SuccessorNodes[ctType])
                    {
                        if (node.DebugSetting.IsEnable)
                        {

                            context.SetPreviousNode(node.Guid, this.Guid);
                        }
                    }
                }
            }


            return flowData;
        }



    }
}