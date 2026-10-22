import { ref } from 'vue';
const apiBaseUrl = import.meta.env?.VITE_API_BASE_URL ?? '';
export interface SecurityCapabilities {
    mode: 'ApiKey' | 'Host';
    apiKeyManagement: boolean;
    initialSetupRequired: boolean;
}
export const securityCapabilities = ref<SecurityCapabilities>();
let tokenProvider: (() => Promise<string | undefined>) | undefined;
export function setBearerTokenProvider(provider?: () => Promise<string | undefined>): void {
    tokenProvider = provider;
}
export async function authenticatedFetch(input: RequestInfo | URL, init: RequestInit = {}): Promise<Response> {
    const headers = new Headers(init.headers);
    const token = await tokenProvider?.();
    if (token && !headers.has('authorization')) {
        headers.set('authorization', `Bearer ${token}`);
    }
    const response = await fetch(input, { ...init, headers, credentials: 'include' });
    if (response.status === 401) {
        const event = new Event('sereinflow:authentication-required');
        window.dispatchEvent(event);
    }
    if (response.status === 403) {
        const event = new Event('sereinflow:access-denied');
        window.dispatchEvent(event);
    }
    return response;
}
export async function loadSecurityCapabilities(): Promise<SecurityCapabilities> {
    const response = await fetch(`${apiBaseUrl}/api/security/capabilities`, { credentials: 'include' });
    if (!response.ok) {
        throw new Error(`Security configuration unavailable (${response.status}).`);
    }
    const capabilities = await response.json() as SecurityCapabilities;
    securityCapabilities.value = capabilities;
    return capabilities;
}
export async function hasSecuritySession(): Promise<boolean> {
    const response = await fetch(`${apiBaseUrl}/api/security/session`, { credentials: 'include' });
    return response.ok;
}
export async function loginWithKey(secret: string): Promise<void> {
    const response = await fetch(`${apiBaseUrl}/api/security/session`, { method: 'POST', headers: { authorization: `Bearer ${secret}` }, credentials: 'include' });
    if (!response.ok) {
        throw new Error(response.status === 401 ? 'Invalid or expired key. 密钥无效或已过期。' : 'Unable to open a session. 无法建立会话。');
    }
}
export async function logoutSecuritySession(): Promise<void> {
    const response = await authenticatedFetch(`${apiBaseUrl}/api/security/session`, { method: 'DELETE' });
    if (!response.ok) {
        throw new Error('Unable to sign out. 注销失败。');
    }
    const event = new Event('sereinflow:authentication-required');
    window.dispatchEvent(event);
}
export async function createInitialAdministrator(): Promise<string> {
    const response = await fetch(`${apiBaseUrl}/api/security/api-keys/initial-admin`, { method: 'POST', credentials: 'include' });
    if (!response.ok) {
        throw new Error(`Initial setup failed (${response.status}). 初始设置失败。`);
    }
    const created = await response.json() as {
        secret: string;
    };
    return created.secret;
}
