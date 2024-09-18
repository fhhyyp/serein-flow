using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Serein.Library.Api;
using Serein.Library.Entity;
using Serein.Library.Enums;
using Serein.Library.Ex;
using Serein.NodeFlow.Tool.SereinExpression;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;

namespace Serein.NodeFlow.Base
{

    /// <summary>
    /// 节点基类（数据）：条件控件，动作控件，条件区域，动作区域
    /// </summary>
    public abstract partial class NodeModelBase : IDynamicFlowNode
    {
        #region 导出/导入项目文件节点信息

        internal abstract Parameterdata[] GetParameterdatas();
        internal virtual NodeInfo ToInfo()
        {
            // if (MethodDetails == null) return null;

            var trueNodes = SuccessorNodes[ConnectionType.IsSucceed].Select(item => item.Guid); // 真分支
            var falseNodes = SuccessorNodes[ConnectionType.IsFail].Select(item => item.Guid);// 假分支
            var errorNodes = SuccessorNodes[ConnectionType.IsError].Select(item => item.Guid);// 异常分支
            var upstreamNodes = SuccessorNodes[ConnectionType.Upstream].Select(item => item.Guid);// 上游分支

            // 生成参数列表
            Parameterdata[] parameterData = GetParameterdatas();

            return new NodeInfo
            {
                Guid = Guid,
                MethodName = MethodDetails?.MethodName,
                Label = DisplayName ?? "",
                Type = this.GetType().ToString(),
                TrueNodes = trueNodes.ToArray(),
                FalseNodes = falseNodes.ToArray(),
                UpstreamNodes = upstreamNodes.ToArray(),
                ParameterData = parameterData.ToArray(),
                ErrorNodes = errorNodes.ToArray(),

            };
        }

        internal virtual NodeModelBase LoadInfo(NodeInfo nodeInfo)
        {
            var node = this;
            if (node != null)
            {
                node.Guid = nodeInfo.Guid;
                for (int i = 0; i < nodeInfo.ParameterData.Length; i++)
                {
                    Parameterdata? pd = nodeInfo.ParameterData[i];
                    node.MethodDetails.ExplicitDatas[i].IsExplicitData = pd.State;
                    node.MethodDetails.ExplicitDatas[i].DataValue = pd.Value;
                }
            }

            //if (control is ConditionNodeControl conditionNodeControl)
            //{
            //    conditionNodeControl.ViewModel.IsCustomData = pd.state;
            //    conditionNodeControl.ViewModel.CustomData = pd.value;
            //    conditionNodeControl.ViewModel.Expression = pd.expression;
            //}
            //else if (control is ExpOpNodeControl expOpNodeControl)
            //{
            //    expOpNodeControl.ViewModel.Expression = pd.expression;
            //}
            //else
            //{
            //    node.MethodDetails.ExplicitDatas[i].IsExplicitData = pd.state;
            //    node.MethodDetails.ExplicitDatas[i].DataValue = pd.value;
            //}
            return this;
        }

        #endregion


