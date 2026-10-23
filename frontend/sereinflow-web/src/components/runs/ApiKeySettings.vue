<script setup lang="ts">
import { computed, onMounted, reactive, ref } from 'vue';
import { EyeOff, KeyRound, Plus, RefreshCw, RotateCcw, Trash2 } from 'lucide-vue-next';
import { createApiKey, listApiKeys, revokeApiKey, rotateApiKey, type ApiKeyDto, type CreateApiKeyRequest, type SereinFlowPermission } from '../../api/mcpApi';
import type { ProjectWorkspaceDto } from '../../api/flowApi';
import { t } from '../../i18n';
const props = defineProps<{
    projectWorkspaces: ProjectWorkspaceDto[];
}>();
const projectOptions = computed(() => {
    const projects = props.projectWorkspaces.filter(item => item.project.status !== 'archived');
    return projects;
});
const keys = ref<ApiKeyDto[]>([]);
const error = ref('');
const revealedSecret = ref('');
const busy = ref(false);
const form = reactive<CreateApiKeyRequest>({ name: '', projectId: '', permissions: ['project.read'], isAdministrator: false });
const operations: SereinFlowPermission[] = ['project.read', 'project.write', 'library.read', 'run.read', 'debug.read', 'flow.write', 'debug.control', 'flow.publish', 'flow.rollback', 'script.compile', 'library.import', 'library.manage', 'sensitive.read', 'run.execute', 'run.message.publish', 'run.variable.write'];
function hideSecret(): void {
    revealedSecret.value = '';
}
function keyDisabled(key: ApiKeyDto): boolean {
    const disabled = busy.value || Boolean(key.revokedAt);
    return disabled;
}
function keyStatus(key: ApiKeyDto): 'active' | 'expired' | 'revoked' {
    if (key.revokedAt) {
        return 'revoked';
    }
    const expires = key.expiresAt ? Date.parse(key.expiresAt) : undefined;
    if (expires !== undefined && expires <= Date.now()) {
        return 'expired';
    }
    return 'active';
}
function projectName(key: ApiKeyDto): string {
    if (key.isAdministrator) {
        return t('console.mcpKeyAdministrator');
    }
    return props.projectWorkspaces.find(workspace => workspace.project.id === key.projectId)?.project.name
        ?? t('console.mcpKeyProjectUnknown');
}
function setExpiry(event: Event): void {
    const value = (event.target as HTMLInputElement).value;
    form.expiresAt = value ? new Date(value).toISOString() : null;
}
async function reload(): Promise<void> {
    keys.value = await listApiKeys('');
}
async function execute(action: () => Promise<void>): Promise<void> {
    busy.value = true;
    error.value = '';
    try {
        await action();
        await reload();
    }
    catch (failure) {
        error.value = String(failure);
    }
    finally {
        busy.value = false;
    }
}
async function refresh(): Promise<void> {
    await execute(async () => {});
}
async function create(): Promise<void> {
    await execute(async () => {
        const created = await createApiKey('', { ...form, projectId: form.isAdministrator ? null : form.projectId });
        revealedSecret.value = created.secret;
    });
}
async function rotate(id: string): Promise<void> {
    if (!window.confirm(t('console.mcpKeyRotateConfirm', { name: keys.value.find(key => key.id === id)?.name ?? id }))) {
        return;
    }
    await execute(async () => {
        revealedSecret.value = (await rotateApiKey('', id)).secret ?? '';
    });
}
async function revoke(id: string): Promise<void> {
    if (!window.confirm(t('console.mcpKeyRevokeConfirm', { name: keys.value.find(key => key.id === id)?.name ?? id }))) {
        return;
    }
    await execute(async () => {
        await revokeApiKey('', id);
    });
}
onMounted(() => {
    void refresh();
});
</script>

