using Serein.Library;
using Serein.Library.Api;

namespace Serein.NodeFlow
{

    /// <summary>
    /// 流程任务类，包含流程的起始节点和所有节点信息。
    /// </summary>
    public class FlowTask
    {
        /// <summary>
        /// 是否异步启动流程
        /// </summary>
        public bool IsWaitStartFlow { get; set; } = true;

        /// <summary>
        /// 流程起始节点
        /// </summary>
        public Func<IFlowNode>? GetStartNode { get; set; } 

        /// <summary>
        /// 获取当前画布流程的所有节点
        /// </summary>
        public Func<List<IFlowNode>>? GetNodes { get; set; }
    }

    /// <summary>
    /// 节点任务执行依赖
    /// </summary>
    public sealed class FlowWorkOptions()
    {
        /// <summary>
        /// 流程IOC容器
        /// </summary>
        public required ISereinIOC FlowIOC { get; set; } 
        /// <summary>
        /// 流程运行环境
        /// </summary>
        public required IFlowEnvironment Environment { get; set; }// = environment;

        /// <summary>
        /// 表示运行环境状态
        /// </summary>
        public CancellationTokenSource CancellationTokenSource { get; } = new CancellationTokenSource();

        /// <summary>
        /// 上下文线程池
        /// </summary>
        public required Serein.Library.Utils.ObjectPool<IFlowContext> FlowContextPool { get;  set; } 

        /// <summary>
        /// 每个画布需要启用的节点
        /// </summary>
        public Dictionary<string, FlowTask> Flows { get; set; } = [];

        /// <summary>
        /// 初始化时需要的方法
        /// </summary>
        public List<MethodDetails> InitMds { get; set; } = [];
        /// <summary>
        /// 加载时需要的方法
        /// </summary>
        public List<MethodDetails> LoadMds { get; set; } = [];
        /// <summary>
        /// 退出时需要调用的方法
        /// </summary>
        public List<MethodDetails> ExitMds { get; set; } = [];
    }

}
