using Avalonia.Controls;
using Avalonia.Media;
using Avalonia;
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

namespace Serein.Workbench.Avalonia.Services
{



    internal class FlowEEForwardingService : IFlowEEForwardingService
    {
        /// <summary>
        /// 流程运行环境
        /// </summary>
        private readonly IFlowEnvironment flowEnvironment;
        private readonly IFlowEnvironmentEvent flowEnvironmentEvent;

        /// <summary>
        /// 转发流程运行环境各个事件的实现类
        /// </summary>
        /// <param name="flowEnvironment"></param>
        /// <param name="flowNodeControlService"></param>
        public FlowEEForwardingService(IFlowEnvironment flowEnvironment,
                                       IFlowEnvironmentEvent flowEnvironmentEvent)
        {
            this.flowEnvironment = flowEnvironment;
            this.flowEnvironmentEvent = flowEnvironmentEvent;
            InitFlowEnvironmentEvent();
        }

        #region 工作台事件转发
        /// <summary>
        /// 加载了依赖文件事件
        /// </summary>
        public event LoadDllHandler? OnDllLoad;
        /// <summary>
        /// 项目加载完成事件
        /// </summary>
        public event ProjectLoadedHandler? OnProjectLoaded;
        /// <summary>
        /// 项目保存中事件
        /// </summary>
        public event ProjectSavingHandler? OnProjectSaving;
        /// <summary>
        /// 节点连接改变事件
        /// </summary>
        public event NodeConnectChangeHandler? OnNodeConnectChange;
        /// <summary>
        /// 节点创建事件
        /// </summary>
        public event NodeCreateHandler? OnNodeCreate;
        /// <summary>
        /// 节点移除事件
        /// </summary>
        public event NodeRemoveHandler? OnNodeRemove;
        /// <summary>
        /// 节点放置容器事件
        /// </summary>
        public event NodePlaceHandler? OnNodePlace;
        /// <summary>
        /// 节点取出事件
        /// </summary>
        public event NodeTakeOutHandler? OnNodeTakeOut;
        /// <summary>
        /// 流程起始节点改变事件
        /// </summary>
        public event StartNodeChangeHandler? OnStartNodeChange;
        /// <summary>
        /// 流程运行完毕事件
        /// </summary>
        public event FlowRunCompleteHandler? OnFlowRunComplete;
        /// <summary>
        /// 被监视的对象数据改变事件
        /// </summary>
        public event MonitorObjectChangeHandler? OnMonitorObjectChange;
        /// <summary>
        /// 节点中断状态改变事件
        /// </summary>
        public event NodeInterruptStateChangeHandler? OnNodeInterruptStateChange;
        /// <summary>
        /// 表达式中断触发事件
        /// </summary>
        public event ExpInterruptTriggerHandler? OnInterruptTrigger;
        /// <summary>
        /// 容器对象改变事件
        /// </summary>
        public event IOCMembersChangedHandler? OnIOCMembersChanged;
        /// <summary>
        /// 节点定位事件
        /// </summary>
        public event NodeLocatedHandler? OnNodeLocated;
        /// <summary>
        /// 节点移动事件
        /// </summary>
        public event NodeMovedHandler? OnNodeMoved;
        /// <summary>
        /// 运行环境输出事件
        /// </summary>
        public event EnvOutHandler? OnEnvOut;

        #endregion

        #region 流程运行环境事件

        private void InitFlowEnvironmentEvent()
        {
            flowEnvironmentEvent.OnDllLoad += FlowEnvironment_DllLoadEvent;
            flowEnvironmentEvent.OnProjectSaving += EnvDecorator_OnProjectSaving;
            flowEnvironmentEvent.OnProjectLoaded += FlowEnvironment_OnProjectLoaded;
            flowEnvironmentEvent.OnStartNodeChange += FlowEnvironment_StartNodeChangeEvent;
            flowEnvironmentEvent.OnNodeConnectChange += FlowEnvironment_NodeConnectChangeEvemt;
            flowEnvironmentEvent.OnNodeCreate += FlowEnvironment_NodeCreateEvent;
            flowEnvironmentEvent.OnNodeRemove += FlowEnvironment_NodeRemoveEvent;
            flowEnvironmentEvent.OnNodePlace += EnvDecorator_OnNodePlaceEvent;
            flowEnvironmentEvent.OnNodeTakeOut += EnvDecorator_OnNodeTakeOutEvent;
            flowEnvironmentEvent.OnFlowRunComplete += FlowEnvironment_OnFlowRunCompleteEvent;
            
            flowEnvironmentEvent.OnMonitorObjectChange += FlowEnvironment_OnMonitorObjectChangeEvent;
            flowEnvironmentEvent.OnNodeInterruptStateChange += FlowEnvironment_OnNodeInterruptStateChangeEvent;
            flowEnvironmentEvent.OnInterruptTrigger += FlowEnvironment_OnInterruptTriggerEvent;
            
            flowEnvironmentEvent.OnIOCMembersChanged += FlowEnvironment_OnIOCMembersChangedEvent;
            
            flowEnvironmentEvent.OnNodeLocated += FlowEnvironment_OnNodeLocateEvent;
            flowEnvironmentEvent.OnNodeMoved += FlowEnvironment_OnNodeMovedEvent;
            
            flowEnvironmentEvent.OnEnvOut += FlowEnvironment_OnEnvOutEvent;
        }

