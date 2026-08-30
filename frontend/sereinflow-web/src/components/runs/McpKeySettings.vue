<script setup lang="ts">
import { computed, onMounted, reactive, ref } from 'vue'
import { Clipboard, KeyRound, LockKeyhole, Plus, RefreshCw, ShieldCheck, Trash2, Unplug, X } from 'lucide-vue-next'
import {
  clearMcpManagementCredential,
  createMcpApiKey,
  getMcpManagementCredential,
  hasRememberedMcpManagementCredential,
  listMcpApiKeys,
  mcpKeyProjectOptions,
  McpApiError,
  revokeMcpApiKey,
  rotateMcpApiKey,
  setupMcpApiKey,
  storeMcpManagementCredential,
  type CreateMcpApiKeyRequestDto,
  type McpApiKeyDto,
  type McpPermission,
} from '../../api/mcpApi'
import type { ProjectWorkspaceDto } from '../../api/flowApi'
import { t } from '../../i18n'

const props = defineProps<{ projectWorkspaces: ProjectWorkspaceDto[] }>()

const managementSecret = ref(getMcpManagementCredential())
const rememberCredential = ref(hasRememberedMcpManagementCredential())
const keys = ref<McpApiKeyDto[]>([])
const isLoading = ref(false)
const isSaving = ref(false)
const error = ref('')
const notice = ref('')
const revealedSecret = ref('')
const connected = computed(() => Boolean(managementSecret.value))
const projectOptions = computed(() => mcpKeyProjectOptions(props.projectWorkspaces))

const createForm = reactive<CreateMcpApiKeyRequestDto>({
  projectId: '',
  name: 'SereinFlow MCP Client',
  permissions: ['project.read', 'library.read', 'script.compile'],
  expiresAt: null,
  isAdministrator: false,
})

const permissionOptions: Array<{ value: McpPermission; label: string; hint: string }> = [
  { value: 'project.read', label: '项目读取', hint: '查看项目和流程信息' },
  { value: 'library.read', label: '类库读取', hint: '检查类库和节点目录' },
  { value: 'script.compile', label: '脚本编译', hint: '检查 SereinLang 语法' },
  { value: 'run.read', label: '运行读取', hint: '查看运行状态和输出' },
  { value: 'debug.read', label: '调试读取', hint: '查看调试会话信息' },
  { value: 'flow.write', label: '流程写入', hint: '修改流程草稿' },
  { value: 'flow.publish', label: '流程发布', hint: '发布生产版本' },
  { value: 'flow.rollback', label: '流程回滚', hint: '回滚流程版本' },
  { value: 'library.import', label: '类库导入', hint: '导入类库包' },
  { value: 'library.manage', label: '类库管理', hint: '管理项目类库引用' },
  { value: 'debug.control', label: '调试控制', hint: '继续、单步或停止调试' },
  { value: 'sensitive.read', label: '敏感内容读取', hint: '读取脚本源码或流程字面量' },
]

async function loadKeys(): Promise<void> {
  if (!managementSecret.value) return
  isLoading.value = true
  error.value = ''
  try {
    keys.value = await listMcpApiKeys(managementSecret.value)
  } catch (exception) {
    if (exception instanceof McpApiError && exception.status === 401) {
      clearCredential()
      error.value = t('console.mcpKeyUnauthorized')
    } else {
      error.value = errorMessage(exception)
    }
  } finally {
    isLoading.value = false
  }
}

async function setupKey(): Promise<void> {
  isSaving.value = true
  error.value = ''
  notice.value = ''
  try {
    const created = await setupMcpApiKey()
    managementSecret.value = created.secret
    storeMcpManagementCredential(created.secret, rememberCredential.value)
    revealedSecret.value = created.secret
    notice.value = t('console.mcpKeyCreated')
    keys.value = [created.key]
  } catch (exception) {
    error.value = errorMessage(exception)
  } finally {
    isSaving.value = false
  }
}

async function connectKey(): Promise<void> {
  const candidate = managementSecret.value?.trim()
  if (!candidate) {
    error.value = t('console.mcpKeyRequired')
    return
  }
  storeMcpManagementCredential(candidate, rememberCredential.value)
  managementSecret.value = candidate
  await loadKeys()
}

async function createKey(): Promise<void> {
  const secret = managementSecret.value
  if (!secret || !createForm.name.trim() || (!createForm.projectId && !createForm.isAdministrator)) {
    error.value = t('console.mcpKeyCreateRequired')
    return
  }
  isSaving.value = true
  error.value = ''
  notice.value = ''
  try {
    const created = await createMcpApiKey(secret, {
      ...createForm,
      projectId: createForm.isAdministrator ? null : createForm.projectId,
      permissions: [...createForm.permissions],
      expiresAt: createForm.expiresAt ? new Date(createForm.expiresAt).toISOString() : null,
    })
    revealedSecret.value = created.secret
    keys.value = [created.key, ...keys.value]
    notice.value = t('console.mcpKeyCreated')
  } catch (exception) {
    error.value = errorMessage(exception)
  } finally {
    isSaving.value = false
  }
}

