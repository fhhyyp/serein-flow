import { authenticatedFetch } from './securityApi';
import type { FlowEdgeLineType } from '../flow/types';
import { localizeMessage } from '../i18n';
const hubRecordSeparator = '\u001e';
const hubJsonProtocol = 'json';
const hubProtocolVersion = 1;
const hubInvocationMessageType = 1;
const subscribeRunMethod = 'Subscribe';
const subscribeProjectMethod = 'SubscribeProject';
const subscribeGlobalMethod = 'SubscribeGlobal';
const runEventTarget = 'runEvent';
const workspaceChangeTarget = 'workspaceChanged';
export type ApiNodeType = 'action' | 'flowCall' | 'flipflop' | 'script';
export type ApiCanvasLifecycle = 'main' | 'init' | 'loading' | 'exit' | 'custom';
export type ApiConnectionKind = 'execution' | 'data';
export type ApiDataSource = 'literal' | 'previousNode' | 'projectInput' | 'expression';
export type FlowConcurrencyMode = 'parallel' | 'exclusiveReject';
export type FlowVersionTrack = 'development' | 'production';
export type FlowVersionOperation = 'created' | 'saved' | 'published' | 'rolledBack' | 'libraryUpgraded' | 'imported';
export interface FlowConnectionLineTypesDto {
  execution?: FlowEdgeLineType;
  data?: FlowEdgeLineType;
}
export interface FlowCanvasFocusSettingsDto {
  enabled?: boolean;
  parameterSources?: boolean;
  parameterConsumers?: boolean;
  callers?: boolean;
  callees?: boolean;
}
export interface FlowUiMetadataDto {
  connectionLineTypes?: FlowConnectionLineTypesDto;
  canvasFocusSettings?: FlowCanvasFocusSettingsDto;
}
export interface FlowRunPolicyDto {
  concurrencyMode: FlowConcurrencyMode;
}
export interface NodePortDto {
  id: string;
  name: string;
  direction: 'input' | 'output';
  required: boolean;
}
export interface NodeParameterUiMetadataDto {
  id: string;
  nameKey: string;
  valueKind: string;
  type?: string;
  description?: string;
  inputMode?: 'connection' | 'manual' | 'select';
  literalValue?: string;
  projectInputKey?: string;
  expression?: string;
  sourceNodeId?: string;
  sourcePortId?: string;
  isVariadic?: boolean;
  variadicGroupId?: string;
  elementType?: string;
  variadicMode?: 'expanded' | 'collection';
  enumMetadata?: EnumParameterMetadataDto;
}
export interface EnumValueOptionDto {
  name: string;
  numericValue: string;
}
export interface EnumParameterMetadataDto {
  typeName: string;
  isFlags: boolean;
  underlyingType: string;
  options: EnumValueOptionDto[];
}
export interface NodeParameterDto {
  name: string;
  valueJson?: string;
  source: ApiDataSource;
  required: boolean;
  ui?: NodeParameterUiMetadataDto;
}
export interface NodeUiMetadataDto {
  kind: string;
  titleKey: string;
  subtitleKey: string;
  description?: string;
  status: string;
  hasDataOutput: boolean;
  width?: number;
  category?: 'method' | 'basic';
  libraryId?: string;
  flowLibraryName?: string;
  className?: string;
  methodName?: string;
  dllName?: string;
  dllVersion?: string;
  returnType?: string;
  targetNodeId?: string;
  targetFlowId?: string;
  isAwaitable?: boolean;
  staticReturnType?: string;
  isDynamicReturnType?: boolean;
  targetCanvasId?: string;
  isPublic?: boolean;
  flowCallParameterBindings?: FlowCallParameterBindingDto[];
  libraryNodeContractId?: string;
}
export interface NodeDto {
  id: string;
  type: ApiNodeType;
  displayName: string;
  x: number;
  y: number;
  ports: NodePortDto[];
  parameters: NodeParameterDto[];
  script: ScriptNodeDataDto | null;
  ui?: NodeUiMetadataDto;
}
export interface ScriptValueContractDto {
  name: string;
  valueKind: string;
  required: boolean;
  id?: string;
  description?: string;
}
export interface ScriptNodeDataDto {
  nodeId: string;
  source: string;
  languageVersion: string;
  sourceHash: string;
  inputs: ScriptValueContractDto[];
  outputs: ScriptValueContractDto[];
}
export interface FlowCallParameterBindingDto {
  callParameterId: string;
  targetParameterId: string;
}
export interface NodeCreationDescriptorDto {
  id: string;
  type: ApiNodeType;
  displayName: string;
  description: string | null;
  ui: NodeUiMetadataDto;
  script?: ScriptNodeDataDto | null;
  parameters?: NodeParameterDto[] | null;
}
export interface BuiltinNodeCatalogDto {
  nodes: NodeCreationDescriptorDto[];
}
export interface ConnectionDto {
  id: string;
  fromNodeId: string;
  fromPortId: string;
  toNodeId: string;
  toPortId: string;
  kind: ApiConnectionKind;
  branch?: 'success' | 'failure' | 'error';
  dataSource?: ApiDataSource;
  priority: number;
}
export interface CanvasDto {
  id: string;
  lifecycle: ApiCanvasLifecycle;
  nodes: NodeDto[];
  connections: ConnectionDto[];
  name?: string;
}
export interface FlowDefinitionDto {
  id: string;
  schemaVersion: number;
  version: number;
  canvases: CanvasDto[];
  entryNodeId: string;
  checksum: string;
  runPolicy?: FlowRunPolicyDto;
  ui?: FlowUiMetadataDto;
}
export interface ProjectDto {
  id: string;
  name: string;
  version: number;
  status: string;
  createdAt: string;
  updatedAt: string;
}
export interface FlowDefinitionSummaryDto {
  id: string;
  version: number;
  entryNodeId: string;
  canvasCount?: number;
  nodeCount?: number;
  productionVersion?: number;
}
export interface ProjectWorkspaceDto {
  project: ProjectDto;
  flows: FlowDefinitionSummaryDto[];
}
export interface CreateProjectRequestDto {
  name: string;
  definition: FlowDefinitionDto;
}
export interface RenameProjectRequestDto {
  name: string;
  expectedVersion: number;
}
export interface UpdateFlowDefinitionRequestDto {
  expectedVersion: number;
  definition: FlowDefinitionDto;
}
export interface FlowVersionSummaryDto {
  flowId: string;
  version: number;
  track: FlowVersionTrack;
  operation: FlowVersionOperation;
  parentVersion?: number;
  sourceVersion?: number;
  remark: string;
  createdAt?: string;
  isCurrent: boolean;
}
export interface FlowVersionDetailDto {
  version: FlowVersionSummaryDto;
  definition: FlowDefinitionDto;
}
export interface PublishFlowVersionRequestDto {
  expectedDevelopmentVersion: number;
  remark?: string;
}
export interface RollbackFlowVersionRequestDto {
  track: FlowVersionTrack;
  expectedHeadVersion: number;
}
export interface RunFlowRequestDto {
  expectedFlowVersion?: number;
  projectInputs?: Record<string, unknown>;
  timeoutSeconds?: number;
  maxSteps?: number;
  maxNodeVisits?: number;
}
export interface FlowRunDto {
  id: string;
  flowId: string;
  flowVersion: number;
  status: 'pending' | 'running' | 'succeeded' | 'failed' | 'cancelled' | 'timedOut' | 'interrupted';
  startedAt?: string;
  endedAt?: string;
  errorSummary?: string;
  projectId?: string;
  createdAt?: string;
  cancellationReason?: string;
  concurrencyMode?: FlowConcurrencyMode;
  isListenerRun?: boolean;
  queuedAt?: string;
  executionKind?: 'production' | 'debug';
  debugSessionId?: string;
  definitionChecksum?: string;
}
export type FlowDebugSessionStatus = 'pending' | 'running' | 'paused' | 'completed' | 'cancelled' | 'failed';
export interface StartFlowDebugSessionRequestDto {
  breakpointNodeIds?: string[];
  projectInputs?: Record<string, unknown>;
  timeoutSeconds?: number;
  maxSteps?: number;
  maxNodeVisits?: number;
  expectedFlowVersion?: number;
  maxQueuedFlipflopTriggers?: number;
  definition?: FlowDefinitionDto;
}
export interface FlowDebugSessionDto {
  id: string;
  runId: string;
  projectId: string;
  flowId: string;
  status: FlowDebugSessionStatus;
  breakpointNodeIds: string[];
  currentNodeId?: string;
  activeInvocationId?: string;
  activeFlipflopNodeId?: string;
  queuedTriggerCount: number;
  lastCommandSequence: number;
  failureMessage?: string;
  createdAt: string;
  updatedAt: string;
  stateRevision?: number;
  pauseState?: FlowDebugPauseStateDto;
  lastNodeResult?: FlowDebugNodeResultDto;
}
export interface FlowDebugPauseStateDto {
  nodeId: string;
  nodeType: string;
  step: number;
  frameDepth: number;
  invocationId?: string;
  boundarySequence: number;
  inputs: unknown;
  pausedAt: string;
  executionId?: string;
}
export interface FlowDebugNodeResultDto {
  nodeId: string;
  sequence: number;
  completedAt: string;
  outcome: string;
  branch?: string;
  inputs: unknown;
  outputs: unknown;
  errorCode?: string;
  errorMessage?: string;
}
export interface FlowDebugWaitResultDto {
  hasChanged: boolean;
  timedOut: boolean;
  session: FlowDebugSessionDto;
}
export interface FlowRunOverviewDto {
  queueCapacity: number;
  queuedCount: number;
  activeRunCount: number;
  activeListenerRunCount: number;
  maxConcurrentRuns: number;
  maxConcurrentListenerRuns: number;
  maxConcurrentRunsPerProject: number;
  queuedRuns: FlowRunDto[];
  activeRuns: FlowRunDto[];
  recentRuns: FlowRunDto[];
}
export interface FlowRunEventDto {
  runId: string;
  sequence: number;
  timestamp: string;
  type: string;
  nodeId?: string;
  payloadJson: string;
}
export interface WorkspaceChangeEventDto {
  eventId: string;
  occurredAt: string;
  changeType: 'flow.changed' | 'project.library.changed' | 'library.catalog.changed' | string;
  projectId?: string | null;
  flowId?: string | null;
  version?: number | null;
  checksum?: string | null;
  origin: 'web' | 'mcp' | 'system' | string;
  operation: string;
  canvasIds?: string[] | null;
  nodeIds?: string[] | null;
  connectionIds?: string[] | null;
  parameterIds?: string[] | null;
  libraryIds?: string[] | null;
}
export interface FlowRunOutputDto {
  runId: string;
  sequence: number;
  timestamp: string;
  nodeId: string;
  outcome: 'completed' | 'failed' | 'error';
  branch?: string;
  inputs: unknown;
  outputs: unknown;
  errorCode?: string;
  errorMessage?: string;
}
export interface FlowWorkpieceDto {
  runId: string;
  id: string;
  kind: 'Image' | 'File' | string;
  name: string;
  contentType: string;
  length: number;
  createdAt: string;
  downloadUrl: string;
  nodeId?: string;
  executionId?: string;
}
export interface RunExecutionSettingsDto {
  queueCapacity: number;
  maxConcurrentRuns: number;
  maxConcurrentListenerRuns: number;
  maxConcurrentRunsPerProject: number;
  queueWaitTimeoutSeconds: number;
  shutdownGracePeriodSeconds: number;
  synchronousInvocationTimeoutSeconds: number;
  maxLibraryUploadBytes: number;
}
export type FlowInvocationMode = 'asynchronous' | 'synchronous';
export interface FlowInterfaceDto {
  id: string;
  projectId: string;
  flowId: string;
  name: string;
  invocationMode: FlowInvocationMode;
  isEnabled: boolean;
  createdAt: string;
  updatedAt: string;
  productionVersion?: number;
}
export interface CreateFlowInterfaceRequestDto {
  projectId: string;
  flowId: string;
  name: string;
  invocationMode: FlowInvocationMode;
  isEnabled: boolean;
}
export interface UpdateFlowInterfaceRequestDto {
  name: string;
  invocationMode: FlowInvocationMode;
  isEnabled: boolean;
}
export interface FlowValidationDiagnostic {
  code: string;
  message: string;
  path?: string | null;
}
interface ApiProblem {
  title?: string;
  detail?: string;
  currentVersion?: number;
  diagnostics?: FlowValidationDiagnostic[];
}
export class FlowApiError extends Error {
  public readonly status: number;
  public readonly currentVersion?: number;
  public readonly diagnostics: FlowValidationDiagnostic[];
  public constructor(status: number, message: string, currentVersion?: number, diagnostics: FlowValidationDiagnostic[] = []) {
    super(message);
    this.name = 'FlowApiError';
    this.status = status;
    this.currentVersion = currentVersion;
    this.diagnostics = diagnostics;
  }
}
const apiBaseUrl = (import.meta.env.VITE_API_BASE_URL ?? '').replace(/\/$/, '');
export async function listProjects(includeArchived = false): Promise<ProjectWorkspaceDto[]> {
  const suffix = includeArchived ? '?includeArchived=true' : '';
  const requestResult: ProjectWorkspaceDto[] | PromiseLike<ProjectWorkspaceDto[]> = request<ProjectWorkspaceDto[]>(`/api/projects${suffix}`);
  return requestResult;
}
export async function getBuiltinNodeCatalog(): Promise<BuiltinNodeCatalogDto> {
  const requestResult1: BuiltinNodeCatalogDto | PromiseLike<BuiltinNodeCatalogDto> = request<BuiltinNodeCatalogDto>('/api/node-catalog/builtins');
  return requestResult1;
}
export async function createProject(requestBody: CreateProjectRequestDto): Promise<ProjectWorkspaceDto> {
  const requestResult2: ProjectWorkspaceDto | PromiseLike<ProjectWorkspaceDto> = request<ProjectWorkspaceDto>('/api/projects', { method: 'POST', body: requestBody });
  return requestResult2;
}
export async function renameProject(projectId: string, requestBody: RenameProjectRequestDto): Promise<ProjectWorkspaceDto> {
  const requestResult3: ProjectWorkspaceDto | PromiseLike<ProjectWorkspaceDto> = request<ProjectWorkspaceDto>(`/api/projects/${projectId}`, { method: 'PUT', body: requestBody });
  return requestResult3;
}
export async function archiveProject(projectId: string): Promise<ProjectDto> {
  const requestResult4: ProjectDto | PromiseLike<ProjectDto> = request<ProjectDto>(`/api/projects/${projectId}/archive`, { method: 'POST' });
  return requestResult4;
}
export async function loadFlow(projectId: string, flowId: string): Promise<FlowDefinitionDto> {
  const requestResult5: FlowDefinitionDto | PromiseLike<FlowDefinitionDto> = request<FlowDefinitionDto>(`/api/projects/${projectId}/flows/${flowId}`);
  return requestResult5;
}
export async function saveFlow(projectId: string, flowId: string, requestBody: UpdateFlowDefinitionRequestDto): Promise<FlowDefinitionDto> {
  const requestResult6: FlowDefinitionDto | PromiseLike<FlowDefinitionDto> = request<FlowDefinitionDto>(`/api/projects/${projectId}/flows/${flowId}`, { method: 'PUT', body: requestBody });
  return requestResult6;
}
export async function listFlowVersions(projectId: string, flowId: string, track: FlowVersionTrack): Promise<FlowVersionSummaryDto[]> {
  const requestResult7: FlowVersionSummaryDto[] | PromiseLike<FlowVersionSummaryDto[]> = request<FlowVersionSummaryDto[]>(`/api/projects/${projectId}/flows/${flowId}/versions?track=${track}`);
  return requestResult7;
}
export async function getFlowVersion(projectId: string, flowId: string, version: number): Promise<FlowVersionDetailDto> {
  const requestResult8: FlowVersionDetailDto | PromiseLike<FlowVersionDetailDto> = request<FlowVersionDetailDto>(`/api/projects/${projectId}/flows/${flowId}/versions/${version}`);
  return requestResult8;
}
export async function publishFlowVersion(projectId: string, flowId: string, requestBody: PublishFlowVersionRequestDto): Promise<FlowVersionSummaryDto> {
  const requestResult9: FlowVersionSummaryDto | PromiseLike<FlowVersionSummaryDto> = request<FlowVersionSummaryDto>(`/api/projects/${projectId}/flows/${flowId}/publish`, { method: 'POST', body: requestBody });
  return requestResult9;
}
export async function rollbackFlowVersion(projectId: string, flowId: string, version: number, requestBody: RollbackFlowVersionRequestDto): Promise<FlowVersionSummaryDto> {
  const requestResult10: FlowVersionSummaryDto | PromiseLike<FlowVersionSummaryDto> = request<FlowVersionSummaryDto>(`/api/projects/${projectId}/flows/${flowId}/versions/${version}/rollback`, { method: 'POST', body: requestBody });
  return requestResult10;
}
export async function startFlowRun(projectId: string, flowId: string, requestBody: RunFlowRequestDto = {}): Promise<FlowRunDto> {
  const requestResult11: FlowRunDto | PromiseLike<FlowRunDto> = request<FlowRunDto>(`/api/projects/${projectId}/flows/${flowId}/runs`, { method: 'POST', body: requestBody });
  return requestResult11;
}
export async function startFlowDebugSession(projectId: string, flowId: string, requestBody: StartFlowDebugSessionRequestDto): Promise<FlowDebugSessionDto> {
  const requestResult12: FlowDebugSessionDto | PromiseLike<FlowDebugSessionDto> = request<FlowDebugSessionDto>(`/api/projects/${projectId}/flows/${flowId}/debug-sessions`, { method: 'POST', body: requestBody });
  return requestResult12;
}
export async function getFlowDebugSession(sessionId: string): Promise<FlowDebugSessionDto> {
  const requestResult13: FlowDebugSessionDto | PromiseLike<FlowDebugSessionDto> = request<FlowDebugSessionDto>(`/api/debug-sessions/${sessionId}`);
  return requestResult13;
}
export async function getDebugSessionForRun(runId: string): Promise<FlowDebugSessionDto> {
  const requestResult14: FlowDebugSessionDto | PromiseLike<FlowDebugSessionDto> = request<FlowDebugSessionDto>(`/api/runs/${runId}/debug-session`);
  return requestResult14;
}
export async function waitForFlowDebugSession(sessionId: string, afterRevision: number, timeoutSeconds = 15): Promise<FlowDebugWaitResultDto> {
  const evaluatedValue: string | string[][] | Record<string, string> | URLSearchParams | undefined = {
    afterRevision: String(afterRevision),
    timeoutSeconds: String(timeoutSeconds),
  };
  const query = new URLSearchParams(evaluatedValue);
  const evaluatedValue1: string = `/api/debug-sessions/${sessionId}/wait?${query.toString()}`;
  const requestResult15: FlowDebugWaitResultDto | PromiseLike<FlowDebugWaitResultDto> = request<FlowDebugWaitResultDto>(evaluatedValue1);
  return requestResult15;
}
export async function continueFlowDebugSession(sessionId: string, commandSequence: number): Promise<void> {
  await request<unknown>(`/api/debug-sessions/${sessionId}/continue`, { method: 'POST', body: { commandSequence } });
}
export async function stepFlowDebugSession(sessionId: string, commandSequence: number): Promise<void> {
  await request<unknown>(`/api/debug-sessions/${sessionId}/step`, { method: 'POST', body: { commandSequence } });
}
export async function stopFlowDebugSession(sessionId: string, commandSequence: number): Promise<void> {
  await request<unknown>(`/api/debug-sessions/${sessionId}/stop`, { method: 'POST', body: { commandSequence } });
}
export async function getFlowRun(runId: string): Promise<FlowRunDto> {
  const requestResult16: FlowRunDto | PromiseLike<FlowRunDto> = request<FlowRunDto>(`/api/runs/${runId}`);
  return requestResult16;
}
export interface RunVariableDto {
  name: string;
  clrType: string | null;
  valueAvailable: boolean;
  value: unknown;
  valueJson?: string | null;
  unavailableReason?: string | null;
}
export interface RunVariableSnapshotDto {
  runId: string;
  variables: RunVariableDto[];
  totalCount: number;
  offset: number;
  hasMore: boolean;
  capturedAt: string;
  isFinal?: boolean;
  isTruncated?: boolean;
}
export async function getFlowRunVariables(runId: string, offset = 0, take = 100, signal?: AbortSignal): Promise<RunVariableSnapshotDto> {
  const evaluatedValue2: string = `/api/runs/${encodeURIComponent(runId)}/variables?offset=${offset}&take=${take}`;
  const requestResult17: RunVariableSnapshotDto | PromiseLike<RunVariableSnapshotDto> = request<RunVariableSnapshotDto>(evaluatedValue2, { signal });
  return requestResult17;
}
export async function listFlowRuns(options: {
  statuses?: FlowRunDto['status'][];
  projectId?: string;
  take?: number;
} = {}): Promise<FlowRunDto[]> {
  const query = new URLSearchParams();
  if (options.statuses?.length) {
    const joinResult: string = options.statuses.join(',');
    query.set('status', joinResult);
  }
  if (options.projectId) {
    query.set('projectId', options.projectId);
  }
  if (options.take) {
    const stringResult: string = String(options.take);
    query.set('take', stringResult);
  }
  const suffix = query.size > 0 ? `?${query.toString()}` : '';
  const requestResult18: FlowRunDto[] | PromiseLike<FlowRunDto[]> = request<FlowRunDto[]>(`/api/runs${suffix}`);
  return requestResult18;
}
export async function getFlowRunOverview(): Promise<FlowRunOverviewDto> {
  const requestResult19: FlowRunOverviewDto | PromiseLike<FlowRunOverviewDto> = request<FlowRunOverviewDto>('/api/runs/overview');
  return requestResult19;
}
export async function cancelFlowRun(runId: string): Promise<void> {
  await request<unknown>(`/api/runs/${runId}/cancel`, { method: 'POST' });
}
export async function interruptFlowRun(runId: string): Promise<FlowRunDto> {
  const requestResult20: FlowRunDto | PromiseLike<FlowRunDto> = request<FlowRunDto>(`/api/runs/${runId}/interrupt`, { method: 'POST' });
  return requestResult20;
}
export async function getFlowRunSnapshot(runId: string): Promise<FlowDefinitionDto> {
  const requestResult21: FlowDefinitionDto | PromiseLike<FlowDefinitionDto> = request<FlowDefinitionDto>(`/api/runs/${runId}/snapshot`);
  return requestResult21;
}
export async function listFlowRunOutputs(runId: string): Promise<FlowRunOutputDto[]> {
  const requestResult22: FlowRunOutputDto[] | PromiseLike<FlowRunOutputDto[]> = request<FlowRunOutputDto[]>(`/api/runs/${runId}/outputs`);
  return requestResult22;
}
export async function listFlowRunWorkpieces(runId: string): Promise<FlowWorkpieceDto[]> {
  const requestResult23: FlowWorkpieceDto[] | PromiseLike<FlowWorkpieceDto[]> = request<FlowWorkpieceDto[]>(`/api/runs/${runId}/workpieces`);
  return requestResult23;
}
export function getFlowWorkpieceUrl(runId: string, workpieceId: string, download = false): string {
  const query = download ? '?download=true' : '';
  const evaluatedValue3: string = `${apiBaseUrl}/api/runs/${encodeURIComponent(runId)}/workpieces/${encodeURIComponent(workpieceId)}${query}`;
  return evaluatedValue3;
}
export async function getRunExecutionSettings(): Promise<RunExecutionSettingsDto> {
  const requestResult24: RunExecutionSettingsDto | PromiseLike<RunExecutionSettingsDto> = request<RunExecutionSettingsDto>('/api/environment/settings');
  return requestResult24;
}
export async function saveRunExecutionSettings(settings: RunExecutionSettingsDto): Promise<RunExecutionSettingsDto> {
  const requestResult25: RunExecutionSettingsDto | PromiseLike<RunExecutionSettingsDto> = request<RunExecutionSettingsDto>('/api/environment/settings', { method: 'PUT', body: settings });
  return requestResult25;
}
export async function listFlowInterfaces(): Promise<FlowInterfaceDto[]> {
  const requestResult26: FlowInterfaceDto[] | PromiseLike<FlowInterfaceDto[]> = request<FlowInterfaceDto[]>('/api/environment/interfaces');
  return requestResult26;
}
export async function createFlowInterface(requestBody: CreateFlowInterfaceRequestDto): Promise<FlowInterfaceDto> {
  const requestResult27: FlowInterfaceDto | PromiseLike<FlowInterfaceDto> = request<FlowInterfaceDto>('/api/environment/interfaces', { method: 'POST', body: requestBody });
  return requestResult27;
}
export async function updateFlowInterface(interfaceId: string, requestBody: UpdateFlowInterfaceRequestDto): Promise<FlowInterfaceDto> {
  const requestResult28: FlowInterfaceDto | PromiseLike<FlowInterfaceDto> = request<FlowInterfaceDto>(`/api/environment/interfaces/${interfaceId}`, { method: 'PUT', body: requestBody });
  return requestResult28;
}
export async function deleteFlowInterface(interfaceId: string): Promise<void> {
  await request<unknown>(`/api/environment/interfaces/${interfaceId}`, { method: 'DELETE' });
}
export async function listFlowRunEvents(runId: string, afterSequence = 0): Promise<FlowRunEventDto[]> {
  const requestResult29: FlowRunEventDto[] | PromiseLike<FlowRunEventDto[]> = request<FlowRunEventDto[]>(`/api/runs/${runId}/events?afterSequence=${afterSequence}`);
  return requestResult29;
}
export function subscribeFlowRunEvents(runId: string, onEvent: (event: FlowRunEventDto) => void, onError?: () => void): () => void {
  let closed = false;
  let fallbackStarted = false;
  let socket: WebSocket | undefined;
  let sseCleanup: (() => void) | undefined;
  let lastSequence = 0;
  let replayInFlight = false;
  const deliver = (event: FlowRunEventDto) => {
    if (event.sequence <= lastSequence) {
      return;
    }
    lastSequence = event.sequence;
    onEvent(event);
  };
  const replay = async () => {
    if (closed || replayInFlight) {
      return;
    }
    replayInFlight = true;
    try {
      const events = await listFlowRunEvents(runId, lastSequence);
      events.forEach(deliver);
    }
    catch {
      onError?.();
    }
    finally {
      replayInFlight = false;
    }
  };
  const startSseFallback = () => {
    if (closed || fallbackStarted) {
      return;
    }
    fallbackStarted = true;
    const source = new EventSource(`${apiBaseUrl}/api/runs/${runId}/events/stream`, { withCredentials: true });
    const eventTypes = ['run.started', 'node.started', 'node.completed', 'node.failed', 'node.error', 'run.completed', 'run.failed', 'run.cancelled', 'run.timed_out', 'run.interrupted', 'log'];
    const listeners = eventTypes.map((type) => {
      const listener = (event: Event) => {
        try {
          const evaluatedValue4: FlowRunEventDto = JSON.parse((event as MessageEvent).data) as FlowRunEventDto;
          deliver(evaluatedValue4);
        }
        catch {
          onError?.();
        }
      };
      source.addEventListener(type, listener);
      return [type, listener] as const;
    });
    // EventSource reconnects natively, but the API may have committed events
    // while the connection was down. Replay from the last sequence on every
    // reconnect/open event to close that gap.
    // EventSource 原生支持重连，但断线期间 API 仍可能提交事件；每次连接建立时从最后序号开始回放，补齐缺口。
    source.onopen = () => {
      void replay();
    };
    source.onerror = () => {
      onError?.();
      // Keep the EventSource alive so its built-in retry can recover. A
      // replay is harmless when the stream is still unavailable and will be
      // de-duplicated by sequence when it comes back.
      // 保持 EventSource 存活，以便内置重试恢复连接；流尚不可用时回放不会产生副作用，恢复后按序号去重。
      void replay();
    };
    sseCleanup = () => {
      listeners.forEach((socketListener) => {
        const type = socketListener[0];
        const listener = socketListener[1];
        const removeEventListenerResult: void = source.removeEventListener(type, listener);
        return removeEventListenerResult;
      });
      source.close();
    };
  };
  const startSignalR = () => {
    if (typeof WebSocket === 'undefined') {
      startSseFallback();
      return;
    }
    const configured = apiBaseUrl || window.location.origin;
    const base = new URL(configured);
    base.protocol = base.protocol === 'https:' ? 'wss:' : 'ws:';
    base.pathname = `${base.pathname.replace(/\/$/, '')}/hubs/runs`;
    base.search = '';
    let buffer = '';
    let handshaken = false;
    let failed = false;
    const failover = () => {
      if (failed || closed) {
        return;
      }
      failed = true;
      try {
        socket?.close();
      }
      catch { /* ignore close failures */
      }
      startSseFallback();
    };
    try {
      const toStringResult: string | URL = base.toString();
      socket = new WebSocket(toStringResult);
      socket.onopen = () => {
        {
          const activeSocket1 = socket;
          if (activeSocket1) {
            const hubMessage1 = JSON.stringify({ protocol: hubJsonProtocol, version: hubProtocolVersion }) + hubRecordSeparator;
            activeSocket1.send(hubMessage1);
          }
        }
      };
      socket.onmessage = (message) => {
        buffer += typeof message.data === 'string' ? message.data : '';
        const records = buffer.split(hubRecordSeparator);
        buffer = records.pop() ?? '';
        for (const record of records) {
          if (!record) {
            continue;
          }
          let payload: any;
          try {
            payload = JSON.parse(record);
          }
          catch {
            failover();
            return;
          }
          if (!handshaken) {
            if (payload.error) {
              failover();
              return;
            }
            handshaken = true;
            {
              const activeSocket2 = socket;
              if (activeSocket2) {
                const hubMessage2 = JSON.stringify({ type: hubInvocationMessageType, invocationId: `subscribe-${runId}`, target: subscribeRunMethod, arguments: [runId] }) + hubRecordSeparator;
                activeSocket2.send(hubMessage2);
              }
            }
            void replay();
            continue;
          }
          if (payload.type === hubInvocationMessageType && payload.target === runEventTarget && payload.arguments?.[0]) {
            deliver(payload.arguments[0] as FlowRunEventDto);
          }
        }
      };
      socket.onerror = failover;
      socket.onclose = () => {
        if (!closed) {
          failover();
        }
      };
    }
    catch {
      failover();
    }
  };
  // Replay persisted events before subscribing to the live hub. Sequence
  // de-duplication closes the small gap between the replay query and the hub
  // handshake.
  // 订阅实时 Hub 前先回放持久化事件，再按序号去重，衔接回放查询与 Hub 握手之间的间隙。
  void replay();
  startSignalR();
  return () => {
    closed = true;
    try {
      socket?.close();
    }
    catch { /* ignore close failures */
    }
    sseCleanup?.();
  };
}
export function subscribeWorkspaceChanges(projectId: string, onEvent: (event: WorkspaceChangeEventDto) => void, onConnected?: () => void, onError?: () => void): () => void {
  let closed = false;
  let socket: WebSocket | undefined;
  let reconnectTimer: ReturnType<typeof setTimeout> | undefined;
  let reconnectAttempt = 0;
  const clearReconnectTimer = () => {
    if (reconnectTimer !== undefined) {
      clearTimeout(reconnectTimer);
      reconnectTimer = undefined;
    }
  };
  const scheduleReconnect = () => {
    if (closed || reconnectTimer !== undefined) {
      return;
    }
    const evaluatedValue5: number = 500 * 2 ** Math.min(reconnectAttempt, 4);
    const delay = Math.min(5000, evaluatedValue5);
    reconnectAttempt += 1;
    reconnectTimer = setTimeout(() => {
      reconnectTimer = undefined;
      connect();
    }, delay);
  };
  const fail = () => {
    if (closed) {
      return;
    }
    onError?.();
    try {
      socket?.close();
    }
    catch { /* ignore close failures */
    }
    scheduleReconnect();
  };
  const connect = () => {
    if (closed || socket) {
      return;
    }
    if (typeof WebSocket === 'undefined') {
      onError?.();
      scheduleReconnect();
      return;
    }
    const configured = apiBaseUrl || window.location.origin;
    const base = new URL(configured);
    base.protocol = base.protocol === 'https:' ? 'wss:' : 'ws:';
    base.pathname = `${base.pathname.replace(/\/$/, '')}/hubs/workspace`;
    base.search = '';
    let buffer = '';
    let handshaken = false;
    let failed = false;
    try {
      const toStringResult1: string | URL = base.toString();
      socket = new WebSocket(toStringResult1);
      socket.onopen = () => {
        reconnectAttempt = 0;
        {
          const activeSocket3 = socket;
          if (activeSocket3) {
            const hubMessage3 = JSON.stringify({ protocol: hubJsonProtocol, version: hubProtocolVersion }) + hubRecordSeparator;
            activeSocket3.send(hubMessage3);
          }
        }
      };
      socket.onmessage = (message) => {
        buffer += typeof message.data === 'string' ? message.data : '';
        const records = buffer.split(hubRecordSeparator);
        buffer = records.pop() ?? '';
        for (const record of records) {
          if (!record) {
            continue;
          }
          let payload: any;
          try {
            payload = JSON.parse(record);
          }
          catch {
            failed = true;
            fail();
            return;
          }
          if (!handshaken) {
            if (payload.error) {
              failed = true;
              fail();
              return;
            }
            handshaken = true;
            {
              const activeSocket4 = socket;
              if (activeSocket4) {
                const hubMessage4 = JSON.stringify({
                  type: hubInvocationMessageType,
                  invocationId: `subscribe-project-${projectId}`,
                  target: subscribeProjectMethod,
                  arguments: [projectId],
                }) + hubRecordSeparator;
                activeSocket4.send(hubMessage4);
              }
            }
            {
              const activeSocket5 = socket;
              if (activeSocket5) {
                const hubMessage5 = JSON.stringify({
                  type: hubInvocationMessageType,
                  invocationId: 'subscribe-global',
                  target: subscribeGlobalMethod,
                  arguments: [],
                }) + hubRecordSeparator;
                activeSocket5.send(hubMessage5);
              }
            }
            onConnected?.();
            continue;
          }
          if (payload.type === hubInvocationMessageType
            && payload.target === workspaceChangeTarget
            && payload.arguments?.[0]) {
            onEvent(payload.arguments[0] as WorkspaceChangeEventDto);
          }
        }
      };
      socket.onerror = () => {
        if (!failed) {
          failed = true;
          fail();
        }
      };
      socket.onclose = () => {
        socket = undefined;
        if (!closed) {
          if (!failed) {
            onError?.();
          }
          scheduleReconnect();
        }
      };
    }
    catch {
      socket = undefined;
      fail();
    }
  };
  connect();
  return () => {
    closed = true;
    clearReconnectTimer();
    try {
      socket?.close();
    }
    catch { /* ignore close failures */
    }
    socket = undefined;
  };
}
async function request<T>(path: string, options: {
  method?: 'POST' | 'PUT' | 'DELETE';
  body?: unknown;
  signal?: AbortSignal;
} = {}): Promise<T> {
  const argumentValue: URL | RequestInfo = `${apiBaseUrl}${path}`;
  const evaluatedValue7: RequestInit | undefined = {
    method: options.method,
    signal: options.signal,
    headers: options.body ? { 'content-type': 'application/json' } : undefined,
    body: options.body ? JSON.stringify(options.body) : undefined,
  };
  const response = await authenticatedFetch(argumentValue, evaluatedValue7);
  if (response.ok) {
    if (response.status === 204) {
      return undefined as T;
    }
    const payload = await response.text();
    const evaluatedValue6: T | PromiseLike<T> = (payload ? JSON.parse(payload) : undefined) as T;
    return evaluatedValue6;
  }
  const jsonResult = response.json();
  const problem = await jsonResult.catch(() => ({})) as ApiProblem;
  const diagnostics = Array.isArray(problem.diagnostics)
    ? problem.diagnostics.filter((diagnostic): diagnostic is FlowValidationDiagnostic => typeof diagnostic?.code === 'string' && typeof diagnostic.message === 'string')
    : [];
  const fallback = `Request failed with status ${response.status}. 请求失败，状态码为 ${response.status}。`;
  const message = problem.detail ?? problem.title ?? diagnostics[0]?.message ?? fallback;
  const argumentValue1: number = response.status;
  const localizeMessageResult: string = localizeMessage(message);
  throw new FlowApiError(argumentValue1, localizeMessageResult, problem.currentVersion, diagnostics);
}
