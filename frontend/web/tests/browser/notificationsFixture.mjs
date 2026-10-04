import { fixtures } from './lastSignalFixture.mjs'
import { basePost, postId, viewerId } from './postInteractionsFixture.mjs'

export { postId, viewerId }
export const actorId = '00000000-0000-0000-0000-000000000008'
export const eventId = '00000000-0000-0000-0000-000000000301'
export const commentId = '00000000-0000-0000-0000-000000000401'
const pause = (milliseconds) => new Promise((resolve) => setTimeout(resolve, milliseconds))

export function notification(index, overrides = {}) {
  return {
    id: `00000000-0000-0000-0000-${String(500 + index).padStart(12, '0')}`,
    recipientUserId: viewerId, actorUserId: actorId, actorUsername: 'minh', actorDisplayName: 'Minh',
    type: 'PostReaction', entityType: 'Post', entityId: postId, parentEntityId: null,
    isRead: false, createdAtUtc: new Date(Date.now() - index * 60_000).toISOString(), readAtUtc: null,
    ...overrides,
  }
}

// The real SignalR client negotiates, handshakes and polls this transport. No app hooks are installed.
export async function notificationFixtures(context, options = {}) {
  await fixtures(context)
  await context.addInitScript(() => localStorage.setItem('fookbase.preferences', JSON.stringify({ language: 'vi', theme: 'dark' })))
  const initial = options.items ?? [notification(1), notification(2, { isRead: true, createdAtUtc: '2025-01-01T00:00:00Z', readAtUtc: '2025-01-01T00:01:00Z' })]
  const state = {
    items: structuredClone(initial), unreadCount: options.unreadCount ?? initial.filter((item) => !item.isRead).length,
    pageDelay: options.pageDelay ?? 0, countDelay: options.countDelay ?? 0, readDelay: 600, allReadDelay: 600,
    readAllIncludesArrivals: options.readAllIncludesArrivals ?? false,
    failInitial: options.failInitial ?? false, failMore: false, failRead: false, failAllRead: false,
    reads: [], writes: [], countReads: 0, profileReads: [], readResponses: 0, allReadResponses: 0,
    connections: new Map(), negotiations: 0,
  }
  await context.route('**/hubs/notifications**', async (route) => {
    const request = route.request()
    const url = new URL(request.url())
    if (url.pathname.endsWith('/negotiate')) {
      const id = `notification-${++state.negotiations}`
      state.connections.set(id, { started: false, connected: false, queue: [] })
      return route.fulfill({ json: { negotiateVersion: 1, connectionId: id, connectionToken: id, availableTransports: [{ transport: 'LongPolling', transferFormats: ['Text'] }] } })
    }
    const id = url.searchParams.get('id')
    const connection = state.connections.get(id)
    if (request.method() === 'DELETE') {
      state.connections.delete(id)
      return route.fulfill({ status: 202, body: '' })
    }
    if (!connection) return route.fulfill({ status: 404, body: '' }).catch(() => undefined)
    if (request.method() === 'POST') {
      if (request.postData()?.includes('protocol')) {
        connection.queue.unshift('{}\x1e')
        connection.connected = true
      }
      return route.fulfill({ status: 200, body: '' }).catch(() => undefined)
    }
    if (!connection.started) {
      connection.started = true
      return route.fulfill({ status: 200, body: '' }).catch(() => undefined)
    }
    await pause(70)
    const body = connection.queue.splice(0).join('') || '{"type":6}\x1e'
    return route.fulfill({ status: 200, contentType: 'text/plain', body }).catch(() => undefined)
  })
  await context.route('**/api/**', async (route) => {
    const request = route.request()
    const url = new URL(request.url())
    const path = url.pathname
    if (path === '/api/notifications/unread-count') {
      state.countReads++
      const unreadNotificationCount = state.unreadCount
      await pause(state.countDelay)
      return route.fulfill({ json: { unreadNotificationCount } })
    }
    if (path === '/api/notifications') {
      const before = url.searchParams.get('before')
      const limit = Number(url.searchParams.get('limit') ?? 20)
      const offset = before ? Math.max(0, state.items.findIndex((item) => item.id === before) + 1) : 0
      const items = structuredClone(state.items.slice(offset, offset + limit))
      const nextCursor = offset + items.length < state.items.length ? items.at(-1)?.id ?? null : null
      const fail = before ? state.failMore : state.failInitial
      state.reads.push({ before, limit, ids: items.map((item) => item.id) })
      await pause(state.pageDelay)
      return route.fulfill(fail ? { status: 503, json: { detail: 'Không thể tải thông báo thử nghiệm.' } } : { json: { items, nextCursor } })
    }
    if (path === '/api/notifications/read-all') {
      state.writes.push({ action: 'all-read' })
      const ids = new Set(state.items.map((item) => item.id))
      const fail = state.failAllRead
      await pause(state.allReadDelay)
      state.allReadResponses++
      if (fail) return route.fulfill({ status: 503, json: { detail: 'Không thể đánh dấu thông báo thử nghiệm.' } })
      const included = (item) => state.readAllIncludesArrivals || ids.has(item.id)
      const newlyRead = state.items.filter((item) => included(item) && !item.isRead).length
      state.items = state.items.map((item) => included(item) ? { ...item, isRead: true, readAtUtc: new Date().toISOString() } : item)
      state.unreadCount = Math.max(0, state.unreadCount - newlyRead)
      return route.fulfill({ status: 204 })
    }
    if (/^\/api\/notifications\/[^/]+\/read$/.test(path)) {
      const id = path.split('/')[3]
      state.writes.push({ action: 'read', id })
      const fail = state.failRead
      await pause(state.readDelay)
      state.readResponses++
      if (fail) return route.fulfill({ status: 503, json: { detail: 'Không thể đánh dấu thông báo thử nghiệm.' } })
      const item = state.items.find((candidate) => candidate.id === id)
      if (item && !item.isRead) { item.isRead = true; item.readAtUtc = new Date().toISOString(); state.unreadCount = Math.max(0, state.unreadCount - 1) }
      return route.fulfill({ status: 204 })
    }
    if (path.startsWith('/api/users/') && !path.includes('/search')) state.profileReads.push(path)
    if (path === `/api/posts/${postId}`) return route.fulfill({ json: basePost })
    if (path === `/api/posts/${postId}/comments`) return route.fulfill({ json: { items: [], total: 0, offset: 0, limit: 20 } })
    if (path === `/api/events/${eventId}`) return route.fulfill({ json: {
      id: eventId, name: 'Sự kiện kiểm tra thông báo', description: null, displayHost: { type: 'user', id: actorId, name: 'Minh' },
      privacy: 'public', locationType: 'online', locationName: null, address: null, onlineUrl: null,
      startsAtUtc: '2027-01-01T00:00:00Z', endsAtUtc: null, status: 'published', coverUrl: null,
      goingCount: 0, interestedCount: 0, viewerRsvpStatus: null, canManage: false, canPost: false,
      createdAtUtc: '2026-01-01T00:00:00Z', updatedAtUtc: null,
    } })
    if (path === '/api/birthdays/today') return route.fulfill({ json: [] })
    return route.fallback()
  })
  state.waitConnected = async () => {
    for (let attempt = 0; attempt < 200; attempt++) {
      if ([...state.connections.values()].some((connection) => connection.connected)) return
      await pause(30)
    }
    throw new Error('Notification SignalR transport did not connect')
  }
  state.push = async (item, persist = true) => {
    await state.waitConnected()
    if (persist && !state.items.some((candidate) => candidate.id === item.id)) {
      state.items.unshift(structuredClone(item))
      if (!item.isRead) state.unreadCount++
    }
    for (const connection of state.connections.values()) if (connection.connected) {
      connection.queue.push(JSON.stringify({ type: 1, target: 'NotificationReceived', arguments: [item] }) + '\x1e')
    }
  }
  state.reconnect = async () => {
    await state.waitConnected()
    for (const connection of state.connections.values()) if (connection.connected) {
      connection.queue.push(JSON.stringify({ type: 7, error: 'Browser fixture reconnect', allowReconnect: true }) + '\x1e')
    }
  }
  return state
}
