import type { AppNotification } from '../api/notifications'

export interface NotificationState {
  items: AppNotification[]
  unreadCount: number
}

export type NotificationReadOverrides = ReadonlyMap<string, Pick<AppNotification, 'isRead' | 'readAtUtc'>>

export function mergeNotificationItems(
  current: AppNotification[],
  incoming: AppNotification[],
  readOverrides: NotificationReadOverrides,
) {
  const byId = new Map(incoming.map((item) => [item.id, item]))
  const items = current.map((item) => {
    const next = byId.get(item.id) ?? item
    const readOverride = readOverrides.get(item.id)
    byId.delete(item.id)
    return readOverride ? { ...next, ...readOverride } : next
  })
  const additionalItems = [...byId.values()].sort((left, right) =>
    Date.parse(right.createdAtUtc) - Date.parse(left.createdAtUtc) || right.id.localeCompare(left.id))
  for (const item of additionalItems) {
    const readOverride = readOverrides.get(item.id)
    const next = readOverride ? { ...item, ...readOverride } : item
    const index = items.findIndex((existing) => Date.parse(existing.createdAtUtc) < Date.parse(item.createdAtUtc))
    if (index === -1) items.push(next)
    else items.splice(index, 0, next)
  }
  return items
}

export function receiveNotification(state: NotificationState, notification: AppNotification): NotificationState {
  if (state.items.some((item) => item.id === notification.id)) return state

  return {
    items: [notification, ...state.items],
    unreadCount: state.unreadCount + (notification.isRead ? 0 : 1),
  }
}

export function markNotificationRead(state: NotificationState, notificationId: string, readAtUtc: string): NotificationState {
  const notification = state.items.find((item) => item.id === notificationId)
  if (!notification || notification.isRead) return state

  return {
    items: state.items.map((item) => item.id === notificationId ? { ...item, isRead: true, readAtUtc } : item),
    unreadCount: Math.max(0, state.unreadCount - 1),
  }
}

export function restoreNotificationReads(
  state: NotificationState,
  snapshot: AppNotification[],
  unreadCountToRestore: number,
): NotificationState {
  const originals = new Map(snapshot.map((item) => [item.id, item]))
  return {
    items: state.items.map((item) => {
      const original = originals.get(item.id)
      return original ? { ...item, isRead: original.isRead, readAtUtc: original.readAtUtc } : item
    }),
    unreadCount: state.unreadCount + unreadCountToRestore,
  }
}
