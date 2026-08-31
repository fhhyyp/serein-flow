import type { FlowEdgeLineType } from '../flow/types'
import { localizeMessage } from '../i18n'

export type ApiNodeType = 'action' | 'flowCall' | 'flipflop' | 'script'
export type ApiCanvasLifecycle = 'main' | 'init' | 'loading' | 'exit' | 'custom'
export type ApiConnectionKind = 'execution' | 'data'
export type ApiDataSource = 'literal' | 'previousNode' | 'projectInput' | 'expression'
export type FlowConcurrencyMode = 'parallel' | 'exclusiveReject'
export type FlowVersionTrack = 'development' | 'production'
export type FlowVersionOperation = 'created' | 'saved' | 'published' | 'rolledBack' | 'libraryUpgraded' | 'imported'

export interface FlowConnectionLineTypesDto {
  execution?: FlowEdgeLineType
  data?: FlowEdgeLineType
}

export interface FlowCanvasFocusSettingsDto {
  enabled?: boolean
  parameterSources?: boolean
  parameterConsumers?: boolean
  callers?: boolean
  callees?: boolean
}

export interface FlowUiMetadataDto {
  connectionLineTypes?: FlowConnectionLineTypesDto
  canvasFocusSettings?: FlowCanvasFocusSettingsDto
}

export interface FlowRunPolicyDto {
  concurrencyMode: FlowConcurrencyMode
}

export interface NodePortDto {
  id: string
  name: string
  direction: 'input' | 'output'
  required: boolean
}

export interface NodeParameterUiMetadataDto {
  id: string
  nameKey: string
  valueKind: string
  type?: string
  description?: string
  inputMode?: 'connection' | 'manual' | 'select'
  literalValue?: string
  projectInputKey?: string
  expression?: string
  sourceNodeId?: string
  sourcePortId?: string
  isVariadic?: boolean
  variadicGroupId?: string
  elementType?: string
  variadicMode?: 'expanded' | 'collection'
  enumMetadata?: EnumParameterMetadataDto
}

export interface EnumValueOptionDto {
  name: string
  numericValue: string
}

export interface EnumParameterMetadataDto {
  typeName: string
  isFlags: boolean
  underlyingType: string
  options: EnumValueOptionDto[]
}

export interface NodeParameterDto {
  name: string
  valueJson?: string
  source: ApiDataSource
  required: boolean
  ui?: NodeParameterUiMetadataDto
}

export interface NodeUiMetadataDto {
  kind: string
  titleKey: string
  subtitleKey: string
  description?: string
  status: string
  hasDataOutput: boolean
  width?: number
  category?: 'method' | 'basic'
  libraryId?: string
  flowLibraryName?: string
  className?: string
  methodName?: string
  dllName?: string
  dllVersion?: string
  returnType?: string
  targetNodeId?: string
  targetFlowId?: string
  isAwaitable?: boolean
  staticReturnType?: string
  isDynamicReturnType?: boolean
  targetCanvasId?: string
  isPublic?: boolean
  flowCallParameterBindings?: FlowCallParameterBindingDto[]
  libraryNodeContractId?: string
}

export interface NodeDto {
  id: string
  type: ApiNodeType
  displayName: string
  x: number
  y: number
  ports: NodePortDto[]
  parameters: NodeParameterDto[]
  script: ScriptNodeDataDto | null
  ui?: NodeUiMetadataDto
}

export interface ScriptValueContractDto {
  name: string
  valueKind: string
  required: boolean
  id?: string
  description?: string
}

export interface ScriptNodeDataDto {
  nodeId: string
  source: string
  languageVersion: string
  sourceHash: string
  inputs: ScriptValueContractDto[]
  outputs: ScriptValueContractDto[]
}

export interface FlowCallParameterBindingDto {
  callParameterId: string
  targetParameterId: string
}

export interface NodeCreationDescriptorDto {
  id: string
  type: ApiNodeType
  displayName: string
  description: string | null
  ui: NodeUiMetadataDto
  script?: ScriptNodeDataDto | null
  parameters?: NodeParameterDto[] | null
}

export interface BuiltinNodeCatalogDto {
  nodes: NodeCreationDescriptorDto[]
}

