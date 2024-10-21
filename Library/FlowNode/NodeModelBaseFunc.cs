using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Serein.Library.Api;
using Serein.Library.Utils;
using Serein.Library.Utils.SereinExpression;
using System;
using System.Collections;
using System.Collections.Generic;
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

        /// <summary>
        /// 获取节点参数
        /// </summary>
        /// <returns></returns>
        public abstract Parameterdata[] GetParameterdatas();

        /// <summary>
        /// 导出为节点信息
        /// </summary>
        /// <returns></returns>
        public virtual NodeInfo ToInfo()
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
                    Parameterdata pd = nodeInfo.ParameterData[i];
                    this.MethodDetails.ParameterDetailss[i].IsExplicitData = pd.State;
                    this.MethodDetails.ParameterDetailss[i].DataValue = pd.Value;
                }
            }
            return this;
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
                var upstreamNodes = currentNode.SuccessorNodes[ConnectionType.Upstream].ToArray();
                for (int index = 0; index < upstreamNodes.Length; index++)
                {
                    NodeModelBase upstreamNode = upstreamNodes[index];
                    if (!(upstreamNode is null) && upstreamNode.DebugSetting.IsEnable)
                    {
                        if (upstreamNode.DebugSetting.InterruptClass != InterruptClass.None) // 执行触发前
                        {
                            var cancelType = await upstreamNode.DebugSetting.GetInterruptTask();
                            await Console.Out.WriteLineAsync($"[{upstreamNode.MethodDetails?.MethodName}]中断已{cancelType}，开始执行后继分支");
                        }
                        upstreamNode.PreviousNode = currentNode;
                        await upstreamNode.StartFlowAsync(context); // 执行流程节点的上游分支
                        if (upstreamNode.NextOrientation == ConnectionType.IsError)
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
                var nextNodes = currentNode.SuccessorNodes[currentNode.NextOrientation];

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

            if (DebugSetting.InterruptClass != InterruptClass.None) // 执行触发检查是否需要中断
            {
                var cancelType = await this.DebugSetting.GetInterruptTask(); // 等待中断结束
                await Console.Out.WriteLineAsync($"[{this.MethodDetails?.MethodName}]中断已{cancelType}，开始执行后继分支");
            }

            #endregion

            MethodDetails md = MethodDetails;
            //var del = md.MethodDelegate.Clone();
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
            // md.ActingInstance ??= context.Env.IOC.Get(md.ActingInstanceType);
            object instance = md.ActingInstance;


            object result = null;

            try
            {
                object[] args = GetParameters(context, this, md);
                result = await dd.InvokeAsync(md.ActingInstance, args);
                NextOrientation = ConnectionType.IsSucceed;
                return result;
            }
            catch (Exception ex)
            {
                await Console.Out.WriteLineAsync($"节点[{this.MethodDetails?.MethodName}]异常：" + ex);
                NextOrientation = ConnectionType.IsError;
                RuningException = ex;
                return null;
            }
        }



        /// <summary>
        /// 获取对应的参数数组
        /// </summary>
        public static object[] GetParameters(IDynamicContext context, NodeModelBase nodeModel, MethodDetails md)
        {
            // 用正确的大小初始化参数数组
            if (md.ParameterDetailss.Length == 0)
            {
                return null;// md.ActingInstance
            }

            object[] parameters = new object[md.ParameterDetailss.Length];
            var flowData = nodeModel.PreviousNode?.FlowData; // 当前传递的数据
            var previousDataType = flowData?.GetType();

            for (int i = 0; i < parameters.Length; i++)
            {

                object inputParameter; // 存放解析的临时参数
                var ed = md.ParameterDetailss[i]; // 方法入参描述


                if (ed.IsExplicitData) // 判断是否使用显示的输入参数
                {
                    if (ed.DataValue.StartsWith("@get", StringComparison.OrdinalIgnoreCase) && !(flowData is null))
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
                    //if (Enum.TryParse(ed.ExplicitType, ed.DataValue, out var resultEnum))
                    //{

                    //}
                }

                // 入参存在类型转换器，获取枚举转换器中记录的枚举
                if (ed.ExplicitType.IsEnum && ed.DataType != ed.ExplicitType)
                {
                    var resultEnum = Enum.Parse(ed.ExplicitType, ed.DataValue);
                    // 获取绑定的类型
                    var type = EnumHelper.GetBoundValue(ed.ExplicitType, resultEnum, attr => attr.Value);
                    if (type is Type enumBindType && !(enumBindType is null))
                    {
                        var value = context.Env.IOC.Instantiate(enumBindType);
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



                if (ed.DataType.IsValueType)
                {
                    var valueStr = inputParameter?.ToString();
                    parameters[i] = valueStr.ToValueData(ed.DataType);
                }
                else
                {
                    var valueStr = inputParameter?.ToString();
                    if (ed.DataType == typeof(string))
                    {
                        parameters[i] = valueStr;
                    }
                    else if (ed.DataType == typeof(IDynamicContext))
                    {
                        parameters[i] = context;
                    }
                    else if (ed.DataType == typeof(MethodDetails))
                    {
                        parameters[i] = md;
                    }
                    else if (ed.DataType == typeof(NodeModelBase))
                    {
                        parameters[i] = nodeModel;
                    }
                    else
                    {
                        parameters[i] = inputParameter;
                    }

                    //parameters[i] = ed.DataType switch
                    //{
                    //    Type t when t == typeof(string) => valueStr,
                    //    Type t when t == typeof(IDynamicContext) => context, // 上下文
                    //    Type t when t == typeof(DateTime)  => string.IsNullOrEmpty(valueStr) ? null :  DateTime.Parse(valueStr),

                    //    Type t when t == typeof(MethodDetails) => md, // 节点方法描述
                    //    Type t when t == typeof(NodeModelBase) => nodeModel, // 节点实体类

                    //    Type t when t.IsArray => (inputParameter as Array)?.Cast<object>().ToList(),
                    //    Type t when t.IsGenericType && t.GetGenericTypeDefinition() == typeof(List<>) => inputParameter,
                    //    _ => inputParameter,
                    //};
                }



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
                nodeModel.FlowData = newData; // 替换数据
                context.AddOrUpdate(guid, nodeModel); // 上下文中更新数据
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
                        InterruptClass interruptClass = InterruptClass.Branch; // 分支中断
                        if (await context.Env.SetNodeInterruptAsync(nodeModel.Guid, interruptClass))
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
        public object GetFlowData()
        {
            return this.FlowData;
        }
        #endregion

    }
}
