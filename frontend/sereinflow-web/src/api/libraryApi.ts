import type { ApiNodeType } from './flowApi';
import type { EnumParameterMetadataDto } from './flowApi';
import { localizeMessage } from '../i18n';
export interface LibraryParameterDto {
  id: string;
  name: string;
  type: string;
  description?: string | null;
  required: boolean;
  isVariadic?: boolean;
  variadicGroupId?: string | null;
  elementType?: string | null;
  enumMetadata?: EnumParameterMetadataDto | null;
  defaultValue?: string | null;
  aliases?: string[] | null;
  identityConfidence?: 'explicit' | 'legacy';
}
export interface LibraryNodeDto {
  id: string;
  type: ApiNodeType;
  displayName: string;
  description?: string | null;
  libraryId: string;
  flowLibraryName?: string | null;
  className: string;
  methodName: string;
  dllName: string;
  dllVersion: string;
  returnType: string;
  isAwaitable?: boolean;
  contractId?: string | null;
  overloadSignature?: string | null;
  identityConfidence?: 'explicit' | 'legacy';
  parameters: LibraryParameterDto[];
}
export interface LibraryDto {
  id: string;
  name: string;
  version: string;
  fileName: string;
  sizeBytes: number;
  sha256: string;
  uploadedAt: string;
  nodes: LibraryNodeDto[];
  lifecycle: 'available' | 'archived';
  familyId?: string | null;
  familyName?: string | null;
  semanticVersion?: string | null;
}
export interface LibraryFamilyDto {
  id: string;
  name: string;
  description?: string | null;
  latestArtifactId?: string | null;
  createdAt: string;
  updatedAt: string;
  artifacts?: LibraryDto[] | null;
}
export interface LibraryArtifactUsageDto {
  libraryArtifactId: string;
  currentFlowCount: number;
  flowVersionCount: number;
  runSnapshotCount: number;
}
export type LibraryLifecycle = 'available' | 'archived';
export type LibraryCompatibilityClassification = 'exact' | 'compatible' | 'requiresMapping' | 'requiresRewire' | 'breaking' | 'unknown';
export type LibraryUpgradePlanStatus = 'analyzed' | 'applied' | 'failed' | 'superseded';
export interface AssignLibraryFamilyRequestDto {
  familyId?: string | null;
  name?: string | null;
  description?: string | null;
}
export interface LibraryUpgradePreviewRequestDto {
  sourceArtifactId: string;
  targetArtifactId: string;
  flowIds: string[];
}
export interface LibraryUpgradeIssueDto {
  id: string;
  classification: LibraryCompatibilityClassification;
  code: string;
  message: string;
  canvasId?: string | null;
  nodeId?: string | null;
  sourceNodeContractId?: string | null;
  targetNodeContractId?: string | null;
  sourceParameterId?: string | null;
  targetParameterId?: string | null;
  connectionId?: string | null;
  requiresAcknowledgement: boolean;
  blocksApplication: boolean;
}
export interface FlowLibraryUpgradePreviewDto {
  flowId: string;
  flowVersion: number;
  canApply: boolean;
  affectedNodeCount: number;
  issues: LibraryUpgradeIssueDto[];
}
export interface LibraryUpgradePlanDto {
  id: string;
  projectId: string;
  sourceArtifactId: string;
  targetArtifactId: string;
  status: LibraryUpgradePlanStatus;
  flows: FlowLibraryUpgradePreviewDto[];
  createdAt: string;
  appliedAt?: string | null;
  failureMessage?: string | null;
  appliedFlows?: LibraryUpgradePlanFlowResultDto[] | null;
}
export interface LibraryUpgradePlanFlowResultDto {
  flowId: string;
  previousVersion: number;
  newVersion: number;
  appliedAt: string;
}
export interface ApplyLibraryUpgradeRequestDto {
  flowId: string;
  expectedFlowVersion: number;
  acknowledgedItemIds?: string[];
}
export interface LibraryUpgradeApplyResultDto {
  flowId: string;
  previousVersion: number;
  newVersion: number;
  sourceArtifactId: string;
  targetArtifactId: string;
  migratedNodeCount: number;
  issues: LibraryUpgradeIssueDto[];
}
export interface ApplyLibraryUpgradeBatchRequestDto {
  flows: ApplyLibraryUpgradeRequestDto[];
}
export interface LibraryUpgradeApplyFailureDto {
  flowId: string;
  statusCode: number;
  code?: string | null;
  message?: string | null;
  currentVersion?: number | null;
}
export interface LibraryUpgradeBatchApplyResultDto {
  planId: string;
  succeeded: LibraryUpgradeApplyResultDto[];
  failed: LibraryUpgradeApplyFailureDto[];
}
export interface LibraryUploadResultDto {
  library: LibraryDto;
  alreadyExists: boolean;
}
export interface ProjectLibraryReferenceDto {
  projectId: string;
  libraryId: string;
  referencedAt: string;
  library: LibraryDto;
}
export class LibraryApiError extends Error {
  public readonly status: number;
  public constructor(status: number, message: string) {
    super(message);
    this.name = 'LibraryApiError';
    this.status = status;
  }
}
const apiBaseUrl = (import.meta.env.VITE_API_BASE_URL ?? '').replace(/\/$/, '');
export async function listLibraries(): Promise<LibraryDto[]> {
  const requestResult: LibraryDto[] | PromiseLike<LibraryDto[]> = request<LibraryDto[]>('/api/libraries');
  return requestResult;
}
export async function listLibraryArtifactUsages(): Promise<LibraryArtifactUsageDto[]> {
  const requestResult1: LibraryArtifactUsageDto[] | PromiseLike<LibraryArtifactUsageDto[]> = request<LibraryArtifactUsageDto[]>('/api/libraries/usage');
  return requestResult1;
}
export async function listProjectLibraryArtifactUsages(projectId: string): Promise<LibraryArtifactUsageDto[]> {
  const evaluatedValue: string = `/api/projects/${encodeURIComponent(projectId)}/libraries/usage`;
  const requestResult2: LibraryArtifactUsageDto[] | PromiseLike<LibraryArtifactUsageDto[]> = request<LibraryArtifactUsageDto[]>(evaluatedValue);
  return requestResult2;
}
export async function listLibraryFamilies(): Promise<LibraryFamilyDto[]> {
  const requestResult3: LibraryFamilyDto[] | PromiseLike<LibraryFamilyDto[]> = request<LibraryFamilyDto[]>('/api/library-families');
  return requestResult3;
}
export async function listLibraryFamilyArtifacts(familyId: string): Promise<LibraryDto[]> {
  const evaluatedValue1: string = `/api/library-families/${encodeURIComponent(familyId)}/artifacts`;
  const requestResult4: LibraryDto[] | PromiseLike<LibraryDto[]> = request<LibraryDto[]>(evaluatedValue1);
  return requestResult4;
}
export async function assignLibraryFamily(libraryId: string, requestBody: AssignLibraryFamilyRequestDto): Promise<LibraryFamilyDto> {
  const evaluatedValue2: string = `/api/libraries/${encodeURIComponent(libraryId)}/family`;
  const requestResult5: LibraryFamilyDto | PromiseLike<LibraryFamilyDto> = request<LibraryFamilyDto>(evaluatedValue2, { method: 'PATCH', json: requestBody });
  return requestResult5;
}
export async function updateLibraryLifecycle(libraryId: string, lifecycle: LibraryLifecycle): Promise<LibraryDto> {
  const evaluatedValue3: string = `/api/libraries/${encodeURIComponent(libraryId)}/lifecycle`;
  const requestResult6: LibraryDto | PromiseLike<LibraryDto> = request<LibraryDto>(evaluatedValue3, { method: 'PATCH', json: { lifecycle } });
  return requestResult6;
}
export async function listEnvironmentLibraries(): Promise<LibraryDto[]> {
  const requestResult7: LibraryDto[] | PromiseLike<LibraryDto[]> = request<LibraryDto[]>('/api/environment/libraries');
  return requestResult7;
}
export async function listProjectLibraries(projectId: string): Promise<ProjectLibraryReferenceDto[]> {
  const evaluatedValue4: string = `/api/projects/${encodeURIComponent(projectId)}/libraries`;
  const requestResult8: ProjectLibraryReferenceDto[] | PromiseLike<ProjectLibraryReferenceDto[]> = request<ProjectLibraryReferenceDto[]>(evaluatedValue4);
  return requestResult8;
}
export async function uploadLibrary(file: File): Promise<LibraryUploadResultDto> {
  const body = new FormData();
  body.append('file', file);
  const requestResult9: LibraryUploadResultDto | PromiseLike<LibraryUploadResultDto> = request<LibraryUploadResultDto>('/api/environment/libraries/upload', { method: 'POST', body });
  return requestResult9;
}
export async function referenceProjectLibrary(projectId: string, libraryId: string): Promise<ProjectLibraryReferenceDto[]> {
  const evaluatedValue5: string = `/api/projects/${encodeURIComponent(projectId)}/libraries/${encodeURIComponent(libraryId)}`;
  const requestResult10: ProjectLibraryReferenceDto[] | PromiseLike<ProjectLibraryReferenceDto[]> = request<ProjectLibraryReferenceDto[]>(evaluatedValue5, { method: 'PUT' });
  return requestResult10;
}
export async function unreferenceProjectLibrary(projectId: string, libraryId: string): Promise<ProjectLibraryReferenceDto[]> {
  const evaluatedValue6: string = `/api/projects/${encodeURIComponent(projectId)}/libraries/${encodeURIComponent(libraryId)}`;
  const requestResult11: ProjectLibraryReferenceDto[] | PromiseLike<ProjectLibraryReferenceDto[]> = request<ProjectLibraryReferenceDto[]>(evaluatedValue6, { method: 'DELETE' });
  return requestResult11;
}
export async function archiveEnvironmentLibrary(libraryId: string): Promise<void> {
  const evaluatedValue7: RequestInfo | URL = `${apiBaseUrl}/api/environment/libraries/${encodeURIComponent(libraryId)}/archive`;
  const response = await fetch(evaluatedValue7, { method: 'POST' });
  if (!response.ok) {
    const argumentValue: number = response.status;
    const readErrorResult: string = await readError(response);
    throw new LibraryApiError(argumentValue, readErrorResult);
  }
}
export async function reindexEnvironmentLibrary(libraryId: string): Promise<LibraryDto> {
  const evaluatedValue8: string = `/api/environment/libraries/${encodeURIComponent(libraryId)}/reindex`;
  const requestResult12: LibraryDto | PromiseLike<LibraryDto> = request<LibraryDto>(evaluatedValue8, { method: 'POST' });
  return requestResult12;
}
export async function deleteLibrary(libraryId: string): Promise<void> {
  const evaluatedValue9: RequestInfo | URL = `${apiBaseUrl}/api/libraries/${encodeURIComponent(libraryId)}`;
  const response = await fetch(evaluatedValue9, { method: 'DELETE' });
  if (!response.ok) {
    const argumentValue1: number = response.status;
    const readErrorResult1: string = await readError(response);
    throw new LibraryApiError(argumentValue1, readErrorResult1);
  }
}
export async function previewLibraryUpgrade(projectId: string, requestBody: LibraryUpgradePreviewRequestDto): Promise<LibraryUpgradePlanDto> {
  const evaluatedValue10: string = `/api/projects/${encodeURIComponent(projectId)}/library-upgrades/preview`;
  const requestResult13: LibraryUpgradePlanDto | PromiseLike<LibraryUpgradePlanDto> = request<LibraryUpgradePlanDto>(evaluatedValue10, { method: 'POST', json: requestBody });
  return requestResult13;
}
export async function getLibraryUpgradePlan(projectId: string, planId: string): Promise<LibraryUpgradePlanDto> {
  const evaluatedValue11: string = `/api/projects/${encodeURIComponent(projectId)}/library-upgrades/${encodeURIComponent(planId)}`;
  const requestResult14: LibraryUpgradePlanDto | PromiseLike<LibraryUpgradePlanDto> = request<LibraryUpgradePlanDto>(evaluatedValue11);
  return requestResult14;
}
export async function applyLibraryUpgrade(projectId: string, planId: string, requestBody: ApplyLibraryUpgradeRequestDto): Promise<LibraryUpgradeApplyResultDto> {
  const evaluatedValue12: string = `/api/projects/${encodeURIComponent(projectId)}/library-upgrades/${encodeURIComponent(planId)}/apply`;
  const requestResult15: LibraryUpgradeApplyResultDto | PromiseLike<LibraryUpgradeApplyResultDto> = request<LibraryUpgradeApplyResultDto>(evaluatedValue12, { method: 'POST', json: requestBody });
  return requestResult15;
}
export async function applyLibraryUpgradeBatch(projectId: string, planId: string, requestBody: ApplyLibraryUpgradeBatchRequestDto): Promise<LibraryUpgradeBatchApplyResultDto> {
  const evaluatedValue13: string = `/api/projects/${encodeURIComponent(projectId)}/library-upgrades/${encodeURIComponent(planId)}/apply-batch`;
  const requestResult16: LibraryUpgradeBatchApplyResultDto | PromiseLike<LibraryUpgradeBatchApplyResultDto> = request<LibraryUpgradeBatchApplyResultDto>(evaluatedValue13, { method: 'POST', json: requestBody });
  return requestResult16;
}
async function request<T>(path: string, options: {
  method?: 'POST' | 'PUT' | 'PATCH' | 'DELETE';
  body?: BodyInit;
  json?: unknown;
} = {}): Promise<T> {
  const argumentValue2: RequestInfo | URL = `${apiBaseUrl}${path}`;
  const evaluatedValue15: RequestInit | undefined = {
    method: options.method,
    headers: options.json === undefined ? undefined : { 'content-type': 'application/json' },
    body: options.json === undefined ? options.body : JSON.stringify(options.json),
  };
  const response = await fetch(argumentValue2, evaluatedValue15);
  if (response.ok) {
    const evaluatedValue14: T | PromiseLike<T> = response.json() as Promise<T>;
    return evaluatedValue14;
  }
  const argumentValue3: number = response.status;
  const readErrorResult2: string = await readError(response);
  throw new LibraryApiError(argumentValue3, readErrorResult2);
}
async function readError(response: Response): Promise<string> {
  const jsonResult = response.json();
  const problem = await jsonResult.catch(() => ({})) as {
    detail?: string;
    title?: string;
  };
  const localizeMessageResult: string | PromiseLike<string> = localizeMessage(problem.detail ?? problem.title ?? `Library request failed with status ${response.status}. 类库请求失败，状态码为 ${response.status}。`);
  return localizeMessageResult;
}
