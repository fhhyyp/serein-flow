using Serein.Library;
using Serein.Library.Api;
using Serein.Library.Utils;
using Serein.NodeFlow.Services;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Serein.NodeFlow.Model.Operations
{
    internal interface IOperation
    {
        /// <summary>
        /// 用于判断是否可以撤销
        /// </summary>
        bool IsCanUndo { get; }
        /// <summary>
        /// 执行操作前验证数据
        /// </summary>
        /// <returns></returns>
        bool ValidationParameter();
        /// <summary>
        /// 执行操作
        /// </summary>
        Task<bool> ExecuteAsync();
        /// <summary>
        /// 撤销操作
        /// </summary>
        bool Undo();   
    }

    internal abstract class OperationBase : IOperation
    {
        /// <summary>
        /// 运行环境
        /// </summary>
        [AutoInjection]
        protected IFlowEnvironment flowEnvironment;

        /// <summary>
        /// 节点管理服务
        /// </summary>
        [AutoInjection]
        protected FlowModelService flowModelService;

        /// <summary>
        /// 节点管理服务
        /// </summary>
        [AutoInjection]
        protected UIContextOperation uiContextOperation;

        /// <summary>
        /// 流程依赖服务
        /// </summary>
        [AutoInjection]
        protected IFlowLibraryService flowLibraryManagement;

        /// <summary>
        /// 流程事件服务
        /// </summary>
        [AutoInjection]
        protected IFlowEnvironmentEvent flowEnvironmentEvent;

       
        public abstract string Theme { get;}

        /// <summary>
        /// 是否支持特效
        /// </summary>
        public virtual bool IsCanUndo => true;


        /// <summary>
        /// 验证参数
        /// </summary>
        /// <returns></returns>
        public abstract bool ValidationParameter();

        /// <summary>
        /// 执行
        /// </summary>
        public abstract Task<bool> ExecuteAsync();

        /// <summary>
        /// 撤销
        /// </summary>
        public virtual bool Undo()
        {
            if (!IsCanUndo)
            {
                Debug.WriteLine($"该操作暂未提供撤销功能[{Theme}]");
                return false;
            }
            return true;
        }

        /// <summary>
        /// 导出操作信息
        /// </summary>
        public abstract void ToInfo();


        protected async Task TriggerEvent(Action action) => await SereinEnv.TriggerEvent(action);

    }

    internal class OperationInfo
    {

    }


    class Test {

       

    }




    
}
