using Serein.Library;
using Serein.Library.Utils;
using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;

namespace Serein.Library.Api
{
    /// <summary>
    /// 流程上下文，包含运行环境接口，可以通过注册环境事件或调用环境接口，实现在流程运行时更改流程行为。
    /// </summary>
    public interface IFlowContext
    {
        /// <summary>
        /// 标识流程
        /// </summary>
        string Guid {get; }

        /// <summary>
        /// 是否记录流程信息
        /// </summary>
        bool IsRecordInvokeInfo { get; set; }

        /*/// <summary>
        /// <para>用于同一个流程上下文中共享、存储任意数据</para>
        /// <para>流程完毕时，如果存储的对象实现了 IDisposable 接口，将会自动调用</para>
        /// <para>该属性的 set 仅限内部访问，如需赋值，请通过 SetTag() </para>
        /// <para>请谨慎使用，请注意数据的生命周期和内存管理</para>
        /// </summary>
        object? Tag { get; }*/

        /// <summary>
        /// 运行环境
        /// </summary>
        IFlowEnvironment Env { get; }


        /// <summary>
        /// 是否正在运行
        /// </summary>
        RunState RunState { get; }


        /// <summary>
        /// 下一个要执行的节点类别
        /// </summary>
        ConnectionInvokeType NextOrientation { get; set; }

        /// <summary>
        /// 运行时异常信息
        /// </summary>
        Exception ExceptionOfRuning { get; set; }


        /// <summary>
        /// 获取当前流程上下文的所有节点调用信息，包含每个节点的执行时间、调用类型、执行状态等。
        /// </summary>
        /// <returns></returns>
        List<FlowInvokeInfo> GetAllInvokeInfos();

    
        /// <summary>
        /// 新增当前流程上下文的节点调用信息。
        /// </summary>
        FlowInvokeInfo NewInvokeInfo(IFlowNode previousNode, IFlowNode theNode, FlowInvokeInfo.InvokeType invokeType);


        /// <summary>
        /// 获取节点的运行时参数数据
        /// </summary>
        /// <param name="nodeModel">节点</param>
        /// <param name="index">第几个参数</param>
        /// <param name="data">数据</param>
        void SetParamsTempData(string nodeModel, int index, object data);

        /// <summary>
        /// 获取节点的运行时参数数据
        /// </summary>
        /// <param name="nodeModel">节点</param>
        /// <param name="index">第几个参数</param>
        /// <param name="data">获取到的参数</param>
        bool TryGetParamsTempData(string nodeModel, int index, out object data);



        /// <summary>
        /// 设置节点的运行时上一节点，用以多线程中隔开不同流程的数据
        /// </summary>
        /// <param name="currentNodeModel">当前节点</param>
        /// <param name="PreviousNode">运行时上一节点</param>
        void SetPreviousNode(string currentNodeModel, string PreviousNode);

        /// <summary>
        /// 获取当前节点的运行时上一节点，用以流程中获取数据
        /// </summary>
        /// <param name="currentNodeModel"></param>
        /// <returns></returns>
        string GetPreviousNode(string currentNodeModel);

        /// <summary>
        /// 获取节点的数据（当前节点需要获取上一节点数据时，需要从 运行时上一节点 的Guid 通过这个方法进行获取
        /// </summary>
        /// <param name="nodeModel"></param>
        /// <returns></returns>
        FlowResult GetFlowData(string nodeModel);
        /// <summary>
        /// 上一节点数据透传到下一节点
        /// </summary>
        /// <param name="nodeModel"></param>
        FlowResult TransmissionData(string nodeModel);

        /// <summary>
        /// 添加或更新当前节点的数据
        /// </summary>
        /// <param name="nodeModel"></param>
        /// <param name="flowData"></param>
        void AddOrUpdateFlowData(string nodeModel, FlowResult flowData);

        /// <summary>
        /// 添加或更新当前节点的数据
        /// </summary>
        /// <param name="nodeModel"></param>
        /// <param name="data"></param>
        void AddOrUpdate(string nodeModel, object data);

        /// <summary>
        /// 设置共享对象（在同一个上下文中保持一致）
        /// </summary>
        /// <param name="tag"></param>
        void SetTag<T>(T tag);

        /// <summary>
        /// 指定泛型尝试获取共享对象（在同一个上下文中保持一致）
        /// </summary>
        /// <typeparam name="T"></typeparam>
        T? GetTag<T>();

#if NET6_0_OR_GREATER
        /// <summary>
        /// 指定泛型尝试获取共享对象（在同一个上下文中保持一致）
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="tag"></param>
        bool TryGetTag<T>([NotNullWhen(true)] out T? tag);
#else
        /// <summary>
        /// 指定泛型尝试获取共享对象（在同一个上下文中保持一致）
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="tag"></param>
        bool TryGetTag<T>(out T? tag);
#endif


