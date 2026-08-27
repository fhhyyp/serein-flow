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
