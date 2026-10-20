<script setup lang="ts">
type TemplateItem<T> = T extends readonly (infer Item)[] ? Item : T extends Record<string, infer Value> ? Value : T extends number ? number : never;
import { computed, onMounted, reactive, ref } from 'vue';
import { Clipboard, KeyRound, LockKeyhole, Plus, RefreshCw, ShieldCheck, Trash2, Unplug, X } from 'lucide-vue-next';
import { clearMcpManagementCredential, createMcpApiKey, getMcpManagementCredential, hasRememberedMcpManagementCredential, listMcpApiKeys, mcpKeyProjectOptions, McpApiError, revokeMcpApiKey, rotateMcpApiKey, setupMcpApiKey, storeMcpManagementCredential, type CreateMcpApiKeyRequestDto, type McpApiKeyDto, type McpPermission, } from '../../api/mcpApi';
import type { ProjectWorkspaceDto } from '../../api/flowApi';
import { t } from '../../i18n';
import McpSkillSettings from './McpSkillSettings.vue';
const props = defineProps<{
  projectWorkspaces: ProjectWorkspaceDto[];
}>();
const skillSettings = ref<InstanceType<typeof McpSkillSettings>>();
const initialCredential = getMcpManagementCredential();
const managementSecret = ref(initialCredential);
const credentialInput = ref(initialCredential ?? '');
const hasRememberedMcpManagementCredentialResult: boolean = hasRememberedMcpManagementCredential();
const rememberCredential = ref(hasRememberedMcpManagementCredentialResult);
const keys = ref<McpApiKeyDto[]>([]);
const isLoading = ref(false);
const isSaving = ref(false);
const isAuthenticated = ref(false);
const error = ref('');
const notice = ref('');
const revealedSecret = ref('');
const connected = computed(() => isAuthenticated.value);
const projectOptions = computed(() => {
  const mcpKeyProjectOptionsResult: import("../../api/mcpApi").McpKeyProjectOption[] = mcpKeyProjectOptions(props.projectWorkspaces);
  return mcpKeyProjectOptionsResult;
});
const createForm = reactive<CreateMcpApiKeyRequestDto>({
  projectId: '',
  name: 'SereinFlow MCP Client',
  permissions: ['project.read', 'library.read', 'script.compile'],
  expiresAt: null,
  isAdministrator: false,
});
const permissionOptions: Array<{
  value: McpPermission;
  label: string;
  hint: string;
}> = [
  { value: 'project.read', label: '项目读取', hint: '查看项目和流程信息' },
  { value: 'library.read', label: '类库读取', hint: '检查类库和节点目录' },
  { value: 'script.compile', label: '脚本编译', hint: '检查 SereinLang 语法' },
  { value: 'run.read', label: '运行读取', hint: '查看运行状态和输出' },
  { value: 'run.variable.write', label: '流程变量写入', hint: '设置活动运行的变量值' },
  { value: 'debug.read', label: '调试读取', hint: '查看调试会话信息' },
  { value: 'flow.write', label: '流程写入', hint: '修改流程草稿' },
  { value: 'flow.publish', label: '流程发布', hint: '发布生产版本' },
  { value: 'flow.rollback', label: '流程回滚', hint: '回滚流程版本' },
  { value: 'library.import', label: '类库导入', hint: '导入类库包' },
  { value: 'library.manage', label: '类库管理', hint: '管理项目类库引用' },
  { value: 'debug.control', label: '调试控制', hint: '继续、单步或停止调试' },
  { value: 'sensitive.read', label: '敏感内容读取', hint: '读取脚本源码或流程字面量' },
];
async function loadKeys(secret = managementSecret.value): Promise<boolean> {
  if (!secret) {
    return false;
  }
  isLoading.value = true;
  error.value = '';
  try {
    keys.value = await listMcpApiKeys(secret);
    managementSecret.value = secret;
    isAuthenticated.value = true;
    return true;
  }
  catch (exception) {
    if (exception instanceof McpApiError && exception.status === 401) {
      if (skillSettings.value?.confirmDiscard() !== false) {
        clearCredential(true);
      }
      error.value = t('console.mcpKeyUnauthorized');
    }
    else {
      error.value = errorMessage(exception);
    }
    return false;
  }
  finally {
    isLoading.value = false;
  }
}
async function setupKey(): Promise<void> {
  isSaving.value = true;
  error.value = '';
  notice.value = '';
  try {
    const created = await setupMcpApiKey();
    managementSecret.value = created.secret;
    credentialInput.value = created.secret;
    isAuthenticated.value = true;
    storeMcpManagementCredential(created.secret, rememberCredential.value);
    revealedSecret.value = created.secret;
    notice.value = t('console.mcpKeyCreated');
    keys.value = [created.key];
  }
  catch (exception) {
    error.value = errorMessage(exception);
  }
  finally {
    isSaving.value = false;
  }
}
async function connectKey(): Promise<void> {
  if (isLoading.value || isSaving.value) {
    return;
  }
  const candidate = credentialInput.value.trim();
  if (!candidate) {
    error.value = t('console.mcpKeyRequired');
    return;
  }
  notice.value = '';
  if (await loadKeys(candidate)) {
    storeMcpManagementCredential(candidate, rememberCredential.value);
    notice.value = t('console.mcpKeyConnected');
  }
}
async function createKey(): Promise<void> {
  const secret = managementSecret.value;
  if (!secret || !createForm.name.trim() || (!createForm.projectId && !createForm.isAdministrator)) {
    error.value = t('console.mcpKeyCreateRequired');
    return;
  }
  isSaving.value = true;
  error.value = '';
  notice.value = '';
  try {
    const argumentValue: string = secret;
    const resolveToISOStringResult = () => {
      const dateInstance1 = new Date(createForm.expiresAt!);
      const toISOStringResult: string | null | undefined = dateInstance1.toISOString();
      return toISOStringResult;
    };
    const evaluatedValue: CreateMcpApiKeyRequestDto = {
      ...createForm,
      projectId: createForm.isAdministrator ? null : createForm.projectId,
      permissions: [...createForm.permissions],
      expiresAt: createForm.expiresAt ? resolveToISOStringResult() : null,
    };
    const created = await createMcpApiKey(argumentValue, evaluatedValue);
    revealedSecret.value = created.secret;
    keys.value = [created.key, ...keys.value];
    notice.value = t('console.mcpKeyCreated');
  }
  catch (exception) {
    error.value = errorMessage(exception);
  }
  finally {
    isSaving.value = false;
  }
}
async function rotateKey(key: McpApiKeyDto): Promise<void> {
  const resolveConfirmResult = () => {
    const tResult: string | undefined = t('console.mcpKeyRotateConfirm', { name: key.name });
    const confirmResult = window.confirm(tResult);
    return confirmResult;
  };
  if (!managementSecret.value || !resolveConfirmResult()) {
    return;
  }
  isSaving.value = true;
  error.value = '';
  try {
    const rotated = await rotateMcpApiKey(managementSecret.value, key.id);
    keys.value = keys.value.map((item) => {
      const resolveToISOStringResult1 = () => {
        const dateInstance2 = new Date();
        const toISOStringResult1: string | null | undefined = dateInstance2.toISOString();
        return toISOStringResult1;
      };
      const evaluatedValue1: {
        id: string;
        projectId?: string | null | undefined;
        name: string;
        keyPrefix: string;
        permissions: McpPermission[];
        createdAt: string;
        expiresAt?: string | null | undefined;
        revokedAt?: string | null | undefined;
        lastUsedAt?: string | null | undefined;
        isAdministrator: boolean;
      } = item.id === key.id ? { ...item, revokedAt: resolveToISOStringResult1() } : item;
      return evaluatedValue1;
    });
    keys.value.unshift(rotated.key);
    revealedSecret.value = rotated.secret ?? '';
    notice.value = t('console.mcpKeyRotated');
  }
  catch (exception) {
    error.value = errorMessage(exception);
  }
  finally {
    isSaving.value = false;
  }
}
async function revokeKey(key: McpApiKeyDto): Promise<void> {
  const resolveConfirmResult1 = () => {
    const tResult1: string | undefined = t('console.mcpKeyRevokeConfirm', { name: key.name });
    const confirmResult1 = window.confirm(tResult1);
    return confirmResult1;
  };
  if (!managementSecret.value || !resolveConfirmResult1()) {
    return;
  }
  isSaving.value = true;
  error.value = '';
  try {
    const revoked = await revokeMcpApiKey(managementSecret.value, key.id);
    keys.value = keys.value.map((item) => item.id === key.id ? revoked : item);
    notice.value = t('console.mcpKeyRevoked');
  }
  catch (exception) {
    error.value = errorMessage(exception);
  }
  finally {
    isSaving.value = false;
  }
}
async function copySecret(): Promise<void> {
  if (!revealedSecret.value) {
    return;
  }
  let fallbackInput: HTMLTextAreaElement | undefined;
  try {
    if (navigator.clipboard?.writeText) {
      await navigator.clipboard.writeText(revealedSecret.value);
    }
    else {
      fallbackInput = document.createElement('textarea');
      fallbackInput.value = revealedSecret.value;
      fallbackInput.setAttribute('readonly', '');
      fallbackInput.style.position = 'fixed';
      fallbackInput.style.opacity = '0';
      document.body.appendChild(fallbackInput);
      fallbackInput.select();
      if (!document.execCommand('copy')) {
        throw new Error('Clipboard access is unavailable.');
      }
    }
  }
  catch (exception) {
    notice.value = errorMessage(exception);
    return;
  }
  finally {
    fallbackInput?.remove();
  }
  notice.value = t('console.mcpKeyCopied');
}
function clearCredential(force = false): void {
  if (!force && skillSettings.value && !skillSettings.value.confirmDiscard()) {
    return;
  }
  clearMcpManagementCredential();
  managementSecret.value = undefined;
  credentialInput.value = '';
  isAuthenticated.value = false;
  keys.value = [];
  revealedSecret.value = '';
}
function setPermission(permission: McpPermission, event: Event): void {
  const checked = (event.target as HTMLInputElement).checked;
  createForm.permissions = checked
    ? [...new Set([...createForm.permissions, permission])]
    : createForm.permissions.filter((item) => item !== permission);
}
function keyStatus(key: McpApiKeyDto): 'active' | 'revoked' | 'expired' {
  if (key.revokedAt) {
    return 'revoked';
  }
  if (key.expiresAt && new Date(key.expiresAt!) <= new Date()) {
    return 'expired';
  }
  return 'active';
}
function formatDate(value?: string | null): string {
  if (!value) {
    return '—';
  }
  const dateTimeFormatInstance = new Intl.DateTimeFormat(undefined, { dateStyle: 'medium', timeStyle: 'short' });
  const dateInstance: number | Date | undefined = new Date(value);
  const formatResult: string = dateTimeFormatInstance.format(dateInstance);
  return formatResult;
}
function errorMessage(exception: unknown): string {
  const evaluatedValue2: string = exception instanceof Error ? exception.message : t('console.mcpKeyOperationFailed');
  return evaluatedValue2;
}
onMounted(() => {
  void loadKeys();
});
defineExpose({ confirmLeave: () => {
    const evaluatedValue3 = skillSettings.value?.confirmDiscard() ?? true;
    return evaluatedValue3;
  } });
