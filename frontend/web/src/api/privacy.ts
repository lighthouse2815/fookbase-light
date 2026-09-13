import { apiRequest } from './client'

export interface PrivacySettings { defaultPostPrivacy: string; friendRequestPolicy: string; friendListVisibility: string; followListVisibility: string; updatedAtUtc: string }
export const privacyApi = {
  get: () => apiRequest<PrivacySettings>('/api/privacy'),
  update: (settings: Partial<PrivacySettings>) => apiRequest<PrivacySettings>('/api/privacy', { method: 'PATCH', body: JSON.stringify(settings) }),
}
