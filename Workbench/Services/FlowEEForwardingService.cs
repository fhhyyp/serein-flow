
using Newtonsoft.Json.Linq;
using Serein.Library.Api;
using Serein.NodeFlow;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Serein.Library;
using Serein.Library.Utils;
using Serein.Workbench.Avalonia.Api;
using Serein.Workbench.Api;
using System.Diagnostics;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.StartPanel;

namespace Serein.Workbench.Services
{



    internal class FlowEEForwardingService : IFlowEEForwardingService
    {
        /// <summary>
        /// 流程运行环境
        /// </summary>
        private readonly IFlowEnvironment flowEnvironment;
        private readonly IFlowEnvironmentEvent flowEnvironmentEvent;
        private readonly UIContextOperation uiContextOperation;

        /// <summary>
        /// 转发流程运行环境各个事件的实现类
        /// </summary>
        /// <param name="flowEnvironment"></param>
        /// <param name="flowEnvironmentEvent"></param>
        /// <param name="uIContextOperation"></param>
        public FlowEEForwardingService(IFlowEnvironment flowEnvironment,
                                       IFlowEnvironmentEvent flowEnvironmentEvent,
                                       UIContextOperation uIContextOperation)
        {
            this.flowEnvironment = flowEnvironment;
            this.flowEnvironmentEvent = flowEnvironmentEvent;
            this.uiContextOperation = uIContextOperation;
            InitFlowEnvironmentEvent();
        }

        #region 工作台事件转发
        /// <summary>
        /// 加载了依赖文件事件
        /// </summary>
        public event LoadDllHandler? DllLoad;
        /// <summary>
        /// 项目加载完成事件
        /// </summary>
        public event ProjectLoadedHandler? ProjectLoaded;
        /// <summary>
        /// 项目保存中事件
        /// </summary>
        public event ProjectSavingHandler? ProjectSaving;
        /// <summary>
        /// 节点连接改变事件
        /// </summary>
        public event NodeConnectChangeHandler? NodeConnectChanged;
        /// <summary>
        /// 节点创建事件
        /// </summary>
        public event NodeCreateHandler? NodeCreated;
        /// <summary>
        /// 节点移除事件
        /// </summary>
        public event NodeRemoveHandler? NodeRemoved;
        /// <summary>
        /// 节点放置容器事件
        /// </summary>
        public event NodePlaceHandler? NodePlace;
        /// <summary>
        /// 节点取出事件
        /// </summary>
        public event NodeTakeOutHandler? NodeTakeOut;
        /// <summary>
        /// 流程起始节点改变事件
        /// </summary>
        public event StartNodeChangeHandler? StartNodeChanged;
        /// <summary>
        /// 流程运行完毕事件
        /// </summary>
        public event FlowRunCompleteHandler? FlowRunComplete;
        /// <summary>
        /// 被监视的对象数据改变事件
        /// </summary>
        public event MonitorObjectChangeHandler? MonitorObjectChanged;
        /// <summary>
        /// 节点中断状态改变事件
        /// </summary>
        public event NodeInterruptStateChangeHandler? NodeInterruptStateChanged;
        /// <summary>
        /// 表达式中断触发事件
        /// </summary>
        public event ExpInterruptTriggerHandler? InterruptTriggered;
        /// <summary>
        /// 容器对象改变事件
        /// </summary>
        public event IOCMembersChangedHandler? IOCMembersChanged;
        /// <summary>
        /// 节点定位事件
        /// </summary>
        public event NodeLocatedHandler? NodeLocated;
        /// <summary>
        /// 运行环境输出事件
        /// </summary>
        public event EnvOutHandler? EnvOutput;
        /// <summary>
        /// 添加画布事件
        /// </summary>
        public event CanvasCreateHandler CanvasCreated;
        /// <summary>
        /// 移除了画布事件
        /// </summary>
        public event CanvasRemoveHandler CanvasRemoved;

        #endregion

        #region 流程运行环境事件