export interface ConnectionDto {
  id: string
  fromNodeId: string
  fromPortId: string
  toNodeId: string
  toPortId: string
  kind: ApiConnectionKind
  branch?: 'success' | 'failure' | 'error'
  dataSource?: ApiDataSource
  priority: number
}

export interface CanvasDto {
  id: string
  lifecycle: ApiCanvasLifecycle
  nodes: NodeDto[]
  connections: ConnectionDto[]
  name?: string
}

export interface FlowDefinitionDto {
  id: string
  schemaVersion: number
  version: number
  canvases: CanvasDto[]
  entryNodeId: string
  checksum: string
  runPolicy?: FlowRunPolicyDto
  ui?: FlowUiMetadataDto
}

export interface ProjectDto {
  id: string
  name: string
  version: number
  status: string
  createdAt: string
  updatedAt: string
}

export interface FlowDefinitionSummaryDto {
  id: string
  version: number
  entryNodeId: string
  canvasCount?: number
  nodeCount?: number
  productionVersion?: number
}

export interface ProjectWorkspaceDto {
  project: ProjectDto
  flows: FlowDefinitionSummaryDto[]
}

export interface CreateProjectRequestDto {
  name: string
  definition: FlowDefinitionDto
}

export interface RenameProjectRequestDto {
  name: string
  expectedVersion: number
}

export interface UpdateFlowDefinitionRequestDto {
  expectedVersion: number
  definition: FlowDefinitionDto
}

export interface FlowVersionSummaryDto {
  flowId: string
  version: number
  track: FlowVersionTrack
  operation: FlowVersionOperation
  parentVersion?: number
  sourceVersion?: number
  remark: string
  createdAt?: string
  isCurrent: boolean
}

export interface FlowVersionDetailDto {
  version: FlowVersionSummaryDto
  definition: FlowDefinitionDto
}

export interface PublishFlowVersionRequestDto {
  expectedDevelopmentVersion: number
  remark?: string
}

export interface RollbackFlowVersionRequestDto {
  track: FlowVersionTrack
  expectedHeadVersion: number
}

export interface RunFlowRequestDto {
  expectedFlowVersion?: number
  projectInputs?: Record<string, unknown>
  timeoutSeconds?: number
  maxSteps?: number
  maxNodeVisits?: number
}

export interface FlowRunDto {
  id: string
  flowId: string
  flowVersion: number
  status: 'pending' | 'running' | 'succeeded' | 'failed' | 'cancelled' | 'timedOut' | 'interrupted'
  startedAt?: string
  endedAt?: string
  errorSummary?: string
  projectId?: string
  createdAt?: string
  cancellationReason?: string
  concurrencyMode?: FlowConcurrencyMode
  isListenerRun?: boolean
  queuedAt?: string
  executionKind?: 'production' | 'debug'
  debugSessionId?: string
}

export type FlowDebugSessionStatus = 'pending' | 'running' | 'paused' | 'completed' | 'cancelled' | 'failed'

export interface StartFlowDebugSessionRequestDto {
  breakpointNodeIds?: string[]
  projectInputs?: Record<string, unknown>
  timeoutSeconds?: number
  maxSteps?: number
  maxNodeVisits?: number
  expectedFlowVersion?: number
  maxQueuedFlipflopTriggers?: number
}

export interface FlowDebugSessionDto {
  id: string
  runId: string
  projectId: string
  flowId: string
  status: FlowDebugSessionStatus
  breakpointNodeIds: string[]
  currentNodeId?: string
  activeInvocationId?: string
  activeFlipflopNodeId?: string
  queuedTriggerCount: number
  lastCommandSequence: number
  failureMessage?: string
  createdAt: string
  updatedAt: string
  stateRevision?: number
  pauseState?: FlowDebugPauseStateDto
  lastNodeResult?: FlowDebugNodeResultDto
}

export interface FlowDebugPauseStateDto {
  nodeId: string
  nodeType: string
  step: number
  frameDepth: number
  invocationId?: string
  boundarySequence: number
  inputs: unknown
  pausedAt: string
}

