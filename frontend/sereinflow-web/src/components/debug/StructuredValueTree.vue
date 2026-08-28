<script setup lang="ts">
import { computed } from 'vue'
import { Braces, ChevronRight, List } from 'lucide-vue-next'

defineOptions({ name: 'StructuredValueTree' })

const props = withDefaults(defineProps<{
  value: unknown
  label?: string
  depth?: number
  maxDepth?: number
  maxEntries?: number
  maxStringLength?: number
}>(), {
  depth: 0,
  maxDepth: 5,
  maxEntries: 50,
  maxStringLength: 1_200,
})

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
]

const isSensitive = computed(() => {
  const key = props.label?.toLocaleLowerCase()
  return key !== undefined && sensitiveKeyFragments.some((fragment) => key.includes(fragment))
})
const arrayValue = computed<unknown[]>(() => Array.isArray(props.value) ? props.value : [])
const objectValue = computed<Record<string, unknown>>(() => isRecord(props.value) ? props.value : {})
const isArray = computed(() => Array.isArray(props.value))
const isObject = computed(() => isRecord(props.value))
const isStructured = computed(() => isArray.value || isObject.value)
const entries = computed(() => {
  if (isArray.value) return arrayValue.value.slice(0, props.maxEntries).map((value, index) => [String(index), value] as const)
  if (isObject.value) return Object.entries(objectValue.value).slice(0, props.maxEntries)
  return []
})
const entryCount = computed(() => isArray.value ? arrayValue.value.length : Object.keys(objectValue.value).length)
const isTruncated = computed(() => entryCount.value > entries.value.length)
const limitReached = computed(() => props.depth >= props.maxDepth && isStructured.value)
const summary = computed(() => {
  if (isArray.value) return `[${arrayValue.value.length}]`
  if (isObject.value) return `{${Object.keys(objectValue.value).length}}`
  return formatPrimitive(props.value, props.maxStringLength)
})

function isRecord(value: unknown): value is Record<string, unknown> {
  return typeof value === 'object' && value !== null && !Array.isArray(value)
}

function formatPrimitive(value: unknown, maxLength: number): string {
  if (value === null) return 'null'
  if (value === undefined) return 'undefined'
  if (typeof value === 'string') {
    const quoted = JSON.stringify(value)
    return quoted.length > maxLength ? `${quoted.slice(0, maxLength)}..."` : quoted
  }
  if (typeof value === 'number' || typeof value === 'boolean' || typeof value === 'bigint') return String(value)
  return String(value)
}
</script>

<template>
  <div class="structured-value-tree" :class="{ 'structured-value-tree--nested': props.depth > 0 }">
    <template v-if="isSensitive">
      <span v-if="props.label" class="structured-value-tree__key">{{ props.label }}</span>
      <code class="structured-value-tree__masked">[masked]</code>
    </template>

    <template v-else-if="isStructured">
      <details class="structured-value-tree__details" :open="props.depth < 2">
        <summary>
          <ChevronRight :size="13" aria-hidden="true" />
          <span v-if="props.label" class="structured-value-tree__key">{{ props.label }}</span>
          <Braces v-if="isObject" :size="13" aria-hidden="true" />
          <List v-else :size="13" aria-hidden="true" />
          <code>{{ summary }}</code>
        </summary>
        <p v-if="limitReached" class="structured-value-tree__limit">[depth limit]</p>
        <div v-else class="structured-value-tree__children">
          <StructuredValueTree
            v-for="([key, entryValue], index) in entries"
            :key="`${key}-${index}`"
            :value="entryValue"
            :label="key"
            :depth="props.depth + 1"
            :max-depth="props.maxDepth"
            :max-entries="props.maxEntries"
            :max-string-length="props.maxStringLength"
          />
          <p v-if="isTruncated" class="structured-value-tree__limit">[{{ entries.length }} of {{ entryCount }} shown]</p>
        </div>
      </details>
    </template>

    <template v-else>
      <span v-if="props.label" class="structured-value-tree__key">{{ props.label }}</span>
      <code class="structured-value-tree__primitive">{{ summary }}</code>
    </template>
  </div>
</template>
