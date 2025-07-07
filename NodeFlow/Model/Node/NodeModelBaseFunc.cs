using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Serein.Library;
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
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace Serein.NodeFlow.Model
{

    




    /// <summary>
    /// 节点基类
    /// </summary>
    public abstract partial class NodeModelBase : IDynamicFlowNode
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

       /* /// <summary>
        /// 移除该节点
        /// </summary>
        public virtual void Remove()
        {
            if (this.DebugSetting.CancelInterrupt != null)
            {
                this.DebugSetting.CancelInterrupt?.Invoke();
            }

            if (this.IsPublic)
            {
                this.CanvasDetails.PublicNodes.Remove(this);
            }

            this.DebugSetting.NodeModel = null;
            this.DebugSetting = null;
            if(this.MethodDetails is not null)
            {
                if (this.MethodDetails.ParameterDetailss != null)
                {
                    foreach (var pd in this.MethodDetails.ParameterDetailss)
                    {
                        pd.DataValue = null;
                        pd.Items = null;
                        pd.NodeModel = null;
                        pd.ExplicitType = null;
                        pd.DataType = null;
                        pd.Name = null;
                        pd.ArgDataSourceNodeGuid = null;
                        pd.InputType = ParameterValueInputType.Input;
                    }
                }
                this.MethodDetails.ParameterDetailss = null;
                this.MethodDetails.NodeModel = null;
                this.MethodDetails.ReturnType = null;
                this.MethodDetails.ActingInstanceType = null;
                this.MethodDetails = null;
            }
           
            this.Position = null;
            this.DisplayName = null;

            this.Env = null;
        }*/

        /// <summary>
        /// 执行节点对应的方法
        /// </summary>
        /// <param name="context">流程上下文</param>
        /// <param name="token"></param>
        /// <param name="args">自定义参数</param>
        /// <returns>节点传回数据对象</returns>
        public virtual async Task<FlowResult> ExecutingAsync(IDynamicContext context, CancellationToken token)
        {
            
            // 执行触发检查是否需要中断
            if (DebugSetting.IsInterrupt)
            {
                context.Env.FlowControl.TriggerInterrupt(Guid, "", InterruptTriggerEventArgs.InterruptTriggerType.Monitor); // 通知运行环境该节点中断了
                await DebugSetting.GetInterruptTask.Invoke();
                SereinEnv.WriteLine(InfoType.INFO, $"[{this.MethodDetails?.MethodName}]中断已取消，开始执行后继分支");
                if (token.IsCancellationRequested) { return null; }
            }

            MethodDetails md = MethodDetails;
            if (md is null)
            {
                throw new Exception($"节点{this.Guid}不存在方法信息，请检查是否需要重写节点的ExecutingAsync");
            }
            if (!context.Env.TryGetDelegateDetails(md.AssemblyName, md.MethodName, out var dd))  // 流程运行到某个节点
            {
                
                throw new Exception($"节点{this.Guid}不存在对应委托");
            }
            var instance = Env.IOC.Get(md.ActingInstanceType);
            if (instance is null)
            {
                Env.IOC.Register(md.ActingInstanceType).Build();
                instance = Env.IOC.Get(md.ActingInstanceType);
            }
            object[] args = await this.GetParametersAsync(context, token);
            var result = await dd.InvokeAsync(instance, args);
            var flowReslt = new FlowResult(this.Guid, context, result);
            return flowReslt;

        }

    }


}
