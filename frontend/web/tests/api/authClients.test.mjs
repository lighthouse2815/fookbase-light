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
  })
}
