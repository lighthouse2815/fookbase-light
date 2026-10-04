import type { AppNotification, AppNotificationType } from '../api/notifications'

export const notificationMessages: Record<AppNotificationType, string> = {
  FriendRequestReceived: 'đã gửi cho bạn lời mời kết bạn.',
  FriendRequestAccepted: 'đã chấp nhận lời mời kết bạn của bạn.',
  UserFollowed: 'đã bắt đầu theo dõi bạn.',
  PostReaction: 'đã bày tỏ cảm xúc về bài viết của bạn.',
  PostComment: 'đã bình luận về bài viết của bạn.',
  CommentReaction: 'đã bày tỏ cảm xúc về bình luận của bạn.',
  PostShared: 'đã chia sẻ bài viết của bạn.',
  PostMention: 'đã nhắc đến bạn trong một bài viết.',
  CommentMention: 'đã nhắc đến bạn trong một bình luận.',
  GroupInvite: 'đã mời bạn tham gia một nhóm.',
  GroupJoinApproved: 'đã chấp nhận yêu cầu tham gia nhóm của bạn.',
  StoryReaction: 'đã bày tỏ cảm xúc về tin của bạn.',
  PageRoleInvite: 'đã mời bạn quản lý một trang.',
  EventInvite: 'đã mời bạn tham gia một sự kiện.',
  EventUpdated: 'Sự kiện bạn quan tâm vừa được cập nhật.',
  EventCancelled: 'Sự kiện đã bị hủy.',
  AccountWarning: 'Tài khoản của bạn đã nhận một cảnh báo.',
}

const anonymousNotificationMessages: Record<AppNotificationType, string> = {
  FriendRequestReceived: 'Bạn có một lời mời kết bạn mới.',
  FriendRequestAccepted: 'Lời mời kết bạn của bạn đã được chấp nhận.',
  UserFollowed: 'Bạn có người theo dõi mới.',
  PostReaction: 'Bài viết của bạn có cảm xúc mới.',
  PostComment: 'Bài viết của bạn có bình luận mới.',
  CommentReaction: 'Bình luận của bạn có cảm xúc mới.',
  PostShared: 'Bài viết của bạn đã được chia sẻ.',
  PostMention: 'Bạn được nhắc đến trong một bài viết.',
  CommentMention: 'Bạn được nhắc đến trong một bình luận.',
  GroupInvite: 'Bạn được mời tham gia một nhóm.',
  GroupJoinApproved: 'Yêu cầu tham gia nhóm của bạn đã được chấp nhận.',
  StoryReaction: 'Tin của bạn có cảm xúc mới.',
  PageRoleInvite: 'Bạn được mời quản lý một trang.',
  EventInvite: 'Bạn được mời tham gia một sự kiện.',
  EventUpdated: notificationMessages.EventUpdated,
  EventCancelled: notificationMessages.EventCancelled,
  AccountWarning: notificationMessages.AccountWarning,
}

export type NotificationIcon = 'friends' | 'heart' | 'comment' | 'share' | 'groups' | 'page' | 'story' | 'calendar' | 'shield' | 'bell'

interface NotificationBadge {
  icon: string
  className: string
}

export interface NotificationPresentation {
  actor: string | null
  message: string
  text: string
  badge: NotificationBadge
  destination: string
  isSystem: boolean
  icon: NotificationIcon
  canToast: boolean
}

const systemNotificationTypes = new Set<AppNotificationType>([
  'EventUpdated',
  'EventCancelled',
  'AccountWarning',
])

const emptyGuid = '00000000-0000-0000-0000-000000000000'

function getEntityId(value: string | null) {
  const id = value?.trim()
  return id && id !== emptyGuid ? encodeURIComponent(id) : null
}

function getCommentDestination(notification: AppNotification) {
  const postId = getEntityId(notification.parentEntityId)
  const commentId = getEntityId(notification.entityId)
  return postId ? `/posts/${postId}${commentId ? `#comment-${commentId}` : ''}` : '/notifications'
}

