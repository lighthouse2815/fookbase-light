import { apiRequest } from './client'

export interface AppNotification {
  id: string
  recipientUserId: string
  actorUserId: string | null
  actorUsername: string | null
  actorDisplayName: string | null
  type: 'FriendRequestReceived' | 'FriendRequestAccepted' | 'PostReaction' | 'PostComment' | 'CommentReaction' | 'PostMention' | 'CommentMention' | 'GroupInvite' | 'GroupJoinApproved'
  entityType: 'FriendRequest' | 'Post' | 'Comment' | 'Group' | 'GroupJoinRequest' | 'GroupInvite' | null
  entityId: string | null
  isRead: boolean
  createdAtUtc: string
  readAtUtc: string | null
}

export interface NotificationPage {
  items: AppNotification[]
  nextCursor: string | null
}

interface NotificationCount {
  unreadNotificationCount: number
}

export const notificationsApi = {
  getPage: (before?: string, limit = 20) => {
    const query = new URLSearchParams({ limit: String(limit) })
    if (before) query.set('before', before)
    return apiRequest<NotificationPage>('/api/notifications?' + query.toString())
  },
  getUnreadCount: () =>
    apiRequest<NotificationCount>('/api/notifications/unread-count'),
  markRead: (notificationId: string) =>
    apiRequest<void>('/api/notifications/' + notificationId + '/read', { method: 'POST' }),
  markAllRead: () =>
    apiRequest<void>('/api/notifications/read-all', { method: 'POST' }),
}