const readRefreshCwClass = computed(() => ({ 'is-spinning': isLoading.value }));
function handleButtonClick() {
  revealedSecret.value = '';
}
const readButtonDisabled = computed(() => (isSaving.value || isLoading.value));
const readButtonDisabled2 = computed(() => (isSaving.value || isLoading.value));
function readOptionItems() {
  return (projectOptions.value);
}
function readLabelItems() {
  return (permissionOptions);
}
const readButtonDisabled3 = computed(() => {
  const evaluatedValue4: boolean = (isSaving.value || !createForm.name.trim() || !createForm.projectId);
  return evaluatedValue4;
});
function readTrItems() {
  return (keys.value);
}
function readTemplateText(key: TemplateItem<ReturnType<typeof readTrItems>>) {
  const evaluatedValue5 = (key.isAdministrator ? t('console.mcpKeyAdministrator') : (projectOptions.value.find((project) => project.id === key.projectId)?.name ?? t('console.mcpKeyProjectUnknown')));
  return evaluatedValue5;
}
function readSpanClass(key: TemplateItem<ReturnType<typeof readTrItems>>) {
  const evaluatedValue6 = (['mcp-key-settings__status', `mcp-key-settings__status--${keyStatus(key)}`]);
  return evaluatedValue6;
}
function readTemplateText2(key: TemplateItem<ReturnType<typeof readTrItems>>) {
  const evaluatedValue7: string = `console.mcpKeyStatus.${keyStatus(key)}`;
  const evaluatedValue8 = (t(evaluatedValue7));
  return evaluatedValue8;
}
function readButtonIf(key: TemplateItem<ReturnType<typeof readTrItems>>) {
  const evaluatedValue9 = (keyStatus(key) === 'active');
  return evaluatedValue9;
}
function readButtonIf2(key: TemplateItem<ReturnType<typeof readTrItems>>) {
  const evaluatedValue10 = (keyStatus(key) === 'active');
  return evaluatedValue10;
}
const readMcpSkillSettingsIf = computed(() => (connected.value && managementSecret.value));
</script>