        /// <summary>
        /// 开始执行
        /// </summary>
        /// <param name="context"></param>
        /// <returns></returns>
        public async Task StartExecution(IDynamicContext context)
        {
            var cts = context.SereinIoc.GetOrRegisterInstantiate<CancellationTokenSource>();

            Stack<NodeModelBase> stack = new Stack<NodeModelBase>();
            stack.Push(this);

            while (stack.Count > 0 && !cts.IsCancellationRequested) // 循环中直到栈为空才会退出循环
            {
                // 从栈中弹出一个节点作为当前节点进行处理
                var currentNode = stack.Pop();

                // 设置方法执行的对象
                if (currentNode.MethodDetails?.ActingInstance == null && currentNode.MethodDetails?.ActingInstanceType is not null)
                {
                    currentNode.MethodDetails.ActingInstance ??= context.SereinIoc.GetOrRegisterInstantiate(currentNode.MethodDetails.ActingInstanceType);
                }

                // 首先执行上游分支
                var upstreamNodes = currentNode.SuccessorNodes[ConnectionType.Upstream];
                for (int i = upstreamNodes.Count - 1; i >= 0; i--)
                {
                    upstreamNodes[i].PreviousNode = currentNode;
                    await upstreamNodes[i].StartExecution(context); // 执行上游分支
                }

                // 判断是否为触发器节点，如果是，则开始等待。
                if (currentNode.MethodDetails != null && currentNode.MethodDetails.MethodDynamicType == NodeType.Flipflop)
                {
                    
                    currentNode.FlowData = await currentNode.ExecuteAsync(context); // 流程中遇到了触发器
                }
                else
                {
                    currentNode.FlowData = currentNode.Execute(context); // 流程中正常执行
                }

                if(currentNode.NextOrientation == ConnectionType.None)
                {
                    // 不再执行
                    break;
                }

                // 获取下一分支
                var nextNodes = currentNode.SuccessorNodes[currentNode.NextOrientation];

                // 将下一个节点集合中的所有节点逆序推入栈中
                for (int i = nextNodes.Count - 1; i >= 0; i--)
                {
                    nextNodes[i].PreviousNode = currentNode;
                    stack.Push(nextNodes[i]);
                }
            }
        }


        /// <summary>
        /// 执行节点对应的方法
        /// </summary>
        /// <param name="context">流程上下文</param>
        /// <returns>节点传回数据对象</returns>
        public virtual object? Execute(IDynamicContext context)
        {
            MethodDetails md = MethodDetails;
            var del = md.MethodDelegate;
            object instance = md.ActingInstance;

            var haveParameter = md.ExplicitDatas.Length > 0;
            var haveResult = md.ReturnType != typeof(void);
            try
            {
                // Action/Func([方法作用的实例],[可能的参数值],[可能的返回值])
                object? result = (haveParameter, haveResult) switch
                {
                    (false, false) => Execution((Action<object>)del, instance), // 调用节点方法，返回null
                    (true, false) => Execution((Action<object, object?[]?>)del, instance, GetParameters(context, md)),  // 调用节点方法，返回null
                    (false, true) => Execution((Func<object, object?>)del, instance),  // 调用节点方法，返回方法传回类型
                    (true, true) => Execution((Func<object, object?[]?, object?>)del, instance, GetParameters(context, md)), // 调用节点方法，获取入参参数，返回方法忏悔类型
                };
                NextOrientation = ConnectionType.IsSucceed;
                return result;
            }
            catch (Exception ex)
            {
                NextOrientation = ConnectionType.IsError;
                RuningException = ex;
                return null;
            }
        }

        /// <summary>
        /// 执行等待触发器的方法
        /// </summary>
        /// <param name="context"></param>
        /// <returns>节点传回数据对象</returns>
        /// <exception cref="RuningException"></exception>
        public virtual async Task<object?> ExecuteAsync(IDynamicContext context)
        {
            MethodDetails md = MethodDetails;
            Delegate del = md.MethodDelegate;
            object instance = md.ActingInstance;
            var haveParameter = md.ExplicitDatas.Length >= 0;
            try
            {
                // 调用委托并获取结果
                Task<IFlipflopContext> flipflopTask = haveParameter switch
                {
                    true => ((Func<object, object?[]?, Task<IFlipflopContext>>)del).Invoke(instance, GetParameters(context, md)), // 执行流程中的触发器方法时获取入参参数
                    false => ((Func<object, Task<IFlipflopContext>>)del).Invoke(instance),
                };

                IFlipflopContext flipflopContext = (await flipflopTask) ?? throw new FlipflopException("没有返回上下文");
                NextOrientation = flipflopContext.State.ToContentType();
                return flipflopContext.Data;
            }
            //catch(FlipflopException ex)
            //{
            //    NextOrientation = ConnectionType.IsError;
            //    RuningException = ex;
            //    return null;
            //}
            catch (Exception ex)
            {
                NextOrientation = ConnectionType.IsError;
                RuningException = ex;
                return null;
            }
        }


