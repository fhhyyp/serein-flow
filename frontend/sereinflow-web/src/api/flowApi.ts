export type ApiNodeType = 'action' | 'flowCall' | 'globalData' | 'flipflop' | 'script' | 'condition' | 'value' | 'expression' | 'trigger'
export type ApiCanvasLifecycle = 'main' | 'init' | 'loading' | 'exit'
export type ApiConnectionKind = 'execution' | 'data'
export type ApiDataSource = 'literal' | 'previousNode' | 'projectInput' | 'expression'

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
  literalValue?: string
  projectInputKey?: string
  expression?: string
  sourceNodeId?: string
  sourcePortId?: string
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
}

export interface NodeDto {
  id: string
  type: ApiNodeType
  displayName: string
  x: number
  y: number
  ports: NodePortDto[]
  parameters: NodeParameterDto[]
  script: unknown | null
  ui?: NodeUiMetadataDto
}

export interface ConnectionDto {
  id: string
  fromNodeId: string
  fromPortId: string
  toNodeId: string
  toPortId: string
  kind: ApiConnectionKind
  branch?: 'success' | 'failure' | 'error' | 'upstream'
  dataSource?: ApiDataSource
  priority: number
}

export interface CanvasDto {
  id: string
  lifecycle: ApiCanvasLifecycle
  nodes: NodeDto[]
  connections: ConnectionDto[]
}

export interface FlowDefinitionDto {
  id: string
  schemaVersion: number
  version: number
  canvases: CanvasDto[]
  entryNodeId: string
  checksum: string
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
}

export interface ProjectWorkspaceDto {
  project: ProjectDto
  flows: FlowDefinitionSummaryDto[]
}

export interface CreateProjectRequestDto {
  name: string
  definition: FlowDefinitionDto
}

export interface UpdateFlowDefinitionRequestDto {
  expectedVersion: number
  definition: FlowDefinitionDto
}

interface ApiProblem {
  title?: string
  detail?: string
  currentVersion?: number
}

export class FlowApiError extends Error {
  public readonly status: number
  public readonly currentVersion?: number

  public constructor(status: number, message: string, currentVersion?: number) {
    super(message)
    this.name = 'FlowApiError'
    this.status = status
    this.currentVersion = currentVersion
  }
}

const apiBaseUrl = (import.meta.env.VITE_API_BASE_URL ?? '').replace(/\/$/, '')

export async function listProjects(): Promise<ProjectWorkspaceDto[]> {
  return request<ProjectWorkspaceDto[]>('/api/projects')
}

export async function createProject(requestBody: CreateProjectRequestDto): Promise<ProjectWorkspaceDto> {
  return request<ProjectWorkspaceDto>('/api/projects', { method: 'POST', body: requestBody })
}

export async function loadFlow(projectId: string, flowId: string): Promise<FlowDefinitionDto> {
  return request<FlowDefinitionDto>(`/api/projects/${projectId}/flows/${flowId}`)
}

export async function saveFlow(projectId: string, flowId: string, requestBody: UpdateFlowDefinitionRequestDto): Promise<FlowDefinitionDto> {
  return request<FlowDefinitionDto>(`/api/projects/${projectId}/flows/${flowId}`, { method: 'PUT', body: requestBody })
}

async function request<T>(path: string, options: { method?: 'POST' | 'PUT'; body?: unknown } = {}): Promise<T> {
  const response = await fetch(`${apiBaseUrl}${path}`, {
    method: options.method,
    headers: options.body ? { 'content-type': 'application/json' } : undefined,
    body: options.body ? JSON.stringify(options.body) : undefined,
  })
  if (response.ok) {
    return response.json() as Promise<T>
  }

  const problem = await response.json().catch(() => ({})) as ApiProblem
  throw new FlowApiError(response.status, problem.detail ?? problem.title ?? `Request failed with status ${response.status}.`, problem.currentVersion)
}
