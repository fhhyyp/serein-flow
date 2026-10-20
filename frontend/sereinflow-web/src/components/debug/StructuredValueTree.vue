<script setup lang="ts">
type TemplateItem<T> = T extends readonly (infer Item)[] ? Item : T extends Record<string, infer Value> ? Value : T extends number ? number : never;
import { computed } from 'vue';
import { Braces, ChevronRight, List } from 'lucide-vue-next';
defineOptions({ name: 'StructuredValueTree' });
const props = withDefaults(defineProps<{
  value: unknown;
  label?: string;
  depth?: number;
  maxDepth?: number;
  maxEntries?: number;
  maxStringLength?: number;
}>(), {
  depth: 0,
  maxDepth: 5,
  maxEntries: 50,
  maxStringLength: 1200,
});
const sensitiveKeyFragments = [
  'password',
  'passwd',
  'token',
  'secret',
  'authorization',
  'credential',
  'apikey',
  'api_key',
  'secretkey',
  '密码',
  '令牌',
  '密钥',
  '凭证',
  '授权',
];
const isSensitive = computed(() => {
  const key = props.label?.toLocaleLowerCase();
  const evaluatedValue: boolean = key !== undefined && sensitiveKeyFragments.some((fragment) => {
    const includesResult: boolean = key.includes(fragment);
    return includesResult;
  });
  return evaluatedValue;
});
const arrayValue = computed<unknown[]>(() => {
  const evaluatedValue1: any[] = Array.isArray(props.value) ? props.value : [];
  return evaluatedValue1;
});
const objectValue = computed<Record<string, unknown>>(() => {
  const evaluatedValue2: Record<string, unknown> = isRecord(props.value) ? props.value : {};
  return evaluatedValue2;
});
const isArray = computed(() => {
  const isArrayResult: boolean = Array.isArray(props.value);
  return isArrayResult;
});
const isObject = computed(() => {
  const isRecordResult: boolean = isRecord(props.value);
  return isRecordResult;
});
const isStructured = computed(() => isArray.value || isObject.value);
const entries = computed(() => {
  if (isArray.value) {
    const sliceResult = arrayValue.value.slice(0, props.maxEntries);
    const mapResult: (readonly [
      string,
      unknown
    ])[] = sliceResult.map((value, index) => {
      const evaluatedValue3: readonly [
        string,
        unknown
      ] = [String(index), value] as const;
      return evaluatedValue3;
    });
    return mapResult;
  }
  if (isObject.value) {
    const entriesResult = Object.entries(objectValue.value);
    const sliceResult1: [
      string,
      unknown
    ][] = entriesResult.slice(0, props.maxEntries);
    return sliceResult1;
  }
  return [];
});
const entryCount = computed(() => {
  const evaluatedValue4: number = isArray.value ? arrayValue.value.length : Object.keys(objectValue.value).length;
  return evaluatedValue4;
});
const isTruncated = computed(() => entryCount.value > entries.value.length);
const limitReached = computed(() => props.depth >= props.maxDepth && isStructured.value);
const summary = computed(() => {
  if (isArray.value) {
    return `[${arrayValue.value.length}]`;
  }
  if (isObject.value) {
    const evaluatedValue5: string = `{${Object.keys(objectValue.value).length}}`;
    return evaluatedValue5;
  }
  const formatPrimitiveResult: string = formatPrimitive(props.value, props.maxStringLength);
  return formatPrimitiveResult;
});
function isRecord(value: unknown): value is Record<string, unknown> {
  const evaluatedValue6: boolean = typeof value === 'object' && value !== null && !Array.isArray(value);
  return evaluatedValue6;
}
function formatPrimitive(value: unknown, maxLength: number): string {
  if (value === null) {
    return 'null';
  }
  if (value === undefined) {
    return 'undefined';
  }
  if (typeof value === 'string') {
    const quoted = JSON.stringify(value);
    const evaluatedValue7: string = quoted.length > maxLength ? `${quoted.slice(0, maxLength)}..."` : quoted;
    return evaluatedValue7;
  }
  if (typeof value === 'number' || typeof value === 'boolean' || typeof value === 'bigint') {
    const stringResult: string = String(value);
    return stringResult;
  }
  const stringResult1: string = String(value);
  return stringResult1;
}
const readDivClass = computed(() => ({ 'structured-value-tree--nested': props.depth > 0 }));
const readDetailsOpen = computed(() => (props.depth < 2));
function readStructuredValueTreeItems() {
  return (entries.value);
}
function readStructuredValueTreeKey(key: TemplateItem<ReturnType<typeof readStructuredValueTreeItems>>[0], index: number) {
  return (`${key}-${index}`);
}
const readStructuredValueTreeDepth = computed(() => (props.depth + 1));
</script>

<template>
  <div
    class="structured-value-tree"
    :class="readDivClass"
  >
    <template v-if="isSensitive">
      <span
        v-if="props.label"
        class="structured-value-tree__key"
      >{{ props.label }}</span>
      <code class="structured-value-tree__masked">[masked]</code>
    </template>
    <template v-else-if="isStructured">
      <details
        class="structured-value-tree__details"
        :open="readDetailsOpen"
      >
        <summary>
          <ChevronRight
            :size="13"
            aria-hidden="true"
           />
          <span
            v-if="props.label"
            class="structured-value-tree__key"
          >{{ props.label }}</span>
          <Braces
            v-if="isObject"
            :size="13"
            aria-hidden="true"
           />
          <List
            v-else
            :size="13"
            aria-hidden="true"
           />
          <code>{{ summary }}</code>
        </summary>
        <p
          v-if="limitReached"
          class="structured-value-tree__limit"
        >[depth limit]</p>
        <div
          v-else
          class="structured-value-tree__children"
        >
          <StructuredValueTree
            v-for="([key, entryValue], index) in readStructuredValueTreeItems()"
            :key="readStructuredValueTreeKey(key, index)"
            :value="entryValue"
            :label="key"
            :depth="readStructuredValueTreeDepth"
            :max-depth="props.maxDepth"
            :max-entries="props.maxEntries"
            :max-string-length="props.maxStringLength"
           />
          <p
            v-if="isTruncated"
            class="structured-value-tree__limit"
          >[{{ entries.length }} of {{ entryCount }} shown]</p>
        </div>
      </details>
    </template>
    <template v-else>
      <span
        v-if="props.label"
        class="structured-value-tree__key"
      >{{ props.label }}</span>
      <code class="structured-value-tree__primitive">{{ summary }}</code>
    </template>
  </div>
</template>