        private void InitFlowEnvironmentEvent()
        {
            flowEnvironmentEvent.DllLoad += FlowEnvironment_DllLoadEvent;
            flowEnvironmentEvent.ProjectSaving += FlowEnvironment_OnProjectSaving;
            flowEnvironmentEvent.ProjectLoaded += FlowEnvironment_OnProjectLoaded;
            flowEnvironmentEvent.CanvasCreated += FlowEnvironmentEvent_OnCanvasCreate;
            flowEnvironmentEvent.CanvasRemoved += FlowEnvironmentEvent_OnCanvasRemove;
            flowEnvironmentEvent.StartNodeChanged += FlowEnvironment_StartNodeChangeEvent;
            flowEnvironmentEvent.NodeConnectChanged += FlowEnvironment_NodeConnectChangeEvemt;
            flowEnvironmentEvent.NodeCreated += FlowEnvironment_NodeCreateEvent;
            flowEnvironmentEvent.NodeRemoved += FlowEnvironment_NodeRemoveEvent;
            flowEnvironmentEvent.NodePlace += FlowEnvironment_OnNodePlaceEvent;
            flowEnvironmentEvent.NodeTakeOut += FlowEnvironment_OnNodeTakeOutEvent;
            flowEnvironmentEvent.FlowRunComplete += FlowEnvironment_OnFlowRunCompleteEvent;
            
            flowEnvironmentEvent.MonitorObjectChanged += FlowEnvironment_OnMonitorObjectChangeEvent;
            flowEnvironmentEvent.NodeInterruptStateChanged += FlowEnvironment_OnNodeInterruptStateChangeEvent;
            flowEnvironmentEvent.InterruptTriggered += FlowEnvironment_OnInterruptTriggerEvent;
            
            flowEnvironmentEvent.IOCMembersChanged += FlowEnvironment_OnIOCMembersChangedEvent;
            
            flowEnvironmentEvent.NodeLocated += FlowEnvironment_OnNodeLocateEvent;;
            
            flowEnvironmentEvent.EnvOutput += FlowEnvironment_OnEnvOutEvent;
        }

      
        private void ResetFlowEnvironmentEvent()
        {
           flowEnvironmentEvent.DllLoad -= FlowEnvironment_DllLoadEvent;
           flowEnvironmentEvent.ProjectSaving -= FlowEnvironment_OnProjectSaving;
           flowEnvironmentEvent.ProjectLoaded -= FlowEnvironment_OnProjectLoaded;
           flowEnvironmentEvent.StartNodeChanged -= FlowEnvironment_StartNodeChangeEvent;
           flowEnvironmentEvent.NodeConnectChanged -= FlowEnvironment_NodeConnectChangeEvemt;
           flowEnvironmentEvent.NodeCreated -= FlowEnvironment_NodeCreateEvent;
           flowEnvironmentEvent.NodeRemoved -= FlowEnvironment_NodeRemoveEvent;
           flowEnvironmentEvent.NodePlace -= FlowEnvironment_OnNodePlaceEvent;
           flowEnvironmentEvent.NodeTakeOut -= FlowEnvironment_OnNodeTakeOutEvent;
            flowEnvironmentEvent.FlowRunComplete -= FlowEnvironment_OnFlowRunCompleteEvent;


           flowEnvironmentEvent.MonitorObjectChanged -= FlowEnvironment_OnMonitorObjectChangeEvent;
           flowEnvironmentEvent.NodeInterruptStateChanged -= FlowEnvironment_OnNodeInterruptStateChangeEvent;
            flowEnvironmentEvent.InterruptTriggered -= FlowEnvironment_OnInterruptTriggerEvent;

            flowEnvironmentEvent.IOCMembersChanged -= FlowEnvironment_OnIOCMembersChangedEvent;
            flowEnvironmentEvent.NodeLocated -= FlowEnvironment_OnNodeLocateEvent;

            flowEnvironmentEvent.EnvOutput -= FlowEnvironment_OnEnvOutEvent;

        }

        #region 运行环境事件

        /// <summary>
        /// 环境内容输出
        /// </summary>
        /// <param name="type"></param>
        /// <param name="value"></param>
        private void FlowEnvironment_OnEnvOutEvent(InfoType type, string value)
        {

            System.Windows.Application.Current.Dispatcher.Invoke((Action)(() =>
            {
                EnvOutput?.Invoke(type, value);
            }));
        }

        /// <summary>
        /// 需要保存项目
        /// </summary>
        /// <param name="eventArgs"></param>
        /// <exception cref="NotImplementedException"></exception>
        private void FlowEnvironment_OnProjectSaving(ProjectSavingEventArgs eventArgs)
        {
            System.Windows.Application.Current.Dispatcher.Invoke((Action)(() =>
            {
                ProjectSaving?.Invoke(eventArgs);
            }));
        }

