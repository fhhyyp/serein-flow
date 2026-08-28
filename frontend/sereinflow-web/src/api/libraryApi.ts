import type { ApiNodeType } from './flowApi'
import type { EnumParameterMetadataDto } from './flowApi'
import { localizeMessage } from '../i18n'

export interface LibraryParameterDto {
  id: string
  name: string
  type: string
  description?: string | null
  required: boolean
  isVariadic?: boolean
  variadicGroupId?: string | null
  elementType?: string | null
  enumMetadata?: EnumParameterMetadataDto | null
  defaultValue?: string | null
  aliases?: string[] | null
  identityConfidence?: 'explicit' | 'legacy'
}

export interface LibraryNodeDto {
  id: string
  type: ApiNodeType
  displayName: string
  description?: string | null
  libraryId: string
  flowLibraryName?: string | null
  className: string
  methodName: string
  dllName: string
  dllVersion: string
  returnType: string
  isAwaitable?: boolean
  contractId?: string | null
  overloadSignature?: string | null
  identityConfidence?: 'explicit' | 'legacy'
  parameters: LibraryParameterDto[]
}

export interface LibraryDto {
  id: string
  name: string
  version: string
  fileName: string
  sizeBytes: number
  sha256: string
  uploadedAt: string
  nodes: LibraryNodeDto[]
  lifecycle: 'available' | 'archived'
  familyId?: string | null
  familyName?: string | null
  semanticVersion?: string | null
}

export interface LibraryFamilyDto {
  id: string
  name: string
  description?: string | null
  latestArtifactId?: string | null
  createdAt: string
  updatedAt: string
  artifacts?: LibraryDto[] | null
}

export interface LibraryArtifactUsageDto {
  libraryArtifactId: string
  currentFlowCount: number
  flowVersionCount: number
  runSnapshotCount: number
}

export type LibraryLifecycle = 'available' | 'archived'
export type LibraryCompatibilityClassification = 'exact' | 'compatible' | 'requiresMapping' | 'requiresRewire' | 'breaking' | 'unknown'
export type LibraryUpgradePlanStatus = 'analyzed' | 'applied' | 'failed' | 'superseded'

export interface AssignLibraryFamilyRequestDto {
  familyId?: string | null
  name?: string | null
  description?: string | null
}

export interface LibraryUpgradePreviewRequestDto {
  sourceArtifactId: string
  targetArtifactId: string
  flowIds: string[]
}

export interface LibraryUpgradeIssueDto {
  id: string
  classification: LibraryCompatibilityClassification
  code: string
  message: string
  canvasId?: string | null
  nodeId?: string | null
  sourceNodeContractId?: string | null
  targetNodeContractId?: string | null
  sourceParameterId?: string | null
  targetParameterId?: string | null
  connectionId?: string | null
  requiresAcknowledgement: boolean
  blocksApplication: boolean
}

export interface FlowLibraryUpgradePreviewDto {
  flowId: string
  flowVersion: number
  canApply: boolean
  affectedNodeCount: number
  issues: LibraryUpgradeIssueDto[]
}

export interface LibraryUpgradePlanDto {
  id: string
  projectId: string
  sourceArtifactId: string
  targetArtifactId: string
  status: LibraryUpgradePlanStatus
  flows: FlowLibraryUpgradePreviewDto[]
  createdAt: string
  appliedAt?: string | null
  failureMessage?: string | null
  appliedFlows?: LibraryUpgradePlanFlowResultDto[] | null
}

export interface LibraryUpgradePlanFlowResultDto {
  flowId: string
  previousVersion: number
  newVersion: number
  appliedAt: string
}

export interface ApplyLibraryUpgradeRequestDto {
  flowId: string
  expectedFlowVersion: number
  acknowledgedItemIds?: string[]
}

export interface LibraryUpgradeApplyResultDto {
  flowId: string
  previousVersion: number
  newVersion: number
  sourceArtifactId: string
  targetArtifactId: string
  migratedNodeCount: number
  issues: LibraryUpgradeIssueDto[]
}

export interface ApplyLibraryUpgradeBatchRequestDto {
  flows: ApplyLibraryUpgradeRequestDto[]
}

export interface LibraryUpgradeApplyFailureDto {
  flowId: string
  statusCode: number
  code?: string | null
  message?: string | null
  currentVersion?: number | null
}

export interface LibraryUpgradeBatchApplyResultDto {
  planId: string
  succeeded: LibraryUpgradeApplyResultDto[]
  failed: LibraryUpgradeApplyFailureDto[]
}

export interface LibraryUploadResultDto {
  library: LibraryDto
  alreadyExists: boolean
}

export interface ProjectLibraryReferenceDto {
  projectId: string
  libraryId: string
  referencedAt: string
  library: LibraryDto
}

export class LibraryApiError extends Error {
  public readonly status: number

  public constructor(status: number, message: string) {
    super(message)
    this.name = 'LibraryApiError'
    this.status = status
  }
}

const apiBaseUrl = (import.meta.env.VITE_API_BASE_URL ?? '').replace(/\/$/, '')

export async function listLibraries(): Promise<LibraryDto[]> {
  return request<LibraryDto[]>('/api/libraries')
}

export async function listLibraryArtifactUsages(): Promise<LibraryArtifactUsageDto[]> {
  return request<LibraryArtifactUsageDto[]>('/api/libraries/usage')
}

export async function listProjectLibraryArtifactUsages(projectId: string): Promise<LibraryArtifactUsageDto[]> {
  return request<LibraryArtifactUsageDto[]>(`/api/projects/${encodeURIComponent(projectId)}/libraries/usage`)
}