function getNotificationDestination(notification: AppNotification): string {
  const entityId = getEntityId(notification.entityId)
  const actorId = getEntityId(notification.actorUserId)
  switch (notification.type) {
    case 'FriendRequestReceived':
    case 'FriendRequestAccepted':
    case 'UserFollowed':
      return actorId ? `/profile/${actorId}` : '/explore'
    case 'PostReaction':
    case 'PostComment':
    case 'PostShared':
    case 'PostMention':
      if (notification.entityType === 'Comment') return getCommentDestination(notification)
      return entityId && (notification.entityType === 'Post' || notification.entityType === null)
        ? `/posts/${entityId}` : '/notifications'
    case 'CommentReaction':
    case 'CommentMention':
      return getCommentDestination(notification)
    case 'EventInvite': {
      // EntityId is the invitation ID, not the event ID.
      const eventId = getEntityId(notification.parentEntityId)
      return eventId ? `/events/${eventId}` : '/events'
    }
    case 'EventUpdated':
    case 'EventCancelled':
      return entityId ? `/events/${entityId}` : '/events'
    case 'GroupInvite':
    case 'GroupJoinApproved':
      // Current invite/request DTOs do not include the group ID.
      return notification.entityType === 'Group' && entityId ? `/groups/${entityId}` : '/groups'
    case 'PageRoleInvite':
      // Page routes need a username; a role invitation ID cannot identify a page.
      return '/pages'
    case 'StoryReaction':
      // Stories are opened from the feed; no story ID route exists.
      return '/feed'
    case 'AccountWarning':
      return '/settings/security'
    default:
      return '/notifications'
  }
}

function getNotificationIcon(type: AppNotificationType): NotificationIcon {
  switch (type) {
    case 'FriendRequestReceived': case 'FriendRequestAccepted': case 'UserFollowed': return 'friends'
    case 'PostReaction': case 'CommentReaction': return 'heart'
    case 'PostComment': case 'CommentMention': return 'comment'
    case 'PostShared': return 'share'
    case 'PostMention': return 'bell'
    case 'GroupInvite': case 'GroupJoinApproved': return 'groups'
    case 'PageRoleInvite': return 'page'
    case 'StoryReaction': return 'story'
    case 'EventInvite': case 'EventUpdated': case 'EventCancelled': return 'calendar'
    case 'AccountWarning': return 'shield'
    default: return 'bell'
  }
}

function getNotificationBadge(notification: AppNotification): NotificationBadge {
  if (notification.type === 'PostReaction' || notification.type === 'CommentReaction' || notification.type === 'StoryReaction') return { icon: '♥', className: 'bg-[#f02849]' }
  if (notification.type === 'PostComment' || notification.type === 'CommentMention') return { icon: '●', className: 'bg-[#1877f2]' }
  if (notification.type === 'FriendRequestReceived' || notification.type === 'FriendRequestAccepted' || notification.type === 'UserFollowed') return { icon: '♟', className: 'bg-[#31a24c]' }
  return { icon: '●', className: 'bg-[#1877f2]' }
}

export function getNotificationPresentation(notification: AppNotification): NotificationPresentation {
  const knownType = Object.hasOwn(notificationMessages, notification.type)
  const systemType = systemNotificationTypes.has(notification.type)
  const actorNames = [notification.actorDisplayName, notification.actorUsername]
  const actor = !systemType && getEntityId(notification.actorUserId)
    ? actorNames.find((name) => name?.trim() && !/^[\da-f]{8}-[\da-f]{4}-[\da-f]{4}-[\da-f]{4}-[\da-f]{12}$/i.test(name.trim()))?.trim() ?? null
    : null
  const message = knownType
    ? actor ? notificationMessages[notification.type] : anonymousNotificationMessages[notification.type]
    : 'Bạn có một thông báo mới.'
  const destination = getNotificationDestination(notification)

  return {
    actor,
    message,
    text: actor ? `${actor} ${message}` : message,
    badge: getNotificationBadge(notification),
    destination,
    isSystem: systemType || actor === null,
    icon: getNotificationIcon(notification.type),
    canToast: knownType && (systemType || actor !== null) && destination !== '/notifications',
  }
}
