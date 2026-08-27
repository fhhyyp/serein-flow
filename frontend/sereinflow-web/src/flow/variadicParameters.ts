import type { MethodParameter } from './types'

export type VariadicParameterMode = 'expanded' | 'collection'

export type VariadicModeConversionResult =
  | {
    ok: true
    parameters: MethodParameter[]
    targetParameterIdRemap: ReadonlyMap<string, string>
  }
  | {
    ok: false
  }

export function convertVariadicParameterMode(
  parameters: readonly MethodParameter[],
  groupId: string,
  mode: VariadicParameterMode,
): VariadicModeConversionResult {
  const members = parameters.filter((parameter) => parameter.variadicGroupId === groupId)
  if (members.length === 0 || members.every((parameter) => (parameter.variadicMode ?? 'expanded') === mode)) {
    return { ok: true, parameters: [...parameters], targetParameterIdRemap: new Map() }
  }

  return mode === 'collection'
    ? collapseVariadicParameters(parameters, members)
    : expandVariadicParameters(parameters, members)
}

function collapseVariadicParameters(
  parameters: readonly MethodParameter[],
  members: readonly MethodParameter[],
): VariadicModeConversionResult {
  const configuredLiteralMembers = members.filter((parameter) =>
    parameter.source === 'literal' && hasLiteralValue(parameter))
  const dynamicMembers = members.filter((parameter) => parameter.source !== 'literal')

  if (dynamicMembers.length > 1 || (dynamicMembers.length === 1 && configuredLiteralMembers.length > 0)) {
    return { ok: false }
  }

  const retained = dynamicMembers[0] ?? members[0]!
  const collapsed = dynamicMembers.length === 1
    ? { ...retained, variadicMode: 'collection' as const }
    : {
      ...retained,
      source: 'literal' as const,
      literalValue: JSON.stringify(configuredLiteralMembers.map(literalValueForCollection)),
      projectInputKey: undefined,
      expression: undefined,
      sourceNodeId: undefined,
      sourcePortId: undefined,
      variadicMode: 'collection' as const,
    }

  return replaceVariadicMembers(parameters, members, [collapsed])
}

function expandVariadicParameters(
  parameters: readonly MethodParameter[],
  members: readonly MethodParameter[],
): VariadicModeConversionResult {
  if (members.length !== 1 || members[0]!.source !== 'literal') {
    return { ok: false }
  }

  const collection = members[0]!
  if (!hasLiteralValue(collection)) {
    return replaceVariadicMembers(parameters, members, [{ ...collection, variadicMode: 'expanded' }])
  }

  let values: unknown[]
  try {
    const parsed = JSON.parse(collection.literalValue!)
    if (!Array.isArray(parsed)) {
      return { ok: false }
    }
    values = parsed
  } catch {
    return { ok: false }
  }

  const expanded = values.length === 0
    ? [{ ...collection, literalValue: '', variadicMode: 'expanded' as const }]
    : values.map((value, index) => ({
      ...collection,
      id: index === 0 ? collection.id : `${collection.variadicGroupId}-${index + 1}`,
      nameKey: index === 0 ? collection.nameKey : `${collection.nameKey} ${index + 1}`,
      name: index === 0 ? collection.name : `${collection.name ?? collection.nameKey} ${index + 1}`,
      literalValue: JSON.stringify(value),
      variadicMode: 'expanded' as const,
    }))

  return replaceVariadicMembers(parameters, members, expanded)
}

function replaceVariadicMembers(
  parameters: readonly MethodParameter[],
  members: readonly MethodParameter[],
  replacement: readonly MethodParameter[],
): VariadicModeConversionResult {
  const memberIds = new Set(members.map((parameter) => parameter.id))
  const firstIndex = parameters.findIndex((parameter) => memberIds.has(parameter.id))
  const next = [
    ...parameters.slice(0, firstIndex),
    ...replacement,
    ...parameters.slice(firstIndex).filter((parameter) => !memberIds.has(parameter.id)),
  ]
  const targetParameterIdRemap = new Map<string, string>()
  for (const member of members) {
    if (member.id !== replacement[0]!.id) {
      targetParameterIdRemap.set(member.id, replacement[0]!.id)
    }
  }

  return { ok: true, parameters: next, targetParameterIdRemap }
}

function hasLiteralValue(parameter: MethodParameter): boolean {
  return parameter.literalValue?.trim().length !== 0
}

function literalValueForCollection(parameter: MethodParameter): unknown {
  const literalValue = parameter.literalValue!
  try {
    return JSON.parse(literalValue)
  } catch {
    return literalValue
  }
}
