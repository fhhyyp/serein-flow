using Microsoft.CodeAnalysis.CSharp.Syntax;
using Serein.Library.Api;
using Serein.Library.Utils;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Serein.Library
{
    /// <summary>
    /// 节点方法拓展
    /// </summary>
    public static class NodeModelExtension
    {
        /// <summary>
        /// 输出方法参数信息
        /// </summary>
        /// <returns></returns>
        public static ParameterData[] SaveParameterInfo(this NodeModelBase nodeModel)
        {
            if (nodeModel.MethodDetails.ParameterDetailss == null)
            {
                return new ParameterData[0];
            }

            if (nodeModel.MethodDetails.ParameterDetailss.Length > 0)
            {
                return nodeModel.MethodDetails.ParameterDetailss
                                    .Select(it => new ParameterData
                                    {
                                        SourceNodeGuid = it.ArgDataSourceNodeGuid,
                                        SourceType = it.ArgDataSourceType.ToString(),
                                        State = it.IsExplicitData,
                                        ArgName = it.Name,
                                        Value = it.DataValue,

                                    })
                                    .ToArray();
            }
            else
            {
                return Array.Empty<ParameterData>();
            }
        }

        /// <summary>
        /// 导出为节点信息
        /// </summary>
        /// <returns></returns>
        public static NodeInfo ToInfo(this NodeModelBase nodeModel)
        {
            // if (MethodDetails == null) return null;

            var trueNodes = nodeModel.SuccessorNodes[ConnectionInvokeType.IsSucceed].Select(item => item.Guid); // 真分支
            var falseNodes = nodeModel.SuccessorNodes[ConnectionInvokeType.IsFail].Select(item => item.Guid);// 假分支
            var errorNodes = nodeModel.SuccessorNodes[ConnectionInvokeType.IsError].Select(item => item.Guid);// 异常分支
            var upstreamNodes = nodeModel.SuccessorNodes[ConnectionInvokeType.Upstream].Select(item => item.Guid);// 上游分支
            // 生成参数列表
            ParameterData[] parameterData = nodeModel.SaveParameterInfo();

            NodeInfo nodeInfo = new NodeInfo
            {
                Guid = nodeModel.Guid,
                AssemblyName = nodeModel.MethodDetails.AssemblyName,
                MethodName = nodeModel.MethodDetails?.MethodName,
                Label = nodeModel.MethodDetails?.MethodAnotherName,
                Type = nodeModel.ControlType.ToString(), //this.GetType().ToString(),
                TrueNodes = trueNodes.ToArray(),
                FalseNodes = falseNodes.ToArray(),
                UpstreamNodes = upstreamNodes.ToArray(),
                ParameterData = parameterData.ToArray(),
                ErrorNodes = errorNodes.ToArray(),
                Position = nodeModel.Position,
                IsProtectionParameter = nodeModel.MethodDetails.IsProtectionParameter,
                IsInterrupt = nodeModel.DebugSetting.IsInterrupt,
                IsEnable = nodeModel.DebugSetting.IsEnable,
                ParentNodeGuid = nodeModel.ContainerNode?.Guid,
                ChildNodeGuids = nodeModel.ChildrenNode.Select(item => item.Guid).ToArray(),
            };
            nodeInfo.Position.X = Math.Round(nodeInfo.Position.X, 1);
            nodeInfo.Position.Y = Math.Round(nodeInfo.Position.Y, 1);
            nodeInfo = nodeModel.SaveCustomData(nodeInfo);
            return nodeInfo;
        }

        /// <summary>
        /// 从节点信息加载节点
        /// </summary>
        /// <param name="nodeInfo"></param>
        /// <returns></returns>
        public static void LoadInfo(this NodeModelBase nodeModel, NodeInfo nodeInfo)
        {
            nodeModel.Guid = nodeInfo.Guid;
            nodeModel.Position = nodeInfo.Position ?? new PositionOfUI(0, 0);// 加载位置信息
            var md = nodeModel.MethodDetails; // 当前节点的方法说明
            nodeModel.MethodDetails.IsProtectionParameter = nodeInfo.IsProtectionParameter; // 保护参数
            nodeModel.DebugSetting.IsInterrupt = nodeInfo.IsInterrupt; // 是否中断
            nodeModel.DebugSetting.IsEnable = nodeInfo.IsEnable; // 是否使能

            if (md != null)
            {
                if (md.ParameterDetailss == null)
                {
                    md.ParameterDetailss = new ParameterDetails[0];
                }

                var pds = md.ParameterDetailss; // 当前节点的入参描述数组
                #region 类库方法型节点加载参数
                if (nodeInfo.ParameterData.Length > pds.Length && md.HasParamsArg)
                {
                    // 保存的参数信息项数量大于方法本身的方法入参数量（可能存在可变入参）
                    var length = nodeInfo.ParameterData.Length - pds.Length; // 需要扩容的长度
                    nodeModel.MethodDetails.ParameterDetailss = ArrayHelper.Expansion(pds, length); // 扩容入参描述数组
                    pds = md.ParameterDetailss; // 当前节点的入参描述数组
                    var startParmsPd = pds[md.ParamsArgIndex]; // 获取可变入参参数描述
                    for (int i = md.ParamsArgIndex + 1; i <= md.ParamsArgIndex + length; i++)
                    {
                        pds[i] = startParmsPd.CloneOfModel(nodeModel);
                        pds[i].Index = pds[i - 1].Index + 1;
                        pds[i].IsParams = true;
                    }
                }

                for (int i = 0; i < nodeInfo.ParameterData.Length; i++)
                {
                    if (i >= pds.Length)
                    {
                        nodeModel.Env.WriteLine(InfoType.ERROR, $"保存的参数数量大于方法此时的入参参数数量：[{nodeInfo.Guid}][{nodeInfo.MethodName}]");
                        break;
                    }
                    var pd = pds[i];
                    ParameterData pdInfo = nodeInfo.ParameterData[i];
                    pd.IsExplicitData = pdInfo.State;
                    pd.DataValue = pdInfo.Value;
                    pd.ArgDataSourceType = EnumHelper.ConvertEnum<ConnectionArgSourceType>(pdInfo.SourceType);
                    pd.ArgDataSourceNodeGuid =  pdInfo.SourceNodeGuid;

                }

                nodeModel.LoadCustomData(nodeInfo); // 加载自定义数据

                #endregion
            }
        }


        /// <summary>
        /// 开始执行
        /// </summary>
        /// <param name="context"></param>
        /// <param name="token">流程运行</param>
        /// <returns></returns>
        public static async Task StartFlowAsync(this NodeModelBase nodeModel, IDynamicContext context, CancellationToken token)
        {
            Stack<NodeModelBase> stack = new Stack<NodeModelBase>();
            HashSet<NodeModelBase> processedNodes = new HashSet<NodeModelBase>(); // 用于记录已处理上游节点的节点
            stack.Push(nodeModel);
            while (context.RunState != RunState.Completion  // 没有完成
                && token.IsCancellationRequested == false // 没有取消
                && stack.Count > 0) // 循环中直到栈为空才会退出循环
            {


#if DEBUG
                await Task.Delay(1);
#endif

                #region 执行相关

                // 从栈中弹出一个节点作为当前节点进行处理
                var currentNode = stack.Pop();
                context.NextOrientation = ConnectionInvokeType.None; // 重置上下文状态

                FlowResult flowResult;
                try
                {
                    flowResult = await currentNode.ExecutingAsync(context, token);

                    if (context.NextOrientation == ConnectionInvokeType.None) // 没有手动设置时，进行自动设置
                    {
                        context.NextOrientation = ConnectionInvokeType.IsSucceed;
                    }
                }
                catch (Exception ex)
                {
                    flowResult = new FlowResult(currentNode,context);
                    context.Env.WriteLine(InfoType.ERROR, $"节点[{currentNode.Guid}]异常：" + ex);
                    context.NextOrientation = ConnectionInvokeType.IsError;
                    context.ExceptionOfRuning = ex;
                }
                context.AddOrUpdate(currentNode, flowResult); // 上下文中更新数据
                #endregion

                #region 执行完成

                // 首先将指定类别后继分支的所有节点逆序推入栈中
                var nextNodes = currentNode.SuccessorNodes[context.NextOrientation];
                for (int index = nextNodes.Count - 1; index >= 0; index--)
                {
                    // 筛选出启用的节点的节点
                    if (nextNodes[index].DebugSetting.IsEnable)
                    {
                        context.SetPreviousNode(nextNodes[index], currentNode);
                        stack.Push(nextNodes[index]);
                    }
                }
                // 然后将指上游分支的所有节点逆序推入栈中
                var upstreamNodes = currentNode.SuccessorNodes[ConnectionInvokeType.Upstream];
                for (int index = upstreamNodes.Count - 1; index >= 0; index--)
                {
                    // 筛选出启用的节点的节点
                    if (upstreamNodes[index].DebugSetting.IsEnable)
                    {
                        context.SetPreviousNode(upstreamNodes[index], currentNode);
                        stack.Push(upstreamNodes[index]);
                    }
                }
                #endregion

            }
        }

        /// <summary>
        /// 获取对应的参数数组
        /// </summary>
        public static async Task<object[]> GetParametersAsync(this NodeModelBase nodeModel, IDynamicContext context, CancellationToken token)
        {
            if (nodeModel.MethodDetails.ParameterDetailss.Length == 0)
            {
                return Array.Empty<object>(); // 无参数
            }
            var md = nodeModel.MethodDetails;
            var pds = md.ParameterDetailss;

            #region 定义返回的参数数组
            object[] args;
            Array paramsArgs = null; // 初始化可选参数
            int paramsArgIndex = 0; // 可选参数下标，与 object[] paramsArgs 一起使用
            if (md.ParamsArgIndex >= 0) // 存在可变入参参数
            {
                var paramsArgType = pds[md.ParamsArgIndex].DataType; // 获取可变参数的参数类型
                int paramsLength = pds.Length - md.ParamsArgIndex;  // 可变参数数组长度 = 方法参数个数 - （ 可选入参下标 + 1 ）
                paramsArgs = Array.CreateInstance(paramsArgType, paramsLength);// 可变参数
                args = new object[md.ParamsArgIndex + 1]; // 调用方法的入参数组
                args[md.ParamsArgIndex] = paramsArgs; // 如果存在可选参数，入参参数最后一项则为可变参数
            }
            else
            {
                // 不存在可选参数
                args = new object[pds.Length]; // 调用方法的入参数组
            }
            #endregion

            // 常规参数的获取
            for (int i = 0; i < args.Length; i++)
            {
                var pd = pds[i];

                args[i] = await pd.ToMethodArgData(context); // 获取数据
            }

            // 可选参数的获取
            if (md.ParamsArgIndex >= 0)
            {
                for (int i = 0; i < paramsArgs.Length; i++)
                {
                    var pd = md.ParameterDetailss[paramsArgIndex + i];
                    var data = await pd.ToMethodArgData(context); // 获取数据
                    paramsArgs.SetValue(data, i);// 设置到数组中
                }
                args[args.Length - 1] = paramsArgs;
            }

            return args;
        }

        /// <summary>
        /// 检查监视表达式是否生效
        /// </summary>
        /// <param name="nodeModel">节点Moel</param>
        /// <param name="context">上下文</param>
        /// <param name="newData">新的数据</param>
        /// <returns></returns>
        /*public static async Task CheckExpInterrupt(this NodeModelBase nodeModel, IDynamicContext context,  object newData = null)
        {
            string guid = nodeModel.Guid;
            context.AddOrUpdate(guid, newData); // 上下文中更新数据
            if (newData is null)
            {
            }
            else
            {
                await nodeModel.MonitorObjExpInterrupt(context, newData, 0); // 首先监视对象
                await nodeModel.MonitorObjExpInterrupt(context, newData, 1); // 然后监视节点
                //nodeModel.FlowData = newData; // 替换数据
            }
        }*/

        /// <summary>
        /// 监视对象表达式中断
        /// </summary>
        /// <param name="nodeModel"></param>
        /// <param name="context"></param>
        /// <param name="data"></param>
        /// <param name="monitorType"></param>
        /// <returns></returns>
        /*private static async Task MonitorObjExpInterrupt(this NodeModelBase nodeModel, IDynamicContext context, object data, int monitorType)
        {
            MonitorObjectEventArgs.ObjSourceType sourceType;
            string key;
            if (monitorType == 0)
            {
                key = data?.GetType()?.FullName;
                sourceType = MonitorObjectEventArgs.ObjSourceType.IOCObj;
            }
            else
            {
                key = nodeModel.Guid;
                sourceType = MonitorObjectEventArgs.ObjSourceType.IOCObj;
            }
            if (string.IsNullOrEmpty(key))
            {
                return;
            }
            //(var isMonitor, var exps) = await context.Env.CheckObjMonitorStateAsync(key);
            //if (isMonitor) // 如果新的数据处于查看状态，通知UI进行更新？交给运行环境判断？
            //{
            //    context.Env.MonitorObjectNotification(nodeModel.Guid, data, sourceType); // 对象处于监视状态，通知UI更新数据显示
            //    if (exps.Length > 0)
            //    {
            //        // 表达式环境下判断是否需要执行中断
            //        bool isExpInterrupt = false;
            //        string exp = "";
            //        // 判断执行监视表达式，直到为 true 时退出
            //        for (int i = 0; i < exps.Length && !isExpInterrupt; i++)
            //        {
            //            exp = exps[i];
            //            if (string.IsNullOrEmpty(exp)) continue;
            //            // isExpInterrupt = SereinConditionParser.To(data, exp);
            //        }

            //        if (isExpInterrupt) // 触发中断
            //        {
            //            nodeModel.DebugSetting.IsInterrupt = true;
            //            if (await context.Env.SetNodeInterruptAsync(nodeModel.Guid,true))
            //            {
            //                context.Env.TriggerInterrupt(nodeModel.Guid, exp, InterruptTriggerEventArgs.InterruptTriggerType.Exp);
            //                var cancelType = await nodeModel.DebugSetting.GetInterruptTask();
            //                await Console.Out.WriteLineAsync($"[{data}]中断已{cancelType}，开始执行后继分支");
            //                nodeModel.DebugSetting.IsInterrupt = false;
            //            }
            //        }
            //    }

            //}
        }*/


        /// <summary>
        /// 不再中断
        /// </summary>
        public static void CancelInterrupt(NodeModelBase nodeModel)
        {
            nodeModel.DebugSetting.IsInterrupt = false;
            nodeModel.DebugSetting.CancelInterrupt?.Invoke();
        }

#if DEBUG
        /// <summary>
        /// 程序集更新，更新节点方法描述、以及所有入参描述的类型
        /// </summary>
        /// <param name="nodeModel">节点Model</param>
        /// <param name="newMd">新的方法描述</param>
        public static void UploadMethod(this NodeModelBase nodeModel, MethodDetails newMd)
        {
            var thisMd = nodeModel.MethodDetails;

            thisMd.ActingInstanceType = newMd.ActingInstanceType; // 更新方法需要的类型

            var thisPds = thisMd.ParameterDetailss;
            var newPds = newMd.ParameterDetailss;
            // 当前存在可变参数，且新的方法也存在可变参数，需要把可变参数的数目与值传递过去
            if (thisMd.HasParamsArg && newMd.HasParamsArg)
            {
                int paramsLength = thisPds.Length - thisMd.ParamsArgIndex - 1; // 确定扩容长度
                newMd.ParameterDetailss = ArrayHelper.Expansion(newPds, paramsLength);// 为新方法的入参参数描述进行扩容
                newPds = newMd.ParameterDetailss;
                int index = newMd.ParamsArgIndex; // 记录
                var templatePd = newPds[newMd.ParamsArgIndex]; // 新的入参模板
                for (int i = thisMd.ParamsArgIndex; i < thisPds.Length; i++)
                {
                    ParameterDetails thisPd = thisPds[i];
                    var newPd = templatePd.CloneOfModel(nodeModel); // 复制参数描述
                    newPd.Index = i + 1; // 更新索引
                    newPd.IsParams = true;
                    newPd.DataValue = thisPd.DataValue; // 保留参数值
                    newPd.ArgDataSourceNodeGuid = thisPd.ArgDataSourceNodeGuid; // 保留参数来源信息
                    newPd.ArgDataSourceType = thisPd.ArgDataSourceType;  // 保留参数来源信息
                    newPd.IsParams = thisPd.IsParams; // 保留显式参数设置
                    newPds[index++] = newPd;
                }
            }


            var thidPdLength = thisMd.HasParamsArg ? thisMd.ParamsArgIndex : thisPds.Length;
            // 遍历当前的参数描述（不包含可变参数），找到匹配项，复制必要的数据进行保留
            for (int i = 0; i < thisPds.Length; i++)
            {
                ParameterDetails thisPd = thisPds[i];
                var newPd = newPds.FirstOrDefault(t_newPd => !t_newPd.IsParams // 不为可变参数
                                                         && t_newPd.Name.Equals(thisPd.Name, StringComparison.OrdinalIgnoreCase) // 存在相同名称
                                                         && t_newPd.DataType.Name.Equals(thisPd.DataType.Name) // 存在相同入参类型名称（以类型作为区分）
                                                         );
                if (newPd != null) // 如果匹配上了
                {
                    newPd.DataValue = thisPd.DataValue; // 保留参数值
                    newPd.ArgDataSourceNodeGuid = thisPd.ArgDataSourceNodeGuid; // 保留参数来源信息
                    newPd.ArgDataSourceType = thisPd.ArgDataSourceType;  // 保留参数来源信息
                    newPd.IsParams = thisPd.IsParams; // 保留显式参数设置
                }
            }
            thisMd.ReturnType = newMd.ReturnType;
            nodeModel.MethodDetails = newMd;

        }
#endif

    }
}
