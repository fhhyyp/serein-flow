import type { NodeCategory, NodeKind, ReferenceNodeKind } from './types'

/**
 * The node catalog is a server-owned contract. This module intentionally
 * contains no built-in node instances or demo definitions; it only describes
 * the shape and classification used by the editor when a catalog is supplied.
 * 节点目录是由服务端管理的契约。本模块不包含内置节点实例或示例定义，
 * 仅描述编辑器接入目录时使用的数据结构和分类。
 */
export const referenceNodeKinds = [
  'action',
  'flipflop',
  'script',
  'condition',
  'flowCall',
] as const satisfies readonly ReferenceNodeKind[]

export const methodNodeKinds = ['action', 'flipflop'] as const satisfies readonly ReferenceNodeKind[]
export const basicNodeKinds = ['script', 'condition', 'flowCall'] as const satisfies readonly ReferenceNodeKind[]
export const allNodeKinds = [...referenceNodeKinds] as const satisfies readonly NodeKind[]

export interface NodeParameterDefinition {
  name: string
  type: string
  description?: string
}

export interface NodeDefinition {
  id: string
  type: ReferenceNodeKind
  className: string
  methodName: string
  parameters: NodeParameterDefinition[]
  returnType: string
  description?: string
  libraryId?: string
  dllName?: string
  dllVersion?: string
}

export interface LibraryDefinition {
  id: string
  name: string
  description?: string
  fileName: string
  version: string
  dllPath?: string
  uploadedAt?: string
  nodeDefinitions: NodeDefinition[]
}

export function nodeCategory(kind: ReferenceNodeKind): NodeCategory {
  return methodNodeKinds.includes(kind as (typeof methodNodeKinds)[number]) ? 'method' : 'basic'
}

export function isNodeKind(value: unknown): value is NodeKind {
  return typeof value === 'string' && (allNodeKinds as readonly string[]).includes(value)
}
