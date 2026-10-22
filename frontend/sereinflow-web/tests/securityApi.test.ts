import assert from 'node:assert/strict';
import test from 'node:test';
import { authenticatedFetch, createInitialAdministrator, hasSecuritySession, loadSecurityCapabilities, loginWithKey, logoutSecuritySession, securityCapabilities, setBearerTokenProvider } from '../src/api/securityApi.ts';
test('shared requests include cookies and resolve external bearer credentials per request', async () => {
    const originalFetch = globalThis.fetch;
    const requests: RequestInit[] = [];
    let sequence = 0;
    setBearerTokenProvider(async () => `token-${++sequence}`);
    globalThis.fetch = async (_input, init) => {
        requests.push(init!);
        const response = new Response('{}');
        return response;
    };
    try {
        await authenticatedFetch('/api/projects');
        await authenticatedFetch('/api/projects');
        assert.equal(requests[0].credentials, 'include');
        const firstHeaders = new Headers(requests[0].headers);
        const secondHeaders = new Headers(requests[1].headers);
        const firstToken = firstHeaders.get('authorization');
        const secondToken = secondHeaders.get('authorization');
        assert.equal(firstToken, 'Bearer token-1');
        assert.equal(secondToken, 'Bearer token-2');
    }
    finally {
        globalThis.fetch = originalFetch;
        setBearerTokenProvider();
    }
});
test('authentication failures and forbidden responses have distinct UI events', async () => {
    const originalFetch = globalThis.fetch;
    const originalWindow = globalThis.window;
    const events = new EventTarget();
    let authentication = 0;
    let denied = 0;
    events.addEventListener('sereinflow:authentication-required', () => authentication++);
    events.addEventListener('sereinflow:access-denied', () => denied++);
    globalThis.window = events as unknown as Window & typeof globalThis;
    try {
        globalThis.fetch = async () => {
            const response = new Response('', { status: 401 });
            return response;
        };
        await authenticatedFetch('/api/projects');
        globalThis.fetch = async () => {
            const response = new Response('', { status: 403 });
            return response;
        };
        await authenticatedFetch('/api/projects');
        assert.equal(authentication, 1);
        assert.equal(denied, 1);
    }
    finally {
        globalThis.fetch = originalFetch;
        globalThis.window = originalWindow;
    }
});

test('default key initialization, login and logout share a credentialed session', async () => {
    const originalFetch = globalThis.fetch;
    const originalWindow = globalThis.window;
    const events = new EventTarget();
    const requests: { path: string; init?: RequestInit }[] = [];
    let signedOut = false;
    events.addEventListener('sereinflow:authentication-required', () => {
        signedOut = true;
    });
    globalThis.window = events as unknown as Window & typeof globalThis;
    globalThis.fetch = async (input, init) => {
        const path = String(input);
        requests.push({ path, init });
        const payload = path.endsWith('/capabilities')
            ? { mode: 'ApiKey', apiKeyManagement: true, initialSetupRequired: true }
            : { secret: 'created-once' };
        const json = JSON.stringify(payload);
        const response = new Response(json);
        return response;
    };
    try {
        const capabilities = await loadSecurityCapabilities();
        assert.equal(capabilities.initialSetupRequired, true);
        const secret = await createInitialAdministrator();
        await loginWithKey(secret);
        const session = await hasSecuritySession();
        assert.equal(session, true);
        await logoutSecuritySession();
        const loginHeaders = new Headers(requests[2].init?.headers);
        const authorization = loginHeaders.get('authorization');
        assert.equal(authorization, 'Bearer created-once');
        assert.equal(requests[4].init?.method, 'DELETE');
        for (const request of requests) {
            assert.equal(request.init?.credentials, 'include');
            const secretInUrl = request.path.includes(secret);
            assert.equal(secretInUrl, false);
        }
        assert.equal(signedOut, true);
    } finally {
        globalThis.fetch = originalFetch;
        globalThis.window = originalWindow;
        securityCapabilities.value = undefined;
    }
});