<template>
  <section
    class="operations-console__section mcp-key-settings"
    aria-labelledby="mcp-key-settings-title"
  >
    <div class="operations-console__section-heading">
      <div>
        <h2 id="mcp-key-settings-title">
          <KeyRound :size="17" />
          {{ t('console.mcpKeyTitle') }}
        </h2>
        <p>{{ t('console.mcpKeyHint') }}</p>
      </div>
      <button
        v-if="connected"
        class="icon-button"
        type="button"
        :title="t('console.mcpKeyRefresh')"
        :aria-label="t('console.mcpKeyRefresh')"
        :disabled="isLoading"
        @click="loadKeys()"
      >
        <RefreshCw
          :size="15"
          :class="readRefreshCwClass"
         />
      </button>
    </div>
    <p
      v-if="error"
      class="mcp-key-settings__message mcp-key-settings__message--error"
      role="alert"
    >{{ error }}</p>
    <p
      v-else-if="notice"
      class="mcp-key-settings__message"
      role="status"
    >{{ notice }}</p>
    <div
      v-if="revealedSecret"
      class="mcp-key-settings__secret"
      role="alert"
    >
      <div>
        <strong>{{ t('console.mcpKeySecretTitle') }}</strong>
        <p>{{ t('console.mcpKeySecretHint') }}</p>
        <p class="mcp-key-settings__secret-config">
          <code>SEREINFLOW_MCP_API_KEY</code>
          {{ ' ' }}
          {{ t('console.mcpKeySecretConfigHint') }}
        </p>
      </div>
      <code>{{ revealedSecret }}</code>
      <button
        class="command-button quiet"
        type="button"
        @click="copySecret"
      >
        <Clipboard :size="15" />
        <span>{{ t('console.mcpKeyCopy') }}</span>
      </button>
      <button
        class="icon-button"
        type="button"
        :title="t('command.close')"
        :aria-label="t('command.close')"
        @click="handleButtonClick()"
      >
        <X :size="15" />
      </button>
    </div>
    <div
      v-if="!connected"
      class="mcp-key-settings__connect"
    >
      <div class="mcp-key-settings__empty">
        <LockKeyhole :size="20" />
        <div>
          <strong>{{ t('console.mcpKeyConnectTitle') }}</strong>
          <p>{{ t('console.mcpKeyConnectHint') }}</p>
        </div>
      </div>
      <label class="mcp-key-settings__credential">
        <span>{{ t('console.mcpKeySecretLabel') }}</span>
        <input
          v-model="credentialInput"
          type="password"
          autocomplete="off"
          :placeholder="t('console.mcpKeySecretPlaceholder')"
          @keyup.enter="connectKey"
         />
      </label>
      <label class="mcp-key-settings__remember">
        <input
          v-model="rememberCredential"
          type="checkbox"
         />
        <span>{{ t('console.mcpKeyRemember') }}</span>
      </label>
      <div class="mcp-key-settings__actions">
        <button
          class="command-button run"
          type="button"
          :disabled="readButtonDisabled"
          @click="connectKey"
        >
          <ShieldCheck :size="15" />
          <span>{{ t('console.mcpKeyConnect') }}</span>
        </button>
        <button
          class="command-button quiet"
          type="button"
          :disabled="readButtonDisabled2"
          @click="setupKey"
        >
          <Plus :size="15" />
          <span>{{ t('console.mcpKeySetup') }}</span>
        </button>
      </div>
    </div>
    <template v-else>
      <div class="mcp-key-settings__toolbar">
        <span>
          <ShieldCheck :size="15" />
          {{ t('console.mcpKeyConnected') }}
        </span>
        <button
          class="command-button quiet"
          type="button"
          @click="clearCredential()"
        >
          <Unplug :size="15" />
          <span>{{ t('console.mcpKeyDisconnect') }}</span>
        </button>
      </div>
      <div class="mcp-key-settings__create">
        <div class="mcp-key-settings__subheading">
          <div>
            <h3>{{ t('console.mcpKeyCreateTitle') }}</h3>
            <p>{{ t('console.mcpKeyCreateHint') }}</p>
          </div>
        </div>
        <div class="mcp-key-settings__form">
          <label>
            <span>{{ t('console.mcpKeyName') }}</span>
            <input
              v-model.trim="createForm.name"
              maxlength="80"
             />
          </label>
          <label>
            <span>{{ t('console.mcpKeyProject') }}</span>
            <select
              v-model="createForm.projectId"
              :disabled="createForm.isAdministrator"
            >
              <option value="">{{ t('console.mcpKeyProjectPlaceholder') }}</option>
              <option
                v-for="project in readOptionItems()"
                :key="project.id"
                :value="project.id"
              >{{ project.name }}</option>
            </select>
          </label>
          <label class="mcp-key-settings__expiration">
            <span>{{ t('console.mcpKeyExpires') }}</span>
            <input
              v-model="createForm.expiresAt"
              type="datetime-local"
             />
          </label>
        </div>
        <fieldset class="mcp-key-settings__permissions">
          <legend>{{ t('console.mcpKeyPermissions') }}</legend>
          <label
            v-for="permission in readLabelItems()"
            :key="permission.value"
          >
            <input
              :checked="createForm.permissions.includes(permission.value)"
              type="checkbox"
              @change="setPermission(permission.value, $event)"
             />
            <span>
              <strong>{{ permission.label }}</strong>
              <small>{{ permission.hint }}</small>
            </span>
          </label>
        </fieldset>
        <div class="mcp-key-settings__actions">
          <button
            class="command-button run"
            type="button"
            :disabled="readButtonDisabled3"
            @click="createKey"
          >
            <Plus :size="15" />
            <span>{{ t('console.mcpKeyCreate') }}</span>
          </button>
        </div>
      </div>
      <div class="operations-table-wrap">
        <table class="operations-table operations-table--mcp-keys">
          <thead>
            <tr>
              <th>{{ t('console.mcpKeyName') }}</th>
              <th>{{ t('console.mcpKeyScope') }}</th>
              <th>{{ t('console.mcpKeyPermissions') }}</th>
              <th>{{ t('console.mcpKeyCreated') }}</th>
              <th>{{ t('console.mcpKeyStatus') }}</th>
              <th>{{ t('console.mcpKeyActions') }}</th>
            </tr>
          </thead>
          <tbody v-if="keys.length">
            <tr
              v-for="key in readTrItems()"
              :key="key.id"
            >
              <td>
                <strong>{{ key.name }}</strong>
                <small>
                  <code>{{ key.keyPrefix }}…</code>
                </small>
              </td>
              <td>{{ readTemplateText(key) }}</td>
              <td>
                <span class="mcp-key-settings__permission-count">{{ key.permissions.length }}</span>
              </td>
              <td>{{ formatDate(key.createdAt) }}</td>
              <td>
                <span :class="readSpanClass(key)">{{ readTemplateText2(key) }}</span>
              </td>
              <td class="operations-table__actions">
                <button
                  v-if="readButtonIf(key)"
                  class="icon-button"
                  type="button"
                  :title="t('console.mcpKeyRotate')"
                  :aria-label="t('console.mcpKeyRotate')"
                  :disabled="isSaving"
                  @click="rotateKey(key)"
                >
                  <RefreshCw :size="15" />
                </button>
                <button
                  v-if="readButtonIf2(key)"
                  class="icon-button icon-button--danger"
                  type="button"
                  :title="t('console.mcpKeyRevoke')"
                  :aria-label="t('console.mcpKeyRevoke')"
                  :disabled="isSaving"
                  @click="revokeKey(key)"
                >
                  <Trash2 :size="15" />
                </button>
              </td>
            </tr>
          </tbody>
          <tbody v-else>
            <tr>
              <td
                class="operations-table__empty"
                colspan="6"
              >{{ t('console.mcpKeyEmpty') }}</td>
            </tr>
          </tbody>
        </table>
      </div>
    </template>
  </section>
  <McpSkillSettings
    v-if="readMcpSkillSettingsIf"
    ref="skillSettings"
    :secret="managementSecret!"
   />
</template>