        /// <summary>
        /// 加载完成
        /// </summary>
        /// <param name="eventArgs"></param>
        private void FlowEnvironment_OnProjectLoaded(ProjectLoadedEventArgs eventArgs)
        {
           System.Windows.Application.Current.Dispatcher.Invoke((Action)(() =>
           {
               ProjectLoaded?.Invoke(eventArgs);
           }));
        }

        /// <summary>
        /// 运行完成
        /// </summary>
        /// <param name="eventArgs"></param>
        /// <exception cref="NotImplementedException"></exception>
        private void FlowEnvironment_OnFlowRunCompleteEvent(FlowEventArgs eventArgs)
        {
            System.Windows.Application.Current.Dispatcher.Invoke((Action)(() =>
            {
                SereinEnv.WriteLine(InfoType.INFO, "-------运行完成---------\r\n");
                FlowRunComplete?.Invoke(eventArgs);
            }));
        }

        /// <summary>
        /// 加载了DLL文件，dll内容
        /// </summary>
        private void FlowEnvironment_DllLoadEvent(LoadDllEventArgs eventArgs)
        {
            System.Windows.Application.Current.Dispatcher.Invoke((Action)(() =>
            {
                DllLoad?.Invoke(eventArgs);
            }));
        }

        /// <summary>
        /// 节点连接关系变更
        /// </summary>
        /// <param name="eventArgs"></param>
        private void FlowEnvironment_NodeConnectChangeEvemt(NodeConnectChangeEventArgs eventArgs)
        {
            System.Windows.Application.Current.Dispatcher.Invoke((Action)(() =>
            {
                NodeConnectChanged?.Invoke(eventArgs);
            }));
        }


        /// <summary>
        /// 添加了画布
        /// </summary>
        /// <param name="eventArgs"></param>
        /// <exception cref="NotImplementedException"></exception>
        private void FlowEnvironmentEvent_OnCanvasCreate(CanvasCreateEventArgs eventArgs)
        {
            System.Windows.Application.Current.Dispatcher.Invoke((Action)(() =>
            {
                CanvasCreated?.Invoke(eventArgs);
            }));
        }

        /// <summary>
        /// 移除了画布
        /// </summary>
        /// <param name="eventArgs"></param>
        /// <exception cref="NotImplementedException"></exception>
        private void FlowEnvironmentEvent_OnCanvasRemove(CanvasRemoveEventArgs eventArgs)
        {
            System.Windows.Application.Current.Dispatcher.Invoke((Action)(() =>
            {
                CanvasRemoved?.Invoke(eventArgs);
            }));

        }

        /// <summary>
        /// 节点移除事件
        /// </summary>
        /// <param name="eventArgs"></param>
        private void FlowEnvironment_NodeRemoveEvent(NodeRemoveEventArgs eventArgs)
        {
            System.Windows.Application.Current.Dispatcher.Invoke((Action)(() =>
            {
                NodeRemoved?.Invoke(eventArgs);
            }));
        }

        /// <summary>
        /// 添加节点事件
        /// </summary>
        /// <param name="eventArgs">添加节点事件参数</param>
        /// <exception cref="NotImplementedException"></exception>
        private void FlowEnvironment_NodeCreateEvent(NodeCreateEventArgs eventArgs)
        {
            System.Windows.Application.Current.Dispatcher.Invoke((Action)(() =>
            {
                Debug.WriteLine(DateTime.Now, $"Create Node {eventArgs.NodeModel.Guid}");
                NodeCreated?.Invoke(eventArgs);
            }));
        }

        /// <summary>
        /// 放置一个节点
        /// </summary>
        /// <param name="eventArgs"></param>
        /// <exception cref="NotImplementedException"></exception>
        private void FlowEnvironment_OnNodePlaceEvent(NodePlaceEventArgs eventArgs)
        {
            System.Windows.Application.Current.Dispatcher.Invoke((Action)(() =>
            {
                NodePlace?.Invoke(eventArgs);
            }));
        }

        /// <summary>
        /// 取出一个节点
        /// </summary>
        /// <param name="eventArgs"></param>
        private void FlowEnvironment_OnNodeTakeOutEvent(NodeTakeOutEventArgs eventArgs)
        {
            System.Windows.Application.Current.Dispatcher.Invoke((Action)(() =>
            {
                NodeTakeOut?.Invoke(eventArgs);
            }));
        }

        /// <summary>
        /// 设置了流程起始控件
        /// </summary>
        /// <param name="eventArgs"></param>

