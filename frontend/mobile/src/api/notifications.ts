import { apiRequest } from './client'

export type AppNotificationType =
  | 'FriendRequestReceived'
  | 'FriendRequestAccepted'
  | 'UserFollowed'
  | 'PostReaction'
  | 'PostComment'
  | 'CommentReaction'
  | 'PostShared'
  | 'PostMention'
  | 'CommentMention'
  | 'GroupInvite'
  | 'GroupJoinApproved'
  | 'StoryReaction'
  | 'PageRoleInvite'
  | 'EventInvite'
  | 'EventUpdated'
  | 'EventCancelled'
  | 'AccountWarning'

export type AppNotificationEntityType =
  | 'FriendRequest'
  | 'UserFollow'
  | 'Post'
  | 'Comment'
  | 'Group'
  | 'GroupJoinRequest'
  | 'GroupInvite'
  | 'Story'
  | 'Page'
  | 'PageRoleInvitation'
  | 'Event'

export interface AppNotification {
  id: string
  recipientUserId: string
  actorUserId: string | null
  actorUsername: string | null
  actorDisplayName: string | null
  type: AppNotificationType
  entityType: AppNotificationEntityType | null
  entityId: string | null
  parentEntityId: string | null
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
