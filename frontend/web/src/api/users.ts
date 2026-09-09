import { apiRequest } from './client'

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
  getCurrent: () => apiRequest<UserProfile>('/api/users/me'),
  updateCurrent: (details: UpdateUserProfileDetails) =>
    apiRequest<UserProfile>('/api/users/me', {
      method: 'PATCH',
      body: JSON.stringify(details),
    }),
}

export interface UpdateUserProfileDetails {
  displayName?: string | null
  bio?: string | null
  dateOfBirth?: string | null
  currentCity?: string | null
}