        private void FlowEnvironment_StartNodeChangeEvent(StartNodeChangeEventArgs eventArgs)
        {
            System.Windows.Application.Current.Dispatcher.Invoke((Action)(() =>
            {
                StartNodeChanged?.Invoke(eventArgs);
            }));
        }

        /// <summary>
        /// 被监视的对象发生改变
        /// </summary>
        /// <param name="eventArgs"></param>
        private void FlowEnvironment_OnMonitorObjectChangeEvent(MonitorObjectEventArgs eventArgs)
        {
            System.Windows.Application.Current.Dispatcher.Invoke((Action)(() =>
            {
                MonitorObjectChanged?.Invoke(eventArgs);
            }));
        }

        /// <summary>
        /// 节点中断状态改变。
        /// </summary>
        /// <param name="eventArgs"></param>
        private void FlowEnvironment_OnNodeInterruptStateChangeEvent(NodeInterruptStateChangeEventArgs eventArgs)
        {
            NodeInterruptStateChanged?.Invoke(eventArgs);
        }

        /// <summary>
        /// 节点触发了中断
        /// </summary>
        /// <param name="eventArgs"></param>
        /// <exception cref="NotImplementedException"></exception>
        private void FlowEnvironment_OnInterruptTriggerEvent(InterruptTriggerEventArgs eventArgs)
        {
            InterruptTriggered?.Invoke(eventArgs);
        }

        /// <summary>
        /// IOC变更
        /// </summary>
        /// <param name="eventArgs"></param>
        /// <exception cref="NotImplementedException"></exception>
        private void FlowEnvironment_OnIOCMembersChangedEvent(IOCMembersChangedEventArgs eventArgs)
        {
            IOCMembersChanged?.Invoke(eventArgs);

        }

        /// <summary>
        /// 节点需要定位
        /// </summary>
        /// <param name="eventArgs"></param>
        /// <exception cref="NotImplementedException"></exception>
        private void FlowEnvironment_OnNodeLocateEvent(NodeLocatedEventArgs eventArgs)
        {
            NodeLocated?.Invoke(eventArgs);
        }







        #endregion


        #endregion

        #region 主动触发运行环境事件
        public void OnDllLoad(LoadDllEventArgs eventArgs)
        {
            throw new NotImplementedException();
        }

        public void OnProjectLoaded(ProjectLoadedEventArgs eventArgs)
        {
            throw new NotImplementedException();
        }

        public void OnProjectSaving(ProjectSavingEventArgs eventArgs)
        {
            throw new NotImplementedException();
        }

        public void OnNodeConnectChanged(NodeConnectChangeEventArgs eventArgs)
        {
            throw new NotImplementedException();
        }

        public void OnCanvasCreated(CanvasCreateEventArgs eventArgs)
        {
            throw new NotImplementedException();
        }

        public void OnCanvasRemoved(CanvasRemoveEventArgs eventArgs)
        {
            throw new NotImplementedException();
        }

        public void OnNodeCreated(NodeCreateEventArgs eventArgs)
        {
            throw new NotImplementedException();
        }

        public void OnNodeRemoved(NodeRemoveEventArgs eventArgs)
        {
            throw new NotImplementedException();
        }

        public void OnNodePlace(NodePlaceEventArgs eventArgs)
        {
            throw new NotImplementedException();
        }

        public void OnNodeTakeOut(NodeTakeOutEventArgs eventArgs)
        {
            throw new NotImplementedException();
        }

        public void OnStartNodeChanged(StartNodeChangeEventArgs eventArgs)
        {
            throw new NotImplementedException();
        }

        public void OnFlowRunComplete(FlowEventArgs eventArgs)
        {
            throw new NotImplementedException();
        }

        public void OnMonitorObjectChanged(MonitorObjectEventArgs eventArgs)
        {
            throw new NotImplementedException();
        }

        public void OnNodeInterruptStateChanged(NodeInterruptStateChangeEventArgs eventArgs)
        {
            throw new NotImplementedException();
        }

        public void OnInterruptTriggered(InterruptTriggerEventArgs eventArgs)
        {
            throw new NotImplementedException();
        }

        public void OnIOCMembersChanged(IOCMembersChangedEventArgs eventArgs)
        {
            throw new NotImplementedException();
        }

        public void OnNodeLocated(NodeLocatedEventArgs eventArgs)
        {
            throw new NotImplementedException();
        }

        public void OnEnvOutput(InfoType type, string value)
        {
            throw new NotImplementedException();
        }
        #endregion
    }
}
