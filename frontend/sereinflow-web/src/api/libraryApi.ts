import type { ApiNodeType } from './flowApi'
import { localizeMessage } from '../i18n'

export interface LibraryParameterDto {
  id: string
  name: string
  type: string
  description?: string | null
  required: boolean
}

export interface LibraryNodeDto {
  id: string
  type: ApiNodeType
  displayName: string
  description?: string | null
  libraryId: string
  className: string
  methodName: string
  dllName: string
  dllVersion: string
  returnType: string
  isAwaitable?: boolean
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
}

export interface LibraryUploadResultDto {
  library: LibraryDto
  alreadyExists: boolean
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

export async function uploadLibrary(file: File): Promise<LibraryUploadResultDto> {
  const body = new FormData()
  body.append('file', file)
  return request<LibraryUploadResultDto>('/api/libraries/upload', { method: 'POST', body })
}

export async function deleteLibrary(libraryId: string): Promise<void> {
  const response = await fetch(`${apiBaseUrl}/api/libraries/${encodeURIComponent(libraryId)}`, { method: 'DELETE' })
  if (!response.ok) {
    throw new LibraryApiError(response.status, await readError(response))
  }
}

async function request<T>(path: string, options: { method?: 'POST'; body?: BodyInit } = {}): Promise<T> {
  const response = await fetch(`${apiBaseUrl}${path}`, {
    method: options.method,
    body: options.body,
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
