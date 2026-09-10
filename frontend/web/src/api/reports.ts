import { apiRequest } from './client'

export type ReportReason = 'spam' | 'harassment' | 'hateSpeech' | 'nudity' | 'violence' | 'scam' | 'other'

export interface CreateReportDetails {
  reason: ReportReason
  details?: string
}

export interface ContentReport {
  id: string
  targetType: 'user' | 'post'
  targetId: string
  reason: string
  details: string | null
  status: string
  createdAtUtc: string
}

const jsonBody = (value: unknown) => ({ body: JSON.stringify(value) })

export const reportsApi = {
  reportUser: (userId: string, details: CreateReportDetails) =>
    apiRequest<ContentReport>(`/api/reports/users/${userId}`, {
      method: 'POST',
      ...jsonBody(details),
    }),
  reportPost: (postId: string, details: CreateReportDetails) =>
    apiRequest<ContentReport>(`/api/reports/posts/${postId}`, {
      method: 'POST',
      ...jsonBody(details),
    }),
}
