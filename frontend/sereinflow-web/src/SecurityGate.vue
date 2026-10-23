<script setup lang="ts">
import { computed, onBeforeUnmount, onMounted, ref } from 'vue';
import App from './App.vue';
import { LogOut } from 'lucide-vue-next';
import { t } from './i18n';
import { createInitialAdministrator, hasSecuritySession, loadSecurityCapabilities, loginWithKey, logoutSecuritySession, securityCapabilities } from './api/securityApi';
const ready = ref(false);
const busy = ref(false);
const isInitializing = ref(false);
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
    if (isInitializing.value) {
        return;
    }
    isInitializing.value = true;
    error.value = '';
    try {
        const capabilities = await loadSecurityCapabilities();
        ready.value = capabilities.mode === 'None' || capabilities.mode === 'Host' || await hasSecuritySession();
    }
    catch (failure) {
        error.value = String(failure);
    }
    finally {
        isInitializing.value = false;
    }
}
async function login(): Promise<void> {
    busy.value = true;
    error.value = '';
    try {
        await loginWithKey(secret.value);
        secret.value = '';
        createdSecret.value = '';
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
      class="command-button quiet security-signout"
      type="button"
      @click="logoutSecuritySession"
    ><LogOut :size="15" />{{ t('security.signOut') }}</button>
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
      <p v-if="isInitializing" role="status">{{ t('security.loading') }}</p>
      <p v-else-if="hostMode">{{ t('security.hostSignInHint') }}</p>
      <template v-else-if="securityCapabilities">
        <p>{{ t('security.signInHint') }}</p>
        <button
          v-if="securityCapabilities.initialSetupRequired"
          class="command-button quiet"
          type="button"
          :disabled="busy"
          @click="setup"
        >{{ t('security.initialSetup') }}</button>
        <p v-if="createdSecret">
          {{ t('security.saveInitialKey') }}
          <code>{{ createdSecret }}</code>
        </p>
        <label class="security-card__credential">
          <span>{{ t('security.apiKey') }}</span>
          <input
            v-model="secret"
            type="password"
            autocomplete="off"
            required
          />
        </label>
        <button
          class="command-button run"
          type="submit"
          :disabled="busy"
        >{{ t('security.signIn') }}</button>
      </template>
      <button
        v-else
        class="command-button quiet"
        type="button"
        @click="initialize"
      >{{ t('security.retry') }}</button>
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
  border: 1px solid var(--line-strong);
  border-radius: 12px;
  background: var(--surface);
}
.security-card h1 {
  margin: 0;
  font-size: 20px;
}
.security-card p {
  margin: 0;
  color: var(--muted);
  line-height: 1.6;
}
.security-card__credential {
  display: grid;
  gap: 6px;
  color: var(--muted);
  font-size: 13px;
}
.security-card input {
  width: 100%;
  min-width: 0;
  height: 40px;
  padding: 0 10px;
  border: 1px solid var(--line-strong);
  border-radius: 3px;
  background: var(--surface);
  color: var(--ink);
}
.security-card .command-button {
  justify-content: center;
  width: 100%;
  height: 40px;
}
.security-card [role='alert'] {
  color: var(--danger);
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
