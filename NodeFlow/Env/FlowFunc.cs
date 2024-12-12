using Serein.Library;
using Serein.Library.Api;
using Serein.Library.Utils;
using Serein.NodeFlow.Model;
using System.Collections.Concurrent;

namespace Serein.NodeFlow.Env
{

    /// <summary>
    /// 流程环境需要的扩展方法
    /// </summary>
    public static class FlowFunc
    {
      

        /// <summary>
        /// 判断是否为基础节点
        /// </summary>
        /// <returns></returns>
        public static bool IsBaseNode(this NodeControlType nodeControlType)
        {
            if(nodeControlType == NodeControlType.ExpCondition
                || nodeControlType == NodeControlType.ExpOp
                || nodeControlType == NodeControlType.GlobalData)
            {
                return true;
            }
            return false;
        }


        /// <summary>
        /// 创建节点
        /// </summary>
        /// <param name="env">运行环境</param>
        /// <param name="nodeControlType">节点类型</param>
        /// <param name="methodDetails">方法描述</param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        public static NodeModelBase CreateNode(IFlowEnvironment env, NodeControlType nodeControlType,
            MethodDetails? methodDetails = null)
        {

            // 尝试获取需要创建的节点类型

            if (!NodeMVVMManagement.TryGetType(nodeControlType, out var nodeMVVM) || nodeMVVM.ModelType == null)
            {
                throw new Exception($"无法创建{nodeControlType}节点，节点类型尚未注册。");
            }

            // 生成实例
            var nodeObj = Activator.CreateInstance(nodeMVVM.ModelType, env);
            if (nodeObj is not NodeModelBase nodeModel)
            {
                throw new Exception($"无法创建目标节点类型的实例[{nodeControlType}]");
            }

            // 配置基础的属性
            nodeModel.ControlType = nodeControlType;
            if (methodDetails == null) // 不存在方法描述时，可能是基础节点（表达式节点、条件表达式节点）
            {
                methodDetails = new MethodDetails();
            }
            var md = methodDetails.CloneOfNode(nodeModel);
            nodeModel.DisplayName = md.MethodAnotherName;
            nodeModel.MethodDetails = md;
            nodeModel.OnCreating();
            return nodeModel;
        }


        /// <summary>
        /// 从节点信息读取节点类型
        /// </summary>
        /// <param name="nodeInfo"></param>
        /// <returns></returns>
        /// <exception cref="NotImplementedException"></exception>
        public static NodeControlType GetNodeControlType(NodeInfo nodeInfo)
        {
            // 创建控件实例
            NodeControlType controlType = nodeInfo.Type switch
            {
                $"{NodeStaticConfig.NodeSpaceName}.{nameof(SingleActionNode)}" => NodeControlType.Action,// 动作节点控件
                $"{NodeStaticConfig.NodeSpaceName}.{nameof(SingleFlipflopNode)}" => NodeControlType.Flipflop, // 触发器节点控件

                $"{NodeStaticConfig.NodeSpaceName}.{nameof(SingleConditionNode)}" => NodeControlType.ExpCondition,// 条件表达式控件
                $"{NodeStaticConfig.NodeSpaceName}.{nameof(SingleExpOpNode)}" => NodeControlType.ExpOp, // 操作表达式控件

                $"{NodeStaticConfig.NodeSpaceName}.{nameof(CompositeConditionNode)}" => NodeControlType.ConditionRegion, // 条件区域控件

                $"{NodeStaticConfig.NodeSpaceName}.{nameof(SingleGlobalDataNode)}" => NodeControlType.GlobalData, // 数据节点
                _ => NodeControlType.None,
            };

            return controlType;
        }

        /// <summary>
        /// 程序集封装依赖
        /// </summary>
        /// <param name="libraryInfo"></param>
        /// <returns></returns>
        public static NodeLibraryInfo ToLibrary(this Library.NodeLibraryInfo libraryInfo)
        {
            return new NodeLibraryInfo
            {
                AssemblyName = libraryInfo.AssemblyName,
                FileName = libraryInfo.FileName,
                FilePath = libraryInfo.FilePath,
            };
        }

        /// <summary>
        /// 触发器运行后状态转为对应的后继分支类别
        /// </summary>
        /// <param name="flowStateType"></param>
        /// <returns></returns>
        /// <exception cref="NotImplementedException"></exception>
        public static ConnectionInvokeType ToContentType(this FlipflopStateType flowStateType)
        {
            return flowStateType switch
            {
                FlipflopStateType.Succeed => ConnectionInvokeType.IsSucceed,
                FlipflopStateType.Fail => ConnectionInvokeType.IsFail,
                FlipflopStateType.Error => ConnectionInvokeType.IsError,
                FlipflopStateType.Cancel => ConnectionInvokeType.None,
                _ => throw new NotImplementedException("未定义的流程状态")
            };
        }

        /// <summary>
        /// 判断 触发器节点 是否存在上游分支
        /// </summary>
        /// <param name="node"></param>
        /// <returns></returns>
        public static bool NotExitPreviousNode(this SingleFlipflopNode node)
        {
            ConnectionInvokeType[] ct = [ConnectionInvokeType.IsSucceed,
                                   ConnectionInvokeType.IsFail,
                                   ConnectionInvokeType.IsError,
                                   ConnectionInvokeType.Upstream];
            foreach (ConnectionInvokeType ctType in ct)
            {
                if (node.PreviousNodes[ctType].Count > 0)
                {
                    return false;
                }
            }
            return true;
        }


        ///// <summary>
        ///// 从节点类型枚举中转为对应的 Model 类型
        ///// </summary>
        ///// <param name="nodeControlType"></param>
        ///// <returns></returns>
        //public static Type? ControlTypeToModel(this NodeControlType nodeControlType)
        //{
        //    // 确定创建的节点类型
        //    Type? nodeType = nodeControlType switch
        //    {
        //        NodeControlType.Action => typeof(SingleActionNode),
        //        NodeControlType.Flipflop => typeof(SingleFlipflopNode),

        //        NodeControlType.ExpOp => typeof(SingleExpOpNode),
        //        NodeControlType.ExpCondition => typeof(SingleConditionNode),
        //        NodeControlType.ConditionRegion => typeof(CompositeConditionNode),
        //        _ => null
        //    };
        //    return nodeType;
        //}
        //public static NodeControlType ModelToControlType(this NodeControlType nodeControlType)
        //{
        //    var type = nodeControlType.GetType();
        //    NodeControlType controlType = type switch
        //    {
        //        Type when type == typeof(SingleActionNode) => NodeControlType.Action,
        //        Type when type == typeof(SingleFlipflopNode) => NodeControlType.Flipflop,

        //        Type when type == typeof(SingleExpOpNode) => NodeControlType.ExpOp,
        //        Type when type == typeof(SingleConditionNode) => NodeControlType.ExpCondition,
        //        Type when type == typeof(CompositeConditionNode) => NodeControlType.ConditionRegion,
        //        _ => NodeControlType.None,
        //    };
        //    return controlType;
        //}
    }

}
