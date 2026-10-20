import type { MethodParameter } from './types';
export type VariadicParameterMode = 'expanded' | 'collection';
export type VariadicModeConversionResult = {
  ok: true;
  parameters: MethodParameter[];
  targetParameterIdRemap: ReadonlyMap<string, string>;
} | {
  ok: false;
};
export function convertVariadicParameterMode(parameters: readonly MethodParameter[], groupId: string, mode: VariadicParameterMode): VariadicModeConversionResult {
  const members = parameters.filter((parameter) => parameter.variadicGroupId === groupId);
  if (members.length === 0 || members.every((parameter) => (parameter.variadicMode ?? 'expanded') === mode)) {
    const evaluatedValue: VariadicModeConversionResult = { ok: true, parameters: [...parameters], targetParameterIdRemap: new Map() };
    return evaluatedValue;
  }
  const evaluatedValue1: VariadicModeConversionResult = mode === 'collection'
    ? collapseVariadicParameters(parameters, members)
    : expandVariadicParameters(parameters, members);
  return evaluatedValue1;
}
function collapseVariadicParameters(parameters: readonly MethodParameter[], members: readonly MethodParameter[]): VariadicModeConversionResult {
  const configuredLiteralMembers = members.filter((parameter) => {
    const evaluatedValue2: boolean = parameter.source === 'literal' && hasLiteralValue(parameter);
    return evaluatedValue2;
  });
  const dynamicMembers = members.filter((parameter) => parameter.source !== 'literal');
  if (dynamicMembers.length > 1 || (dynamicMembers.length === 1 && configuredLiteralMembers.length > 0)) {
    return { ok: false };
  }
  const retained = dynamicMembers[0] ?? members[0]!;
  const resolveStringifyResult = () => {
    const mapResult1 = configuredLiteralMembers.map(literalValueForCollection);
    const stringifyResult = JSON.stringify(mapResult1);
    return stringifyResult;
  };
  const collapsed = dynamicMembers.length === 1
    ? { ...retained, variadicMode: 'collection' as const }
    : {
      ...retained,
      source: 'literal' as const,
      literalValue: resolveStringifyResult(),
      projectInputKey: undefined,
      expression: undefined,
      sourceNodeId: undefined,
      sourcePortId: undefined,
      variadicMode: 'collection' as const,
    };
  const replaceVariadicMembersResult: VariadicModeConversionResult = replaceVariadicMembers(parameters, members, [collapsed]);
  return replaceVariadicMembersResult;
}
function expandVariadicParameters(parameters: readonly MethodParameter[], members: readonly MethodParameter[]): VariadicModeConversionResult {
  if (members.length !== 1 || members[0]!.source !== 'literal') {
    return { ok: false };
  }
  const collection = members[0]!;
  if (!hasLiteralValue(collection)) {
    const replaceVariadicMembersResult1: VariadicModeConversionResult = replaceVariadicMembers(parameters, members, [{ ...collection, variadicMode: 'expanded' }]);
    return replaceVariadicMembersResult1;
  }
  let values: unknown[];
  try {
    const parsed = JSON.parse(collection.literalValue!);
    if (!Array.isArray(parsed)) {
      return { ok: false };
    }
    values = parsed;
  }
  catch {
    return { ok: false };
  }
  const expanded = values.length === 0
    ? [{ ...collection, literalValue: '', variadicMode: 'expanded' as const }]
    : values.map((value, index) => {
      const evaluatedValue3: {
        id: string;
        nameKey: string;
        name: string | undefined;
        literalValue: string;
        variadicMode: "expanded";
        valueKind: string;
        required?: boolean;
        type?: string;
        description?: string;
        inputMode?: import("./types").ParameterInputMode;
        position?: {
          x: number;
          y: number;
        };
        source: import("./types").ParameterSource;
        projectInputKey?: string;
        expression?: string;
        sourceNodeId?: string;
        sourcePortId?: string;
        isVariadic?: boolean;
        variadicGroupId?: string;
        elementType?: string;
        enumMetadata?: import("./types").EnumParameterMetadata;
      } = ({
        ...collection,
        id: index === 0 ? collection.id : `${collection.variadicGroupId}-${index + 1}`,
        nameKey: index === 0 ? collection.nameKey : `${collection.nameKey} ${index + 1}`,
        name: index === 0 ? collection.name : `${collection.name ?? collection.nameKey} ${index + 1}`,
        literalValue: JSON.stringify(value),
        variadicMode: 'expanded' as const,
      });
      return evaluatedValue3;
    });
  const replaceVariadicMembersResult2: VariadicModeConversionResult = replaceVariadicMembers(parameters, members, expanded);
  return replaceVariadicMembersResult2;
}
function replaceVariadicMembers(parameters: readonly MethodParameter[], members: readonly MethodParameter[], replacement: readonly MethodParameter[]): VariadicModeConversionResult {
  const mapResult: Iterable<string> | null | undefined = members.map((parameter) => parameter.id);
  const memberIds = new Set(mapResult);
  const firstIndex = parameters.findIndex((parameter) => {
    const hasResult: boolean = memberIds.has(parameter.id);
    return hasResult;
  });
  const resolveFilterResult = () => {
    const sliceResult = parameters.slice(firstIndex);
    const filterResult = sliceResult.filter((parameter) => {
      const evaluatedValue4: boolean = !memberIds.has(parameter.id);
      return evaluatedValue4;
    });
    return filterResult;
  };
  const next = [
    ...parameters.slice(0, firstIndex),
    ...replacement,
    ...resolveFilterResult(),
  ];
  const targetParameterIdRemap = new Map<string, string>();
  for (const member of members) {
    if (member.id !== replacement[0]!.id) {
      targetParameterIdRemap.set(member.id, replacement[0]!.id);
    }
  }
  return { ok: true, parameters: next, targetParameterIdRemap };
}
function hasLiteralValue(parameter: MethodParameter): boolean {
  const evaluatedValue5: boolean = parameter.literalValue?.trim().length !== 0;
  return evaluatedValue5;
}
function literalValueForCollection(parameter: MethodParameter): unknown {
  const literalValue = parameter.literalValue!;
  try {
    const parseResult = JSON.parse(literalValue);
    return parseResult;
  }
  catch {
    return literalValue;
  }
}
