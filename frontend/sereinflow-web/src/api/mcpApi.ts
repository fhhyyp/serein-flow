import { authenticatedFetch } from './securityApi';
import type { ProjectWorkspaceDto } from './flowApi';
export type SereinFlowPermission = 'project.read' | 'project.write' | 'library.read' | 'run.read' | 'debug.read' | 'flow.write' | 'debug.control' | 'flow.publish' | 'flow.rollback' | 'script.compile' | 'library.import' | 'library.manage' | 'security.manage' | 'workspace.manage' | 'sensitive.read' | 'run.variable.write' | 'run.execute' | 'run.message.publish';
export interface ApiKeyDto {
  id: string;
  projectId?: string | null;
  name: string;
  keyPrefix: string;
  permissions: SereinFlowPermission[];
  createdAt: string;
  expiresAt?: string | null;
  revokedAt?: string | null;
  lastUsedAt?: string | null;
  isAdministrator: boolean;
}
export interface CreatedApiKeyDto {
  key: ApiKeyDto;
  secret: string;
}
export interface RotatedApiKeyDto {
  revokedKeyId: string;
  key: ApiKeyDto;
  secret?: string | null;
}
export interface CreateApiKeyRequest {
  projectId?: string | null;
  name: string;
  permissions: SereinFlowPermission[];
  expiresAt?: string | null;
  isAdministrator?: boolean;
}
export interface KeyProjectOption {
  id: string;
  name: string;
}
export interface McpSkillResourceDto {
  key: string;
  uri: string;
  name: string;
  description: string;
  source: 'embedded' | 'disk';
}
export interface McpSkillContentDto {
  resource: McpSkillResourceDto;
  content: string;
  revision: string;
  maxBytes: number;
}
export async function listMcpSkills(secret: string): Promise<McpSkillResourceDto[]> {
  const requestResult: McpSkillResourceDto[] | PromiseLike<McpSkillResourceDto[]> = request<McpSkillResourceDto[]>('/api/environment/settings/mcp-skills', { secret });
  return requestResult;
}
export async function readMcpSkill(secret: string, key: string): Promise<McpSkillContentDto> {
  const evaluatedValue: string = `/api/environment/settings/mcp-skills/${encodeURIComponent(key)}`;
  const requestResult1: McpSkillContentDto | PromiseLike<McpSkillContentDto> = request<McpSkillContentDto>(evaluatedValue, { secret });
  return requestResult1;
}
export async function saveMcpSkill(secret: string, key: string, content: string, expectedRevision: string): Promise<McpSkillContentDto> {
  const evaluatedValue1: string = `/api/environment/settings/mcp-skills/${encodeURIComponent(key)}`;
  const requestResult2: McpSkillContentDto | PromiseLike<McpSkillContentDto> = request<McpSkillContentDto>(evaluatedValue1, {
    method: 'PUT', secret, body: { content, expectedRevision },
  });
  return requestResult2;
}
export async function resetMcpSkill(secret: string, key: string, expectedRevision: string): Promise<McpSkillContentDto> {
  const evaluatedValue2: string = `/api/environment/settings/mcp-skills/${encodeURIComponent(key)}/reset`;
  const requestResult3: McpSkillContentDto | PromiseLike<McpSkillContentDto> = request<McpSkillContentDto>(evaluatedValue2, {
    method: 'POST', secret, body: { expectedRevision },
  });
  return requestResult3;
}
const apiBaseUrl = (import.meta.env.VITE_API_BASE_URL ?? '').replace(/\/$/, '');
export async function setupApiKey(): Promise<CreatedApiKeyDto> {
  const requestResult4: CreatedApiKeyDto | PromiseLike<CreatedApiKeyDto> = request<CreatedApiKeyDto>('/api/security/api-keys/initial-admin', { method: 'POST' });
  return requestResult4;
}
export async function listApiKeys(secret: string): Promise<ApiKeyDto[]> {
  const requestResult5: ApiKeyDto[] | PromiseLike<ApiKeyDto[]> = request<ApiKeyDto[]>('/api/security/api-keys', { secret });
  return requestResult5;
}
export async function createApiKey(secret: string, body: CreateApiKeyRequest): Promise<CreatedApiKeyDto> {
  const requestResult6: CreatedApiKeyDto | PromiseLike<CreatedApiKeyDto> = request<CreatedApiKeyDto>('/api/security/api-keys', {
    method: 'POST',
    secret,
    body,
  });
  return requestResult6;
}
export async function rotateApiKey(secret: string, keyId: string): Promise<RotatedApiKeyDto> {
  const evaluatedValue4: string = `/api/security/api-keys/${encodeURIComponent(keyId)}/rotate`;
  const requestResult7: RotatedApiKeyDto | PromiseLike<RotatedApiKeyDto> = request<RotatedApiKeyDto>(evaluatedValue4, {
    method: 'POST',
    secret,
  });
  return requestResult7;
}
export async function revokeApiKey(secret: string, keyId: string): Promise<ApiKeyDto> {
  const evaluatedValue5: string = `/api/security/api-keys/${encodeURIComponent(keyId)}`;
  const requestResult8: ApiKeyDto | PromiseLike<ApiKeyDto> = request<ApiKeyDto>(evaluatedValue5, {
    method: 'DELETE',
    secret,
  });
  return requestResult8;
}
export function keyProjectOptions(workspaces: ProjectWorkspaceDto[]): KeyProjectOption[] {
  const filterResult = workspaces
    .filter((workspace) => workspace.project.status !== 'archived');
  const mapResult: KeyProjectOption[] = filterResult.map((workspace) => ({ id: workspace.project.id, name: workspace.project.name }));
  return mapResult;
}
async function request<T>(path: string, options: {
  method?: 'POST' | 'PUT' | 'DELETE';
  secret?: string;
  body?: unknown;
} = {}): Promise<T> {
  const headers: Record<string, string> = {};
  if (options.body !== undefined) {
    headers['content-type'] = 'application/json';
  }
  if (options.secret) {
    headers.authorization = `Bearer ${options.secret}`;
  }
  const argumentValue: RequestInfo | URL = `${apiBaseUrl}${path}`;
  const evaluatedValue7: RequestInit | undefined = {
    method: options.method,
    headers: Object.keys(headers).length > 0 ? headers : undefined,
    body: options.body === undefined ? undefined : JSON.stringify(options.body),
  };
  const response = await authenticatedFetch(argumentValue, evaluatedValue7);
  if (response.ok) {
    const payload = await response.text();
    const evaluatedValue6: T | PromiseLike<T> = (payload ? JSON.parse(payload) : undefined) as T;
    return evaluatedValue6;
  }
  const jsonResult = response.json();
  const problem = await jsonResult.catch(() => ({})) as {
    detail?: string;
    title?: string;
  };
  throw new McpApiError(response.status, problem.detail ?? problem.title ?? `MCP request failed (${response.status}).`);
}
export class McpApiError extends Error {
  public readonly status: number;
  constructor(status: number, message: string) {
    super(message);
    this.status = status;
    this.name = 'McpApiError';
  }
}