        /// <summary>
        /// 重置流程状态（用于对象池回收）
        /// </summary>
        void Reset();
        
        /// <summary>
        /// 用以提前结束当前上下文流程的运行
        /// </summary>
        void Exit();
    }

   
    /// <summary>
    /// 流程调用信息，记录每个节点的调用信息，包括执行时间、调用类型、执行状态等。
    /// </summary>
    public sealed class FlowInvokeInfo
    {
        /// <summary>
        /// 调用类型枚举，指示节点的调用方式。
        /// </summary>
        public enum InvokeType 
        {
            /// <summary>
            /// 初始化。
            /// </summary>
            None = -1,
            /// <summary>
            /// 上游分支调用，指向上游流程的节点。
            /// </summary>
            Upstream = 0,
            /// <summary>
            /// 真分支调用，指示节点执行成功后的分支。
            /// </summary>
            IsSucceed = 1,
            /// <summary>
            /// 假分支调用，指示节点执行失败后的分支。
            /// </summary>
            IsFail = 2,
            /// <summary>
            /// 异常发生分支调用，指示节点执行过程中发生异常后的分支。
            /// </summary>
            IsError = 3,
            /// <summary>
            /// 参数来源调用，标明此次调用出自其它节点需要参数时的执行。
            /// </summary>
            ArgSource = 4,
        }

        /// <summary>
        /// 运行状态枚举，指示节点的执行结果。
        /// </summary>
        public enum RunState
        {
            /// <summary>
            /// 初始化
            /// </summary>
            None = -1,
            /// <summary>
            /// 正在运行，指示节点正在执行中。
            /// </summary>
            Running = 0,
            /// <summary>
            /// 执行成功，指示节点执行完成且结果正常。
            /// </summary>
            Succeed = 1,
            /// <summary>
            /// 执行失败，指示节点执行完成但结果异常或不符合预期。
            /// </summary>
            Failed = 2,
            /// <summary>
            /// 执行异常，指示节点在执行过程中发生了未处理的异常。
            /// </summary>
            Error = 3,
        }

        /// <summary>
        /// 流程上下文标识符，唯一标识一个流程上下文
        /// </summary>
        public string ContextGuid { get;  set; }

        /// <summary>
        /// 节点执行信息标识符，唯一标识一个节点执行信息
        /// </summary>
        public long Id { get; set; }

        /// <summary>
        /// 上一节点的唯一标识符，指向流程图中的上一个节点
        /// </summary>
        public string PreviousNodeGuid { get; set; }

        /// <summary>
        /// 节点唯一标识符，指向流程图中的节点
        /// </summary>
        public string NodeGuid { get; set; }

        /// <summary>
        /// 执行时间，记录节点执行的时间戳
        /// </summary>
        public DateTime StateTime { get; } = DateTime.Now;

        /// <summary>
        /// 节点调用类型，指示节点的调用方式
        /// </summary>
        public InvokeType Type { get; set; }



        /// <summary>
        /// 节点方法名称，指示节点执行的方法
        /// </summary>
        public string Method { get; set; }

        /// <summary>
        /// 节点执行状态，指示节点的执行结果
        /// </summary>
        public RunState State { get; set; }

        /// <summary>
        /// 耗时，记录节点执行的耗时（单位：毫秒）
        /// </summary>
        public TimeSpan TS { get; private set; } = TimeSpan.Zero;

        /// <summary>
        /// 节点参数，存储节点执行时的参数信息
        /// </summary>
        public string[] Parameters { get; private set; }

        /// <summary>
        /// 节点执行结果，存储节点执行后的结果信息
        /// </summary>
        public string Result { get; private set; }

        /// <summary>
        /// 上传当前节点的执行状态和结果信息。
        /// </summary>
        /// <param name="runState"></param>
        public void UploadState(RunState runState)
        {
            State = runState;
            TS = DateTime.Now - StateTime;
        }

        /// <summary>
        /// 上传当前节点的执行结果值。
        /// </summary>
        /// <param name="value"></param>
        public void UploadResultValue(object value = null)
        {
            if(value is null)
            {
                Result = string.Empty;
            }
            else
            {
                var type = value.GetType();
                Result = $"{type.FullName}::{value}";
            }
        }

        /// <summary>
        /// 上传当前节点的执行参数信息。
        /// </summary>
        /// <param name="values"></param>
        public void UploadParameters(object[] values = null)
        {
            if (values is null)
            {
                Parameters = [];

            }
            else
            {
                Parameters = values.Select(v => v.ToString()).ToArray();
            }
        }

        /// <summary>
        /// 返回当前节点的执行信息字符串，包含状态、耗时和结果。
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            return $"[{State}]{TS.TotalSeconds:0.000}ms : {Result}";
        }
    }













}


