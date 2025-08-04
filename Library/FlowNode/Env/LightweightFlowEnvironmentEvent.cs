using Serein.Library.Api;

namespace Serein.Library
{
    /// <summary>
    /// 轻量级流程环境事件实现
    /// </summary>
    public class LightweightFlowEnvironmentEvent : IFlowEnvironmentEvent
    {
        /// <inheritdoc/>
        public event LoadDllHandler DllLoad;
        /// <inheritdoc/>
        public event ProjectLoadedHandler ProjectLoaded;
        /// <inheritdoc/>
        public event ProjectSavingHandler ProjectSaving;
        /// <inheritdoc/>
        public event NodeConnectChangeHandler NodeConnectChanged;
        /// <inheritdoc/>
        public event CanvasCreateHandler CanvasCreated;
        /// <inheritdoc/>
        public event CanvasRemoveHandler CanvasRemoved;
        /// <inheritdoc/>
        public event NodeCreateHandler NodeCreated;
        /// <inheritdoc/>
        public event NodeRemoveHandler NodeRemoved;
        /// <inheritdoc/>
        public event NodePlaceHandler NodePlace;
        /// <inheritdoc/>
        public event NodeTakeOutHandler NodeTakeOut;
        /// <inheritdoc/>
        public event StartNodeChangeHandler StartNodeChanged;
        /// <inheritdoc/>
        public event FlowRunCompleteHandler FlowRunComplete;
        /// <inheritdoc/>
        public event MonitorObjectChangeHandler MonitorObjectChanged;
        /// <inheritdoc/>
        public event NodeInterruptStateChangeHandler NodeInterruptStateChanged;
        /// <inheritdoc/>
        public event ExpInterruptTriggerHandler InterruptTriggered;
        /// <inheritdoc/>
        public event IOCMembersChangedHandler IOCMembersChanged;
        /// <inheritdoc/>
        public event NodeLocatedHandler NodeLocated;
        /// <inheritdoc/>
        public event EnvOutHandler EnvOutput;
        /// <inheritdoc/>
        public void OnDllLoad(LoadDllEventArgs eventArgs)
        {
            DllLoad?.Invoke(eventArgs);
        }
        /// <inheritdoc/>
        public void OnProjectLoaded(ProjectLoadedEventArgs eventArgs)
        {
            ProjectLoaded?.Invoke(eventArgs);
        }
        /// <inheritdoc/>
        public void OnProjectSaving(ProjectSavingEventArgs eventArgs)
        {
            ProjectSaving?.Invoke(eventArgs);
        }
        /// <inheritdoc/>
        public void OnNodeConnectChanged(NodeConnectChangeEventArgs eventArgs)
        {
            NodeConnectChanged?.Invoke(eventArgs);
        }
        /// <inheritdoc/>
        public void OnCanvasCreated(CanvasCreateEventArgs eventArgs)
        {
            CanvasCreated?.Invoke(eventArgs);
        }
        /// <inheritdoc/>
        public void OnCanvasRemoved(CanvasRemoveEventArgs eventArgs)
        {
            CanvasRemoved?.Invoke(eventArgs);
        }
        /// <inheritdoc/>
        public void OnNodeCreated(NodeCreateEventArgs eventArgs)
        {
            NodeCreated?.Invoke(eventArgs);
        }
        /// <inheritdoc/>
        public void OnNodeRemoved(NodeRemoveEventArgs eventArgs)
        {
            NodeRemoved?.Invoke(eventArgs);
        }
        /// <inheritdoc/>
        public void OnNodePlace(NodePlaceEventArgs eventArgs)
        {
            NodePlace?.Invoke(eventArgs);
        }
        /// <inheritdoc/>
        public void OnNodeTakeOut(NodeTakeOutEventArgs eventArgs)
        {
            NodeTakeOut?.Invoke(eventArgs);
        }
        /// <inheritdoc/>
        public void OnStartNodeChanged(StartNodeChangeEventArgs eventArgs)
        {
            StartNodeChanged?.Invoke(eventArgs);
        }
        /// <inheritdoc/>
        public void OnFlowRunComplete(FlowEventArgs eventArgs)
        {
            FlowRunComplete?.Invoke(eventArgs);
        }
        /// <inheritdoc/>
        public void OnMonitorObjectChanged(MonitorObjectEventArgs eventArgs)
        {
            MonitorObjectChanged?.Invoke(eventArgs);
        }
        /// <inheritdoc/>
        public void OnNodeInterruptStateChanged(NodeInterruptStateChangeEventArgs eventArgs)
        {
            NodeInterruptStateChanged?.Invoke(eventArgs);
        }
        /// <inheritdoc/>
        public void OnInterruptTriggered(InterruptTriggerEventArgs eventArgs)
        {
            InterruptTriggered?.Invoke(eventArgs);
        }
        /// <inheritdoc/>
        public void OnIOCMembersChanged(IOCMembersChangedEventArgs eventArgs)
        {
            IOCMembersChanged?.Invoke(eventArgs);
        }
        /// <inheritdoc/>
        public void OnNodeLocated(NodeLocatedEventArgs eventArgs)
        {
            NodeLocated?.Invoke(eventArgs);
        }
        /// <inheritdoc/>
        public void OnEnvOutput(InfoType type, string value)
        {
            EnvOutput?.Invoke(type, value);
        }

    }
}
