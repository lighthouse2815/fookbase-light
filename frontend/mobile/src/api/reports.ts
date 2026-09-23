import { apiRequest } from './client'

export type ReportReason = 'spam' | 'harassment' | 'hateSpeech' | 'nudity' | 'violence' | 'scam' | 'other'

export interface CreateReportDetails {
  reason: ReportReason
  details?: string | null
}

export interface ContentReport {
  id: string
  targetType: 'user' | 'post'
  targetId: string
  reason: ReportReason
  details: string | null
  status: 'pending' | 'reviewed' | 'resolved' | 'dismissed'
  createdAtUtc: string
}

export const reportsApi = {
  reportUser: (userId: string, details: CreateReportDetails) =>
    apiRequest<ContentReport>(`/api/reports/users/${userId}`, {
      method: 'POST',
      body: JSON.stringify(details),
    }),
  reportPost: (postId: string, details: CreateReportDetails) =>
    apiRequest<ContentReport>(`/api/reports/posts/${postId}`, {
      method: 'POST',
      body: JSON.stringify(details),
    }),
}
