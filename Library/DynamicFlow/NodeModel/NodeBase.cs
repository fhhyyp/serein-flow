using Serein.DynamicFlow;
using Serein.DynamicFlow.Tool;
using Newtonsoft.Json;
using SqlSugar;
using System;
using System.Threading.Tasks;
using System.Threading;
using System.Collections.Generic;
using System.Linq;

namespace Serein.DynamicFlow.NodeModel
{

    public enum ConnectionType
    {
        /// <summary>
        /// 真分支
        /// </summary>
        IsSucceed,
        /// <summary>
        /// 假分支
        /// </summary>
        IsFail,
        /// <summary>
        /// 异常发生分支
        /// </summary>
        IsError,
        /// <summary>
        /// 上游分支（执行当前节点前会执行一次上游分支）
        /// </summary>
        Upstream,
    }

    /// <summary>
    /// 节点基类（数据）：条件控件，动作控件，条件区域，动作区域
    /// </summary>
    public abstract class NodeBase : IDynamicFlowNode
    {

        public MethodDetails MethodDetails { get; set; }


        public string Guid { get; set; }


        public string DisplayName { get; set; }

        public bool IsStart { get; set; }

        public string DelegateName { get; set; }


        /// <summary>
        /// 运行时的上一节点
        /// </summary>
        public NodeBase? PreviousNode { get; set; }

        /// <summary>
        /// 上一节点集合
        /// </summary>
        public List<NodeBase> PreviousNodes { get; set; } = [];
        /// <summary>
        /// 下一节点集合（真分支）
        /// </summary>
        public List<NodeBase> SucceedBranch { get; set; } = [];
        /// <summary>
        /// 下一节点集合（假分支）
        /// </summary>
        public List<NodeBase> FailBranch { get; set; } = [];
        /// <summary>
        /// 异常分支
        /// </summary>
        public List<NodeBase> ErrorBranch { get; set; } = [];
        /// <summary>
        /// 上游分支
        /// </summary>
        public List<NodeBase> UpstreamBranch { get; set; } = [];

        /// <summary>
        /// 当前状态（进入真分支还是假分支，异常分支在异常中确定）
        /// </summary>
        public bool FlowState { get; set; } = true;
        //public ConnectionType NextType { get; set; } = ConnectionType.IsTrue;
        /// <summary>
        /// 当前传递数据
        /// </summary>
        public object? FlowData { get; set; } = null;


        // 正常流程节点调用
        public virtual object? Execute(DynamicContext context)
        {
            MethodDetails md = MethodDetails;
            object? result = null;

            if (DelegateCache.GlobalDicDelegates.TryGetValue(md.MethodName, out Delegate del))
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
                    object?[]? parameters = GetParameters(context, MethodDetails);
                    if (md.ReturnType == typeof(void))
                    {
                        ((Action<object, object[]>)del).Invoke(md.ActingInstance, parameters);
                    }
                    else
                    {
                        result = ((Func<object, object[], object>)del).Invoke(md.ActingInstance, parameters);


                    }
                }
                // context.SetFlowData(result);
                // CurrentData = result;
            }