export interface FlowDebugNodeResultDto {
  nodeId: string
  sequence: number
  completedAt: string
  outcome: string
  branch?: string
  inputs: unknown
  outputs: unknown
  errorCode?: string
  errorMessage?: string
}

export interface FlowDebugWaitResultDto {
  hasChanged: boolean
  timedOut: boolean
  session: FlowDebugSessionDto
}

export interface FlowRunOverviewDto {
  queueCapacity: number
  queuedCount: number
  activeRunCount: number
  activeListenerRunCount: number
  maxConcurrentRuns: number
  maxConcurrentListenerRuns: number
  maxConcurrentRunsPerProject: number
  queuedRuns: FlowRunDto[]
  activeRuns: FlowRunDto[]
  recentRuns: FlowRunDto[]
}

export interface FlowRunEventDto {
  runId: string
  sequence: number
  timestamp: string
  type: string
  nodeId?: string
  payloadJson: string
}

export interface FlowRunOutputDto {
  runId: string
  sequence: number
  timestamp: string
  nodeId: string
  outcome: 'completed' | 'failed' | 'error'
  branch?: string
  inputs: unknown
  outputs: unknown
  errorCode?: string
  errorMessage?: string
}

export interface RunExecutionSettingsDto {
  queueCapacity: number
  maxConcurrentRuns: number
  maxConcurrentListenerRuns: number
  maxConcurrentRunsPerProject: number
  queueWaitTimeoutSeconds: number
  shutdownGracePeriodSeconds: number
  synchronousInvocationTimeoutSeconds: number
}

export type FlowInvocationMode = 'asynchronous' | 'synchronous'

export interface FlowInterfaceDto {
  id: string
  projectId: string
  flowId: string
  name: string
  invocationMode: FlowInvocationMode
  isEnabled: boolean
  createdAt: string
  updatedAt: string
  productionVersion?: number
}

export interface CreateFlowInterfaceRequestDto {
  projectId: string
  flowId: string
  name: string
  invocationMode: FlowInvocationMode
  isEnabled: boolean
}

export interface UpdateFlowInterfaceRequestDto {
  name: string
  invocationMode: FlowInvocationMode
  isEnabled: boolean
}

export interface FlowValidationDiagnostic {
  code: string
  message: string
  path?: string | null
}

interface ApiProblem {
  title?: string
  detail?: string
  currentVersion?: number
  diagnostics?: FlowValidationDiagnostic[]
}

export class FlowApiError extends Error {
  public readonly status: number
  public readonly currentVersion?: number
  public readonly diagnostics: FlowValidationDiagnostic[]

  public constructor(
    status: number,
    message: string,
    currentVersion?: number,
    diagnostics: FlowValidationDiagnostic[] = [],
  ) {
    super(message)
    this.name = 'FlowApiError'
    this.status = status
    this.currentVersion = currentVersion
    this.diagnostics = diagnostics
  }
}

const apiBaseUrl = (import.meta.env.VITE_API_BASE_URL ?? '').replace(/\/$/, '')

export async function listProjects(includeArchived = false): Promise<ProjectWorkspaceDto[]> {
  const suffix = includeArchived ? '?includeArchived=true' : ''
  return request<ProjectWorkspaceDto[]>(`/api/projects${suffix}`)
}

export async function getBuiltinNodeCatalog(): Promise<BuiltinNodeCatalogDto> {
  return request<BuiltinNodeCatalogDto>('/api/node-catalog/builtins')
}

export async function createProject(requestBody: CreateProjectRequestDto): Promise<ProjectWorkspaceDto> {
  return request<ProjectWorkspaceDto>('/api/projects', { method: 'POST', body: requestBody })
}

export async function renameProject(projectId: string, requestBody: RenameProjectRequestDto): Promise<ProjectWorkspaceDto> {
  return request<ProjectWorkspaceDto>(`/api/projects/${projectId}`, { method: 'PUT', body: requestBody })
}

export async function archiveProject(projectId: string): Promise<ProjectDto> {
  return request<ProjectDto>(`/api/projects/${projectId}/archive`, { method: 'POST' })
}

export async function loadFlow(projectId: string, flowId: string): Promise<FlowDefinitionDto> {
  return request<FlowDefinitionDto>(`/api/projects/${projectId}/flows/${flowId}`)
}

