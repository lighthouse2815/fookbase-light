import { apiRequest } from './client'

export type AlbumPrivacy = 'public' | 'friends' | 'onlyMe'

export interface PhotoAlbumSummary {
  id: string
  name: string
  albumType: 'custom' | 'profilePictures' | 'coverPhotos' | 'timelinePhotos'
  privacy: string
  photoCount: number
  previewUrl: string | null
  createdAtUtc: string
}

export interface PhotoAlbum extends PhotoAlbumSummary {
  ownerUserId: string
  description: string | null
  updatedAtUtc: string
  canManage: boolean
}

export interface AlbumMedia {
  mediaId: string
  caption: string | null
  sortOrder: number
  addedAtUtc: string
  accessUrl: string
}

export interface PhotoDetail {
  mediaId: string
  albumId: string
  ownerUserId: string
  caption: string | null
  addedAtUtc: string
  url: string
}

export interface CursorPage<T> {
  items: T[]
  nextCursor: string | null
}

export interface CreateAlbumDetails {
  name: string
  description?: string | null
  privacy: AlbumPrivacy
}

export type UpdateAlbumDetails = CreateAlbumDetails

function cursorQuery(cursor?: string, limit = 20) {
  const query = new URLSearchParams({ limit: String(limit) })
  if (cursor) query.set('cursor', cursor)
  return query.toString()
}

export const albumsApi = {
  create: (details: CreateAlbumDetails) =>
    apiRequest<PhotoAlbum>('/api/albums', { method: 'POST', body: JSON.stringify(details) }),
  get: (albumId: string) => apiRequest<PhotoAlbum>(`/api/albums/${albumId}`),
  update: (albumId: string, details: UpdateAlbumDetails) =>
    apiRequest<PhotoAlbum>(`/api/albums/${albumId}`, { method: 'PATCH', body: JSON.stringify(details) }),
  delete: (albumId: string) => apiRequest<void>(`/api/albums/${albumId}`, { method: 'DELETE' }),
  addMedia: (albumId: string, mediaId: string) =>
    apiRequest<AlbumMedia>(`/api/albums/${albumId}/media`, {
      method: 'POST',
      body: JSON.stringify({ mediaId }),
    }),
  getMedia: (albumId: string, cursor?: string, limit = 20) =>
    apiRequest<CursorPage<AlbumMedia>>(`/api/albums/${albumId}/media?${cursorQuery(cursor, limit)}`),
  getPhoto: (albumId: string, mediaId: string) =>
    apiRequest<PhotoDetail>(`/api/albums/${albumId}/media/${mediaId}`),
  updateCaption: (albumId: string, mediaId: string, caption: string | null) =>
    apiRequest<AlbumMedia>(`/api/albums/${albumId}/media/${mediaId}`, {
      method: 'PATCH',
      body: JSON.stringify({ caption }),
    }),
  removeMedia: (albumId: string, mediaId: string) =>
    apiRequest<void>(`/api/albums/${albumId}/media/${mediaId}`, { method: 'DELETE' }),
  getUserAlbums: (userId: string, cursor?: string, limit = 20) =>
    apiRequest<CursorPage<PhotoAlbumSummary>>(`/api/users/${userId}/albums?${cursorQuery(cursor, limit)}`),
}
