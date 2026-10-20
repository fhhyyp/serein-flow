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
};
const friendlyTypeNames: Record<string, string> = {
  'System.Threading.Tasks.Task': 'Task',
  'System.Threading.Tasks.ValueTask': 'ValueTask',
  'System.DateTime': 'DateTime',
  'System.DateTimeOffset': 'DateTimeOffset',
  'System.TimeSpan': 'TimeSpan',
  'System.Guid': 'Guid',
  'System.Nullable': 'Nullable',
};
/**
 * Converts CLR/C# type metadata into concise, readable node labels.
 * 将 CLR/C# 类型元数据转换为简洁、易读的节点标签。
 */
export function formatNodeType(typeName?: string | null): string {
  const normalized = typeName?.trim();
  if (!normalized) {
    return '';
  }
  const replaceResult: string = normalized.replace(/^global::/, '');
  const formatTypeResult: string = formatType(replaceResult);
  return formatTypeResult;
}
function formatType(typeName: string): string {
  if (typeName.endsWith('?')) {
    const resolveFormatTypeResult = () => {
      const sliceResult3: string = typeName.slice(0, -1);
      const formatTypeResult1 = formatType(sliceResult3);
      return formatTypeResult1;
    };
    const evaluatedValue: string = `${resolveFormatTypeResult()}?`;
    return evaluatedValue;
  }
  const arraySuffix = takeArraySuffix(typeName);
  const generic = splitGenericType(arraySuffix.value);
  const simpleName = formatSimpleName(generic.name);
  const argumentsText = generic.arguments.map(formatType);
  if (simpleName === 'Nullable' && argumentsText.length === 1) {
    return `${argumentsText[0]}?${arraySuffix.suffix}`;
  }
  const display = argumentsText.length > 0
    ? `${simpleName}<${argumentsText.join(', ')}>`
    : simpleName;
  return `${display}${arraySuffix.suffix}`;
}
function takeArraySuffix(typeName: string): {
  value: string;
  suffix: string;
} {
  let value = typeName.trim();
  let suffix = '';
  while (value.endsWith('[]')) {
    const sliceResult = value.slice(0, -2);
    value = sliceResult.trim();
    suffix += '[]';
  }
  return { value, suffix };
}
function splitGenericType(typeName: string): {
  name: string;
  arguments: string[];
} {
  const genericStart = typeName.indexOf('<');
  if (genericStart < 0 || !typeName.endsWith('>')) {
    const evaluatedValue1: {
      name: string;
      arguments: string[];
    } = { name: removeGenericArity(typeName), arguments: [] };
    return evaluatedValue1;
  }
  const resolveRemoveGenericArityResult = () => {
    const sliceResult4 = typeName.slice(0, genericStart);
    const trimResult2: string = sliceResult4.trim();
    const removeGenericArityResult: string = removeGenericArity(trimResult2);
    return removeGenericArityResult;
  };
  const resolveSplitTopLevelArgumentsResult = () => {
    const sliceResult5: string = typeName.slice(genericStart + 1, -1);
    const splitTopLevelArgumentsResult: string[] = splitTopLevelArguments(sliceResult5);
    return splitTopLevelArgumentsResult;
  };
  const evaluatedValue2: {
    name: string;
    arguments: string[];
  } = {
    name: resolveRemoveGenericArityResult(),
    arguments: resolveSplitTopLevelArgumentsResult(),
  };
  return evaluatedValue2;
}
function splitTopLevelArguments(value: string): string[] {
  const argumentsText: string[] = [];
  let depth = 0;
  let start = 0;
  for (let index = 0; index < value.length; index += 1) {
    const character = value[index];
    if (character === '<') {
      depth += 1;
    }
    else if (character === '>') {
      depth -= 1;
    }
    else if (character === ',' && depth === 0) {
      const sliceResult1 = value.slice(start, index);
      const trimResult: string = sliceResult1.trim();
      argumentsText.push(trimResult);
      start = index + 1;
    }
  }
  const sliceResult2 = value.slice(start);
  const last = sliceResult2.trim();
  if (last) {
    argumentsText.push(last);
  }
  return argumentsText;
}
function formatSimpleName(typeName: string): string {
  const trimResult1 = typeName.trim();
  const normalized = trimResult1.replace(/\+/g, '.');
  const keyword = keywordTypeNames[normalized];
  if (keyword) {
    return keyword;
  }
  const friendly = friendlyTypeNames[normalized];
  if (friendly) {
    return friendly;
  }
  const evaluatedValue3: number | undefined = normalized.lastIndexOf('.') + 1;
  const finalSegment = normalized.slice(evaluatedValue3);
  return finalSegment || normalized;
}
function removeGenericArity(typeName: string): string {
  const replaceResult1: string = typeName.replace(/`\d+$/, '');
  return replaceResult1;
}
