import type { AppNotification, AppNotificationType } from '../api/notifications'

export const notificationMessages: Record<AppNotificationType, string> = {
  FriendRequestReceived: 'đã gửi cho bạn lời mời kết bạn.', FriendRequestAccepted: 'đã chấp nhận lời mời kết bạn của bạn.', UserFollowed: 'đã bắt đầu theo dõi bạn.', PostReaction: 'đã bày tỏ cảm xúc về bài viết của bạn.', PostComment: 'đã bình luận về bài viết của bạn.', CommentReaction: 'đã bày tỏ cảm xúc về bình luận của bạn.', PostShared: 'đã chia sẻ bài viết của bạn.', PostMention: 'đã nhắc đến bạn trong một bài viết.', CommentMention: 'đã nhắc đến bạn trong một bình luận.', GroupInvite: 'đã mời bạn tham gia một nhóm.', GroupJoinApproved: 'đã chấp nhận yêu cầu tham gia nhóm của bạn.', StoryReaction: 'đã bày tỏ cảm xúc về Story của bạn.', PageRoleInvite: 'đã mời bạn quản lý một Trang.', EventInvite: 'đã mời bạn tham gia một sự kiện.', EventUpdated: 'Sự kiện bạn quan tâm vừa được cập nhật.', EventCancelled: 'Sự kiện đã bị hủy.', AccountWarning: 'Tài khoản của bạn đã nhận một cảnh báo.',
}

export const getNotificationMessage = (notification: AppNotification) => notificationMessages[notification.type]

export function getNotificationDestination(notification: AppNotification) {
  switch (notification.type) {
    case 'FriendRequestReceived': case 'FriendRequestAccepted': case 'UserFollowed': return notification.actorUserId ? `/profile/${notification.actorUserId}` : '/notifications'
    case 'PostReaction': case 'PostComment': case 'PostShared': case 'PostMention': return notification.entityId ? `/posts/${notification.entityId}` : '/notifications'
    case 'CommentReaction': case 'CommentMention': return notification.entityId && notification.parentEntityId ? `/posts/${notification.parentEntityId}#comment-${notification.entityId}` : '/notifications'
    case 'EventInvite': case 'EventUpdated': case 'EventCancelled': { const eventId = notification.parentEntityId ?? notification.entityId; return eventId ? `/events/${eventId}` : '/notifications' }
    case 'GroupInvite': case 'GroupJoinApproved': return '/groups'
    case 'PageRoleInvite': return '/pages'
    case 'StoryReaction': return '/feed'
    case 'AccountWarning': return '/settings/security'
  }
}

export function getNotificationBadge(notification: AppNotification) {
  if (notification.type === 'PostReaction' || notification.type === 'CommentReaction' || notification.type === 'StoryReaction') return { icon: '♥', className: 'bg-[#f02849]' }
  if (notification.type === 'PostComment' || notification.type === 'CommentMention') return { icon: '●', className: 'bg-[#1877f2]' }
  if (notification.type === 'FriendRequestReceived' || notification.type === 'FriendRequestAccepted' || notification.type === 'UserFollowed') return { icon: '♟', className: 'bg-[#31a24c]' }
  return { icon: '●', className: 'bg-[#1877f2]' }
}