<template>
  <div class="api-key-settings">
    <section class="operations-console__section mcp-key-settings" aria-labelledby="api-key-settings-title" :aria-busy="busy">
      <div class="operations-console__section-heading">
        <div>
          <h2 id="api-key-settings-title"><KeyRound :size="17" />{{ t('console.apiKeyTitle') }}</h2>
          <p>{{ t('console.apiKeyHint') }}</p>
        </div>
        <button class="command-button quiet" type="button" :disabled="busy" @click="refresh">
          <RefreshCw :size="15" :class="{ 'is-spinning': busy }" />
          {{ t('console.apiKeyRefresh') }}
        </button>
      </div>
      <p v-if="error" class="mcp-key-settings__message mcp-key-settings__message--error" role="alert">{{ error }}</p>
      <div v-if="revealedSecret" class="mcp-key-settings__secret" role="status">
        <div>
          <strong>{{ t('console.mcpKeySecretTitle') }}</strong>
          <p>{{ t('console.apiKeySecretHint') }}</p>
        </div>
        <code>{{ revealedSecret }}</code>
        <button class="command-button quiet" type="button" @click="hideSecret">
          <EyeOff :size="15" />{{ t('console.apiKeyHide') }}
        </button>
      </div>
      <div class="operations-table-wrap api-key-settings__table" role="region" :aria-label="t('console.apiKeyTitle')" tabindex="0">
        <table class="operations-table operations-table--mcp-keys">
          <thead>
            <tr>
              <th>{{ t('console.mcpKeyName') }}</th>
              <th>{{ t('console.apiKeyPrefix') }}</th>
              <th>{{ t('console.mcpKeyScope') }}</th>
              <th>{{ t('console.mcpKeyStatus') }}</th>
              <th>{{ t('console.mcpKeyActions') }}</th>
            </tr>
          </thead>
          <tbody>
            <tr v-for="key in keys" :key="key.id">
              <td><strong>{{ key.name }}</strong></td>
              <td><code>{{ key.keyPrefix }}</code></td>
              <td>{{ projectName(key) }}</td>
              <td><span class="mcp-key-settings__status" :class="`mcp-key-settings__status--${keyStatus(key)}`">{{ t(`console.mcpKeyStatus.${keyStatus(key)}`) }}</span></td>
              <td>
                <div class="api-key-settings__row-actions">
                  <button class="command-button quiet" type="button" :disabled="keyDisabled(key)" @click="rotate(key.id)">
                    <RotateCcw :size="14" />{{ t('console.mcpKeyRotate') }}
                  </button>
                  <button class="command-button api-key-settings__revoke" type="button" :disabled="keyDisabled(key)" @click="revoke(key.id)">
                    <Trash2 :size="14" />{{ t('console.mcpKeyRevoke') }}
                  </button>
                </div>
              </td>
            </tr>
            <tr v-if="!keys.length">
              <td class="operations-table__empty" colspan="5">{{ t(busy ? 'console.apiKeyLoading' : error ? 'console.apiKeyLoadFailed' : 'console.apiKeyEmpty') }}</td>
            </tr>
          </tbody>
        </table>
      </div>
      <form class="mcp-key-settings__create" @submit.prevent="create">
        <div class="mcp-key-settings__subheading">
          <h3>{{ t('console.mcpKeyCreateTitle') }}</h3>
          <p>{{ t('console.mcpKeyCreateHint') }}</p>
        </div>
        <div class="mcp-key-settings__form">
          <label>
            <span>{{ t('console.mcpKeyName') }}</span>
            <input v-model="form.name" required :placeholder="t('console.apiKeyNamePlaceholder')" />
          </label>
          <label v-if="!form.isAdministrator">
            <span>{{ t('console.mcpKeyProject') }}</span>
            <select v-model="form.projectId" required>
              <option value="" disabled>{{ t('console.mcpKeyProjectPlaceholder') }}</option>
              <option v-for="workspace in projectOptions" :key="workspace.project.id" :value="workspace.project.id">{{ workspace.project.name }}</option>
            </select>
          </label>
          <label>
            <span>{{ t('console.mcpKeyExpires') }}</span>
            <input type="datetime-local" @change="setExpiry" />
          </label>
        </div>
        <label class="mcp-key-settings__remember">
          <input v-model="form.isAdministrator" type="checkbox" />
          {{ t('console.mcpKeyAdministrator') }}
        </label>
        <fieldset v-if="!form.isAdministrator" class="mcp-key-settings__permissions">
          <legend>{{ t('console.mcpKeyPermissions') }} · {{ form.permissions.length }} / {{ operations.length }}</legend>
          <label v-for="operation in operations" :key="operation">
            <input v-model="form.permissions" type="checkbox" :value="operation" />
            <span>
              <strong>{{ t(`console.apiKeyPermission.${operation}`) }}</strong>
              <small>{{ operation }}</small>
            </span>
          </label>
        </fieldset>
        <p v-else class="api-key-settings__admin-hint">{{ t('console.apiKeyAdministratorHint') }}</p>
        <div class="mcp-key-settings__actions">
          <button class="command-button run" :disabled="busy" type="submit">
            <Plus :size="15" />{{ t('console.mcpKeyCreate') }}
          </button>
        </div>
      </form>
    </section>
  </div>
</template>

<style scoped>
.api-key-settings {
  min-width: 0;
  max-width: 1120px;
}
.api-key-settings__table {
  margin-top: 16px;
}
.operations-console__section-heading > .command-button {
  flex-shrink: 0;
  white-space: nowrap;
}
.api-key-settings__row-actions {
  display: flex;
  gap: 8px;
}
.api-key-settings__revoke {
  color: var(--danger);
}
.api-key-settings__revoke:hover {
  border-color: var(--danger);
  color: var(--danger);
  background: #fef2f2;
}
.api-key-settings__admin-hint {
  margin: 0;
  color: var(--warning);
  font-size: 12px;
  line-height: 1.5;
}
@media (min-width: 761px) and (max-width: 1100px) {
  .mcp-key-settings__form, .mcp-key-settings__permissions {
    grid-template-columns: repeat(2, minmax(0, 1fr));
  }
  .mcp-key-settings__secret {
    grid-template-columns: minmax(0, 1fr);
  }
}
</style>