            return result;
        }

        // 触发器调用
        public virtual async Task<object?> ExecuteAsync(DynamicContext context)
        {
            MethodDetails md = MethodDetails;
            object? result = null;

            if (DelegateCache.GlobalDicDelegates.TryGetValue(md.MethodName, out Delegate del))
            {
                if (md.ExplicitDatas.Length == 0)
                {
                    // 调用委托并获取结果
                    FlipflopContext flipflopContext = await ((Func<object, Task<FlipflopContext>>)del).Invoke(MethodDetails.ActingInstance);

                    if (flipflopContext != null)
                    {
                        if (flipflopContext.State == FfState.Cancel)
                        {
                            throw new Exception("this async task is cancel.");
                        }
                        else
                        {
                            if (flipflopContext.State == FfState.Succeed)
                            {
                                FlowState = true;
                                result = flipflopContext.Data;
                            }
                            else
                            {
                                FlowState = false;
                            }
                        }
                    }
                }
                else
                {
                    object?[]? parameters = GetParameters(context, MethodDetails);
                    // 调用委托并获取结果


                    FlipflopContext flipflopContext = await ((Func<object, object[], Task<FlipflopContext>>)del).Invoke(MethodDetails.ActingInstance, parameters);



                    if (flipflopContext != null)
                    {
                        if (flipflopContext.State == FfState.Cancel)
                        {
                            throw new Exception("取消此异步");
                        }
                        else
                        {
                            FlowState = flipflopContext.State == FfState.Succeed;
                            result = flipflopContext.Data;
                        }
                    }
                }
            }

            return result;
        }

        public async Task ExecuteStack(DynamicContext context)
        {
            await Task.Run(async () =>
            {
                await ExecuteStackTmp(context);
            });
        }

        public async Task ExecuteStackTmp(DynamicContext context)
        {
            var cts = context.ServiceContainer.Get<CancellationTokenSource>();
           
            Stack<NodeBase> stack =[];
            stack.Push(this);

            while (stack.Count > 0 && !cts.IsCancellationRequested) // 循环中直到栈为空才会退出循环
            {
                // 从栈中弹出一个节点作为当前节点进行处理
                var currentNode = stack.Pop();

                if (currentNode.MethodDetails != null)
                {
                    currentNode.MethodDetails.ActingInstance ??= context.ServiceContainer.Get(MethodDetails.ActingInstanceType);
                } // 设置方法执行的对象

                // 获取上游分支，首先执行一次
                var upstreamNodes = currentNode.UpstreamBranch;
                for (int i = upstreamNodes.Count - 1; i >= 0; i--)
                {
                    upstreamNodes[i].PreviousNode = currentNode;
                    await upstreamNodes[i].ExecuteStack(context);
                }


                if (currentNode.MethodDetails != null && currentNode.MethodDetails.MethodDynamicType == DynamicNodeType.Flipflop)
                {
                    currentNode.FlowData = await currentNode.ExecuteAsync(context);
                }
                else
                {
                    currentNode.FlowData = currentNode.Execute(context);
                }
                

                var nextNodes = currentNode.FlowState ? currentNode.SucceedBranch 
                                                         : currentNode.FailBranch;

                // 将下一个节点集合中的所有节点逆序推入栈中
                for (int i = nextNodes.Count - 1; i >= 0; i--)
                {
                    nextNodes[i].PreviousNode = currentNode;
                    stack.Push(nextNodes[i]);
                }
            }
        }
        

        public object[]? GetParameters(DynamicContext context, MethodDetails md)
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

                var f1 = PreviousNode?.FlowData?.GetType();
                var f2 = mdEd.DataType;
                if (type == typeof(DynamicContext))
                {
                    parameters[i] = context;
                }
                else if (type == typeof(MethodDetails))
                {
                    parameters[i] = md;
                }
                else if (type == typeof(NodeBase))
                {
                    parameters[i] = this;
                }
                else if (mdEd.IsExplicitData) // 显式参数
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
                else if ((f1 != null && f2 != null) &&  f2.IsAssignableFrom(f1) || f2.FullName.Equals(f1.FullName))
                {
                    parameters[i] = PreviousNode?.FlowData;
                }
                else
                {
                   

                    var tmpParameter = PreviousNode?.FlowData?.ToString();
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
                        if (tmpParameter != null && mdEd.DataType!= null)
                        {

                            parameters[i] = ConvertValue(tmpParameter, mdEd.DataType);

                        }
                    }
                }

            }
            return parameters;
        }


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
                Console.WriteLine("无法转为对应的JSON对象:"+typeInfo);
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


/* while (stack.Count > 0) // 循环中直到栈为空才会退出
 {
     // 从栈中弹出一个节点作为当前节点进行处理
     var currentNode = stack.Pop();

     if(currentNode is CompositeActionNode || currentNode is CompositeConditionNode)
     {
         currentNode.currentState = true;
     }
     else if (currentNode is CompositeConditionNode)
     {

     }
     currentNode.Execute(context);
     // 根据当前节点的执行结果选择下一节点集合
     // 如果 currentState 为真，选择 TrueBranchNextNodes；否则选择 FalseBranchNextNodes
     var nextNodes = currentNode.currentState ? currentNode.TrueBranchNextNodes 
                                              : currentNode.FalseBranchNextNodes;

     // 将下一个节点集合中的所有节点逆序推入栈中
     for (int i = nextNodes.Count - 1; i >= 0; i--)
     {
         stack.Push(nextNodes[i]);
     }

 }*/