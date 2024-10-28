using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Serein.Library.Api;
using Serein.Library.Utils;
using Serein.Library.Utils.SereinExpression;
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel.Design;
using System.Linq;
using System.Linq.Expressions;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using static Serein.Library.Utils.ChannelFlowInterrupt;

namespace Serein.Library
{

    /// <summary>
    /// 节点基类（数据）：条件控件，动作控件，条件区域，动作区域
    /// </summary>
    public abstract partial class NodeModelBase : IDynamicFlowNode
    {
        #region 导出/导入项目文件节点信息

        /// <summary>
        /// 获取节点参数
        /// </summary>
        /// <returns></returns>
        public abstract ParameterData[] GetParameterdatas();

        /// <summary>
        /// 导出为节点信息
        /// </summary>
        /// <returns></returns>
        public virtual NodeInfo ToInfo()
        {
            // if (MethodDetails == null) return null;

            var trueNodes = SuccessorNodes[ConnectionInvokeType.IsSucceed].Select(item => item.Guid); // 真分支
            var falseNodes = SuccessorNodes[ConnectionInvokeType.IsFail].Select(item => item.Guid);// 假分支
            var errorNodes = SuccessorNodes[ConnectionInvokeType.IsError].Select(item => item.Guid);// 异常分支
            var upstreamNodes = SuccessorNodes[ConnectionInvokeType.Upstream].Select(item => item.Guid);// 上游分支

            // 生成参数列表
            ParameterData[] parameterData = GetParameterdatas();

            return new NodeInfo
            {
                Guid = Guid,
                MethodName = MethodDetails?.MethodName,
                Label = MethodDetails?.MethodAnotherName,
                Type = this.GetType().ToString(),
                TrueNodes = trueNodes.ToArray(),
                FalseNodes = falseNodes.ToArray(),
                UpstreamNodes = upstreamNodes.ToArray(),
                ParameterData = parameterData.ToArray(),
                ErrorNodes = errorNodes.ToArray(),
                Position = Position,
            };
        }

        /// <summary>
        /// 从节点信息加载节点
        /// </summary>
        /// <param name="nodeInfo"></param>
        /// <returns></returns>
        public virtual NodeModelBase LoadInfo(NodeInfo nodeInfo)
        {
            this.Guid = nodeInfo.Guid;

            if (nodeInfo.Position is null)
            {
                nodeInfo.Position = new PositionOfUI(0, 0);
            }
            this.Position = nodeInfo.Position;// 加载位置信息
            if (this.MethodDetails != null)
            {
                for (int i = 0; i < nodeInfo.ParameterData.Length; i++)
                {
                    var mdPd = this.MethodDetails.ParameterDetailss[i];
                    ParameterData pd = nodeInfo.ParameterData[i];
                    mdPd.IsExplicitData = pd.State;
                    mdPd.DataValue = pd.Value;
                    mdPd.ArgDataSourceType  = EnumHelper.ConvertEnum<ConnectionArgSourceType>(pd.SourceType);
                    mdPd.ArgDataSourceNodeGuid = pd.SourceNodeGuid;

                }
            }
            return this;
        }
        #endregion

        #region 调试中断

        /// <summary>
        /// 不再中断
        /// </summary>
        public void CancelInterrupt()
        {
            this.DebugSetting.IsInterrupt = false;
            DebugSetting.CancelInterruptCallback?.Invoke();
        }

        #endregion

        #region 节点方法的执行

        /// <summary>
        /// 是否应该退出执行
        /// </summary>
        /// <param name="context"></param>
        /// <param name="flowCts"></param>
        /// <returns></returns>
        public static bool IsBradk(IDynamicContext context, CancellationTokenSource flowCts)
        {
            // 上下文不再执行
            if (context.RunState == RunState.Completion)
            {
                return true;
            }

            // 不存在全局触发器时，流程运行状态被设置为完成，退出执行，用于打断无限循环分支。
            if (flowCts is null && context.Env.FlowState == RunState.Completion)
            {
                return true;
            }
            // 如果存在全局触发器，且触发器的执行任务已经被取消时，退出执行。
            if (flowCts != null)
            {
                if (flowCts.IsCancellationRequested)
                    return true;
            }
            return false;
        }

