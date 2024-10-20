﻿using Serein.Library;
using Serein.Library.Api;
using Serein.NodeFlow.Model;

namespace Serein.NodeFlow.Env
{

    /// <summary>
    /// 流程环境需要的扩展方法
    /// </summary>
    public static class FlowFunc
    {
       

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
            // 确定创建的节点类型
            Type? nodeType = nodeControlType switch
            {
                NodeControlType.Action => typeof(SingleActionNode),
                NodeControlType.Flipflop => typeof(SingleFlipflopNode),

                NodeControlType.ExpOp => typeof(SingleExpOpNode),
                NodeControlType.ExpCondition => typeof(SingleConditionNode),
                NodeControlType.ConditionRegion => typeof(CompositeConditionNode),
                _ => null
            };

            if (nodeType is null)
            {
                throw new Exception($"节点类型错误[{nodeControlType}]");
            }
            // 生成实例
            var nodeObj = Activator.CreateInstance(nodeType, env);
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
            var md = methodDetails.CloneOfNode(nodeModel.Env, nodeModel);
            nodeModel.DisplayName = md.MethodTips;
            nodeModel.MethodDetails = md;


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
                _ => NodeControlType.None,
            };

            return controlType;
        }

        /// <summary>
        /// 程序集封装依赖
        /// </summary>
        /// <param name="library"></param>
        /// <returns></returns>
        public static Library.Library ToLibrary(this Library.NodeLibrary library)
        {
            var tmp = library.Assembly.ManifestModule.Name;
            return new Library.Library
            {
                AssemblyName = library.Assembly.GetName().Name,
                FileName = library.FileName,
                FilePath = library.FilePath,
            };
        }

        /// <summary>
        /// 触发器运行后状态转为对应的后继分支类别
        /// </summary>
        /// <param name="flowStateType"></param>
        /// <returns></returns>
        /// <exception cref="NotImplementedException"></exception>
        public static ConnectionType ToContentType(this FlipflopStateType flowStateType)
        {
            return flowStateType switch
            {
                FlipflopStateType.Succeed => ConnectionType.IsSucceed,
                FlipflopStateType.Fail => ConnectionType.IsFail,
                FlipflopStateType.Error => ConnectionType.IsError,
                FlipflopStateType.Cancel => ConnectionType.None,
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
            ConnectionType[] ct = [ConnectionType.IsSucceed,
                                   ConnectionType.IsFail,
                                   ConnectionType.IsError,
                                   ConnectionType.Upstream];
            foreach (ConnectionType ctType in ct)
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
