using Serein.Library;
using Serein.Library.Api;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;

namespace Serein.Workbench.Avalonia.Services
{

    #region 工作台事件

    public delegate void PreviewlMethodInfoHandler(PreviewlMethodInfoEventArgs eventArgs);

    #endregion

    #region 工作台事件参数
    public class PreviewlMethodInfoEventArgs(MethodDetailsInfo mdInfo) : EventArgs
    {
        /// <summary>
        /// 方法信息
        /// </summary>
        public MethodDetailsInfo MethodDetailsInfo { get; } = mdInfo;
    }
    #endregion


    /// <summary>
    /// 工作台事件管理
    /// </summary>
    internal interface IWorkbenchEventService
    {
        /// <summary>
        /// 预览了某个方法信息（待创建）
        /// </summary>
        event PreviewlMethodInfoHandler OnPreviewlMethodInfo;

        /// <summary>
        /// 预览依赖方法信息
        /// </summary>
        void PreviewLibraryMethodInfo(MethodDetailsInfo mdInfo);
    }

    /// <summary>
    /// 工作台事件的实现类
    /// </summary>
    internal class WorkbenchEventService : IWorkbenchEventService
    {

        private readonly IFlowEnvironment flowEnvironment;
        /// <summary>
        /// 管理工作台的事件
        /// </summary>
        /// <param name="flowEnvironment"></param>
        public WorkbenchEventService(IFlowEnvironment flowEnvironment)
        {
            this.flowEnvironment = flowEnvironment;
            
        }

        private void SubscribeEvents()
        {
            
        }

        /// <summary>
        /// 预览了某个方法信息（待创建）
        /// </summary>
        public event PreviewlMethodInfoHandler? OnPreviewlMethodInfo;
        /// <summary>
        /// 预览依赖方法信息
        /// </summary>
        public void PreviewLibraryMethodInfo(MethodDetailsInfo mdInfo)
        {
            OnPreviewlMethodInfo?.Invoke(new PreviewlMethodInfoEventArgs(mdInfo));
        }

        /// <summary>
        /// 需要放置节点控件
        /// </summary>
        public void PlateNodeControl()
        {

        }
    }
    
}

