using Microsoft.Extensions.ObjectPool;
using Serein.Library;
using Serein.Library.Api;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Serein.NodeFlow
{
    /// <summary>
    /// 节点任务执行依赖
    /// </summary>
    public class FlowWorkLibrary()
    {
        /// <summary>
        /// 流程运行环境
        /// </summary>
        public IFlowEnvironment Environment { get; set; }// = environment;

        /// <summary>
        /// 表示运行环境状态
        /// </summary>
        public CancellationTokenSource CancellationTokenSource { get; } = new CancellationTokenSource();

        /// <summary>
        /// 上下文线程池
        /// </summary>
        public Serein.Library.Utils.ObjectPool<IDynamicContext> FlowContextPool { get; set; }   

        /// <summary>
        /// 当前任务加载的所有节点
        /// </summary>
        public List<NodeModelBase> Nodes { get; set; }// = nodes;
        /// <summary>
        /// 需要注册的类型
        /// </summary>
        public Dictionary<RegisterSequence, List<Type>> AutoRegisterTypes { get; set; } //= autoRegisterTypes;
        /// <summary>
        /// 初始化时需要的方法
        /// </summary>
        public List<MethodDetails> InitMds { get; set; }// = initMds;
        /// <summary>
        /// 加载时需要的方法
        /// </summary>
        public List<MethodDetails> LoadMds { get; set; }// = loadMds;
        /// <summary>
        /// 退出时需要调用的方法
        /// </summary>
        public List<MethodDetails> ExitMds { get; set; } //= exitMds;
    }

}