export async function listLibraryFamilies(): Promise<LibraryFamilyDto[]> {
  return request<LibraryFamilyDto[]>('/api/library-families')
}

export async function listLibraryFamilyArtifacts(familyId: string): Promise<LibraryDto[]> {
  return request<LibraryDto[]>(`/api/library-families/${encodeURIComponent(familyId)}/artifacts`)
}

export async function assignLibraryFamily(libraryId: string, requestBody: AssignLibraryFamilyRequestDto): Promise<LibraryFamilyDto> {
  return request<LibraryFamilyDto>(`/api/libraries/${encodeURIComponent(libraryId)}/family`, { method: 'PATCH', json: requestBody })
}

export async function updateLibraryLifecycle(libraryId: string, lifecycle: LibraryLifecycle): Promise<LibraryDto> {
  return request<LibraryDto>(`/api/libraries/${encodeURIComponent(libraryId)}/lifecycle`, { method: 'PATCH', json: { lifecycle } })
}

export async function listEnvironmentLibraries(): Promise<LibraryDto[]> {
  return request<LibraryDto[]>('/api/environment/libraries')
}

export async function listProjectLibraries(projectId: string): Promise<ProjectLibraryReferenceDto[]> {
  return request<ProjectLibraryReferenceDto[]>(`/api/projects/${encodeURIComponent(projectId)}/libraries`)
}

export async function uploadLibrary(file: File): Promise<LibraryUploadResultDto> {
  const body = new FormData()
  body.append('file', file)
  return request<LibraryUploadResultDto>('/api/environment/libraries/upload', { method: 'POST', body })
}

export async function referenceProjectLibrary(projectId: string, libraryId: string): Promise<ProjectLibraryReferenceDto[]> {
  return request<ProjectLibraryReferenceDto[]>(`/api/projects/${encodeURIComponent(projectId)}/libraries/${encodeURIComponent(libraryId)}`, { method: 'PUT' })
}

export async function unreferenceProjectLibrary(projectId: string, libraryId: string): Promise<ProjectLibraryReferenceDto[]> {
  return request<ProjectLibraryReferenceDto[]>(`/api/projects/${encodeURIComponent(projectId)}/libraries/${encodeURIComponent(libraryId)}`, { method: 'DELETE' })
}

export async function archiveEnvironmentLibrary(libraryId: string): Promise<void> {
  const response = await fetch(`${apiBaseUrl}/api/environment/libraries/${encodeURIComponent(libraryId)}/archive`, { method: 'POST' })
  if (!response.ok) {
    throw new LibraryApiError(response.status, await readError(response))
  }
}

export async function reindexEnvironmentLibrary(libraryId: string): Promise<LibraryDto> {
  return request<LibraryDto>(`/api/environment/libraries/${encodeURIComponent(libraryId)}/reindex`, { method: 'POST' })
}

export async function deleteLibrary(libraryId: string): Promise<void> {
  const response = await fetch(`${apiBaseUrl}/api/libraries/${encodeURIComponent(libraryId)}`, { method: 'DELETE' })
  if (!response.ok) {
    throw new LibraryApiError(response.status, await readError(response))
  }
}

export async function previewLibraryUpgrade(projectId: string, requestBody: LibraryUpgradePreviewRequestDto): Promise<LibraryUpgradePlanDto> {
  return request<LibraryUpgradePlanDto>(`/api/projects/${encodeURIComponent(projectId)}/library-upgrades/preview`, { method: 'POST', json: requestBody })
}

export async function getLibraryUpgradePlan(projectId: string, planId: string): Promise<LibraryUpgradePlanDto> {
  return request<LibraryUpgradePlanDto>(`/api/projects/${encodeURIComponent(projectId)}/library-upgrades/${encodeURIComponent(planId)}`)
}

export async function applyLibraryUpgrade(projectId: string, planId: string, requestBody: ApplyLibraryUpgradeRequestDto): Promise<LibraryUpgradeApplyResultDto> {
  return request<LibraryUpgradeApplyResultDto>(`/api/projects/${encodeURIComponent(projectId)}/library-upgrades/${encodeURIComponent(planId)}/apply`, { method: 'POST', json: requestBody })
}

export async function applyLibraryUpgradeBatch(projectId: string, planId: string, requestBody: ApplyLibraryUpgradeBatchRequestDto): Promise<LibraryUpgradeBatchApplyResultDto> {
  return request<LibraryUpgradeBatchApplyResultDto>(`/api/projects/${encodeURIComponent(projectId)}/library-upgrades/${encodeURIComponent(planId)}/apply-batch`, { method: 'POST', json: requestBody })
}

async function request<T>(path: string, options: { method?: 'POST' | 'PUT' | 'PATCH' | 'DELETE'; body?: BodyInit; json?: unknown } = {}): Promise<T> {
  const response = await fetch(`${apiBaseUrl}${path}`, {
    method: options.method,
    headers: options.json === undefined ? undefined : { 'content-type': 'application/json' },
    body: options.json === undefined ? options.body : JSON.stringify(options.json),
  })
  if (response.ok) {
    return response.json() as Promise<T>
  }

  throw new LibraryApiError(response.status, await readError(response))
}

async function readError(response: Response): Promise<string> {
  const problem = await response.json().catch(() => ({})) as { detail?: string; title?: string }
  return localizeMessage(problem.detail ?? problem.title ?? `Library request failed with status ${response.status}. 类库请求失败，状态码为 ${response.status}。`)
}
