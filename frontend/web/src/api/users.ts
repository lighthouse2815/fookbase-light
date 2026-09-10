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
