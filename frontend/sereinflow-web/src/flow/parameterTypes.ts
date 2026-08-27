import type { EnumParameterMetadata } from './types'

export function isBooleanParameterType(type: string | undefined): boolean {
  let normalized = type?.replaceAll(/\s/g, '').toLowerCase() ?? ''
  const nullableMatch = /^(?:system\.)?nullable(?:`1)?<(.+)>$/.exec(normalized)
  if (nullableMatch) {
    normalized = nullableMatch[1]!
  }

  return normalized === 'bool'
    || normalized === 'boolean'
    || normalized === 'system.boolean'
    || normalized === 'bool?'
    || normalized === 'boolean?'
    || normalized === 'system.boolean?'
}

export function normalizeBooleanLiteralValue(value: string | undefined): 'true' | 'false' | '' {
  switch (value?.trim().toLowerCase()) {
    case 'true':
    case '1':
      return 'true'
    case 'false':
    case '0':
      return 'false'
    default:
      return ''
  }
}

export function hasEnumOptions(metadata: EnumParameterMetadata | undefined): metadata is EnumParameterMetadata {
  return Boolean(metadata?.typeName && metadata.underlyingType && metadata.options.length)
}

export function parseEnumLiteralValue(value: string | undefined): string[] {
  if (!value?.trim()) {
    return []
  }

  return value
    .split(',')
    .map((item) => item.trim())
    .filter(Boolean)
}

export function isEnumOptionSelected(value: string | undefined, optionName: string): boolean {
  return parseEnumLiteralValue(value).some((item) => item.localeCompare(optionName, undefined, { sensitivity: 'accent' }) === 0)
}

export function updateFlagsLiteralValue(
  metadata: EnumParameterMetadata,
  value: string | undefined,
  optionName: string,
  isSelected: boolean,
): string {
  const knownOptions = new Map(metadata.options.map((option) => [option.name.toLocaleLowerCase(), option]))
  const selected = new Set(
    parseEnumLiteralValue(value)
      .map((name) => knownOptions.get(name.toLocaleLowerCase())?.name)
      .filter((name): name is string => Boolean(name)),
  )
  const option = knownOptions.get(optionName.toLocaleLowerCase())
  if (!option) {
    return serializeEnumLiteralValue(metadata, selected)
  }

  const zeroValueNames = new Set(
    metadata.options
      .filter((candidate) => candidate.numericValue === '0')
      .map((candidate) => candidate.name),
  )
  if (isSelected) {
    if (zeroValueNames.has(option.name)) {
      selected.clear()
    } else {
      for (const zeroValueName of zeroValueNames) {
        selected.delete(zeroValueName)
      }
    }
    selected.add(option.name)
  } else {
    selected.delete(option.name)
  }

  return serializeEnumLiteralValue(metadata, selected)
}

export function serializeEnumLiteralValue(metadata: EnumParameterMetadata, selected: ReadonlySet<string>): string {
  return metadata.options
    .filter((option) => selected.has(option.name))
    .map((option) => option.name)
    .join(', ')
}