export async function saveFlow(projectId: string, flowId: string, requestBody: UpdateFlowDefinitionRequestDto): Promise<FlowDefinitionDto> {
  return request<FlowDefinitionDto>(`/api/projects/${projectId}/flows/${flowId}`, { method: 'PUT', body: requestBody })
}

export async function listFlowVersions(projectId: string, flowId: string, track: FlowVersionTrack): Promise<FlowVersionSummaryDto[]> {
  return request<FlowVersionSummaryDto[]>(`/api/projects/${projectId}/flows/${flowId}/versions?track=${track}`)
}

export async function getFlowVersion(projectId: string, flowId: string, version: number): Promise<FlowVersionDetailDto> {
  return request<FlowVersionDetailDto>(`/api/projects/${projectId}/flows/${flowId}/versions/${version}`)
}

export async function publishFlowVersion(
  projectId: string,
  flowId: string,
  requestBody: PublishFlowVersionRequestDto,
): Promise<FlowVersionSummaryDto> {
  return request<FlowVersionSummaryDto>(`/api/projects/${projectId}/flows/${flowId}/publish`, { method: 'POST', body: requestBody })
}

export async function rollbackFlowVersion(
  projectId: string,
  flowId: string,
  version: number,
  requestBody: RollbackFlowVersionRequestDto,
): Promise<FlowVersionSummaryDto> {
  return request<FlowVersionSummaryDto>(`/api/projects/${projectId}/flows/${flowId}/versions/${version}/rollback`, { method: 'POST', body: requestBody })
}

export async function startFlowRun(projectId: string, flowId: string, requestBody: RunFlowRequestDto = {}): Promise<FlowRunDto> {
  return request<FlowRunDto>(`/api/projects/${projectId}/flows/${flowId}/runs`, { method: 'POST', body: requestBody })
}

export async function startFlowDebugSession(
  projectId: string,
  flowId: string,
  requestBody: StartFlowDebugSessionRequestDto,
): Promise<FlowDebugSessionDto> {
  return request<FlowDebugSessionDto>(`/api/projects/${projectId}/flows/${flowId}/debug-sessions`, { method: 'POST', body: requestBody })
}

export async function getFlowDebugSession(sessionId: string): Promise<FlowDebugSessionDto> {
  return request<FlowDebugSessionDto>(`/api/debug-sessions/${sessionId}`)
}

export async function getDebugSessionForRun(runId: string): Promise<FlowDebugSessionDto> {
  return request<FlowDebugSessionDto>(`/api/runs/${runId}/debug-session`)
}

export async function waitForFlowDebugSession(
  sessionId: string,
  afterRevision: number,
  timeoutSeconds = 15,
): Promise<FlowDebugWaitResultDto> {
  const query = new URLSearchParams({
    afterRevision: String(afterRevision),
    timeoutSeconds: String(timeoutSeconds),
  })
  return request<FlowDebugWaitResultDto>(`/api/debug-sessions/${sessionId}/wait?${query.toString()}`)
}

export async function continueFlowDebugSession(sessionId: string, commandSequence: number): Promise<void> {
  await request<unknown>(`/api/debug-sessions/${sessionId}/continue`, { method: 'POST', body: { commandSequence } })
}

export async function stepFlowDebugSession(sessionId: string, commandSequence: number): Promise<void> {
  await request<unknown>(`/api/debug-sessions/${sessionId}/step`, { method: 'POST', body: { commandSequence } })
}

export async function stopFlowDebugSession(sessionId: string, commandSequence: number): Promise<void> {
  await request<unknown>(`/api/debug-sessions/${sessionId}/stop`, { method: 'POST', body: { commandSequence } })
}

export async function getFlowRun(runId: string): Promise<FlowRunDto> {
  return request<FlowRunDto>(`/api/runs/${runId}`)
}

