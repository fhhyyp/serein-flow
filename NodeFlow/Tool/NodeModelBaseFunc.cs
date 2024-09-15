using Newtonsoft.Json;
using Serein.Library.Api;
using Serein.Library.Entity;
using Serein.Library.Enums;
using Serein.NodeFlow.Tool.SerinExpression;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Serein.Library.Base
{
    /// <summary>
    /// 节点基类（数据）：条件控件，动作控件，条件区域，动作区域
    /// </summary>
    // public abstract partial class NodeModelBase : IDynamicFlowNode
    public static class NodeFunc 
    {
        /// <summary>
        /// 执行节点对应的方法
        /// </summary>
        /// <param name="context">流程上下文</param>
        /// <returns>节点传回数据对象</returns>
        public static object? Execute(this NodeModelBase nodeModelBase, IDynamicContext context)
        {
            MethodDetails md = nodeModelBase.MethodDetails;
            object? result = null;
            var del = md.MethodDelegate;
            try
            {
                if (md.ExplicitDatas.Length == 0)
                {
                    if (md.ReturnType == typeof(void))
                    {
                        ((Action<object>)del).Invoke(md.ActingInstance);
                    }
                    else
                    {
                        result = ((Func<object, object>)del).Invoke(md.ActingInstance);
                    }
                }
                else
                {
                    object?[]? parameters = nodeModelBase.GetParameters(context, md);
                    if (md.ReturnType == typeof(void))
                    {
                        ((Action<object, object[]>)del).Invoke(md.ActingInstance, parameters);
                    }
                    else
                    {
                        result = ((Func<object, object[], object>)del).Invoke(md.ActingInstance, parameters);
                    }
                }

                return result;
            }
            catch (Exception ex)
            {
                nodeModelBase.FlowState = FlowStateType.Error;
                nodeModelBase.RuningException = ex;
            }

            return result;
        }

        /// <summary>
        /// 执行等待触发器的方法
        /// </summary>
        /// <param name="context"></param>
        /// <returns>节点传回数据对象</returns>
        /// <exception cref="RuningException"></exception>
        public static async Task<object?> ExecuteAsync(this NodeModelBase nodeModel, IDynamicContext context)
        {
            MethodDetails md = nodeModel.MethodDetails;
            object? result = null;

            IFlipflopContext flipflopContext = null;
            try
            {
                // 调用委托并获取结果
                if (md.ExplicitDatas.Length == 0)
                {
                    flipflopContext = await ((Func<object, Task<IFlipflopContext>>)md.MethodDelegate).Invoke(md.ActingInstance);
                }
                else
                {
                    object?[]? parameters = nodeModel.GetParameters(context, md);
                    flipflopContext = await ((Func<object, object[], Task<IFlipflopContext>>)md.MethodDelegate).Invoke(md.ActingInstance, parameters);
                }

                if (flipflopContext != null)
                {
                    nodeModel.FlowState = flipflopContext.State;
                    if (flipflopContext.State == FlowStateType.Succeed)
                    {
                        result = flipflopContext.Data;
                    }
                    else
                    {
                        result = null;
                    }
                }
            }
            catch (Exception ex)
            {
                nodeModel.FlowState = FlowStateType.Error;
                nodeModel.RuningException = ex;
            }

            return result;
        }

        /// <summary>
        /// 开始执行
        /// </summary>
        /// <param name="context"></param>
        /// <returns></returns>
        public static async Task StartExecution(this NodeModelBase nodeModel, IDynamicContext context)
        {
            var cts = context.SereinIoc.GetOrInstantiate<CancellationTokenSource>();

            Stack<NodeModelBase> stack = [];
            stack.Push(nodeModel);
            var md = nodeModel.MethodDetails;
            while (stack.Count > 0 && !cts.IsCancellationRequested) // 循环中直到栈为空才会退出循环
            {
                // 从栈中弹出一个节点作为当前节点进行处理
                var currentNode = stack.Pop();

                // 设置方法执行的对象
                if (currentNode.MethodDetails != null)
                {
                    currentNode.MethodDetails.ActingInstance ??= context.SereinIoc.GetOrInstantiate(md.ActingInstanceType);
                }

                // 获取上游分支，首先执行一次
                var upstreamNodes = currentNode.UpstreamBranch;
                for (int i = upstreamNodes.Count - 1; i >= 0; i--)
                {
                    upstreamNodes[i].PreviousNode = currentNode;
                    await upstreamNodes[i].StartExecution(context);
                }

                if (currentNode.MethodDetails != null && currentNode.MethodDetails.MethodDynamicType == NodeType.Flipflop)
                {
                    // 触发器节点
                    currentNode.FlowData = await currentNode.ExecuteAsync(context);
                }
                else
                {
                    // 动作节点
                    currentNode.FlowData = currentNode.Execute(context);
                }

                var nextNodes = currentNode.FlowState switch
                {
                    FlowStateType.Succeed => currentNode.SucceedBranch,
                    FlowStateType.Fail => currentNode.FailBranch,
                    FlowStateType.Error => currentNode.ErrorBranch,
                    _ => throw new Exception("非预期的枚举值")
                };

                // 将下一个节点集合中的所有节点逆序推入栈中
                for (int i = nextNodes.Count - 1; i >= 0; i--)
                {
                    nextNodes[i].PreviousNode = currentNode;
                    stack.Push(nextNodes[i]);
                }
            }
        }

        /// <summary>
        /// 获取对应的参数数组
        /// </summary>
        public static object[]? GetParameters(this NodeModelBase nodeModel, IDynamicContext context, MethodDetails md)
        {
            // 用正确的大小初始化参数数组
            var types = md.ExplicitDatas.Select(it => it.DataType).ToArray();
            if (types.Length == 0)
            {
                return [md.ActingInstance];
            }

            object[]? parameters = new object[types.Length];

            for (int i = 0; i < types.Length; i++)
            {

                var mdEd = md.ExplicitDatas[i];
                Type type = mdEd.DataType;

                var f1 = nodeModel.PreviousNode?.FlowData?.GetType();
                var f2 = mdEd.DataType;
                if (type == typeof(IDynamicContext))
                {
                    parameters[i] = context;
                }
                else if (type == typeof(MethodDetails))
                {
                    parameters[i] = md;
                }
                else if (type == typeof(NodeModelBase))
                {
                    parameters[i] = nodeModel;
                }
                else if (mdEd.IsExplicitData) // 显式参数
                {
                    // 判断是否使用表达式解析
                    if (mdEd.DataValue[0] == '@')
                    {
                        var expResult = SerinExpressionEvaluator.Evaluate(mdEd.DataValue, nodeModel.PreviousNode?.FlowData, out bool isChange);


                        if (mdEd.DataType.IsEnum)
                        {
                            var enumValue = Enum.Parse(mdEd.DataType, mdEd.DataValue);
                            parameters[i] = enumValue;
                        }
                        else if (mdEd.ExplicitType == typeof(string))
                        {
                            parameters[i] = Convert.ChangeType(expResult, typeof(string));
                        }
                        else if (mdEd.ExplicitType == typeof(bool))
                        {
                            parameters[i] = Convert.ChangeType(expResult, typeof(bool));
                        }
                        else if (mdEd.ExplicitType == typeof(int))
                        {
                            parameters[i] = Convert.ChangeType(expResult, typeof(int));
                        }
                        else if (mdEd.ExplicitType == typeof(double))
                        {
                            parameters[i] = Convert.ChangeType(expResult, typeof(double));
                        }
                        else
                        {
                            parameters[i] = expResult;
                            //parameters[i] = ConvertValue(mdEd.DataValue, mdEd.ExplicitType);
                        }
                    }
                    else
                    {
                        if (mdEd.DataType.IsEnum)
                        {
                            var enumValue = Enum.Parse(mdEd.DataType, mdEd.DataValue);
                            parameters[i] = enumValue;
                        }
                        else if (mdEd.ExplicitType == typeof(string))
                        {
                            parameters[i] = mdEd.DataValue;
                        }
                        else if (mdEd.ExplicitType == typeof(bool))
                        {
                            parameters[i] = bool.Parse(mdEd.DataValue);
                        }
                        else if (mdEd.ExplicitType == typeof(int))
                        {
                            parameters[i] = int.Parse(mdEd.DataValue);
                        }
                        else if (mdEd.ExplicitType == typeof(double))
                        {
                            parameters[i] = double.Parse(mdEd.DataValue);
                        }
                        else
                        {
                            parameters[i] = "";

                            //parameters[i] = ConvertValue(mdEd.DataValue, mdEd.ExplicitType);
                        }
                    }


                }
                else if (f1 != null && f2 != null)
                {
                    if (f2.IsAssignableFrom(f1) || f2.FullName.Equals(f1.FullName))
                    {
                        parameters[i] = nodeModel.PreviousNode?.FlowData;

                    }
                }
                else
                {


                    var tmpParameter = nodeModel.PreviousNode?.FlowData?.ToString();
                    if (mdEd.DataType.IsEnum)
                    {

                        var enumValue = Enum.Parse(mdEd.DataType, tmpParameter);

                        parameters[i] = enumValue;
                    }
                    else if (mdEd.DataType == typeof(string))
                    {

                        parameters[i] = tmpParameter;

                    }
                    else if (mdEd.DataType == typeof(bool))
                    {

                        parameters[i] = bool.Parse(tmpParameter);

                    }
                    else if (mdEd.DataType == typeof(int))
                    {

                        parameters[i] = int.Parse(tmpParameter);

                    }
                    else if (mdEd.DataType == typeof(double))
                    {

                        parameters[i] = double.Parse(tmpParameter);

                    }
                    else
                    {
                        if (tmpParameter != null && mdEd.DataType != null)
                        {

                            parameters[i] = ConvertValue(tmpParameter, mdEd.DataType);

                        }
                    }
                }

            }
            return parameters;
        }

        /// <summary>
        /// json文本反序列化为对象
        /// </summary>
        /// <param name="value"></param>
        /// <param name="targetType"></param>
        /// <returns></returns>
        private static dynamic? ConvertValue(string value, Type targetType)
        {
            try
            {
                if (!string.IsNullOrEmpty(value))
                {
                    return JsonConvert.DeserializeObject(value, targetType);
                }
                else
                {
                    return null;
                }
            }
            catch (JsonReaderException ex)
            {
                Console.WriteLine(ex);
                return value;
            }
            catch (JsonSerializationException ex)
            {
                // 如果无法转为对应的JSON对象
                int startIndex = ex.Message.IndexOf("to type '") + "to type '".Length; // 查找类型信息开始的索引
                int endIndex = ex.Message.IndexOf('\'');  // 查找类型信息结束的索引
                var typeInfo = ex.Message[startIndex..endIndex]; // 提取出错类型信息，该怎么传出去？
                Console.WriteLine("无法转为对应的JSON对象:" + typeInfo);
                return null;
            }
            catch // (Exception ex)
            {
                return value;
            }
        }





        #region 完整的ExecuteAsync调用方法（不要删除）
        //public virtual async Task<object?> ExecuteAsync(DynamicContext context)
        //{
        //    MethodDetails md = MethodDetails;
        //    object? result = null;
        //    if (DelegateCache.GlobalDicDelegates.TryGetValue(md.MethodName, out Delegate del))
        //    {
        //        if (md.ExplicitDatas.Length == 0)
        //        {
        //            if (md.ReturnType == typeof(void))
        //            {
        //                ((Action<object>)del).Invoke(md.ActingInstance);
        //            }
        //            else if (md.ReturnType == typeof(Task<FlipflopContext>))
        //            {
        //                // 调用委托并获取结果
        //                FlipflopContext flipflopContext = await ((Func<object, Task<FlipflopContext>>)del).Invoke(MethodDetails.ActingInstance);

        //                if (flipflopContext != null)
        //                {
        //                    if (flipflopContext.State == FfState.Cancel)
        //                    {
        //                        throw new Exception("this async task is cancel.");
        //                    }
        //                    else
        //                    {
        //                        if (flipflopContext.State == FfState.Succeed)
        //                        {
        //                            CurrentState = true;
        //                            result = flipflopContext.Data;
        //                        }
        //                        else
        //                        {
        //                            CurrentState = false;
        //                        }
        //                    }
        //                }
        //            }
        //            else
        //            {
        //                result = ((Func<object, object>)del).Invoke(md.ActingInstance);
        //            }
        //        }
        //        else
        //        {
        //            object?[]? parameters = GetParameters(context, MethodDetails);
        //            if (md.ReturnType == typeof(void))
        //            {
        //                ((Action<object, object[]>)del).Invoke(md.ActingInstance, parameters);
        //            }
        //            else if (md.ReturnType == typeof(Task<FlipflopContext>))
        //            {
        //                // 调用委托并获取结果
        //                FlipflopContext flipflopContext = await ((Func<object, object[], Task<FlipflopContext>>)del).Invoke(MethodDetails.ActingInstance, parameters);

        //                if (flipflopContext != null)
        //                {
        //                    if (flipflopContext.State == FfState.Cancel)
        //                    {
        //                        throw new Exception("取消此异步");
        //                    }
        //                    else
        //                    {
        //                        CurrentState = flipflopContext.State == FfState.Succeed;
        //                        result = flipflopContext.Data;
        //                    }
        //                }
        //            }
        //            else
        //            {
        //                result = ((Func<object, object[], object>)del).Invoke(md.ActingInstance, parameters);
        //            }
        //        }
        //        context.SetFlowData(result);
        //    }
        //    return result;
        //} 
        #endregion




    }
}
