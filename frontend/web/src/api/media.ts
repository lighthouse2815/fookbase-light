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
  durationMs: number | null
  width: number | null
  height: number | null
  hasProcessedVideo: boolean
  processedAtUtc: string | null
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
  uploadFile: async (file: File, onProgress?: (progress: number) => void) => {
    const completed = await mediaApi.uploadFileWithMetadata(file, onProgress)
    return completed.id
  },
  uploadFileWithMetadata: async (file: File, onProgress?: (progress: number) => void) => {
    const uploadIntent = await mediaApi.createUpload(file)
    await uploadToStorage(uploadIntent.uploadUrl, file, onProgress)

    return mediaApi.complete(uploadIntent.mediaId)
  },
  uploadFiles: async (files: readonly File[], onProgress?: (progress: number) => void) => {
    const mediaIds: string[] = []
    for (const [index, file] of files.entries()) {
      const mediaId = await mediaApi.uploadFile(file, (fileProgress) => {
        onProgress?.(Math.round(((index + fileProgress / 100) / files.length) * 100))
      })
      mediaIds.push(mediaId)
    }
    return mediaIds
  },
  getReadUrl: (mediaId: string) => apiRequest<MediaReadUrl>(`/api/media/${mediaId}/access`),
  getPosterReadUrl: (mediaId: string) => apiRequest<MediaReadUrl>(`/api/media/${mediaId}/poster/access`),
}

export interface MediaReadUrl {
  mediaId: string
  url: string
  expiresAtUtc: string
  mediaType: string
  contentType: string
}

function uploadToStorage(
  uploadUrl: string,
  file: File,
  onProgress?: (progress: number) => void,
) {
  return new Promise<void>((resolve, reject) => {
    const request = new XMLHttpRequest()
    request.open('PUT', uploadUrl)
    request.setRequestHeader('Content-Type', file.type || 'application/octet-stream')
    request.upload.addEventListener('progress', (event) => {
      if (event.lengthComputable) onProgress?.(Math.round((event.loaded / event.total) * 100))
    })
    request.addEventListener('load', () => {
      if (request.status >= 200 && request.status < 300) {
        onProgress?.(100)
        resolve()
      } else {
        reject(new Error('Không thể tải tệp lên kho lưu trữ.'))
      }
    })
    request.addEventListener('error', () => reject(new Error('Không thể tải tệp lên kho lưu trữ.')))
    request.addEventListener('abort', () => reject(new Error('Tải tệp lên đã bị hủy.')))
    request.send(file)
  })
}
