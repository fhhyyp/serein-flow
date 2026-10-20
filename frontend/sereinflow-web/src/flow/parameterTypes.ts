import type { EnumParameterMetadata } from './types';
export function isBooleanParameterType(type: string | undefined): boolean {
  const compactTypeName = type?.replaceAll(/\s/g, '');
  let normalized = compactTypeName?.toLowerCase() ?? '';
  const nullableMatch = /^(?:system\.)?nullable(?:`1)?<(.+)>$/.exec(normalized);
  if (nullableMatch) {
    normalized = nullableMatch[1]!;
  }
  return normalized === 'bool'
    || normalized === 'boolean'
    || normalized === 'system.boolean'
    || normalized === 'bool?'
    || normalized === 'boolean?'
    || normalized === 'system.boolean?';
}
export function normalizeBooleanLiteralValue(value: string | undefined): 'true' | 'false' | '' {
  const trimmedValue = value?.trim();
  const normalizedValue = trimmedValue?.toLowerCase();
  switch (normalizedValue) {
    case 'true':
    case '1':
      return 'true';
    case 'false':
    case '0':
      return 'false';
    default:
      return '';
  }
}
export function hasEnumOptions(metadata: EnumParameterMetadata | undefined): metadata is EnumParameterMetadata {
  const booleanResult: boolean = Boolean(metadata?.typeName && metadata.underlyingType && metadata.options.length);
  return booleanResult;
}
export function parseEnumLiteralValue(value: string | undefined): string[] {
  if (!value?.trim()) {
    return [];
  }
  const splitResult = value
    .split(',');
  const mapResult = splitResult.map((item) => {
    const trimResult: string = item.trim();
    return trimResult;
  });
  const filterResult: string[] = mapResult.filter(Boolean);
  return filterResult;
}
export function isEnumOptionSelected(value: string | undefined, optionName: string): boolean {
  const parseEnumLiteralValueResult = parseEnumLiteralValue(value);
  const someResult: boolean = parseEnumLiteralValueResult.some((item) => {
    const evaluatedValue: boolean = item.localeCompare(optionName, undefined, { sensitivity: 'accent' }) === 0;
    return evaluatedValue;
  });
  return someResult;
}
export function updateFlagsLiteralValue(metadata: EnumParameterMetadata, value: string | undefined, optionName: string, isSelected: boolean): string {
  const mapResult1: Iterable<readonly [
    string,
    import("./types").EnumValueOption
  ]> | null | undefined = metadata.options.map((option) => {
    const evaluatedValue1: [
      string,
      import("./types").EnumValueOption
    ] = [option.name.toLocaleLowerCase(), option];
    return evaluatedValue1;
  });
  const knownOptions = new Map(mapResult1);
  const parseEnumLiteralValueResult1 = parseEnumLiteralValue(value);
  const mapResult2 = parseEnumLiteralValueResult1.map((name) => {
    const normalizedOptionName = name.toLocaleLowerCase();
    const matchingOption = knownOptions.get(normalizedOptionName);
    const evaluatedValue2: string | undefined = matchingOption?.name;
    return evaluatedValue2;
  });
  const filterResult1: Iterable<string> | null | undefined = mapResult2.filter((name): name is string => {
    const booleanResult1: boolean = Boolean(name);
    return booleanResult1;
  });
  const selected = new Set(filterResult1);
  const toLocaleLowerCaseResult: string = optionName.toLocaleLowerCase();
  const option = knownOptions.get(toLocaleLowerCaseResult);
  if (!option) {
    const serializeEnumLiteralValueResult: string = serializeEnumLiteralValue(metadata, selected);
    return serializeEnumLiteralValueResult;
  }
  const filterResult2 = metadata.options
    .filter((candidate) => candidate.numericValue === '0');
  const mapResult3: Iterable<string> | null | undefined = filterResult2.map((candidate) => candidate.name);
  const zeroValueNames = new Set(mapResult3);
  if (isSelected) {
    if (zeroValueNames.has(option.name)) {
      selected.clear();
    }
    else {
      for (const zeroValueName of zeroValueNames) {
        selected.delete(zeroValueName);
      }
    }
    selected.add(option.name);
  }
  else {
    selected.delete(option.name);
  }
  const serializeEnumLiteralValueResult1: string = serializeEnumLiteralValue(metadata, selected);
  return serializeEnumLiteralValueResult1;
}
export function serializeEnumLiteralValue(metadata: EnumParameterMetadata, selected: ReadonlySet<string>): string {
  const filterResult3 = metadata.options
    .filter((option) => {
    const hasResult: boolean = selected.has(option.name);
    return hasResult;
  });
  const mapResult4 = filterResult3.map((option) => option.name);
  const joinResult: string = mapResult4.join(', ');
  return joinResult;
}
