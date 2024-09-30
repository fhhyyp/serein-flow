using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Serein.Library.Api;
using Serein.Library.Attributes;
using Serein.Library.Entity;
using Serein.Library.Enums;
using Serein.Library.Ex;
using Serein.Library.Utils;
using Serein.NodeFlow.Tool.SereinExpression;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using static Serein.Library.Utils.ChannelFlowInterrupt;

namespace Serein.NodeFlow.Base
{

    /// <summary>
    /// 节点基类（数据）：条件控件，动作控件，条件区域，动作区域
    /// </summary>
    public abstract partial class NodeModelBase : IDynamicFlowNode
    {


        #region 调试中断


        /// <summary>
        /// 不再中断
        /// </summary>
        public void CancelInterrupt()
        {
            this.DebugSetting.InterruptClass = InterruptClass.None;
            DebugSetting.CancelInterruptCallback?.Invoke();
        }

        #endregion

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
            this.Guid = nodeInfo.Guid;
            if(this.MethodDetails is not null)
            {
                for (int i = 0; i < nodeInfo.ParameterData.Length; i++)
                {
                    Parameterdata? pd = nodeInfo.ParameterData[i];
                    this.MethodDetails.ExplicitDatas[i].IsExplicitData = pd.State;
                    this.MethodDetails.ExplicitDatas[i].DataValue = pd.Value;
                }
            }
           
            return this;
        }

        #endregion

        #region 节点方法的执行

