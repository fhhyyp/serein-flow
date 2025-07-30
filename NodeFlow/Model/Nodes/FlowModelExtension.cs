using Serein.Library;
using Serein.Library.Api;
using Serein.Library.Utils;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Serein.NodeFlow.Model.Nodes
{
    /// <summary>
    /// 节点方法拓展
    /// </summary>
    public static class FlowModelExtension
    {
        /// <summary>
        /// 导出为画布信息
        /// </summary>
        /// <param name="model"></param>
        /// <returns></returns>
        public static FlowCanvasDetailsInfo ToInfo(this FlowCanvasDetails model)
        {
            return new FlowCanvasDetailsInfo
            {
                Guid = model.Guid,
                Height = model.Height,
                Width = model.Width,
                Name = model.Name,
                ScaleX = model.ScaleX,
                ScaleY = model.ScaleY,
                ViewX = model.ViewX,
                ViewY = model.ViewY,
                StartNode = model.StartNode?.Guid,
            };
        }

        /// <summary>
        /// 从画布信息加载
        /// </summary>
        /// <param name="canvasModel"></param>
        /// <param name="canvasInfo"></param>
        public static void LoadInfo(this FlowCanvasDetails canvasModel, FlowCanvasDetailsInfo canvasInfo)
        {
            canvasModel.Guid = canvasInfo.Guid;
            canvasModel.Height = canvasInfo.Height;
            canvasModel.Width = canvasInfo.Width;
            canvasModel.Name = canvasInfo.Name;
            canvasModel.ScaleX = canvasInfo.ScaleX;
            canvasModel.ScaleY = canvasInfo.ScaleY;
            canvasModel.ViewX = canvasInfo.ViewX;
            canvasModel.ViewY = canvasInfo.ViewY;
            if(canvasModel.Env.TryGetNodeModel(canvasInfo.StartNode,out var nodeModel))
            {
                canvasModel.StartNode = nodeModel;
            }
        }

        /// <summary>
        /// 输出方法参数信息
        /// </summary>
        /// <returns></returns>
        public static ParameterData[] SaveParameterInfo(this IFlowNode nodeModel)
        {
            if (nodeModel.MethodDetails is null || nodeModel.MethodDetails.ParameterDetailss == null)
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
        public static NodeInfo ToInfo(this IFlowNode nodeModel)
        {
            // if (MethodDetails == null) return null;
            /*var trueNodes = nodeModel.SuccessorNodes[ConnectionInvokeType.IsSucceed].Select(item => item.Guid); // 真分支
            var falseNodes = nodeModel.SuccessorNodes[ConnectionInvokeType.IsFail].Select(item => item.Guid);// 假分支
            var errorNodes = nodeModel.SuccessorNodes[ConnectionInvokeType.IsError].Select(item => item.Guid);// 异常分支
            var upstreamNodes = nodeModel.SuccessorNodes[ConnectionInvokeType.Upstream].Select(item => item.Guid);// 上游分支*/

            var successorNodes = nodeModel.SuccessorNodes.ToDictionary(kv => kv.Key, kv => kv.Value.Select(item => item.Guid).ToArray()); // 后继分支
            var previousNodes = nodeModel.PreviousNodes.ToDictionary(kv => kv.Key, kv => kv.Value.Select(item => item.Guid).ToArray()); // 后继分支


            // 生成参数列表
            ParameterData[] parameterDatas = nodeModel.SaveParameterInfo();

            var nodeInfo = new NodeInfo
            {
                CanvasGuid = nodeModel.CanvasDetails.Guid,
                Guid = nodeModel.Guid,
                IsPublic = nodeModel.IsPublic,
                AssemblyName = nodeModel.MethodDetails.AssemblyName,
                MethodName = nodeModel.MethodDetails?.MethodName,
                Label = nodeModel.MethodDetails?.MethodAnotherName,
                Type = nodeModel.ControlType.ToString(), //this.GetType().ToString(),
                /*TrueNodes = trueNodes.ToArray(),
                FalseNodes = falseNodes.ToArray(),
                UpstreamNodes = upstreamNodes.ToArray(),
                ErrorNodes = errorNodes.ToArray(),*/
                ParameterData = parameterDatas,
                Position = nodeModel.Position,
                IsProtectionParameter = nodeModel.DebugSetting.IsProtectionParameter,
                IsInterrupt = nodeModel.DebugSetting.IsInterrupt,
                IsEnable = nodeModel.DebugSetting.IsEnable,
                ParentNodeGuid = nodeModel.ContainerNode?.Guid,
                ChildNodeGuids = nodeModel.ChildrenNode.Select(item => item.Guid).ToArray(),
                SuccessorNodes = successorNodes,
                PreviousNodes = previousNodes,
            };
            nodeInfo.Position.X = Math.Round(nodeInfo.Position.X, 1);
            nodeInfo.Position.Y = Math.Round(nodeInfo.Position.Y, 1);
            nodeInfo = nodeModel.SaveCustomData(nodeInfo);
            return nodeInfo;
        }

        /// <summary>
        /// 从节点信息加载节点
        /// </summary>
        /// <param name="nodeModel"></param>
        /// <param name="nodeInfo"></param>
        /// <returns></returns>
        public static void LoadInfo(this IFlowNode nodeModel, NodeInfo nodeInfo)
        {
            nodeModel.Guid = nodeInfo.Guid;
            nodeModel.Position = nodeInfo.Position ?? new PositionOfUI(0, 0);// 加载位置信息
            var md = nodeModel.MethodDetails; // 当前节点的方法说明
            nodeModel.DebugSetting.IsProtectionParameter = nodeInfo.IsProtectionParameter; // 保护参数
            nodeModel.DebugSetting.IsInterrupt = nodeInfo.IsInterrupt; // 是否中断
            nodeModel.DebugSetting.IsEnable = nodeInfo.IsEnable; // 是否使能
            nodeModel.IsPublic = nodeInfo.IsPublic; // 是否全局公开
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
                    if (i >= pds.Length && nodeModel.ControlType != NodeControlType.FlowCall)
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
        /// 视为流程接口调用
        /// </summary>
        /// <param name="flowCallNode"></param>
        /// <param name="param"></param>
        /// <returns></returns>
        public static async Task<TResult> ApiInvokeAsync<TResult>(this IFlowNode flowCallNode, Dictionary<string,object> param)
        {
            var pds = flowCallNode.MethodDetails.ParameterDetailss;
            if (param.Keys.Count != pds.Length)
            {
                throw new ArgumentNullException($"参数数量不一致。传入参数数量：{param.Keys.Count}。接口入参数量：{pds.Length}。");
            }

            var context = new FlowContext(flowCallNode.Env);
            
            for (int index = 0; index < pds.Length; index++)
            {
                ParameterDetails pd = pds[index];
                if (param.TryGetValue(pd.Name, out var value))
                {
                    context.SetParamsTempData(flowCallNode.Guid, index, value); // 设置入参参数
                }
            }
            var cts = new CancellationTokenSource();
            var flowResult = await flowCallNode.StartFlowAsync(context, cts.Token);
            cts?.Cancel();
            cts?.Dispose();
            context.Exit();
            if (flowResult.Value is TResult result)
            {
                return result;
            }
            else if (flowResult is FlowResult && flowResult is TResult result2)
            {
                return result2;
            }
            else
            {
                throw new ArgumentNullException($"类型转换失败，流程返回数据与泛型不匹配，当前返回类型为[{flowResult.Value.GetType().FullName}]。");
            }
        }

#if DEBUG
        /// <summary>
        /// 程序集更新，更新节点方法描述、以及所有入参描述的类型
        /// </summary>
        /// <param name="nodeModel">节点Model</param>
        /// <param name="newMd">新的方法描述</param>
        public static void UploadMethod(this IFlowNode nodeModel, MethodDetails newMd)
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
