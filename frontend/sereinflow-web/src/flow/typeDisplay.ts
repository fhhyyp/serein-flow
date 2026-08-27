const keywordTypeNames: Record<string, string> = {
  'System.Boolean': 'bool',
  'System.Byte': 'byte',
  'System.SByte': 'sbyte',
  'System.Char': 'char',
  'System.Decimal': 'decimal',
  'System.Double': 'double',
  'System.Single': 'float',
  'System.Int16': 'short',
  'System.UInt16': 'ushort',
  'System.Int32': 'int',
  'System.UInt32': 'uint',
  'System.Int64': 'long',
  'System.UInt64': 'ulong',
  'System.Object': 'object',
  'System.String': 'string',
  'System.Void': 'void',
}

const friendlyTypeNames: Record<string, string> = {
  'System.Threading.Tasks.Task': 'Task',
  'System.Threading.Tasks.ValueTask': 'ValueTask',
  'System.DateTime': 'DateTime',
  'System.DateTimeOffset': 'DateTimeOffset',
  'System.TimeSpan': 'TimeSpan',
  'System.Guid': 'Guid',
  'System.Nullable': 'Nullable',
}

/**
 * Converts CLR/C# type metadata into concise, readable node labels.
 * 将 CLR/C# 类型元数据转换为简洁、易读的节点标签。
 */
export function formatNodeType(typeName?: string | null): string {
  const normalized = typeName?.trim()
  if (!normalized) {
    return ''
  }

  return formatType(normalized.replace(/^global::/, ''))
}

function formatType(typeName: string): string {
  if (typeName.endsWith('?')) {
    return `${formatType(typeName.slice(0, -1))}?`
  }

  const arraySuffix = takeArraySuffix(typeName)
  const generic = splitGenericType(arraySuffix.value)
  const simpleName = formatSimpleName(generic.name)
  const argumentsText = generic.arguments.map(formatType)

  if (simpleName === 'Nullable' && argumentsText.length === 1) {
    return `${argumentsText[0]}?${arraySuffix.suffix}`
  }

  const display = argumentsText.length > 0
    ? `${simpleName}<${argumentsText.join(', ')}>`
    : simpleName
  return `${display}${arraySuffix.suffix}`
}

function takeArraySuffix(typeName: string): { value: string; suffix: string } {
  let value = typeName.trim()
  let suffix = ''
  while (value.endsWith('[]')) {
    value = value.slice(0, -2).trim()
    suffix += '[]'
  }

  return { value, suffix }
}

function splitGenericType(typeName: string): { name: string; arguments: string[] } {
  const genericStart = typeName.indexOf('<')
  if (genericStart < 0 || !typeName.endsWith('>')) {
    return { name: removeGenericArity(typeName), arguments: [] }
  }

  return {
    name: removeGenericArity(typeName.slice(0, genericStart).trim()),
    arguments: splitTopLevelArguments(typeName.slice(genericStart + 1, -1)),
  }
}

function splitTopLevelArguments(value: string): string[] {
  const argumentsText: string[] = []
  let depth = 0
  let start = 0

  for (let index = 0; index < value.length; index += 1) {
    const character = value[index]
    if (character === '<') {
      depth += 1
    } else if (character === '>') {
      depth -= 1
    } else if (character === ',' && depth === 0) {
      argumentsText.push(value.slice(start, index).trim())
      start = index + 1
    }
  }

  const last = value.slice(start).trim()
  if (last) {
    argumentsText.push(last)
  }

  return argumentsText
}

function formatSimpleName(typeName: string): string {
  const normalized = typeName.trim().replace(/\+/g, '.')
  const keyword = keywordTypeNames[normalized]
  if (keyword) {
    return keyword
  }

  const friendly = friendlyTypeNames[normalized]
  if (friendly) {
    return friendly
  }

  const finalSegment = normalized.slice(normalized.lastIndexOf('.') + 1)
  return finalSegment || normalized
}

function removeGenericArity(typeName: string): string {
  return typeName.replace(/`\d+$/, '')
}
