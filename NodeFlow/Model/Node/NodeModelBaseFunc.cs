using Serein.Library;
using Serein.Library.Api;

namespace Serein.NodeFlow.Model
{

    




    /// <summary>
    /// 节点基类
    /// </summary>
    public abstract partial class NodeModelBase : ISereinFlow
    {
        /// <summary>
        /// 实体节点创建完成后调用的方法，调用时间早于 LoadInfo() 方法
        /// </summary>
        public virtual void OnCreating()
        {

        }

        /// <summary>
        /// 保存自定义信息
        /// </summary>
        /// <returns></returns>
        public virtual NodeInfo SaveCustomData(NodeInfo nodeInfo)
        {
            return nodeInfo;
        }

        /// <summary>
        /// 加载自定义数据
        /// </summary>
        /// <param name="nodeInfo"></param>
        public virtual void LoadCustomData(NodeInfo nodeInfo)
        {
            return;
        }


        /// <summary>
        /// 执行节点对应的方法
        /// </summary>
        /// <param name="context">流程上下文</param>
        /// <param name="token"></param>
        /// <param name="args">自定义参数</param>
        /// <returns>节点传回数据对象</returns>
        public virtual async Task<FlowResult> ExecutingAsync(IFlowContext context, CancellationToken token)
        {
            
            // 执行触发检查是否需要中断
            if (DebugSetting.IsInterrupt)
            {
                context.Env.FlowControl.TriggerInterrupt(Guid, "", InterruptTriggerEventArgs.InterruptTriggerType.Monitor); // 通知运行环境该节点中断了
                await DebugSetting.GetInterruptTask.Invoke();
                SereinEnv.WriteLine(InfoType.INFO, $"[{this.MethodDetails?.MethodName}]中断已取消，开始执行后继分支");
                if (token.IsCancellationRequested) { return null; }
            }

            MethodDetails? md = MethodDetails;
            if (md is null)
            {
                throw new Exception($"节点{Guid}不存在方法信息，请检查是否需要重写节点的ExecutingAsync");
            }
            if (!context.Env.TryGetDelegateDetails(md.AssemblyName, md.MethodName, out var dd))  // 流程运行到某个节点
            {
                throw new Exception($"节点{this.Guid}不存在对应委托");
            }

            if (md.IsStatic)
            {
                object[] args = await this.GetParametersAsync(context, token);
                var result = await dd.InvokeAsync(null, args);
                var flowReslt = new FlowResult(this.Guid, context, result);
                return flowReslt;
            }
            else
            {
                var instance = Env.FlowControl.IOC.Get(md.ActingInstanceType);
                if (instance is null)
                {
                    Env.FlowControl.IOC.Register(md.ActingInstanceType).Build();
                    instance = Env.FlowControl.IOC.Get(md.ActingInstanceType);
                }
                object[] args = await this.GetParametersAsync(context, token);
                var result = await dd.InvokeAsync(instance, args);
                var flowReslt = new FlowResult(this.Guid, context, result);
                return flowReslt;
            }
            

        }


    }


}