        private void ResetFlowEnvironmentEvent()
        {
           flowEnvironmentEvent.OnDllLoad -= FlowEnvironment_DllLoadEvent;
           flowEnvironmentEvent.OnProjectSaving -= EnvDecorator_OnProjectSaving;
           flowEnvironmentEvent.OnProjectLoaded -= FlowEnvironment_OnProjectLoaded;
           flowEnvironmentEvent.OnStartNodeChange -= FlowEnvironment_StartNodeChangeEvent;
           flowEnvironmentEvent.OnNodeConnectChange -= FlowEnvironment_NodeConnectChangeEvemt;
           flowEnvironmentEvent.OnNodeCreate -= FlowEnvironment_NodeCreateEvent;
           flowEnvironmentEvent.OnNodeRemove -= FlowEnvironment_NodeRemoveEvent;
           flowEnvironmentEvent.OnNodePlace -= EnvDecorator_OnNodePlaceEvent;
           flowEnvironmentEvent.OnNodeTakeOut -= EnvDecorator_OnNodeTakeOutEvent;
            flowEnvironmentEvent.OnFlowRunComplete -= FlowEnvironment_OnFlowRunCompleteEvent;


           flowEnvironmentEvent.OnMonitorObjectChange -= FlowEnvironment_OnMonitorObjectChangeEvent;
           flowEnvironmentEvent.OnNodeInterruptStateChange -= FlowEnvironment_OnNodeInterruptStateChangeEvent;
            flowEnvironmentEvent.OnInterruptTrigger -= FlowEnvironment_OnInterruptTriggerEvent;

            flowEnvironmentEvent.OnIOCMembersChanged -= FlowEnvironment_OnIOCMembersChangedEvent;
            flowEnvironmentEvent.OnNodeLocated -= FlowEnvironment_OnNodeLocateEvent;
            flowEnvironmentEvent.OnNodeMoved -= FlowEnvironment_OnNodeMovedEvent;

            flowEnvironmentEvent.OnEnvOut -= FlowEnvironment_OnEnvOutEvent;

        }

        #region 运行环境事件

        /// <summary>
        /// 环境内容输出
        /// </summary>
        /// <param name="type"></param>
        /// <param name="value"></param>
        private void FlowEnvironment_OnEnvOutEvent(InfoType type, string value)
        {
            //LogOutWindow.AppendText($"{DateTime.Now} [{type}] : {value}{Environment.NewLine}");
        }

        /// <summary>
        /// 需要保存项目
        /// </summary>
        /// <param name="eventArgs"></param>
        /// <exception cref="NotImplementedException"></exception>
        private void EnvDecorator_OnProjectSaving(ProjectSavingEventArgs eventArgs)
        {
            OnProjectSaving?.Invoke(eventArgs);
        }

        /// <summary>
        /// 加载完成
        /// </summary>
        /// <param name="eventArgs"></param>
        private void FlowEnvironment_OnProjectLoaded(ProjectLoadedEventArgs eventArgs)
        {
            OnProjectLoaded?.Invoke(eventArgs);
        }

        /// <summary>
        /// 运行完成
        /// </summary>
        /// <param name="eventArgs"></param>
        /// <exception cref="NotImplementedException"></exception>
        private void FlowEnvironment_OnFlowRunCompleteEvent(FlowEventArgs eventArgs)
        {
            SereinEnv.WriteLine(InfoType.INFO, "-------运行完成---------\r\n");
            OnFlowRunComplete?.Invoke(eventArgs);
        }

