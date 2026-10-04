import assert from 'node:assert/strict'
import { test } from 'node:test'
import { fileURLToPath } from 'node:url'
import { mkdtemp, rm } from 'node:fs/promises'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import { createServer } from 'vite'

const success = (data) => ({ success: true, data, error: null, requestId: 'request-1' })
const session = { accessToken: 'fresh', refreshToken: 'next-refresh', user: { id: 'user-1' } }

for (const app of ['web', 'admin', 'zola-light']) {
  await test(`${app} Identity response contract`, async (t) => {
    const storage = new Map()
    globalThis.localStorage = {
      getItem: (key) => storage.get(key) ?? null,
      setItem: (key, value) => storage.set(key, value),
      removeItem: (key) => storage.delete(key),
    }
    globalThis.window = new EventTarget()
    const cacheDir = await mkdtemp(join(tmpdir(), `fookbase-auth-${app}-`))
    const server = await createServer({
      root: fileURLToPath(new URL(`../../../${app}/`, import.meta.url)),
      configFile: false,
      cacheDir,
      server: { middlewareMode: true },
      appType: 'custom',
    })
    t.after(async () => { await server.close(); await rm(cacheDir, { recursive: true, force: true }) })
    const client = await server.ssrLoadModule(app === 'zola-light' ? '/src/api.ts' : '/src/api/client.ts')
    const request = client.apiRequest ?? client.request

    await t.test('unwraps successful Identity data', async (t) => {
      t.mock.method(globalThis, 'fetch', async () => Response.json(success(session)))
      assert.deepEqual(await request('/api/auth/login', { method: 'POST' }), session)
    })

    await t.test('handles successful actions without data', async (t) => {
      t.mock.method(globalThis, 'fetch', async () => Response.json(success(null)))
      assert.equal(await request('/api/auth/logout', { method: 'POST' }), null)
    })

    await t.test('reads the shared error message', async (t) => {
      t.mock.method(globalThis, 'fetch', async () => Response.json({
        success: false, data: null, error: { code: 'invalid_credentials', message: 'Invalid login.', details: null }, requestId: 'request-1',
      }, { status: 401 }))
      await assert.rejects(request('/api/auth/login', { method: 'POST' }), { message: 'Invalid login.', status: 401 })
    })

    await t.test('preserves other modules and their legacy error format', async (t) => {
      const responses = [Response.json({ items: [] }), Response.json({ detail: 'Forbidden.' }, { status: 403 })]
      t.mock.method(globalThis, 'fetch', async () => responses.shift())
      assert.deepEqual(await request('/api/feed'), { items: [] })
      await assert.rejects(request('/api/feed'), { message: 'Forbidden.', status: 403 })
    })

    await t.test('refreshes from envelope data and retries with the new access token', async (t) => {
      const sessions = app === 'zola-light' ? client : await server.ssrLoadModule(
        app === 'web' ? '/src/auth/session.ts' : '/src/session.ts')
      const save = sessions.saveAuthSession ?? sessions.saveSession
      const get = sessions.getAuthSession ?? sessions.getSession
      save({ ...session, accessToken: 'expired', refreshToken: 'old-refresh' })
      const responses = [Response.json({}, { status: 401 }), Response.json(success(session)), Response.json({ items: [] })]
      const fetch = t.mock.method(globalThis, 'fetch', async () => responses.shift())

      assert.deepEqual(await request('/api/feed'), { items: [] })
      assert.equal(fetch.mock.callCount(), 3)
      const refreshCall = fetch.mock.calls[1].arguments
      assert.ok(refreshCall[0].endsWith('/api/auth/refresh'))
      assert.deepEqual(JSON.parse(refreshCall[1].body), {})
      const expectedTransport = app === 'web' ? 'cookie:web' : app === 'admin' ? 'cookie:admin' : 'cookie:zola-light'
      assert.equal(new Headers(refreshCall[1].headers).get('X-Fookbase-Auth-Transport'), expectedTransport)
      assert.equal(refreshCall[1].credentials, 'include')
      assert.equal(new Headers(fetch.mock.calls[2].arguments[1].headers).get('Authorization'), 'Bearer fresh')
      assert.deepEqual(get(), { accessToken: 'fresh', user: { id: 'user-1' } })
    })

    const sessions = app === 'zola-light' ? client : await server.ssrLoadModule(
      app === 'web' ? '/src/auth/session.ts' : '/src/session.ts')
    const save = sessions.saveAuthSession ?? sessions.saveSession
    const get = sessions.getAuthSession ?? sessions.getSession
    const clear = sessions.clearAuthSession ?? sessions.clearSession

    for (const status of [429, 503]) {
      await t.test(`preserves the session when refresh returns ${status}`, async (t) => {
        save({ ...session, accessToken: 'expired' })
        const responses = [Response.json({}, { status: 401 }), Response.json({ detail: 'Try later.' }, { status })]
        t.mock.method(globalThis, 'fetch', async () => responses.shift())
        await assert.rejects(request('/api/feed'), { status })
        assert.equal(get().accessToken, 'expired')
      })
    }

    await t.test('preserves the session on a network failure during refresh', async (t) => {
      save({ ...session, accessToken: 'expired' })
      let calls = 0
      t.mock.method(globalThis, 'fetch', async () => {
        if (++calls === 1) return Response.json({}, { status: 401 })
        throw new TypeError('Network unavailable')
      })
      await assert.rejects(request('/api/feed'), { status: 0 })
      assert.equal(get().accessToken, 'expired')
    })

    await t.test('clears an invalid session when refresh is rejected', async (t) => {
      save({ ...session, accessToken: 'expired' })
      t.mock.method(globalThis, 'fetch', async () => Response.json({}, { status: 401 }))
      await assert.rejects(request('/api/feed'), { status: 401 })
      assert.equal(get(), null)
    })

    await t.test('coalesces refresh requests made by concurrent expired requests', async (t) => {
      save({ ...session, accessToken: 'expired' })
      let refreshCalls = 0
      let releaseRefresh
      const gate = new Promise((resolve) => { releaseRefresh = resolve })
      t.mock.method(globalThis, 'fetch', async (url, init) => {
        if (url.endsWith('/api/auth/refresh')) {
          refreshCalls++
          await gate
          return Response.json(success(session))
        }
        return new Headers(init.headers).get('Authorization') === 'Bearer fresh'
          ? Response.json({ items: [] }) : Response.json({}, { status: 401 })
      })
      const requests = [request('/api/feed'), request('/api/messages/conversations')]
      await new Promise((resolve) => setImmediate(resolve))
      assert.equal(refreshCalls, 1)
      releaseRefresh()
      assert.deepEqual(await Promise.all(requests), [{ items: [] }, { items: [] }])
    })

    await t.test('does not restore a session after the user signs out during refresh', async (t) => {
      save({ ...session, accessToken: 'expired' })
      t.mock.method(globalThis, 'fetch', async (url) => {
        if (url.endsWith('/api/auth/refresh')) {
          clear()
          return Response.json(success(session))
        }
        return Response.json({}, { status: 401 })
      })
      await assert.rejects(request('/api/feed'), { status: 401 })
      assert.equal(get(), null)
    })
  })
}