async function rotateKey(key: McpApiKeyDto): Promise<void> {
  if (!managementSecret.value || !window.confirm(t('console.mcpKeyRotateConfirm', { name: key.name }))) return
  isSaving.value = true
  error.value = ''
  try {
    const rotated = await rotateMcpApiKey(managementSecret.value, key.id)
    keys.value = keys.value.map((item) => item.id === key.id ? { ...item, revokedAt: new Date().toISOString() } : item)
    keys.value.unshift(rotated.key)
    revealedSecret.value = rotated.secret ?? ''
    notice.value = t('console.mcpKeyRotated')
  } catch (exception) {
    error.value = errorMessage(exception)
  } finally {
    isSaving.value = false
  }
}

async function revokeKey(key: McpApiKeyDto): Promise<void> {
  if (!managementSecret.value || !window.confirm(t('console.mcpKeyRevokeConfirm', { name: key.name }))) return
  isSaving.value = true
  error.value = ''
  try {
    const revoked = await revokeMcpApiKey(managementSecret.value, key.id)
    keys.value = keys.value.map((item) => item.id === key.id ? revoked : item)
    notice.value = t('console.mcpKeyRevoked')
  } catch (exception) {
    error.value = errorMessage(exception)
  } finally {
    isSaving.value = false
  }
}

async function copySecret(): Promise<void> {
  if (!revealedSecret.value) return
  let fallbackInput: HTMLTextAreaElement | undefined
  try {
    if (navigator.clipboard?.writeText) {
      await navigator.clipboard.writeText(revealedSecret.value)
    } else {
      fallbackInput = document.createElement('textarea')
      fallbackInput.value = revealedSecret.value
      fallbackInput.setAttribute('readonly', '')
      fallbackInput.style.position = 'fixed'
      fallbackInput.style.opacity = '0'
      document.body.appendChild(fallbackInput)
      fallbackInput.select()
      if (!document.execCommand('copy')) throw new Error('Clipboard access is unavailable.')
    }
  } catch (exception) {
    notice.value = errorMessage(exception)
    return
  } finally {
    fallbackInput?.remove()
  }
  notice.value = t('console.mcpKeyCopied')
}

function clearCredential(): void {
  clearMcpManagementCredential()
  managementSecret.value = undefined
  keys.value = []
  revealedSecret.value = ''
}

function setPermission(permission: McpPermission, event: Event): void {
  const checked = (event.target as HTMLInputElement).checked
  createForm.permissions = checked
    ? [...new Set([...createForm.permissions, permission])]
    : createForm.permissions.filter((item) => item !== permission)
}

function keyStatus(key: McpApiKeyDto): 'active' | 'revoked' | 'expired' {
  if (key.revokedAt) return 'revoked'
  if (key.expiresAt && new Date(key.expiresAt) <= new Date()) return 'expired'
  return 'active'
}

function formatDate(value?: string | null): string {
  if (!value) return '—'
  return new Intl.DateTimeFormat(undefined, { dateStyle: 'medium', timeStyle: 'short' }).format(new Date(value))
}

function errorMessage(exception: unknown): string {
  return exception instanceof Error ? exception.message : t('console.mcpKeyOperationFailed')
}

onMounted(() => { void loadKeys() })
</script>