export async function listFlowRuns(options: { statuses?: FlowRunDto['status'][]; projectId?: string; take?: number } = {}): Promise<FlowRunDto[]> {
  const query = new URLSearchParams()
  if (options.statuses?.length) {
    query.set('status', options.statuses.join(','))
  }
  if (options.projectId) {
    query.set('projectId', options.projectId)
  }
  if (options.take) {
    query.set('take', String(options.take))
  }
  const suffix = query.size > 0 ? `?${query.toString()}` : ''
  return request<FlowRunDto[]>(`/api/runs${suffix}`)
}

export async function getFlowRunOverview(): Promise<FlowRunOverviewDto> {
  return request<FlowRunOverviewDto>('/api/runs/overview')
}

export async function cancelFlowRun(runId: string): Promise<void> {
  await request<unknown>(`/api/runs/${runId}/cancel`, { method: 'POST' })
}

export async function interruptFlowRun(runId: string): Promise<FlowRunDto> {
  return request<FlowRunDto>(`/api/runs/${runId}/interrupt`, { method: 'POST' })
}

export async function getFlowRunSnapshot(runId: string): Promise<FlowDefinitionDto> {
  return request<FlowDefinitionDto>(`/api/runs/${runId}/snapshot`)
}

export async function listFlowRunOutputs(runId: string): Promise<FlowRunOutputDto[]> {
  return request<FlowRunOutputDto[]>(`/api/runs/${runId}/outputs`)
}

export async function getRunExecutionSettings(): Promise<RunExecutionSettingsDto> {
  return request<RunExecutionSettingsDto>('/api/environment/settings')
}

export async function saveRunExecutionSettings(settings: RunExecutionSettingsDto): Promise<RunExecutionSettingsDto> {
  return request<RunExecutionSettingsDto>('/api/environment/settings', { method: 'PUT', body: settings })
}

export async function listFlowInterfaces(): Promise<FlowInterfaceDto[]> {
  return request<FlowInterfaceDto[]>('/api/environment/interfaces')
}

export async function createFlowInterface(requestBody: CreateFlowInterfaceRequestDto): Promise<FlowInterfaceDto> {
  return request<FlowInterfaceDto>('/api/environment/interfaces', { method: 'POST', body: requestBody })
}

export async function updateFlowInterface(interfaceId: string, requestBody: UpdateFlowInterfaceRequestDto): Promise<FlowInterfaceDto> {
  return request<FlowInterfaceDto>(`/api/environment/interfaces/${interfaceId}`, { method: 'PUT', body: requestBody })
}

export async function deleteFlowInterface(interfaceId: string): Promise<void> {
  await request<unknown>(`/api/environment/interfaces/${interfaceId}`, { method: 'DELETE' })
}

export async function listFlowRunEvents(runId: string, afterSequence = 0): Promise<FlowRunEventDto[]> {
  return request<FlowRunEventDto[]>(`/api/runs/${runId}/events?afterSequence=${afterSequence}`)
}

