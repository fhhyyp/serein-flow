import { computed, onBeforeUnmount, ref, watch, type Ref } from 'vue';
import { FlowApiError, continueFlowDebugSession, getFlowDebugSession, startFlowDebugSession, stepFlowDebugSession, stopFlowDebugSession, subscribeFlowRunEvents, type FlowDebugSessionDto, type FlowDefinitionDto, type FlowRunEventDto, } from '../api/flowApi';
import { t, locale } from '../i18n';
import { parseRuntimeLog } from '../flow/runtimeLog';
import { clearStoredDebugSessionId, loadBreakpointNodeIds, loadStoredDebugSessionId, normalizeBreakpointNodeIds, saveBreakpointNodeIds, saveStoredDebugSessionId, } from '../flow/debugBreakpoints';
import { buildNodeExecutionStates } from '../flow/nodeExecutionState';
import type { CanvasState, NodeStatus } from '../flow/types';
import type { RunEvent } from './useFlowRunner';
export interface DebugPauseBoundary {
  nodeId: string;
  nodeType?: string;
  step?: number;
  inputs?: unknown;
  frameDepth?: number;
  triggerInvocationId?: string;
  executionId?: string;
}
interface UseFlowDebuggerOptions {
  canvases: Ref<CanvasState[]>;
  projectId: Ref<string | undefined>;
  flowId: Ref<string | undefined>;
  flowVersion: Ref<number>;
  getCurrentDefinition: () => FlowDefinitionDto | undefined;
  isWorkspaceLoading: Ref<boolean>;
  isNormalRunActive: Ref<boolean>;
  notice: Ref<string>;
  onPauseNode?: (nodeId: string, executionId?: string) => void;
}
const terminalSessionStatuses = new Set<FlowDebugSessionDto['status']>(['completed', 'cancelled', 'failed']);
const terminalRunEventTypes = new Set(['run.completed', 'run.failed', 'run.cancelled', 'run.timed_out', 'run.interrupted']);
export function useFlowDebugger(options: UseFlowDebuggerOptions) {
  const breakpointNodeIds = ref<string[]>([]);
  const debugSession = ref<FlowDebugSessionDto>();
  const pauseBoundary = ref<DebugPauseBoundary>();
  const runEvents = ref<RunEvent[]>([]);
  const rawRunEvents = ref<FlowRunEventDto[]>([]);
  const runPayload = ref('');
  const isStarting = ref(false);
  const isControlling = ref(false);
  const isStopping = ref(false);
  const isRestoring = ref(false);
  let unsubscribe: (() => void) | undefined;
  let pollTimer: ReturnType<typeof setTimeout> | undefined;
  let pollInFlight = false;
  let lastEventSequence = 0;
  let scopeRevision = 0;
  const currentNodeIds = computed(() => {
    const flatMapResult: Iterable<string> | null | undefined = options.canvases.value.flatMap((canvas) => {
      const mapResult: string | readonly string[] = canvas.nodes.map((node) => node.id);
      return mapResult;
    });
    const setInstance: Set<string> = new Set(flatMapResult);
    return setInstance;
  });
  const isSessionForCurrentFlow = computed(() => {
    const session = debugSession.value;
    const booleanResult: boolean = Boolean(session
      && session.projectId === options.projectId.value
      && session.flowId === options.flowId.value);
    return booleanResult;
  });
  const isDebugActive = computed(() => {
    const evaluatedValue: boolean = isSessionForCurrentFlow.value
      && debugSession.value !== undefined
      && !terminalSessionStatuses.has(debugSession.value.status);
    return evaluatedValue;
  });
  const isDebugPaused = computed(() => isDebugActive.value && !isStopping.value && debugSession.value?.status === 'paused');
  const isDebugVisible = computed(() => isSessionForCurrentFlow.value && debugSession.value !== undefined);
  const canStartDebug = computed(() => {
    const booleanResult1: boolean = Boolean(options.projectId.value
      && options.flowId.value
      && currentNodeIds.value.size > 0
      && !options.isWorkspaceLoading.value
      && !options.isNormalRunActive.value
      && !isDebugActive.value
      && !isStarting.value);
    return booleanResult1;
  });
  const executionStates = computed(() => {
    const buildNodeExecutionStatesResult: import("../flow/nodeExecutionState").NodeExecutionState[] = buildNodeExecutionStates(rawRunEvents.value);
    return buildNodeExecutionStatesResult;
  });
  function stopPolling(): void {
    if (pollTimer !== undefined) {
      clearTimeout(pollTimer);
      pollTimer = undefined;
    }
  }
  function closeSubscription(): void {
    unsubscribe?.();
    unsubscribe = undefined;
    stopPolling();
  }
  function setNodeStatus(nodeId: string, status: NodeStatus): void {
    options.canvases.value = options.canvases.value.map((canvas) => {
      const evaluatedValue1: {
        nodes: import("../flow/types").FlowNode[];
        id: string;
        nameKey: string;
        name?: string;
        lifecycle: import("../flow/types").CanvasLifecycle;
        edges: import("../flow/types").FlowEdge[];
        selectedNodeId?: string;
        selectedEdgeId?: string;
      } = ({
        ...canvas,
        nodes: canvas.nodes.map((node) => node.id === nodeId
          ? { ...node, data: { ...node.data, status } }
          : node),
      });
      return evaluatedValue1;
    });
  }
  function resetNodeStatuses(): void {
    options.canvases.value = options.canvases.value.map((canvas) => {
      const evaluatedValue2: {
        nodes: {
          data: {
            status: "idle";
            kind: import("../flow/types").NodeKind;
            titleKey: string;
            subtitleKey: string;
            displayName?: string;
            description?: string;
            hasDataOutput: boolean;
            parameters: import("../flow/types").MethodParameter[];
            script?: import("../flow/types").ScriptNodeData;
            runtime?: import("../flow/types").NodeRuntimeMetadata;
            breakpoint?: boolean;
            debugPaused?: boolean;
            breakpointLocked?: boolean;
            onToggleBreakpoint?: (nodeId: string) => void;
          };
          id: string;
          position: import("@vue-flow/core").XYPosition;
          width?: number;
          selected?: boolean;
          class?: string | string[];
          type: "workflow";
        }[];
        id: string;
        nameKey: string;
        name?: string;
        lifecycle: import("../flow/types").CanvasLifecycle;
        edges: import("../flow/types").FlowEdge[];
        selectedNodeId?: string;
        selectedEdgeId?: string;
      } = ({
        ...canvas,
        nodes: canvas.nodes.map((node) => ({ ...node, data: { ...node.data, status: 'idle' } })),
      });
      return evaluatedValue2;
    });
  }
  function persistBreakpoints(): void {
    saveBreakpointNodeIds(options.projectId.value, options.flowId.value, breakpointNodeIds.value);
  }
  function pruneBreakpoints(): void {
    const normalized = normalizeBreakpointNodeIds(breakpointNodeIds.value, currentNodeIds.value);
    if (normalized.length === breakpointNodeIds.value.length
      && normalized.every((value, index) => value === breakpointNodeIds.value[index])) {
      return;
    }
    breakpointNodeIds.value = normalized;
    persistBreakpoints();
  }
  function toggleBreakpoint(nodeId: string): void {
    if (!currentNodeIds.value.has(nodeId)) {
      return;
    }
    if (isDebugActive.value) {
      options.notice.value = t('debug.breakpointsLocked');
      return;
    }
    if (!options.projectId.value || !options.flowId.value) {
      options.notice.value = t('debug.saveBeforeStart');
      return;
    }
    const next = new Set(breakpointNodeIds.value);
    if (next.has(nodeId)) {
      next.delete(nodeId);
    }
    else {
      next.add(nodeId);
    }
    breakpointNodeIds.value = normalizeBreakpointNodeIds([...next], currentNodeIds.value);
    persistBreakpoints();
  }
  function clearRuntimeState(): void {
    pauseBoundary.value = undefined;
    runEvents.value = [];
    rawRunEvents.value = [];
    runPayload.value = '';
    lastEventSequence = 0;
  }
  function toRunEvent(event: FlowRunEventDto): RunEvent {
    const log = parseRuntimeLog(event.type, event.payloadJson);
    const success = event.type === 'node.completed' || event.type === 'run.completed';
    const isError = event.type === 'node.error'
      || event.type === 'run.failed'
      || event.type === 'run.timed_out'
      || event.type === 'run.interrupted'
      || event.type === 'debug.trigger.failed'
      || event.type === 'debug.trigger.rejected';
    const resolveToLocaleTimeStringResult = () => {
      const dateInstance = new Date(event.timestamp);
      const toLocaleTimeStringResult: string = dateInstance.toLocaleTimeString(locale.value === 'zh-CN' ? 'zh-CN' : 'en-US', { hour12: false });
      return toLocaleTimeStringResult;
    };
    const evaluatedValue3: RunEvent = {
      time: resolveToLocaleTimeStringResult(),
      label: event.type,
      detail: event.payloadJson,
      success,
      status: log?.level === 'error'
        ? 'error'
        : event.type === 'node.started'
          ? 'running'
          : success
            ? 'success'
            : isError
              ? 'error'
              : event.type === 'node.failed' || event.type === 'run.cancelled'
                ? 'failed'
                : 'idle',
      sequence: event.sequence,
      nodeId: event.nodeId ?? undefined,
      logLevel: log?.level,
      message: log?.message,
    };
    return evaluatedValue3;
  }
  function parsePauseBoundary(event: FlowRunEventDto): DebugPauseBoundary | undefined {
    try {
      const value: unknown = JSON.parse(event.payloadJson);
      if (!isRecord(value) || typeof value.nodeId !== 'string' || !value.nodeId.trim()) {
        return undefined;
      }
      return {
        nodeId: value.nodeId,
        nodeType: typeof value.nodeType === 'string' ? value.nodeType : undefined,
        step: typeof value.step === 'number' ? value.step : undefined,
        inputs: value.inputs,
        frameDepth: typeof value.frameDepth === 'number' ? value.frameDepth : undefined,
        triggerInvocationId: typeof value.triggerInvocationId === 'string' ? value.triggerInvocationId : undefined,
        executionId: typeof value.executionId === 'string' ? value.executionId : undefined,
      };
    }
    catch {
      return undefined;
    }
  }
  function handleEvent(event: FlowRunEventDto): void {
    const session = debugSession.value;
    if (!session || event.runId !== session.runId || event.sequence <= lastEventSequence) {
      return;
    }
    lastEventSequence = event.sequence;
    runEvents.value = [...runEvents.value, toRunEvent(event)];
    rawRunEvents.value = [...rawRunEvents.value, event];
    runPayload.value = JSON.stringify(event, null, 2);
    if (event.nodeId && (event.type === 'node.started' || event.type === 'node.completed' || event.type === 'node.failed' || event.type === 'node.error')) {
      setNodeStatus(event.nodeId, event.type === 'node.started' ? 'running' : event.type === 'node.completed' ? 'success' : event.type === 'node.failed' ? 'failed' : 'error');
    }
    if (event.type === 'debug.paused') {
      const boundary = parsePauseBoundary(event);
      if (boundary) {
        pauseBoundary.value = boundary;
        options.onPauseNode?.(boundary.nodeId, boundary.executionId);
      }
      void refreshSession(session.id);
      return;
    }
    if (event.type.startsWith('debug.trigger.')) {
      void refreshSession(session.id);
      return;
    }
    if (terminalRunEventTypes.has(event.type)) {
      void refreshSession(session.id);
    }
  }
  function beginEventSubscription(session: FlowDebugSessionDto): void {
    closeSubscription();
    lastEventSequence = 0;
    unsubscribe = subscribeFlowRunEvents(session.runId, handleEvent, () => {
      options.notice.value = t('debug.streamFailed');
    });
    scheduleSessionPoll(session.id);
  }
  function scheduleSessionPoll(sessionId: string): void {
    stopPolling();
    if (!isDebugActive.value) {
      return;
    }
    pollTimer = setTimeout(() => {
      void pollSession(sessionId);
    }, 700);
  }
  async function pollSession(sessionId: string): Promise<void> {
    if (pollInFlight || debugSession.value?.id !== sessionId || !isDebugActive.value) {
      return;
    }
    pollInFlight = true;
    try {
      await refreshSession(sessionId);
    }
    finally {
      pollInFlight = false;
      if (debugSession.value?.id === sessionId && isDebugActive.value) {
        scheduleSessionPoll(sessionId);
      }
    }
  }
  async function refreshSession(sessionId: string): Promise<void> {
    try {
      const session = await getFlowDebugSession(sessionId);
      if (debugSession.value?.id !== sessionId) {
        return;
      }
      debugSession.value = session;
      pauseBoundary.value = pauseBoundaryFromSession(session);
      if (terminalSessionStatuses.has(session.status)) {
        isStopping.value = false;
        clearStoredDebugSessionId(session.projectId, session.flowId);
        stopPolling();
      }
    }
    catch (error) {
      if (error instanceof FlowApiError && error.status === 404 && debugSession.value?.id === sessionId) {
        clearStoredDebugSessionId(options.projectId.value, options.flowId.value);
        debugSession.value = undefined;
        closeSubscription();
      }
    }
  }
  async function restoreCurrentScope(): Promise<void> {
    const revision = ++scopeRevision;
    closeSubscription();
    debugSession.value = undefined;
    isStopping.value = false;
    clearRuntimeState();
    breakpointNodeIds.value = loadBreakpointNodeIds(options.projectId.value, options.flowId.value, currentNodeIds.value);
    pruneBreakpoints();
    const sessionId = loadStoredDebugSessionId(options.projectId.value, options.flowId.value);
    if (!sessionId) {
      return;
    }
    isRestoring.value = true;
    try {
      const session = await getFlowDebugSession(sessionId);
      if (revision !== scopeRevision
        || session.projectId !== options.projectId.value
        || session.flowId !== options.flowId.value) {
        return;
      }
      debugSession.value = session;
      pauseBoundary.value = pauseBoundaryFromSession(session);
      if (terminalSessionStatuses.has(session.status)) {
        clearStoredDebugSessionId(session.projectId, session.flowId);
        return;
      }
      beginEventSubscription(session);
    }
    catch {
      if (revision === scopeRevision) {
        clearStoredDebugSessionId(options.projectId.value, options.flowId.value);
      }
    }
    finally {
      if (revision === scopeRevision) {
        isRestoring.value = false;
      }
    }
  }
  async function startDebug(): Promise<void> {
    if (!canStartDebug.value) {
      options.notice.value = t('debug.cannotStart');
      return;
    }
    const projectId = options.projectId.value;
    const flowId = options.flowId.value;
    if (!projectId || !flowId) {
      return;
    }
    isStarting.value = true;
    isStopping.value = false;
    clearRuntimeState();
    resetNodeStatuses();
    try {
      const argumentValue: string = projectId;
      const argumentValue1: string = flowId;
      const evaluatedValue4: import("../api/flowApi").StartFlowDebugSessionRequestDto = {
        breakpointNodeIds: breakpointNodeIds.value,
        expectedFlowVersion: options.flowVersion.value,
        definition: options.getCurrentDefinition(),
      };
      const session = await startFlowDebugSession(argumentValue, argumentValue1, evaluatedValue4);
      debugSession.value = session;
      saveStoredDebugSessionId(projectId, flowId, session.id);
      beginEventSubscription(session);
      options.notice.value = t('debug.started');
    }
    catch (error) {
      options.notice.value = error instanceof FlowApiError && error.status === 409
        ? t('debug.startConflict')
        : t('debug.startFailed');
    }
    finally {
      isStarting.value = false;
    }
  }
  async function sendCommand(command: 'continue' | 'step' | 'stop'): Promise<void> {
    const session = debugSession.value;
    if (!session || !isSessionForCurrentFlow.value || isControlling.value) {
      return;
    }
    if (command !== 'stop' && session.status !== 'paused') {
      options.notice.value = t('debug.notPaused');
      return;
    }
    if (terminalSessionStatuses.has(session.status)) {
      return;
    }
    isControlling.value = true;
    if (command === 'stop') {
      isStopping.value = true;
    }
    const sequence = session.lastCommandSequence + 1;
    try {
      if (command === 'continue') {
        await continueFlowDebugSession(session.id, sequence);
      }
      else if (command === 'step') {
        await stepFlowDebugSession(session.id, sequence);
      }
      else {
        await stopFlowDebugSession(session.id, sequence);
      }
      debugSession.value = {
        ...session,
        lastCommandSequence: sequence,
        status: command === 'stop' ? session.status : 'running',
        currentNodeId: command === 'stop' ? session.currentNodeId : undefined,
      };
      if (command !== 'stop') {
        pauseBoundary.value = undefined;
      }
      await refreshSession(session.id);
    }
    catch (error) {
      await refreshSession(session.id);
      if (command === 'stop' && !terminalSessionStatuses.has(debugSession.value?.status ?? 'failed')) {
        isStopping.value = false;
      }
      options.notice.value = error instanceof FlowApiError && error.status === 409
        ? t('debug.commandConflict')
        : t('debug.commandFailed');
    }
    finally {
      isControlling.value = false;
    }
  }
  function isBreakpoint(nodeId: string): boolean {
    const includesResult: boolean = breakpointNodeIds.value.includes(nodeId);
    return includesResult;
  }
  watch([() => options.projectId.value, () => options.flowId.value], () => {
    void restoreCurrentScope();
  }, { immediate: true });
  watch(currentNodeIds, () => {
    if (breakpointNodeIds.value.length === 0) {
      breakpointNodeIds.value = loadBreakpointNodeIds(options.projectId.value, options.flowId.value, currentNodeIds.value);
    }
    pruneBreakpoints();
  });
  onBeforeUnmount(closeSubscription);
  return {
    breakpointNodeIds,
    debugSession,
    pauseBoundary,
    runEvents,
    executionStates,
    runPayload,
    isStarting,
    isControlling,
    isStopping,
    isRestoring,
    isDebugActive,
    isDebugPaused,
    isDebugVisible,
    canStartDebug,
    isBreakpoint,
    toggleBreakpoint,
    startDebug,
    continueDebug: () => {
      const sendCommandResult = sendCommand('continue');
      return sendCommandResult;
    },
    stepDebug: () => {
      const sendCommandResult1 = sendCommand('step');
      return sendCommandResult1;
    },
    stopDebug: () => {
      const sendCommandResult2 = sendCommand('stop');
      return sendCommandResult2;
    },
  };
}
function isRecord(value: unknown): value is Record<string, unknown> {
  return typeof value === 'object' && value !== null;
}
function pauseBoundaryFromSession(session: FlowDebugSessionDto): DebugPauseBoundary | undefined {
  const pauseState = session.status === 'paused' ? session.pauseState : undefined;
  if (!pauseState) {
    return undefined;
  }
  return {
    nodeId: pauseState.nodeId,
    nodeType: pauseState.nodeType,
    step: pauseState.step,
    inputs: pauseState.inputs,
    frameDepth: pauseState.frameDepth,
    triggerInvocationId: pauseState.invocationId,
    executionId: pauseState.executionId,
  };
}