<template>
  <section class="operations-console__section mcp-key-settings" aria-labelledby="mcp-key-settings-title">
    <div class="operations-console__section-heading">
      <div>
        <h2 id="mcp-key-settings-title"><KeyRound :size="17" />{{ t('console.mcpKeyTitle') }}</h2>
        <p>{{ t('console.mcpKeyHint') }}</p>
      </div>
      <button v-if="connected" class="icon-button" type="button" :title="t('console.mcpKeyRefresh')" :aria-label="t('console.mcpKeyRefresh')" :disabled="isLoading" @click="loadKeys"><RefreshCw :size="15" :class="{ 'is-spinning': isLoading }" /></button>
    </div>

    <p v-if="error" class="mcp-key-settings__message mcp-key-settings__message--error" role="alert">{{ error }}</p>
    <p v-else-if="notice" class="mcp-key-settings__message" role="status">{{ notice }}</p>

    <div v-if="revealedSecret" class="mcp-key-settings__secret" role="alert">
      <div><strong>{{ t('console.mcpKeySecretTitle') }}</strong><p>{{ t('console.mcpKeySecretHint') }}</p><p class="mcp-key-settings__secret-config"><code>SEREINFLOW_MCP_API_KEY</code> {{ t('console.mcpKeySecretConfigHint') }}</p></div>
      <code>{{ revealedSecret }}</code>
      <button class="command-button quiet" type="button" @click="copySecret"><Clipboard :size="15" /><span>{{ t('console.mcpKeyCopy') }}</span></button>
      <button class="icon-button" type="button" :title="t('command.close')" :aria-label="t('command.close')" @click="revealedSecret = ''"><X :size="15" /></button>
    </div>

    <div v-if="!connected" class="mcp-key-settings__connect">
      <div class="mcp-key-settings__empty"><LockKeyhole :size="20" /><div><strong>{{ t('console.mcpKeyConnectTitle') }}</strong><p>{{ t('console.mcpKeyConnectHint') }}</p></div></div>
      <label class="mcp-key-settings__credential"><span>{{ t('console.mcpKeySecretLabel') }}</span><input v-model="managementSecret" type="password" autocomplete="off" :placeholder="t('console.mcpKeySecretPlaceholder')" @keyup.enter="connectKey" /></label>
      <label class="mcp-key-settings__remember"><input v-model="rememberCredential" type="checkbox" /><span>{{ t('console.mcpKeyRemember') }}</span></label>
      <div class="mcp-key-settings__actions"><button class="command-button run" type="button" :disabled="isSaving" @click="connectKey"><ShieldCheck :size="15" /><span>{{ t('console.mcpKeyConnect') }}</span></button><button class="command-button quiet" type="button" :disabled="isSaving" @click="setupKey"><Plus :size="15" /><span>{{ t('console.mcpKeySetup') }}</span></button></div>
    </div>

    <template v-else>
      <div class="mcp-key-settings__toolbar"><span><ShieldCheck :size="15" />{{ t('console.mcpKeyConnected') }}</span><button class="command-button quiet" type="button" @click="clearCredential"><Unplug :size="15" /><span>{{ t('console.mcpKeyDisconnect') }}</span></button></div>
      <div class="mcp-key-settings__create">
        <div class="mcp-key-settings__subheading"><div><h3>{{ t('console.mcpKeyCreateTitle') }}</h3><p>{{ t('console.mcpKeyCreateHint') }}</p></div></div>
        <div class="mcp-key-settings__form">
          <label><span>{{ t('console.mcpKeyName') }}</span><input v-model.trim="createForm.name" maxlength="80" /></label>
          <label><span>{{ t('console.mcpKeyProject') }}</span><select v-model="createForm.projectId" :disabled="createForm.isAdministrator"><option value="">{{ t('console.mcpKeyProjectPlaceholder') }}</option><option v-for="project in projectOptions" :key="project.id" :value="project.id">{{ project.name }}</option></select></label>
          <label class="mcp-key-settings__expiration"><span>{{ t('console.mcpKeyExpires') }}</span><input v-model="createForm.expiresAt" type="datetime-local" /></label>
        </div>
        <fieldset class="mcp-key-settings__permissions"><legend>{{ t('console.mcpKeyPermissions') }}</legend><label v-for="permission in permissionOptions" :key="permission.value"><input :checked="createForm.permissions.includes(permission.value)" type="checkbox" @change="setPermission(permission.value, $event)" /><span><strong>{{ permission.label }}</strong><small>{{ permission.hint }}</small></span></label></fieldset>
        <div class="mcp-key-settings__actions"><button class="command-button run" type="button" :disabled="isSaving || !createForm.name.trim() || !createForm.projectId" @click="createKey"><Plus :size="15" /><span>{{ t('console.mcpKeyCreate') }}</span></button></div>
      </div>
      <div class="operations-table-wrap"><table class="operations-table operations-table--mcp-keys"><thead><tr><th>{{ t('console.mcpKeyName') }}</th><th>{{ t('console.mcpKeyScope') }}</th><th>{{ t('console.mcpKeyPermissions') }}</th><th>{{ t('console.mcpKeyCreated') }}</th><th>{{ t('console.mcpKeyStatus') }}</th><th>{{ t('console.mcpKeyActions') }}</th></tr></thead><tbody v-if="keys.length"><tr v-for="key in keys" :key="key.id"><td><strong>{{ key.name }}</strong><small><code>{{ key.keyPrefix }}…</code></small></td><td>{{ key.isAdministrator ? t('console.mcpKeyAdministrator') : (projectOptions.find((project) => project.id === key.projectId)?.name ?? t('console.mcpKeyProjectUnknown')) }}</td><td><span class="mcp-key-settings__permission-count">{{ key.permissions.length }}</span></td><td>{{ formatDate(key.createdAt) }}</td><td><span :class="['mcp-key-settings__status', `mcp-key-settings__status--${keyStatus(key)}`]">{{ t(`console.mcpKeyStatus.${keyStatus(key)}`) }}</span></td><td class="operations-table__actions"><button v-if="keyStatus(key) === 'active'" class="icon-button" type="button" :title="t('console.mcpKeyRotate')" :aria-label="t('console.mcpKeyRotate')" :disabled="isSaving" @click="rotateKey(key)"><RefreshCw :size="15" /></button><button v-if="keyStatus(key) === 'active'" class="icon-button icon-button--danger" type="button" :title="t('console.mcpKeyRevoke')" :aria-label="t('console.mcpKeyRevoke')" :disabled="isSaving" @click="revokeKey(key)"><Trash2 :size="15" /></button></td></tr></tbody><tbody v-else><tr><td class="operations-table__empty" colspan="6">{{ t('console.mcpKeyEmpty') }}</td></tr></tbody></table></div>
    </template>
  </section>
</template>
