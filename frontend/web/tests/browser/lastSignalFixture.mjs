export async function fixtures(context, authenticated = true) {
  await context.addInitScript((signedIn) => {
    if (signedIn) {
      const session = { user: { id: '00000000-0000-0000-0000-000000000007', username: 'explorer', email: null, roles: [] }, accessToken: 'browser-fixture', accessTokenExpiresAt: new Date(Date.now() + 7200000).toISOString() }
      localStorage.setItem('fookbase.session', JSON.stringify(session))
      localStorage.setItem('fookbase.accessToken', session.accessToken)
    }
    // Observe native audio contexts; all oscillators/buffers still use real Web Audio.
    window.__lastSignalAudioContexts = []
    const NativeAudioContext = window.AudioContext
    if (NativeAudioContext) window.AudioContext = class extends NativeAudioContext {
      constructor(...args) { super(...args); window.__lastSignalAudioContexts.push(this) }
    }
  }, authenticated)
  const queues = new Map()
  const starts = new Set()
  let token = 0
  await context.route('**/hubs/**', async (route) => {
    const request = route.request()
    const url = new URL(request.url())
    if (url.pathname.endsWith('/negotiate')) return route.fulfill({ json: { negotiateVersion: 1, connectionId: String(++token), connectionToken: String(token), availableTransports: [{ transport: 'LongPolling', transferFormats: ['Text'] }] } })
    const id = url.searchParams.get('id')
    if (request.method() === 'DELETE') return route.fulfill({ status: 202, body: '' })
    if (request.method() === 'POST') {
      if (request.postData()?.includes('protocol')) queues.set(id, '{}\x1e')
      return route.fulfill({ status: 200, body: '' })
    }
    if (!starts.has(id)) { starts.add(id); return route.fulfill({ status: 200, body: '' }) }
    await new Promise((resolve) => setTimeout(resolve, 400))
    const body = queues.get(id) ?? '{"type":6}\x1e'
    queues.delete(id)
    await route.fulfill({ status: 200, contentType: 'text/plain', body }).catch(() => undefined)
  })
  await context.route('**/api/**', (route) => {
    const path = new URL(route.request().url()).pathname
    if (!path.startsWith('/api/')) return route.continue()
    const data = path === '/api/users/me'
      ? { id: '00000000-0000-0000-0000-000000000007', userId: '00000000-0000-0000-0000-000000000007', username: 'explorer', firstName: 'Game', lastName: 'Tester', displayName: 'Game Tester', profileImageUrl: null }
      : path.startsWith('/api/auth/') ? { data: { google: false } }
        : { items: [], nextCursor: null, unreadNotificationCount: 0, total: 0 }
    return route.fulfill({ json: data })
  })
}

export function trackErrors(page, errors) {
  page.on('pageerror', (error) => errors.push(error.stack))
  page.on('console', (message) => {
    // Existing React StrictMode aborts the first SignalR startup during effect cleanup.
    if (message.type() === 'error' && !/connection was stopped during negotiation|HttpConnection before stop\(\)/.test(message.text())) errors.push(message.text())
  })
}
