import { apiRequest } from './client'

export interface PagedResponse<T> {
  items: T[]
  offset: number
  limit: number
  total: number
}

export interface Friend {
  userId: string
  friendsSinceUtc: string
}

export interface FriendRequest {
  id: string
  senderUserId: string
  receiverUserId: string
  status: string
  createdAtUtc: string
  respondedAtUtc: string | null
}

export interface RelationshipStatus {
  userId: string
  status: string
  requestId: string | null
}

export interface BlockedUser {
  userId: string
  blockedAtUtc: string
}

export interface FriendNotification {
  id: string
  actorUserId: string
  friendRequestId: string
  type: 'friend_request' | 'friend_accepted'
  createdAtUtc: string
  readAtUtc: string | null
}

export interface CursorPageResponse<T> {
  items: T[]
  nextCursor: string | null
  total: number
}

export interface UserFollow {
  userId: string
  username: string
  displayName: string
  avatarUrl: string | null
  followedAtUtc: string
}

export interface FriendSuggestionProfile {
  userId: string
  username: string
  displayName: string
  avatarUrl: string | null
}

export interface FriendSuggestion {
  profile: FriendSuggestionProfile
  mutualFriendCount: number
  sharedGroupCount: number
  sharedPageCount: number
  relationshipStatus: string
  isFollowing: boolean
}

const pageQuery = (offset = 0, limit = 100) => `?offset=${offset}&limit=${limit}`

export const friendsApi = {
  getFriends: (offset = 0, limit = 100) =>
    apiRequest<PagedResponse<Friend>>(`/api/friends${pageQuery(offset, limit)}`),
  getIncomingRequests: (offset = 0, limit = 100) =>
    apiRequest<PagedResponse<FriendRequest>>(`/api/friends/requests/incoming${pageQuery(offset, limit)}`),
  getOutgoingRequests: (offset = 0, limit = 100) =>
    apiRequest<PagedResponse<FriendRequest>>(`/api/friends/requests/outgoing${pageQuery(offset, limit)}`),
  getStatus: (userId: string) =>
    apiRequest<RelationshipStatus>(`/api/friends/status/${userId}`),
  getMutualFriends: (userId: string, offset = 0, limit = 100) =>
    apiRequest<MutualFriends>(`/api/friends/mutual/${userId}${pageQuery(offset, limit)}`),
  getSuggestions: (cursor?: string, limit = 20, init?: RequestInit) => {
    const query = new URLSearchParams({ limit: String(limit) })
    if (cursor) query.set('cursor', cursor)
    return apiRequest<CursorPageResponse<FriendSuggestion>>(`/api/friends/suggestions?${query.toString()}`, init)
  },
  sendRequest: (userId: string) =>
    apiRequest<FriendRequest>(`/api/friends/requests/${userId}`, { method: 'POST' }),
  acceptRequest: (requestId: string) =>
    apiRequest<Friend>(`/api/friends/requests/${requestId}/accept`, { method: 'POST' }),
  declineRequest: (requestId: string) =>
    apiRequest<void>(`/api/friends/requests/${requestId}/decline`, { method: 'POST' }),
  cancelRequest: (requestId: string) =>
    apiRequest<void>(`/api/friends/requests/${requestId}`, { method: 'DELETE' }),
  unfriend: (userId: string) =>
    apiRequest<void>(`/api/friends/${userId}`, { method: 'DELETE' }),
  getBlockedUsers: (offset = 0, limit = 100) =>
    apiRequest<PagedResponse<BlockedUser>>(`/api/friends/blocks${pageQuery(offset, limit)}`),
  getUnreadNotifications: (offset = 0, limit = 20) =>
    apiRequest<PagedResponse<FriendNotification>>(`/api/friends/notifications/unread${pageQuery(offset, limit)}`),
  markNotificationRead: (notificationId: string) =>
    apiRequest<void>(`/api/friends/notifications/${notificationId}/read`, { method: 'POST' }),
  block: (userId: string) =>
    apiRequest<void>(`/api/friends/blocks/${userId}`, { method: 'POST' }),
  unblock: (userId: string) =>
    apiRequest<void>(`/api/friends/blocks/${userId}`, { method: 'DELETE' }),
}

export interface MutualFriends {
  count: number
  userIds: string[]
  offset: number
  limit: number
}