        /// <summary>
        /// 加载了DLL文件，dll内容
        /// </summary>
        private void FlowEnvironment_DllLoadEvent(LoadDllEventArgs eventArgs)
        {
            OnDllLoad?.Invoke(eventArgs);
        }

        /// <summary>
        /// 节点连接关系变更
        /// </summary>
        /// <param name="eventArgs"></param>
        private void FlowEnvironment_NodeConnectChangeEvemt(NodeConnectChangeEventArgs eventArgs)
        {
            OnNodeConnectChange?.Invoke(eventArgs);
        }

        /// <summary>
        /// 节点移除事件
        /// </summary>
        /// <param name="eventArgs"></param>
        private void FlowEnvironment_NodeRemoveEvent(NodeRemoveEventArgs eventArgs)
        {
            OnNodeRemove?.Invoke(eventArgs);
        }

        /// <summary>
        /// 添加节点事件
        /// </summary>
        /// <param name="eventArgs">添加节点事件参数</param>
        /// <exception cref="NotImplementedException"></exception>
        private void FlowEnvironment_NodeCreateEvent(NodeCreateEventArgs eventArgs)
        {
            OnNodeCreate?.Invoke(eventArgs);
        }

        /// <summary>
        /// 放置一个节点
        /// </summary>
        /// <param name="eventArgs"></param>
        /// <exception cref="NotImplementedException"></exception>
        private void EnvDecorator_OnNodePlaceEvent(NodePlaceEventArgs eventArgs)
        {
            OnNodePlace?.Invoke(eventArgs);
        }

        /// <summary>
        /// 取出一个节点
        /// </summary>
        /// <param name="eventArgs"></param>
        private void EnvDecorator_OnNodeTakeOutEvent(NodeTakeOutEventArgs eventArgs)
        {
            OnNodeTakeOut?.Invoke(eventArgs);

        }

        /// <summary>
        /// 设置了流程起始控件
        /// </summary>
        /// <param name="oldNodeGuid"></param>
        /// <param name="newNodeGuid"></param>
        private void FlowEnvironment_StartNodeChangeEvent(StartNodeChangeEventArgs eventArgs)
        {

            OnStartNodeChange?.Invoke(eventArgs);
        }

        /// <summary>
        /// 被监视的对象发生改变
        /// </summary>
        /// <param name="eventArgs"></param>
        private void FlowEnvironment_OnMonitorObjectChangeEvent(MonitorObjectEventArgs eventArgs)
        {
            OnMonitorObjectChange?.Invoke(eventArgs);
        }

        /// <summary>
        /// 节点中断状态改变。
        /// </summary>
        /// <param name="eventArgs"></param>
        private void FlowEnvironment_OnNodeInterruptStateChangeEvent(NodeInterruptStateChangeEventArgs eventArgs)
        {
            OnNodeInterruptStateChange?.Invoke(eventArgs);
        }

        /// <summary>
        /// 节点触发了中断
        /// </summary>
        /// <param name="eventArgs"></param>
        /// <exception cref="NotImplementedException"></exception>
        private void FlowEnvironment_OnInterruptTriggerEvent(InterruptTriggerEventArgs eventArgs)
        {
            OnInterruptTrigger?.Invoke(eventArgs);
        }

        /// <summary>
        /// IOC变更
        /// </summary>
        /// <param name="eventArgs"></param>
        /// <exception cref="NotImplementedException"></exception>
        private void FlowEnvironment_OnIOCMembersChangedEvent(IOCMembersChangedEventArgs eventArgs)
        {
            OnIOCMembersChanged?.Invoke(eventArgs);

        }

        /// <summary>
        /// 节点需要定位
        /// </summary>
        /// <param name="eventArgs"></param>
        /// <exception cref="NotImplementedException"></exception>
        private void FlowEnvironment_OnNodeLocateEvent(NodeLocatedEventArgs eventArgs)
        {
            OnNodeLocated?.Invoke(eventArgs);
        }

       
        /// <summary>
        /// 节点移动
        /// </summary>
        /// <param name="eventArgs"></param>
        private void FlowEnvironment_OnNodeMovedEvent(NodeMovedEventArgs eventArgs)
        {
            OnNodeMoved?.Invoke(eventArgs);
        }


        #endregion


        #endregion


    }
}
