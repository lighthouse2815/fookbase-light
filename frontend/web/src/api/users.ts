import { apiBaseUrl, apiRequest } from './client'

export interface UserProfile {
  userId: string
  username: string
  displayName: string
  bio: string | null
  avatarUrl: string | null
  coverUrl: string | null
  dateOfBirth: string | null
  currentCity: string | null
  createdAt: string
  updatedAt: string
  followerCount: number
  followingCount: number
  isFollowing: boolean | null
  isFollowedBy: boolean | null
  friendshipState: string | null
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

export const usersApi = {
  getById: (userId: string) => apiRequest<UserProfile>(`/api/users/${userId}`),
  search: (query = '', offset = 0, limit = 20) => {
    const params = new URLSearchParams({ offset: String(offset), limit: String(limit) })
    if (query.trim()) params.set('query', query.trim())

    return apiRequest<UserSearchResponse>(`/api/users/search?${params.toString()}`)
  },
  getCurrent: () => apiRequest<UserProfile>('/api/users/me'),
  updateCurrent: (details: UpdateUserProfileDetails) =>
    apiRequest<UserProfile>('/api/users/me', {
      method: 'PATCH',
      body: JSON.stringify(details),
    }),
  follow: (userId: string) =>
    apiRequest<void>(`/api/users/${userId}/follow`, { method: 'POST' }),
  unfollow: (userId: string) =>
    apiRequest<void>(`/api/users/${userId}/follow`, { method: 'DELETE' }),
  getFollowers: (userId: string, cursor?: string, limit = 20) => {
    const query = new URLSearchParams({ limit: String(limit) })
    if (cursor) query.set('cursor', cursor)
    return apiRequest<CursorPageResponse<UserFollow>>(`/api/users/${userId}/followers?${query.toString()}`)
  },
  getFollowing: (userId: string, cursor?: string, limit = 20) => {
    const query = new URLSearchParams({ limit: String(limit) })
    if (cursor) query.set('cursor', cursor)
    return apiRequest<CursorPageResponse<UserFollow>>(`/api/users/${userId}/following?${query.toString()}`)
  },
}

export interface UserSearchResponse {
  items: UserProfile[]
  offset: number
  limit: number
  total: number
}

export interface UpdateUserProfileDetails {
  displayName?: string | null
  bio?: string | null
  dateOfBirth?: string | null
  currentCity?: string | null
  avatarMediaId?: string | null
  coverMediaId?: string | null
}

export function resolveProfileImageUrl(url: string) {
  return url.startsWith('/') ? `${apiBaseUrl}${url}` : url
}
