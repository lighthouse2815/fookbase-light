import { apiRequest } from './client'
import type { PagedResponse } from './friends'

export interface Post {
  id: string
  authorUserId: string
  content: string
  privacy: string
  createdAtUtc: string
  updatedAtUtc: string | null
  mediaIds: string[]
  commentCount: number
  reactionCounts: Record<string, number>
  viewerReaction: string | null
}

export interface Comment {
  id: string
  postId: string
  authorUserId: string
  parentCommentId: string | null
  content: string
  createdAtUtc: string
  updatedAtUtc: string | null
}

export interface CreatePostDetails {
  content: string
  privacy: string
  mediaIds?: string[]
}

const jsonBody = (value: unknown) => ({ body: JSON.stringify(value) })
const pageQuery = (offset = 0, limit = 20) => `?offset=${offset}&limit=${limit}`

export const postsApi = {
  create: (details: CreatePostDetails) =>
    apiRequest<Post>('/api/posts', { method: 'POST', ...jsonBody(details) }),
  update: (postId: string, details: CreatePostDetails) =>
    apiRequest<Post>(`/api/posts/${postId}`, { method: 'PUT', ...jsonBody(details) }),
  delete: (postId: string) => apiRequest<void>(`/api/posts/${postId}`, { method: 'DELETE' }),
  getById: (postId: string) => apiRequest<Post>(`/api/posts/${postId}`),
  getFeed: (offset = 0, limit = 20) =>
    apiRequest<PagedResponse<Post>>(`/api/posts/feed${pageQuery(offset, limit)}`),
  getByUser: (userId: string, offset = 0, limit = 20) =>
    apiRequest<PagedResponse<Post>>(`/api/posts/users/${userId}${pageQuery(offset, limit)}`),
  createComment: (postId: string, content: string, parentCommentId?: string) =>
    apiRequest<Comment>(`/api/posts/${postId}/comments`, {
      method: 'POST',
      ...jsonBody({ content, parentCommentId }),
    }),
  getComments: (postId: string, offset = 0, limit = 20) =>
    apiRequest<PagedResponse<Comment>>(`/api/posts/${postId}/comments${pageQuery(offset, limit)}`),
  updateComment: (commentId: string, content: string) =>
    apiRequest<Comment>(`/api/posts/comments/${commentId}`, {
      method: 'PUT',
      ...jsonBody({ content }),
    }),
  deleteComment: (commentId: string) =>
    apiRequest<void>(`/api/posts/comments/${commentId}`, { method: 'DELETE' }),
  setReaction: (postId: string, type: string) =>
    apiRequest<Post>(`/api/posts/${postId}/reaction`, {
      method: 'PUT',
      ...jsonBody({ type }),
    }),
  removeReaction: (postId: string) =>
    apiRequest<Post>(`/api/posts/${postId}/reaction`, { method: 'DELETE' }),
  getMediaAccess: (postId: string, mediaId: string) =>
    apiRequest<MediaAccess>(`/api/posts/${postId}/media/${mediaId}/access`),
}

export interface MediaAccess {
  mediaId: string
  url: string
  expiresAtUtc: string
  mediaType: 'image' | 'video'
  contentType: string
}
