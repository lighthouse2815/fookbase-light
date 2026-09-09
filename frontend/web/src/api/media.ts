import { apiRequest } from './client'

export interface UploadIntent {
  mediaId: string
  uploadUrl: string
  expiresAtUtc: string
}

export interface Media {
  id: string
  ownerUserId: string
  mediaType: string
  status: string
  fileName: string
  contentType: string
  declaredSizeBytes: number
  actualSizeBytes: number | null
  createdAtUtc: string
  uploadExpiresAtUtc: string | null
  uploadedAtUtc: string | null
  deletedAtUtc: string | null
}

export const mediaApi = {
  createUpload: (file: File) =>
    apiRequest<UploadIntent>('/api/media/uploads', {
      method: 'POST',
      body: JSON.stringify({
        fileName: file.name,
        contentType: file.type || 'application/octet-stream',
        sizeBytes: file.size,
      }),
    }),
  complete: (mediaId: string) =>
    apiRequest<Media>(`/api/media/${mediaId}/complete`, { method: 'POST' }),
  getMetadata: (mediaId: string) => apiRequest<Media>(`/api/media/${mediaId}`),
  delete: (mediaId: string) => apiRequest<void>(`/api/media/${mediaId}`, { method: 'DELETE' }),
  uploadFile: async (file: File) => {
    const uploadIntent = await mediaApi.createUpload(file)
    const uploadResponse = await fetch(uploadIntent.uploadUrl, {
      method: 'PUT',
      headers: { 'Content-Type': file.type || 'application/octet-stream' },
      body: file,
    })

    if (!uploadResponse.ok) {
      throw new Error('Không thể tải tệp lên kho lưu trữ.')
    }

    await mediaApi.complete(uploadIntent.mediaId)
    return uploadIntent.mediaId
  },
}