export function subscribeFlowRunEvents(
  runId: string,
  onEvent: (event: FlowRunEventDto) => void,
  onError?: () => void,
): () => void {
  let closed = false
  let fallbackStarted = false
  let socket: WebSocket | undefined
  let sseCleanup: (() => void) | undefined
  let lastSequence = 0
  let replayInFlight = false
  const deliver = (event: FlowRunEventDto) => {
    if (event.sequence <= lastSequence) return
    lastSequence = event.sequence
    onEvent(event)
  }

  const replay = async () => {
    if (closed || replayInFlight) return
    replayInFlight = true
    try {
      const events = await listFlowRunEvents(runId, lastSequence)
      events.forEach(deliver)
    } catch {
      onError?.()
    } finally {
      replayInFlight = false
    }
  }

  const startSseFallback = () => {
    if (closed || fallbackStarted) return
    fallbackStarted = true
    const source = new EventSource(`${apiBaseUrl}/api/runs/${runId}/events/stream`)
    const eventTypes = ['run.started', 'node.started', 'node.completed', 'node.failed', 'node.error', 'run.completed', 'run.failed', 'run.cancelled', 'run.timed_out', 'run.interrupted', 'log']
    const listeners = eventTypes.map((type) => {
      const listener = (event: Event) => {
        try {
          deliver(JSON.parse((event as MessageEvent).data) as FlowRunEventDto)
        } catch {
          onError?.()
        }
      }
      source.addEventListener(type, listener)
      return [type, listener] as const
    })
    // EventSource reconnects natively, but the API may have committed events
    // while the connection was down. Replay from the last sequence on every
    // reconnect/open event to close that gap.
    source.onopen = () => { void replay() }
    source.onerror = () => {
      onError?.()
      // Keep the EventSource alive so its built-in retry can recover. A
      // replay is harmless when the stream is still unavailable and will be
      // de-duplicated by sequence when it comes back.
      void replay()
    }
    sseCleanup = () => {
      listeners.forEach(([type, listener]) => source.removeEventListener(type, listener))
      source.close()
    }
  }

  // Use the native SignalR JSON hub protocol so the web client does not need
  // an additional runtime dependency. If WebSocket, handshake, or the hub
  // invocation fails, transparently fall back to the resumable SSE stream.
  // 使用原生 SignalR JSON Hub 协议，失败时自动降级到可断点续传的 SSE。
  const startSignalR = () => {
    if (typeof WebSocket === 'undefined') {
      startSseFallback()
      return
    }

    const configured = apiBaseUrl || window.location.origin
    const base = new URL(configured)
    base.protocol = base.protocol === 'https:' ? 'wss:' : 'ws:'
    base.pathname = `${base.pathname.replace(/\/$/, '')}/hubs/runs`
    base.search = ''
    let buffer = ''
    let handshaken = false
    let failed = false

    const failover = () => {
      if (failed || closed) return
      failed = true
      try { socket?.close() } catch { /* ignore close failures */ }
      startSseFallback()
    }

    try {
      socket = new WebSocket(base.toString())
      socket.onopen = () => {
        socket?.send(JSON.stringify({ protocol: 'json', version: 1 }) + '\u001e')
      }
      socket.onmessage = (message) => {
        buffer += typeof message.data === 'string' ? message.data : ''
        const records = buffer.split('\u001e')
        buffer = records.pop() ?? ''
        for (const record of records) {
          if (!record) continue
          let payload: any
          try { payload = JSON.parse(record) } catch { failover(); return }
          if (!handshaken) {
            if (payload.error) { failover(); return }
            handshaken = true
            socket?.send(JSON.stringify({ type: 1, invocationId: `subscribe-${runId}`, target: 'Subscribe', arguments: [runId] }) + '\u001e')
            void replay()
            continue
          }
          if (payload.type === 1 && payload.target === 'runEvent' && payload.arguments?.[0]) {
            deliver(payload.arguments[0] as FlowRunEventDto)
          }
        }
      }
      socket.onerror = failover
      socket.onclose = () => {
        if (!closed) failover()
      }
    } catch {
      failover()
    }
  }

  // Replay persisted events before subscribing to the live hub. Sequence
  // de-duplication closes the small gap between the replay query and the hub
  // handshake.
  void replay()
  startSignalR()
  return () => {
    closed = true
    try { socket?.close() } catch { /* ignore close failures */ }
    sseCleanup?.()
  }
}

async function request<T>(path: string, options: { method?: 'POST' | 'PUT' | 'DELETE'; body?: unknown } = {}): Promise<T> {
  const response = await fetch(`${apiBaseUrl}${path}`, {
    method: options.method,
    headers: options.body ? { 'content-type': 'application/json' } : undefined,
    body: options.body ? JSON.stringify(options.body) : undefined,
  })
  if (response.ok) {
    if (response.status === 204) {
      return undefined as T
    }
    const payload = await response.text()
    return (payload ? JSON.parse(payload) : undefined) as T
  }

  const problem = await response.json().catch(() => ({})) as ApiProblem
  const diagnostics = Array.isArray(problem.diagnostics)
    ? problem.diagnostics.filter((diagnostic): diagnostic is FlowValidationDiagnostic =>
      typeof diagnostic?.code === 'string' && typeof diagnostic.message === 'string')
    : []
  const fallback = `Request failed with status ${response.status}. 请求失败，状态码为 ${response.status}。`
  const message = problem.detail ?? problem.title ?? diagnostics[0]?.message ?? fallback
  throw new FlowApiError(response.status, localizeMessage(message), problem.currentVersion, diagnostics)
}