        /// <summary>
        /// 开始执行
        /// </summary>
        /// <param name="context"></param>
        /// <returns></returns>
        public async Task StartExecute(IDynamicContext context)
        {
            Stack<NodeModelBase> stack = new Stack<NodeModelBase>();
            stack.Push(this);
            var cts = context.Env.IOC.Get<CancellationTokenSource>(FlowStarter.FlipFlopCtsName);
            while (stack.Count > 0 ) // 循环中直到栈为空才会退出循环
            {
                if(cts is not null)
                {
                    if (cts.IsCancellationRequested)
                        break;
                }
                // 节点执行异常时跳过执行

                // 从栈中弹出一个节点作为当前节点进行处理
                var currentNode = stack.Pop();

                //// 设置方法执行的对象
                //if (currentNode.MethodDetails?.ActingInstance is not null && currentNode.MethodDetails?.ActingInstanceType is not null)
                //{
                //    currentNode.MethodDetails.ActingInstance = context.Env.IOC.GetOrRegisterInstantiate(currentNode.MethodDetails.ActingInstanceType);
                //}

                #region 执行相关

                // 首先执行上游分支
                var upstreamNodes = currentNode.SuccessorNodes[ConnectionType.Upstream];
                for (int i = upstreamNodes.Count - 1; i >= 0; i--)
                {
                    // 筛选出启用的节点
                    if (upstreamNodes[i].DebugSetting.IsEnable)
                    {
                        if (upstreamNodes[i].DebugSetting.InterruptClass != InterruptClass.None) // 执行触发前
                        {
                            var cancelType = await upstreamNodes[i].DebugSetting.GetInterruptTask();
                            await Console.Out.WriteLineAsync($"[{upstreamNodes[i]?.MethodDetails?.MethodName}]中断已{cancelType}，开始执行后继分支");
                        }
                        upstreamNodes[i].PreviousNode = currentNode;
                        await upstreamNodes[i].StartExecute(context); // 执行流程节点的上游分支
                    }
                }


                // 执行当前节点
                object? newFlowData = await currentNode.ExecutingAsync(context);
                if (cts is  null || cts.IsCancellationRequested || currentNode.NextOrientation == ConnectionType.None)
                {
                    // 不再执行
                    break;
                }
                await RefreshFlowDataAndExpInterrupt(context, currentNode, newFlowData); // 执行当前节点后刷新数据
                #endregion


                #region 执行完成

                // 选择后继分支
                var nextNodes = currentNode.SuccessorNodes[currentNode.NextOrientation];

                // 将下一个节点集合中的所有节点逆序推入栈中
                for (int i = nextNodes.Count - 1; i >= 0; i--)
                {
                    // 筛选出启用的节点、未被中断的节点
                    if (nextNodes[i].DebugSetting.IsEnable /*&& nextNodes[i].DebugSetting.InterruptClass == InterruptClass.None*/)
                    {
                        if (nextNodes[i].DebugSetting.InterruptClass != InterruptClass.None) // 执行触发前
                        {
                            var cancelType = await nextNodes[i].DebugSetting.GetInterruptTask();
                            await Console.Out.WriteLineAsync($"[{nextNodes[i]?.MethodDetails?.MethodName}]中断已{cancelType}，开始执行后继分支");
                        }
                        nextNodes[i].PreviousNode = currentNode;
                        stack.Push(nextNodes[i]);
                    }
                }
                #endregion

            }
        }

        
        /// <summary>
        /// 执行节点对应的方法
        /// </summary>
        /// <param name="context">流程上下文</param>
        /// <returns>节点传回数据对象</returns>
        public virtual async Task<object?> ExecutingAsync(IDynamicContext context)
        {
            #region 调试中断

            if (DebugSetting.InterruptClass != InterruptClass.None) // 执行触发前
            {
                var cancelType = await this.DebugSetting.GetInterruptTask();
                //if(cancelType == CancelType.Discard)
                //{
                //    this.NextOrientation = ConnectionType.None;
                //    return null;
                //}
                await Console.Out.WriteLineAsync($"[{this.MethodDetails?.MethodName}]中断已{cancelType}，开始执行后继分支");
            }

            #endregion

            MethodDetails? md = MethodDetails;
            //var del = md.MethodDelegate.Clone();
            if (md is null)
            {
                throw new Exception($"节点{this.Guid}不存在方法信息，请检查是否需要重写节点的ExecutingAsync");
            }
            if (!context.Env.TryGetDelegate(md.MethodName, out var del))
            {
                throw new Exception($"节点{this.Guid}不存在对应委托");
            }
            md.ActingInstance ??= context.Env.IOC.Get(md.ActingInstanceType);
            object instance = md.ActingInstance;

            var haveParameter = md.ExplicitDatas.Length > 0;
            var haveResult = md.ReturnType != typeof(void);
            try
            {
                // Action/Func([方法作用的实例],[可能的参数值],[可能的返回值])
                object?[]? parameters = GetParameters(context, this, md);
                object? result = (haveParameter, haveResult) switch
                {
                    (false, false) => Execution((Action<object>)del, instance), // 调用节点方法，返回null
                    (true, false) => Execution((Action<object, object?[]?>)del, instance, parameters),  // 调用节点方法，返回null
                    (false, true) => Execution((Func<object, object?>)del, instance),  // 调用节点方法，返回方法传回类型
                    (true, true) => Execution((Func<object, object?[]?, object?>)del, instance, parameters), // 调用节点方法，获取入参参数，返回方法忏悔类型
                };

                NextOrientation = ConnectionType.IsSucceed;
                return result;
            }
            catch (Exception ex)
            {
                await Console.Out.WriteLineAsync(ex.ToString());
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
        public static object?[]? GetParameters(IDynamicContext context, NodeModelBase nodeModel, MethodDetails md)
        {
            // 用正确的大小初始化参数数组
            if (md.ExplicitDatas.Length == 0)
            {
                return null;// md.ActingInstance
            }

            object?[]? parameters = new object[md.ExplicitDatas.Length];
            var flowData = nodeModel.PreviousNode?.FlowData; // 当前传递的数据
            var previousDataType = flowData?.GetType();

            for (int i = 0; i < parameters.Length; i++)
            {

                object? inputParameter; // 存放解析的临时参数
                var ed = md.ExplicitDatas[i]; // 方法入参描述


                if (ed.IsExplicitData) // 判断是否使用显示的输入参数
                {
                    if (ed.DataValue.StartsWith("@get", StringComparison.OrdinalIgnoreCase) && flowData is not null)
                    {
                        // 执行表达式从上一节点获取对象
                        inputParameter = SerinExpressionEvaluator.Evaluate(ed.DataValue, flowData, out _);
                    }
                    else
                    {
                        // 使用输入的固定值
                        inputParameter = ed.DataValue;
                    }
                }
                else
                {
                    inputParameter = flowData;   // 使用上一节点的对象
                }

                // 存在转换器
                if (ed.Convertor is not null)
                {
                    if (Enum.TryParse(ed.ExplicitType, ed.DataValue, out var resultEnum))
                    {
                        var value = ed.Convertor(resultEnum);
                        if (value is not null)
                        {
                            parameters[i] = value;
                            continue;
                        }
                        else
                        {
                            throw new InvalidOperationException("转换器调用失败");
                        }
                    }
                }

                if ( ed.DataType != ed.ExplicitType) // 获取枚举转换器中记录的枚举
                {
                    if (ed.ExplicitType.IsEnum && Enum.TryParse(ed.ExplicitType, ed.DataValue, out var resultEnum)) // 获取对应的枚举项
                    {
                        var type = EnumHelper.GetBoundValue(ed.ExplicitType, resultEnum, attr => attr.Value);
                        if(type is Type enumBindType && enumBindType is not null)
                        {
                            var value = context.Env.IOC.Instantiate(enumBindType);
                            if(value is not null)
                            {
                                parameters[i] = value;
                                continue;
                            }
                        }
                    }
                } 

                
                

                try
                {
                    string? valueStr = inputParameter?.ToString();
                    parameters[i] = ed.DataType switch
                    {
                        Type t when t == typeof(IDynamicContext) => context, // 上下文
                        Type t when t.IsEnum => Enum.Parse(ed.DataType, ed.DataValue),// 需要枚举
                        Type t when t == typeof(string) => inputParameter?.ToString(),
                        Type t when t == typeof(char)    && !string.IsNullOrEmpty(valueStr) =>  char.Parse(valueStr),
                        Type t when t == typeof(bool)    && !string.IsNullOrEmpty(valueStr) =>  inputParameter is not null && bool.Parse(valueStr),
                        Type t when t == typeof(float)   && !string.IsNullOrEmpty(valueStr) =>  float.Parse(valueStr),
                        Type t when t == typeof(decimal) && !string.IsNullOrEmpty(valueStr) =>  decimal.Parse(valueStr),
                        Type t when t == typeof(double)  && !string.IsNullOrEmpty(valueStr) =>  double.Parse(valueStr),
                        Type t when t == typeof(sbyte)   && !string.IsNullOrEmpty(valueStr) =>  sbyte.Parse(valueStr),
                        Type t when t == typeof(byte)    && !string.IsNullOrEmpty(valueStr) =>  byte.Parse(valueStr),
                        Type t when t == typeof(short)   && !string.IsNullOrEmpty(valueStr) =>  short.Parse(valueStr),
                        Type t when t == typeof(ushort)  && !string.IsNullOrEmpty(valueStr) =>  ushort.Parse(valueStr),
                        Type t when t == typeof(int)     && !string.IsNullOrEmpty(valueStr) =>  int.Parse(valueStr),
                        Type t when t == typeof(uint)    && !string.IsNullOrEmpty(valueStr) =>  uint.Parse(valueStr),
                        Type t when t == typeof(long)    && !string.IsNullOrEmpty(valueStr) =>  long.Parse(valueStr),
                        Type t when t == typeof(ulong)   && !string.IsNullOrEmpty(valueStr) =>  ulong.Parse(valueStr),
                        Type t when t == typeof(nint)    && !string.IsNullOrEmpty(valueStr) =>  nint.Parse(valueStr),
                        Type t when t == typeof(nuint)   && !string.IsNullOrEmpty(valueStr) =>  nuint.Parse(valueStr),
                        //Type t when t == typeof(DateTime)  => string.IsNullOrEmpty(valueStr) ? 0 :  DateTime.Parse(valueStr),

                        Type t when t == typeof(MethodDetails) => md, // 节点方法描述
                        Type t when t == typeof(NodeModelBase) => nodeModel, // 节点实体类

                        Type t when t.IsArray => (inputParameter as Array)?.Cast<object>().ToList(),
                        Type t when t.IsGenericType && t.GetGenericTypeDefinition() == typeof(List<>) => inputParameter,
                        _ => inputParameter,
                        // Type t when Nullable.GetUnderlyingType(t) != null => inputParameter is null ? null : Convert.ChangeType(inputParameter, Nullable.GetUnderlyingType(t)),
                    };


                }
                catch (Exception ex) // 节点参数类型转换异常
                {
                    parameters[i] = new object();
                    Console.WriteLine(ex);
                }
            }
            return parameters;
        }

        /// <summary>
        /// 更新节点数据，并检查监视表达式是否生效
        /// </summary>
        /// <param name="newData"></param>
        public static async Task RefreshFlowDataAndExpInterrupt(IDynamicContext context,NodeModelBase nodeModel, object? newData = null)
        {
            string guid = nodeModel.Guid;
            if(newData is not null)
            {
                await MonitorObjExpInterrupt(context, nodeModel, newData, 0); // 首先监视对象
                await MonitorObjExpInterrupt(context, nodeModel, newData, 1); // 然后监视节点
                nodeModel.FlowData = newData; // 替换数据
            }
        }

        private static async Task MonitorObjExpInterrupt(IDynamicContext context, NodeModelBase nodeModel, object? data, int monitorType)
        {
            MonitorObjectEventArgs.ObjSourceType sourceType;
            string? key;
            if(monitorType == 0)
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

            if (context.Env.CheckObjMonitorState(key, out List<string> exps)) // 如果新的数据处于查看状态，通知UI进行更新？交给运行环境判断？
            {
                context.Env.MonitorObjectNotification(nodeModel.Guid, data, sourceType); // 对象处于监视状态，通知UI更新数据显示
                if (exps.Count > 0)
                {
                    // 表达式环境下判断是否需要执行中断
                    bool isExpInterrupt = false;
                    string? exp = "";
                    // 判断执行监视表达式，直到为 true 时退出
                    for (int i = 0; i < exps.Count && !isExpInterrupt; i++)
                    {
                        exp = exps[i];
                        if (string.IsNullOrEmpty(exp)) continue;
                        isExpInterrupt = SereinConditionParser.To(data, exp);
                    }

                    if (isExpInterrupt) // 触发中断
                    {
                        InterruptClass interruptClass = InterruptClass.Branch; // 分支中断
                        if (context.Env.SetNodeInterrupt(nodeModel.Guid, interruptClass))
                        {
                            context.Env.TriggerInterrupt(nodeModel.Guid, exp, InterruptTriggerEventArgs.InterruptTriggerType.Exp);
                            var cancelType = await nodeModel.DebugSetting.GetInterruptTask();
                            await Console.Out.WriteLineAsync($"[{data}]中断已{cancelType}，开始执行后继分支");
                        }
                    }
                }

            }
        }
      

        /// <summary>
        /// 释放对象
        /// </summary>
        public void ReleaseFlowData()
        {
            if (typeof(IDisposable).IsAssignableFrom(FlowData?.GetType()) && FlowData is IDisposable disposable)
            {
                disposable?.Dispose();
            }
            this.FlowData = null;
        }

        /// <summary>
        /// 获取节点数据
        /// </summary>
        /// <returns></returns>
        public object? GetFlowData()
        {
            return this.FlowData;
        }
        #endregion

    }
}
