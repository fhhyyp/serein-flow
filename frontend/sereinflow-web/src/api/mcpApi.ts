import type { ProjectWorkspaceDto } from './flowApi'

export type McpPermission =
  | 'project.read'
  | 'project.write'
  | 'library.read'
  | 'run.read'
  | 'debug.read'
  | 'flow.write'
  | 'debug.control'
  | 'flow.publish'
  | 'flow.rollback'
  | 'script.compile'
  | 'library.import'
  | 'library.manage'
  | 'mcp.keys.manage'
  | 'sensitive.read'

export interface McpApiKeyDto {
  id: string
  projectId?: string | null
  name: string
  keyPrefix: string
  permissions: McpPermission[]
  createdAt: string
  expiresAt?: string | null
  revokedAt?: string | null
  lastUsedAt?: string | null
  isAdministrator: boolean
}

export interface CreatedMcpApiKeyDto {
  key: McpApiKeyDto
  secret: string
}

export interface RotatedMcpApiKeyDto {
  revokedKeyId: string
  key: McpApiKeyDto
  secret?: string | null
}

export interface CreateMcpApiKeyRequestDto {
  projectId?: string | null
  name: string
  permissions: McpPermission[]
  expiresAt?: string | null
  isAdministrator?: boolean
}

export interface McpKeyProjectOption {
  id: string
  name: string
}

const apiBaseUrl = (import.meta.env.VITE_API_BASE_URL ?? '').replace(/\/$/, '')
const sessionCredentialKey = 'sereinflow.mcp.management-key'
const rememberedCredentialKey = 'sereinflow.mcp.management-key.remembered'

export function getMcpManagementCredential(): string | undefined {
  if (typeof window === 'undefined') return undefined
  try {
    return window.sessionStorage.getItem(sessionCredentialKey)
      ?? window.localStorage.getItem(rememberedCredentialKey)
      ?? undefined
  } catch {
    return undefined
  }
}

export function hasRememberedMcpManagementCredential(): boolean {
  if (typeof window === 'undefined') return false
  try {
    return Boolean(window.localStorage.getItem(rememberedCredentialKey))
  } catch {
    return false
  }
}

export function storeMcpManagementCredential(secret: string, remember: boolean): void {
  if (typeof window === 'undefined') return
  try {
    window.sessionStorage.setItem(sessionCredentialKey, secret)
    if (remember) window.localStorage.setItem(rememberedCredentialKey, secret)
    else window.localStorage.removeItem(rememberedCredentialKey)
  } catch {
    // Storage may be disabled by the browser; the current request still succeeds.
  }
}

export function clearMcpManagementCredential(): void {
  if (typeof window === 'undefined') return
  try {
    window.sessionStorage.removeItem(sessionCredentialKey)
    window.localStorage.removeItem(rememberedCredentialKey)
  } catch {
    // Ignore storage cleanup failures.
  }
}

export async function setupMcpApiKey(): Promise<CreatedMcpApiKeyDto> {
  return request<CreatedMcpApiKeyDto>('/api/environment/settings/mcp-keys/setup', { method: 'POST' })
}

export async function listMcpApiKeys(secret: string): Promise<McpApiKeyDto[]> {
  return request<McpApiKeyDto[]>('/api/environment/settings/mcp-keys', { secret })
}

export async function createMcpApiKey(
  secret: string,
  body: CreateMcpApiKeyRequestDto,
): Promise<CreatedMcpApiKeyDto> {
  return request<CreatedMcpApiKeyDto>('/api/environment/settings/mcp-keys', {
    method: 'POST',
    secret,
    body,
  })
}

export async function rotateMcpApiKey(secret: string, keyId: string): Promise<RotatedMcpApiKeyDto> {
  return request<RotatedMcpApiKeyDto>(`/api/environment/settings/mcp-keys/${encodeURIComponent(keyId)}/rotate`, {
    method: 'POST',
    secret,
  })
}

export async function revokeMcpApiKey(secret: string, keyId: string): Promise<McpApiKeyDto> {
  return request<McpApiKeyDto>(`/api/environment/settings/mcp-keys/${encodeURIComponent(keyId)}`, {
    method: 'DELETE',
    secret,
  })
}

export function mcpKeyProjectOptions(workspaces: ProjectWorkspaceDto[]): McpKeyProjectOption[] {
  return workspaces
    .filter((workspace) => workspace.project.status !== 'archived')
    .map((workspace) => ({ id: workspace.project.id, name: workspace.project.name }))
}

async function request<T>(
  path: string,
  options: { method?: 'POST' | 'DELETE'; secret?: string; body?: unknown } = {},
): Promise<T> {
  const headers: Record<string, string> = {}
  if (options.body !== undefined) headers['content-type'] = 'application/json'
  if (options.secret) headers.authorization = `Bearer ${options.secret}`
  const response = await fetch(`${apiBaseUrl}${path}`, {
    method: options.method,
    headers: Object.keys(headers).length > 0 ? headers : undefined,
    body: options.body === undefined ? undefined : JSON.stringify(options.body),
  })
  if (response.ok) {
    const payload = await response.text()
    return (payload ? JSON.parse(payload) : undefined) as T
  }

  const problem = await response.json().catch(() => ({})) as { detail?: string; title?: string }
  throw new McpApiError(response.status, problem.detail ?? problem.title ?? `MCP request failed (${response.status}).`)
}

export class McpApiError extends Error {
  public readonly status: number

  constructor(status: number, message: string) {
    super(message)
    this.status = status
    this.name = 'McpApiError'
  }
}