        /// <summary>
        /// 开始执行
        /// </summary>
        /// <param name="context"></param>
        /// <returns></returns>
        public async Task StartFlowAsync(IDynamicContext context)
        {
            Stack<NodeModelBase> stack = new Stack<NodeModelBase>();
            HashSet<NodeModelBase> processedNodes = new HashSet<NodeModelBase>(); // 用于记录已处理上游节点的节点
            stack.Push(this);
            var flowCts = context.Env.IOC.Get<CancellationTokenSource>(NodeStaticConfig.FlipFlopCtsName);
            bool hasFlipflow = flowCts != null;
            while (stack.Count > 0) // 循环中直到栈为空才会退出循环
            {
#if DEBUG
                await Task.Delay(1);
#endif

                #region 执行相关

                // 从栈中弹出一个节点作为当前节点进行处理
                var currentNode = stack.Pop();

                // 筛选出上游分支
                var upstreamNodes = currentNode.SuccessorNodes[ConnectionInvokeType.Upstream].ToArray();
                for (int index = 0; index < upstreamNodes.Length; index++)
                {
                    NodeModelBase upstreamNode = upstreamNodes[index];
                    if (!(upstreamNode is null) && upstreamNode.DebugSetting.IsEnable)
                    {
                        if (upstreamNode.DebugSetting.IsInterrupt) // 执行触发前
                        {
                            var cancelType = await upstreamNode.DebugSetting.GetInterruptTask();
                            await Console.Out.WriteLineAsync($"[{upstreamNode.MethodDetails?.MethodName}]中断已{cancelType}，开始执行后继分支");
                        }
                        upstreamNode.PreviousNode = currentNode;
                        await upstreamNode.StartFlowAsync(context); // 执行流程节点的上游分支
                        if (context.NextOrientation == ConnectionInvokeType.IsError)
                        {
                            // 如果上游分支执行失败，不再继续执行
                            // 使上游节点（仅上游节点本身，不包含上游节点的后继节点）
                            // 具备通过抛出异常中断流程的能力
                            break;
                        }
                    }
                }
                // 上游分支执行完成，才执行当前节点
                if (IsBradk(context, flowCts)) break; // 退出执行
                object newFlowData = await currentNode.ExecutingAsync(context);
                if (IsBradk(context, flowCts)) break; // 退出执行

                await RefreshFlowDataAndExpInterrupt(context, currentNode, newFlowData); // 执行当前节点后刷新数据
                #endregion


                #region 执行完成

                // 选择后继分支
                var nextNodes = currentNode.SuccessorNodes[context.NextOrientation];

                // 将下一个节点集合中的所有节点逆序推入栈中
                for (int i = nextNodes.Count - 1; i >= 0; i--)
                {
                    // 筛选出启用的节点的节点
                    if (nextNodes[i].DebugSetting.IsEnable)
                    {
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
        public virtual async Task<object> ExecutingAsync(IDynamicContext context)
        {
            #region 调试中断

            if (DebugSetting.IsInterrupt) // 执行触发检查是否需要中断
            {
                var cancelType = await this.DebugSetting.GetInterruptTask(); // 等待中断结束
                await Console.Out.WriteLineAsync($"[{this.MethodDetails?.MethodName}]中断已{cancelType}，开始执行后继分支");
            }

            #endregion

            MethodDetails md = MethodDetails;
            if (md is null)
            {
                throw new Exception($"节点{this.Guid}不存在方法信息，请检查是否需要重写节点的ExecutingAsync");
            }
            if (!context.Env.TryGetDelegateDetails(md.MethodName, out var dd))
            {
                throw new Exception($"节点{this.Guid}不存在对应委托");
            }
            if (md.ActingInstance is null)
            {
                md.ActingInstance = context.Env.IOC.Get(md.ActingInstanceType);
            }
            try
            {
                object[] args = await GetParametersAsync(context, this, md);
                var result = await dd.InvokeAsync(md.ActingInstance, args);
                context.NextOrientation = ConnectionInvokeType.IsSucceed;
                return result;
            }
            catch (Exception ex)
            {
                await Console.Out.WriteLineAsync($"节点[{this.MethodDetails?.MethodName}]异常：" + ex);
                context.NextOrientation = ConnectionInvokeType.IsError;
                RuningException = ex;
                return null;
            }
        }

        /// <summary>
        /// 执行单个节点对应的方法，并不做状态检查
        /// </summary>
        /// <param name="context">运行时上下文</param>
        /// <returns></returns>
        public virtual async Task<object> InvokeAsync(IDynamicContext context)
        {
            try
            {
                MethodDetails md = MethodDetails;
                if (md is null)
                {
                    throw new Exception($"不存在方法信息{md.MethodName}");
                }
                if (!Env.TryGetDelegateDetails(md.MethodName, out var dd))
                {
                    throw new Exception($"不存在对应委托{md.MethodName}");
                }
                if (md.ActingInstance is null)
                {
                    md.ActingInstance = Env.IOC.Get(md.ActingInstanceType);
                    if (md.ActingInstance is null)
                    {
                        md.ActingInstance = Env.IOC.Instantiate(md.ActingInstanceType);
                        if (md.ActingInstance is null)
                        {
                            throw new Exception($"无法创建相应的实例{md.ActingInstanceType.FullName}");
                        }
                    }
                }

                object[] args = await GetParametersAsync(context, this, md);
                var result = await dd.InvokeAsync(md.ActingInstance, args);
                return result;
            }
            catch (Exception ex)
            {
                await Console.Out.WriteLineAsync($"节点[{this.MethodDetails?.MethodName}]异常：" + ex);
                return null;
            }
        }

        /// <summary>
        /// 获取对应的参数数组
        /// </summary>
        public static async Task<object[]> GetParametersAsync(IDynamicContext context, NodeModelBase nodeModel, MethodDetails md)
        {
            await Task.Delay(0);
            // 用正确的大小初始化参数数组
            if (md.ParameterDetailss.Length == 0)
            {
                return null;// md.ActingInstance
            }

            object[] parameters = new object[md.ParameterDetailss.Length];
            
            //var previousFlowData = nodeModel.PreviousNode?.FlowData; // 当前传递的数据


            for (int i = 0; i < parameters.Length; i++)
            {
                var ed = md.ParameterDetailss[i]; // 方法入参描述

                #region 获取基础的上下文数据
                if (ed.DataType == typeof(IFlowEnvironment)) // 获取流程上下文
                {
                    parameters[i] = nodeModel.Env;
                    continue;
                }
                if (ed.DataType == typeof(IDynamicContext)) // 获取流程上下文
                {
                    parameters[i] = context;
                    continue;
                } 
                #endregion

                #region 确定[预入参]数据
                object inputParameter; // 存放解析的临时参数
                if (ed.IsExplicitData) // 判断是否使用显示的输入参数
                {
                    if (ed.DataValue.StartsWith("@get", StringComparison.OrdinalIgnoreCase))
                    {
                        var previousFlowData = context.GetFlowData(nodeModel?.PreviousNode?.Guid); // 当前传递的数据
                        // 执行表达式从上一节点获取对象
                        inputParameter = SerinExpressionEvaluator.Evaluate(ed.DataValue, previousFlowData, out _);
                    }
                    else
                    {
                        // 使用输入的固定值
                            inputParameter = ed.DataValue;
                    }
                }
                else
                {
                    if (ed.ArgDataSourceType == ConnectionArgSourceType.GetPreviousNodeData)
                    {
                        inputParameter = context.GetFlowData(nodeModel?.PreviousNode?.Guid); // 当前传递的数据
                    }
                    else if (ed.ArgDataSourceType == ConnectionArgSourceType.GetOtherNodeData)
                    {
                        // 获取指定节点的数据
                        // 如果指定节点没有被执行，会返回null
                        // 如果执行过，会获取上一次执行结果作为预入参数据
                        inputParameter = context.GetFlowData(ed.ArgDataSourceNodeGuid);
                    }
                    else if (ed.ArgDataSourceType == ConnectionArgSourceType.GetOtherNodeDataOfInvoke)
                    {
                        // 立刻调用对应节点获取数据。
                        var result = await context.Env.InvokeNodeAsync(ed.ArgDataSourceNodeGuid);
                        inputParameter = result;
                    }
                    else
                    {
                        throw new Exception("节点执行方法获取入参参数时，ConnectionArgSourceType枚举是意外的枚举值");
                    }
                }
                if (inputParameter is null)
                {
                    throw new Exception($"[arg{ed.Index}][{ed.Name}][{ed.DataType}]参数不能为null");
                }

                #endregion

                #region 入参存在取值转换器，调用对应的转换器获取入参数据
                // 入参存在取值转换器
                if (ed.ExplicitType.IsEnum && !(ed.Convertor is null))
                {
                    //var resultEnum = Enum.ToObject(ed.ExplicitType, ed.DataValue);
                    var resultEnum = Enum.Parse(ed.ExplicitType, ed.DataValue);
                    var value = ed.Convertor(resultEnum);
                    if (value is null)
                    {
                        throw new InvalidOperationException("转换器调用失败");

                    }
                    else
                    {
                        parameters[i] = value;
                        continue;
                    }
                }
                #endregion

                #region  入参存在基于BinValue的类型转换器，获取枚举转换器中记录的类型
                // 入参存在基于BinValue的类型转换器，获取枚举转换器中记录的类型
                if (ed.ExplicitType.IsEnum && ed.DataType != ed.ExplicitType)
                {
                    var resultEnum = Enum.Parse(ed.ExplicitType, ed.DataValue);
                    // 获取绑定的类型
                    var type = EnumHelper.GetBoundValue(ed.ExplicitType, resultEnum, attr => attr.Value);
                    if (type is Type enumBindType && !(enumBindType is null))
                    {
                        var value = nodeModel.Env.IOC.Instantiate(enumBindType);
                        if (value is null)
                        {

                        }
                        else
                        {
                            parameters[i] = value;
                            continue;
                        }
                    }
                }

                #endregion

                #region 对入参数据尝试进行转换
                
                if (inputParameter.GetType() == ed.DataType)
                {
                    parameters[i] = inputParameter; // 类型一致无需转换，直接装入入参数组
                }
                else if (ed.DataType.IsValueType) 
                {
                    // 值类型
                    var valueStr = inputParameter?.ToString();
                    parameters[i] = valueStr.ToValueData(ed.DataType); // 类型不一致，尝试进行转换，如果转换失败返回类型对应的默认值
                }
                else 
                {
                    // 引用类型
                    if (ed.DataType == typeof(string)) // 转为字符串
                    {
                        var valueStr = inputParameter?.ToString();
                        parameters[i] = valueStr;
                    }
                    else if(ed.DataType.IsSubclassOf(inputParameter.GetType())) // 入参类型 是 预入参数据类型 的 子类/实现类 
                    {
                        // 方法入参中，父类不能隐式转为子类，这里需要进行强制转换
                        parameters[i] =  ObjectConvertHelper.ConvertParentToChild(inputParameter, ed.DataType);
                    }
                    else if(ed.DataType.IsAssignableFrom(inputParameter.GetType()))  // 入参类型 是 预入参数据类型 的 父类/接口
                    {
                        parameters[i] = inputParameter;
                    }
                    // 集合类型
                    else if(inputParameter is IEnumerable collection)
                    {
                        var enumerableMethods = typeof(Enumerable).GetMethods();   // 获取所有的 Enumerable 扩展方法
                        MethodInfo conversionMethod;
                        if (ed.DataType.IsArray) // 转为数组
                        {
                            parameters[i] = inputParameter;
                            conversionMethod = enumerableMethods.FirstOrDefault(m => m.Name == "ToArray" && m.IsGenericMethodDefinition);
                        }
                        else if (ed.DataType.GetGenericTypeDefinition() == typeof(List<>)) // 转为集合
                        {
                             conversionMethod = enumerableMethods.FirstOrDefault(m => m.Name == "ToList" && m.IsGenericMethodDefinition);
                        }
                        else
                        {
                            throw new InvalidOperationException("输入对象不是集合或目标类型不支持（目前仅支持Array、List的自动转换）");
                        }
                        var genericMethod = conversionMethod.MakeGenericMethod(ed.DataType);
                        var result = genericMethod.Invoke(null, new object[] { collection });
                        parameters[i] = result;
                    }
                   

                  
                    //else if (ed.DataType == typeof(MethodDetails)) // 希望获取节点对应的方法描述，好像没啥用
                    //{
                    //    parameters[i] = md;
                    //}
                    //else if (ed.DataType == typeof(NodeModelBase)) // 希望获取方法生成的节点，好像没啥用
                    //{
                    //    parameters[i] = nodeModel;
                    //}

                } 
                #endregion


            }
            return parameters;
        }


        /// <summary>
        /// 更新节点数据，并检查监视表达式是否生效
        /// </summary>
        /// <param name="context">上下文</param>
        /// <param name="nodeModel">节点Moel</param>
        /// <param name="newData">新的数据</param>
        /// <returns></returns>
        public static async Task RefreshFlowDataAndExpInterrupt(IDynamicContext context, NodeModelBase nodeModel, object newData = null)
        {
            string guid = nodeModel.Guid;
            if (newData is null)
            {
            }
            else
            {
                await MonitorObjExpInterrupt(context, nodeModel, newData, 0); // 首先监视对象
                await MonitorObjExpInterrupt(context, nodeModel, newData, 1); // 然后监视节点
                //nodeModel.FlowData = newData; // 替换数据
                context.AddOrUpdate(guid, newData); // 上下文中更新数据
            }
        }

        private static async Task MonitorObjExpInterrupt(IDynamicContext context, NodeModelBase nodeModel, object data, int monitorType)
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
            (var isMonitor, var exps) = await context.Env.CheckObjMonitorStateAsync(key);
            if (isMonitor) // 如果新的数据处于查看状态，通知UI进行更新？交给运行环境判断？
            {
                context.Env.MonitorObjectNotification(nodeModel.Guid, data, sourceType); // 对象处于监视状态，通知UI更新数据显示
                if (exps.Length > 0)
                {
                    // 表达式环境下判断是否需要执行中断
                    bool isExpInterrupt = false;
                    string exp = "";
                    // 判断执行监视表达式，直到为 true 时退出
                    for (int i = 0; i < exps.Length && !isExpInterrupt; i++)
                    {
                        exp = exps[i];
                        if (string.IsNullOrEmpty(exp)) continue;
                        // isExpInterrupt = SereinConditionParser.To(data, exp);
                    }

                    if (isExpInterrupt) // 触发中断
                    {
                        nodeModel.DebugSetting.IsInterrupt = true;
                        if (await context.Env.SetNodeInterruptAsync(nodeModel.Guid,true))
                        {
                            context.Env.TriggerInterrupt(nodeModel.Guid, exp, InterruptTriggerEventArgs.InterruptTriggerType.Exp);
                            var cancelType = await nodeModel.DebugSetting.GetInterruptTask();
                            await Console.Out.WriteLineAsync($"[{data}]中断已{cancelType}，开始执行后继分支");
                            nodeModel.DebugSetting.IsInterrupt = false;
                        }
                    }
                }

            }
        }

        ///// <summary>
        ///// 释放对象
        ///// </summary>
        //public void ReleaseFlowData()
        //{
        //    if (typeof(IDisposable).IsAssignableFrom(FlowData?.GetType()) && FlowData is IDisposable disposable)
        //    {
        //        disposable?.Dispose();
        //    }
        //    this.FlowData = null;
        //}

        ///// <summary>
        ///// 获取节点数据
        ///// </summary>
        ///// <returns></returns>
        //public object GetFlowData()
        //{
        //    return this.FlowData;
        //}
        #endregion

    }
}
