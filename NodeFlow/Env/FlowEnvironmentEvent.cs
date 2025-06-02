using Serein.Library;
using Serein.Library.Api;

namespace Serein.NodeFlow.Env
{
    public class FlowEnvironmentEvent : IFlowEnvironmentEvent
    {
        public event LoadDllHandler DllLoad;
        public event ProjectLoadedHandler ProjectLoaded;
        public event ProjectSavingHandler ProjectSaving;
        public event NodeConnectChangeHandler NodeConnectChanged;
        public event CanvasCreateHandler CanvasCreated;
        public event CanvasRemoveHandler CanvasRemoved;
        public event NodeCreateHandler NodeCreated;
        public event NodeRemoveHandler NodeRemoved;
        public event NodePlaceHandler NodePlace;
        public event NodeTakeOutHandler NodeTakeOut;
        public event StartNodeChangeHandler StartNodeChanged;
        public event FlowRunCompleteHandler FlowRunComplete;
        public event MonitorObjectChangeHandler MonitorObjectChanged;
        public event NodeInterruptStateChangeHandler NodeInterruptStateChanged;
        public event ExpInterruptTriggerHandler InterruptTriggered;
        public event IOCMembersChangedHandler IOCMembersChanged;
        public event NodeLocatedHandler NodeLocated;
        public event EnvOutHandler EnvOutput;

        public void OnDllLoad(LoadDllEventArgs eventArgs)
        {
            DllLoad.Invoke(eventArgs);
        }

        public void OnProjectLoaded(ProjectLoadedEventArgs eventArgs)
        {
            ProjectLoaded.Invoke(eventArgs);
        }

        public void OnProjectSaving(ProjectSavingEventArgs eventArgs)
        {
            ProjectSaving.Invoke(eventArgs);
        }

        public void OnNodeConnectChanged(NodeConnectChangeEventArgs eventArgs)
        {
            NodeConnectChanged.Invoke(eventArgs);
        }

        public void OnCanvasCreated(CanvasCreateEventArgs eventArgs)
        {
            CanvasCreated.Invoke(eventArgs);
        }

        public void OnCanvasRemoved(CanvasRemoveEventArgs eventArgs)
        {
            CanvasRemoved.Invoke(eventArgs);
        }

        public void OnNodeCreated(NodeCreateEventArgs eventArgs)
        {
            NodeCreated.Invoke(eventArgs);
        }

        public void OnNodeRemoved(NodeRemoveEventArgs eventArgs)
        {
            NodeRemoved.Invoke(eventArgs);
        }

        public void OnNodePlace(NodePlaceEventArgs eventArgs)
        {
            NodePlace.Invoke(eventArgs);
        }

        public void OnNodeTakeOut(NodeTakeOutEventArgs eventArgs)
        {
            NodeTakeOut.Invoke(eventArgs);
        }

        public void OnStartNodeChanged(StartNodeChangeEventArgs eventArgs)
        {
            StartNodeChanged.Invoke(eventArgs);
        }

        public void OnFlowRunComplete(FlowEventArgs eventArgs)
        {
            FlowRunComplete.Invoke(eventArgs);
        }

        public void OnMonitorObjectChanged(MonitorObjectEventArgs eventArgs)
        {
            MonitorObjectChanged.Invoke(eventArgs);
        }

        public void OnNodeInterruptStateChanged(NodeInterruptStateChangeEventArgs eventArgs)
        {
            NodeInterruptStateChanged.Invoke(eventArgs);
        }

        public void OnInterruptTriggered(InterruptTriggerEventArgs eventArgs)
        {
            InterruptTriggered.Invoke(eventArgs);
        }

        public void OnIOCMembersChanged(IOCMembersChangedEventArgs eventArgs)
        {
            IOCMembersChanged.Invoke(eventArgs);
        }

        public void OnNodeLocated(NodeLocatedEventArgs eventArgs)
        {
            NodeLocated.Invoke(eventArgs);
        }

        public void OnEnvOutput(InfoType type, string value)
        {
            EnvOutput.Invoke(type, value);
        }


    }


}
