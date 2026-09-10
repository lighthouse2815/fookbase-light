import { apiRequest } from './client'

export interface AdminDashboard {
  totalUsers: number
  activeUsers: number
  activePosts: number
  pendingReports: number
}

export interface AdminUser {
  id: string
  email: string
  username: string
  isActive: boolean
  createdAt: string
  roles: string[]
}

export interface ModerationReport {
  id: string
  reporterUserId: string
  targetType: 'user' | 'post'
  targetId: string
  reason: string
  details: string | null
  status: ReportStatus
  createdAtUtc: string
}

export type ReportStatus = 'pending' | 'reviewed' | 'resolved' | 'dismissed'

export interface PagedResponse<T> {
  items: T[]
  offset: number
  limit: number
  total: number
}

const jsonBody = (value: unknown) => ({ body: JSON.stringify(value) })

export const adminApi = {
  getDashboard: () => apiRequest<AdminDashboard>('/api/admin/dashboard'),
  getUsers: (query = '', offset = 0, limit = 20) => {
    const params = new URLSearchParams({ offset: String(offset), limit: String(limit) })
    if (query.trim()) params.set('query', query.trim())

    return apiRequest<PagedResponse<AdminUser>>(`/api/admin/users?${params.toString()}`)
  },
  updateUserStatus: (userId: string, isActive: boolean) =>
    apiRequest<AdminUser>(`/api/admin/users/${userId}/status`, {
      method: 'PATCH',
      ...jsonBody({ isActive }),
    }),
  getReports: (status?: ReportStatus, offset = 0, limit = 20) => {
    const params = new URLSearchParams({ offset: String(offset), limit: String(limit) })
    if (status) params.set('status', status)

    return apiRequest<PagedResponse<ModerationReport>>(`/api/admin/reports?${params.toString()}`)
  },
  updateReportStatus: (reportId: string, status: Exclude<ReportStatus, 'pending'>) =>
    apiRequest<ModerationReport>(`/api/admin/reports/${reportId}/status`, {
      method: 'PATCH',
      ...jsonBody({ status }),
    }),
  deletePost: (postId: string) =>
    apiRequest<void>(`/api/admin/posts/${postId}`, { method: 'DELETE' }),
}