        #region 节点转换的委托类型
        public static object? Execution(Action<object> del, object instance)
        {
            del?.Invoke(instance);
            return null;
        }
        public static object? Execution(Action<object, object?[]?> del, object instance, object?[]? parameters)
        {
            del?.Invoke(instance, parameters);
            return null;
        }
        public static object? Execution(Func<object, object?> del, object instance)
        {
            return del?.Invoke(instance);
        }
        public static object? Execution(Func<object, object?[]?, object?> del, object instance, object?[]? parameters)
        {
            return del?.Invoke(instance, parameters);
        }
        #endregion


        /// <summary>
        /// 获取对应的参数数组
        /// </summary>
        public object?[]? GetParameters(IDynamicContext context, MethodDetails md)
        {
            // 用正确的大小初始化参数数组
            var types = md.ExplicitDatas.Select(it => it.DataType).ToArray();
            if (types.Length == 0)
            {
                return [md.ActingInstance];
            }

            object?[]? parameters = new object[types.Length];
            var flowData = PreviousNode?.FlowData; // 当前传递的数据
            var previousDataType = flowData?.GetType();

            for (int i = 0; i < types.Length; i++)
            {
                if (flowData is null)
                {
                    parameters[i] = md.ExplicitDatas[i].DataType switch
                    {
                        Type t when t == typeof(IDynamicContext) => context, // 上下文
                        Type t when t == typeof(MethodDetails) => md, // 节点方法描述
                        Type t when t == typeof(NodeModelBase) => this, // 节点实体类
                        _ => null,
                    };
                    continue; // 上一节点数据为空，提前跳过
                }
                object? inputParameter; // 
                var ed = md.ExplicitDatas[i]; // 方法入参描述

                // 检测是否为表达式
                if (ed.IsExplicitData && ed.DataValue.StartsWith("@get", StringComparison.OrdinalIgnoreCase))
                {
                    inputParameter = SerinExpressionEvaluator.Evaluate(ed.DataValue, flowData, out _);  // 执行表达式从上一节点获取对象
                }
                else
                {
                    inputParameter = flowData;   // 使用上一节点的对象
                }

                try
                {
                    parameters[i] = ed.DataType switch
                    {
                        Type t when t == previousDataType => context, // 上下文
                        Type t when t == typeof(IDynamicContext) => context, // 上下文
                        Type t when t == typeof(MethodDetails) => md, // 节点方法描述
                        Type t when t == typeof(NodeModelBase) => this, // 节点实体类
                        Type t when t == typeof(Guid) => new Guid(inputParameter?.ToString()),
                        Type t when t == typeof(decimal) => decimal.Parse(inputParameter?.ToString()),
                        Type t when t == typeof(string) => inputParameter?.ToString(),
                        Type t when t == typeof(char) => char.Parse(inputParameter?.ToString()),
                        Type t when t == typeof(bool) => bool.Parse(inputParameter?.ToString()),
                        Type t when t == typeof(byte) => byte.Parse(inputParameter?.ToString()),
                        Type t when t == typeof(int) => int.Parse(inputParameter?.ToString()),
                        Type t when t == typeof(long) => long.Parse(inputParameter?.ToString()),
                        Type t when t == typeof(DateTime) => DateTime.Parse(inputParameter?.ToString()),
                        Type t when t == typeof(float) => float.Parse(inputParameter?.ToString()),
                        Type t when t == typeof(double) => double.Parse(inputParameter?.ToString()),
                        Type t when t.IsEnum => Enum.Parse(ed.DataType, ed.DataValue),// 需要枚举
                        Type t when t.IsArray => (inputParameter as Array)?.Cast<object>().ToList(),
                        Type t when t.IsGenericType && t.GetGenericTypeDefinition() == typeof(List<>) => inputParameter,
                        Type t when Nullable.GetUnderlyingType(t) != null => inputParameter == null ? null : Convert.ChangeType(inputParameter, Nullable.GetUnderlyingType(t)),
                        _ => inputParameter,
                    };
                }
                catch (Exception ex) // 节点参数类型转换异常
                {
                    parameters[i] = null;
                    Console.WriteLine(ex);
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
        private dynamic? ConvertValue(string value, Type targetType)
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
    }
}
