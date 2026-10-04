import assert from 'node:assert/strict'
import test from 'node:test'
import {
  markNotificationRead,
  mergeNotificationItems,
  receiveNotification,
  restoreNotificationReads,
} from '../src/realtime/notificationState.ts'

const notification = (id, overrides = {}) => ({
  id,
  recipientUserId: 'recipient',
  actorUserId: 'actor',
  actorUsername: 'minh',
  actorDisplayName: 'Minh',
  type: 'PostReaction',
  entityType: 'Post',
  entityId: 'post',
  parentEntityId: null,
  isRead: false,
  createdAtUtc: '2026-10-04T08:00:00Z',
  readAtUtc: null,
  ...overrides,
})

test('a realtime notification increments once and remains after a duplicate page arrives', () => {
  const initial = { items: [notification('old')], unreadCount: 5 }
  const incoming = notification('new', { createdAtUtc: '2026-10-04T08:05:00Z' })
  const received = receiveNotification(initial, incoming)
  assert.equal(received.unreadCount, 6)
  assert.equal(receiveNotification(received, incoming), received)
  const merged = mergeNotificationItems(received.items, [incoming, notification('old')], new Map())
  assert.deepEqual(merged.map((item) => item.id), ['new', 'old'])
  assert.equal(initial.items.length, 1)
})

test('a read realtime notification does not add unread count', () => {
  const state = receiveNotification({ items: [], unreadCount: 3 }, notification('read', { isRead: true }))
  assert.equal(state.unreadCount, 3)
})

test('refetch preserves current order and pagination inserts older records without duplicates', () => {
  const current = [
    notification('realtime', { createdAtUtc: '2026-10-04T07:59:00Z' }),
    notification('loaded'),
  ]
  const merged = mergeNotificationItems(current, [
    notification('loaded', { isRead: true }),
    notification('older', { createdAtUtc: '2026-10-03T08:00:00Z' }),
    notification('older', { createdAtUtc: '2026-10-03T08:00:00Z' }),
  ], new Map())
  assert.deepEqual(merged.map((item) => item.id), ['realtime', 'loaded', 'older'])
  assert.equal(merged[1].isRead, true)
})

test('an optimistic read decrements immediately once and survives a stale refetch', () => {
  const item = notification('first')
  const state = { items: [item], unreadCount: 5 }
  const readAtUtc = '2026-10-04T08:06:00Z'
  const read = markNotificationRead(state, 'first', readAtUtc)
  assert.equal(read.unreadCount, 4)
  assert.equal(read.items[0].isRead, true)
  assert.equal(markNotificationRead(read, 'first', readAtUtc), read)
  const merged = mergeNotificationItems(read.items, [item], new Map([
    ['first', { isRead: true, readAtUtc }],
  ]))
  assert.equal(merged[0].isRead, true)
  assert.equal(merged[0].readAtUtc, readAtUtc)
  assert.equal(item.isRead, false)
})

test('failed single read restores its unread state while preserving a concurrent arrival', () => {
  const original = notification('first')
  const read = markNotificationRead({ items: [original], unreadCount: 5 }, 'first', '2026-10-04T08:06:00Z')
  const withArrival = receiveNotification(read, notification('incoming'))
  const restored = restoreNotificationReads(withArrival, [original], 1)
  assert.equal(restored.unreadCount, 6)
  assert.deepEqual(restored.items.map((item) => [item.id, item.isRead]), [['incoming', false], ['first', false]])
})

test('failed mark-all restores loaded reads and backend count without removing arrivals', () => {
  const unread = notification('unread')
  const alreadyRead = notification('read', { isRead: true, readAtUtc: '2026-10-04T08:01:00Z' })
  const afterAll = {
    items: [{ ...unread, isRead: true, readAtUtc: '2026-10-04T08:06:00Z' }, alreadyRead],
    unreadCount: 0,
  }
  const withArrival = receiveNotification(afterAll, notification('incoming'))
  const restored = restoreNotificationReads(withArrival, [unread], 12)
  assert.equal(restored.unreadCount, 13)
  assert.equal(restored.items.find((item) => item.id === 'unread').isRead, false)
  assert.equal(restored.items.find((item) => item.id === 'incoming').isRead, false)
  assert.equal(restored.items.find((item) => item.id === 'read').readAtUtc, alreadyRead.readAtUtc)
})
