<script setup lang="ts">
import { computed, onBeforeUnmount, onMounted, ref } from 'vue';
import App from './App.vue';
import { createInitialAdministrator, hasSecuritySession, loadSecurityCapabilities, loginWithKey, logoutSecuritySession, securityCapabilities } from './api/securityApi';
const ready = ref(false);
const busy = ref(false);
const secret = ref('');
const createdSecret = ref('');
const error = ref('');
const apiKeyMode = computed(() => securityCapabilities.value?.mode === 'ApiKey');
const hostMode = computed(() => securityCapabilities.value?.mode === 'Host');
function clearError(): void {
    error.value = '';
}
function requireAuthentication(): void {
    ready.value = false;
}
function denyAccess(): void {
    error.value = 'Permission denied. 当前身份没有操作权限。';
}
async function initialize(): Promise<void> {
    try {
        const capabilities = await loadSecurityCapabilities();
        ready.value = capabilities.mode === 'Host' || await hasSecuritySession();
    }
    catch (failure) {
        error.value = String(failure);
    }
}
async function login(): Promise<void> {
    busy.value = true;
    error.value = '';
    try {
        await loginWithKey(secret.value);
        secret.value = '';
        ready.value = true;
    }
    catch (failure) {
        error.value = String(failure);
    }
    finally {
        busy.value = false;
    }
}
async function setup(): Promise<void> {
    busy.value = true;
    try {
        createdSecret.value = await createInitialAdministrator();
        secret.value = createdSecret.value;
        await loadSecurityCapabilities();
    }
    catch (failure) {
        error.value = String(failure);
    }
    finally {
        busy.value = false;
    }
}
onMounted(() => {
    window.addEventListener('sereinflow:authentication-required', requireAuthentication);
    window.addEventListener('sereinflow:access-denied', denyAccess);
    void initialize();
});
onBeforeUnmount(() => {
    window.removeEventListener('sereinflow:authentication-required', requireAuthentication);
    window.removeEventListener('sereinflow:access-denied', denyAccess);
});
</script>

<template>
  <template v-if="ready">
    <App />
    <button
      v-if="apiKeyMode"
      class="security-signout"
      @click="logoutSecuritySession"
    >Sign out / 注销</button>
    <div
      v-if="error"
      class="security-notice"
      role="alert"
      @click="clearError"
    >{{ error }}</div>
  </template>
  <main
    v-else
    class="security-screen"
  >
    <form
      class="security-card"
      @submit.prevent="login"
    >
      <h1>SereinFlow</h1>
      <p v-if="hostMode">Sign in through your host application, then reload. 请在宿主系统登录后重新加载。</p>
      <template v-else-if="securityCapabilities">
        <p>Enter an API key to open this workspace. 输入 API 密钥以打开工作区。</p>
        <button
          v-if="securityCapabilities.initialSetupRequired"
          type="button"
          :disabled="busy"
          @click="setup"
        >Create initial administrator / 初始化管理员</button>
        <p v-if="createdSecret">
          Save this key before continuing. 请先保存此密钥：
          <code>{{ createdSecret }}</code>
        </p>
        <input
          v-model="secret"
          type="password"
          autocomplete="off"
          aria-label="API key"
          required
        />
        <button
          type="submit"
          :disabled="busy"
        >Sign in / 登录</button>
      </template>
      <button
        v-else
        type="button"
        @click="initialize"
      >Retry / 重试</button>
      <p
        v-if="error"
        role="alert"
      >{{ error }}</p>
    </form>
  </main>
</template>

<style scoped>
.security-screen {
  min-height: 100vh;
  display: grid;
  place-items: center;
  padding: 24px;
}
.security-card {
  width: min(460px, 100%);
  display: grid;
  gap: 16px;
  padding: 24px;
  border: 1px solid #8885;
  border-radius: 12px;
}
.security-card input, .security-card button {
  padding: 10px;
}
.security-card code {
  display: block;
  overflow-wrap: anywhere;
}
.security-signout {
  position: fixed;
  right: 12px;
  bottom: 12px;
  z-index: 500;
}
.security-notice {
  position: fixed;
  top: 12px;
  left: 50%;
  transform: translateX(-50%);
  padding: 12px;
  background: #802b2b;
  color: white;
  z-index: 1000;
}
</style>
