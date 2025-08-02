using Serein.Library;
using Serein.Library.Api;
using Serein.Library.Utils;
using Serein.NodeFlow.Env;
using Serein.NodeFlow.Model;
using Serein.NodeFlow.Model.Nodes;
using System.Collections.Concurrent;
using System.ComponentModel;
using System.Reflection;
using System.Text;

namespace Serein.NodeFlow
{

    /// <summary>
    /// 流程环境需要的扩展方法
    /// </summary>
    public static class FlowNodeExtension
    {
        /// <summary>
        /// 判断是否为基础节点
        /// </summary>
        /// <returns></returns>
        public static bool IsBaseNode(this NodeControlType nodeControlType)
        {
            var nodeDesc = EnumHelper.GetAttribute<NodeControlType, DescriptionAttribute>(nodeControlType);
            if("base".Equals(nodeDesc?.Description, StringComparison.OrdinalIgnoreCase))
            {
                 return true;
            }
            return false;
        }

        /// <summary>
        /// 是否为根节点
        /// </summary>
        /// <param name="node"></param>
        /// <returns></returns>
        public static bool IsRoot(this IFlowNode node)
        {
            var cts = NodeStaticConfig.ConnectionTypes;
            foreach (var ct in cts)
            {
                if (node.PreviousNodes[ct].Count > 0)
                {
                    return false;
                }
            }
            return true;
        }


        /// <summary>
        /// 创建节点
        /// </summary>
        /// <param name="envIOC">运行环境使用的IOC</param>
        /// <param name="nodeControlType">节点类型</param>
        /// <param name="methodDetails">方法描述</param>
        /// <returns></returns>
        /// <exception cref="Exception"></exception>
        public static IFlowNode CreateNode(ISereinIOC envIOC, NodeControlType nodeControlType,  MethodDetails? methodDetails = null)
        {

            // 尝试获取需要创建的节点类型
            var flowEdit = envIOC.Get<IFlowEdit>();
            if (!flowEdit.NodeMVVMManagement.TryGetType(nodeControlType, out var nodeMVVM) || nodeMVVM.ModelType == null)
            {
                throw new Exception($"无法创建{nodeControlType}节点，节点类型尚未注册。");
            }


            // 生成实例
            //var nodeObj = Activator.CreateInstance(nodeMVVM.ModelType, env);
            var nodeObj = envIOC.CreateObject(nodeMVVM.ModelType);
            if (nodeObj is not IFlowNode nodeModel)
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
        /// 程序集封装依赖
        /// </summary>
        /// <param name="libraryInfo"></param>
        /// <returns></returns>
        public static FlowLibraryInfo ToLibrary(this FlowLibraryInfo libraryInfo)
        {
            return new FlowLibraryInfo
            {
                AssemblyName = libraryInfo.AssemblyName,
                FileName = libraryInfo.FileName,
                FilePath = libraryInfo.FilePath,
            };
        }



        /*/// <summary>
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
        }*/



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


        /// <summary>
        ///  添加代码
        /// </summary>
        /// <param name="sb">字符串构建器</param>
        /// <param name="retractCount">缩进次数（4个空格）</param>
        /// <param name="code">要添加的代码</param>
        /// <param name="isWrapping">是否换行</param>
        /// <returns>字符串构建器本身</returns>
        public static StringBuilder AppendCode(this StringBuilder sb,
            int retractCount = 0,
            string? code = null,
            bool isWrapping = true)
        {
            if (!string.IsNullOrWhiteSpace(code))
            {
                string retract = new string(' ', retractCount * 4);
                sb.Append(retract);
                if (isWrapping)
                {
                    sb.AppendLine(code);
                }
                else
                {
                    sb.Append(code);
                }
            }
            return sb;
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
