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
  warningCount: number
  suspendedUntilUtc: string | null
  moderationDisabledAtUtc: string | null
}

export type ReportStatus = 'pending' | 'reviewed' | 'resolved' | 'dismissed'

export interface ModerationAction {
  id: string
  reportId: string | null
  subjectUserId: string
  targetType: 'user' | 'post'
  targetId: string
  actionType: string
  reason: string
  internalNote: string | null
  createdAtUtc: string
  expiresAtUtc: string | null
}

export interface UserModerationState {
  userId: string
  warningCount: number
  suspendedUntilUtc: string | null
  disabledAtUtc: string | null
  updatedAtUtc: string
}

export interface ReportDetail extends ModerationReport {
  resolvedAtUtc: string | null
  targetPreview: string | null
  subjectUserId: string | null
  recentActions: ModerationAction[]
  reportCount: number
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
  updateUserStatus: (userId: string, isActive: boolean) => apiRequest<AdminUser>(`/api/admin/users/${userId}/status`, {
    method: 'PATCH',
    ...jsonBody({ isActive }),
  }),
  getReports: (status?: ReportStatus, offset = 0, limit = 20) => {
    const params = new URLSearchParams({ offset: String(offset), limit: String(limit) })
    if (status) params.set('status', status)
    return apiRequest<PagedResponse<ModerationReport>>(`/api/admin/reports?${params.toString()}`)
  },
  updateReportStatus: (reportId: string, status: Exclude<ReportStatus, 'pending'>) => apiRequest<ModerationReport>(`/api/admin/reports/${reportId}/status`, {
    method: 'PATCH',
    ...jsonBody({ status }),
  }),
  deletePost: (postId: string) => apiRequest<void>(`/api/admin/posts/${postId}`, { method: 'DELETE' }),
  getReport: (reportId: string) => apiRequest<ReportDetail>(`/api/admin/reports/${reportId}`),
  dismissReport: (reportId: string, reason?: string) => apiRequest<ModerationAction>(`/api/admin/reports/${reportId}/dismiss`, {
    method: 'POST', ...jsonBody({ reason }),
  }),
  removeReportedContent: (reportId: string, reason?: string) => apiRequest<ModerationAction>(`/api/admin/reports/${reportId}/remove-content`, {
    method: 'POST', ...jsonBody({ reason }),
  }),
  warnReportedUser: (reportId: string, reason?: string) => apiRequest<ModerationAction>(`/api/admin/reports/${reportId}/warn-user`, {
    method: 'POST', ...jsonBody({ reason }),
  }),
  suspendReportedUser: (reportId: string, durationHours: number, reason?: string) => apiRequest<ModerationAction>(`/api/admin/reports/${reportId}/suspend-user`, {
    method: 'POST', ...jsonBody({ reason, durationHours }),
  }),
  getModerationState: (userId: string) => apiRequest<UserModerationState>(`/api/admin/users/${userId}/moderation-state`),
  warnUser: (userId: string, reason?: string) => apiRequest<ModerationAction>(`/api/admin/users/${userId}/warn`, { method: 'POST', ...jsonBody({ reason }) }),
  suspendUser: (userId: string, durationHours: number, reason?: string) => apiRequest<ModerationAction>(`/api/admin/users/${userId}/suspend`, { method: 'POST', ...jsonBody({ reason, durationHours }) }),
  unsuspendUser: (userId: string) => apiRequest<ModerationAction>(`/api/admin/users/${userId}/unsuspend`, { method: 'POST', ...jsonBody({}) }),
  disableUser: (userId: string, reason?: string) => apiRequest<ModerationAction>(`/api/admin/users/${userId}/disable`, { method: 'POST', ...jsonBody({ reason }) }),
  enableUser: (userId: string) => apiRequest<ModerationAction>(`/api/admin/users/${userId}/enable`, { method: 'POST', ...jsonBody({}) }),
}
