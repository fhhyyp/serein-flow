<script setup lang="ts">
import { computed, onMounted, reactive, ref } from 'vue';
import { createApiKey, listApiKeys, revokeApiKey, rotateApiKey, type ApiKeyDto, type CreateApiKeyRequest, type SereinFlowPermission } from '../../api/mcpApi';
import type { ProjectWorkspaceDto } from '../../api/flowApi';
import McpSkillSettings from './McpSkillSettings.vue';
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
const skills = ref<InstanceType<typeof McpSkillSettings>>();
const form = reactive<CreateApiKeyRequest>({ name: '', projectId: '', permissions: ['project.read'], isAdministrator: false });
const operations: SereinFlowPermission[] = ['project.read', 'project.write', 'library.read', 'run.read', 'debug.read', 'flow.write', 'debug.control', 'flow.publish', 'flow.rollback', 'script.compile', 'library.import', 'library.manage', 'sensitive.read', 'run.execute', 'run.message.publish', 'run.variable.write'];
function hideSecret(): void {
    revealedSecret.value = '';
}
function keyDisabled(key: ApiKeyDto): boolean {
    const disabled = busy.value || Boolean(key.revokedAt);
    return disabled;
}
function keyStatus(key: ApiKeyDto): string {
    if (key.revokedAt) {
        return 'Revoked / 已撤销';
    }
    const expires = key.expiresAt ? Date.parse(key.expiresAt) : undefined;
    if (expires !== undefined && expires <= Date.now()) {
        return 'Expired / 已过期';
    }
    return 'Active / 有效';
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
async function create(): Promise<void> {
    await execute(async () => {
        const created = await createApiKey('', { ...form, projectId: form.isAdministrator ? null : form.projectId });
        revealedSecret.value = created.secret;
    });
}
async function rotate(id: string): Promise<void> {
    if (!window.confirm('Rotate this key? The previous key will be revoked. 轮换后原密钥将被撤销，是否继续？')) {
        return;
    }
    await execute(async () => {
        revealedSecret.value = (await rotateApiKey('', id)).secret ?? '';
    });
}
async function revoke(id: string): Promise<void> {
    if (!window.confirm('Revoke this key? 撤销此密钥？')) {
        return;
    }
    await execute(async () => {
        await revokeApiKey('', id);
    });
}
function confirmLeave(): boolean {
    const allowed = skills.value?.confirmDiscard() !== false;
    return allowed;
}
defineExpose({ confirmLeave });
onMounted(() => {
    void execute(async () => {
    });
});
</script>

<template>
  <section class="api-key-settings">
    <h3>API keys / API 密钥</h3>
    <p>Keys apply to HTTP access. Local stdio does not require a key. 密钥用于 HTTP 访问，本地 stdio 无需密钥。</p>
    <p
      v-if="error"
      role="alert"
    >{{ error }}</p>
    <p v-if="revealedSecret">
      Copy and save this key now. 请立即复制保存密钥：
      <code>{{ revealedSecret }}</code>
      <button @click="hideSecret">Hide / 隐藏</button>
    </p>
    <button
      :disabled="busy"
      @click="execute(reload)"
    >Refresh / 刷新</button>
    <table>
      <thead>
        <tr>
          <th>Name / 名称</th>
          <th>Prefix / 前缀</th>
          <th>Status / 状态</th>
          <th>Actions / 操作</th>
        </tr>
      </thead>
      <tbody>
        <tr
          v-for="key in keys"
          :key="key.id"
        >
          <td>{{ key.name }}</td>
          <td>{{ key.keyPrefix }}</td>
          <td>{{ keyStatus(key) }}</td>
          <td>
            <button
              :disabled="keyDisabled(key)"
              @click="rotate(key.id)"
            >Rotate / 轮换</button>
            <button
              :disabled="keyDisabled(key)"
              @click="revoke(key.id)"
            >Revoke / 撤销</button>
          </td>
        </tr>
      </tbody>
    </table>
    <form @submit.prevent="create">
      <input
        v-model="form.name"
        required
        placeholder="Key name / 密钥名称"
        aria-label="Key name"
      />
      <label>
        <input
          v-model="form.isAdministrator"
          type="checkbox"
        />
        Administrator / 管理员
      </label>
      <select
        v-if="!form.isAdministrator"
        v-model="form.projectId"
        required
        aria-label="Project"
      >
        <option
          value=""
          disabled
        >Select project / 选择项目</option>
        <option
          v-for="workspace in projectOptions"
          :key="workspace.project.id"
          :value="workspace.project.id"
        >{{ workspace.project.name }}</option>
      </select>
      <div v-if="!form.isAdministrator">
        <label
          v-for="operation in operations"
          :key="operation"
        >
          <input
            v-model="form.permissions"
            type="checkbox"
            :value="operation"
          />
          {{ operation }}
        </label>
      </div>
      <label>
        Expires / 过期时间
        <input
          type="datetime-local"
          @change="setExpiry"
        />
      </label>
      <button
        :disabled="busy"
        type="submit"
      >Create / 创建</button>
    </form>
    <McpSkillSettings
      ref="skills"
      secret=""
    />
  </section>
</template>

<style scoped>
.api-key-settings {
  display: grid;
  gap: 12px;
}
form {
  display: grid;
  gap: 10px;
}
form div {
  display: flex;
  flex-wrap: wrap;
  gap: 12px;
}
code {
  display: block;
  overflow-wrap: anywhere;
}
td, th {
  text-align: left;
  padding: 8px;
}
</style>
