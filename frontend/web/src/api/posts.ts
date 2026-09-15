import { apiRequest } from './client'
import type { PagedResponse } from './friends'

export interface PostDisplayIdentity {
  type: 'user' | 'page'
  id: string
  username: string
  name: string
  avatarUrl: string | null
}

export interface ContentMention {
  userId: string
  username: string
  startIndex: number
  length: number
}

export interface Post {
  id: string
  authorUserId: string | null
  content: string
  privacy: string
  createdAtUtc: string
  updatedAtUtc: string | null
  mediaIds: string[]
  commentCount: number
  shareCount: number
  reactionCounts: Record<string, number>
  viewerReaction: string | null
  displayAuthor?: PostDisplayIdentity | null
  containerType?: 'profile' | 'group' | 'page'
  mentions?: ContentMention[]
  contentType?: 'standardPost' | 'reel'
}

export interface Comment {
  id: string
  postId: string
  authorUserId: string
  parentCommentId: string | null
  content: string
  createdAtUtc: string
  updatedAtUtc: string | null
  reactionCounts: Record<string, number>
  viewerReaction: string | null
  mentions?: ContentMention[]
}

export interface PostReaction {
  userId: string
  username: string
  displayName: string
  avatarUrl: string | null
  type: string
  relationshipStatus: string
  relationshipRequestId: string | null
}

export interface SavedPostsPage {
  items: Post[]
  nextCursor: string | null
}

export interface HashtagPostsPage extends SavedPostsPage {
  tag: string
}

export interface PostShare {
  id: string
  sharingUserId: string
  destinationType: 'profile' | 'group' | 'page'
  destinationId: string
  caption: string | null
  createdAtUtc: string
  originalPost: Post
}

export interface CreatePostShareDetails {
  destinationType: 'profile' | 'group' | 'page'
  destinationId: string
  caption?: string
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
  search: (query = '', offset = 0, limit = 20) => {
    const params = new URLSearchParams({ offset: String(offset), limit: String(limit) })
    if (query.trim()) params.set('query', query.trim())

    return apiRequest<PagedResponse<Post>>(`/api/posts/search?${params.toString()}`)
  },
  getByUser: (userId: string, offset = 0, limit = 20) =>
    apiRequest<PagedResponse<Post>>(`/api/posts/users/${userId}${pageQuery(offset, limit)}`),
  getSaved: (cursor?: string, limit = 20, signal?: AbortSignal) => {
    const query = new URLSearchParams({ limit: String(limit) })
    if (cursor) query.set('cursor', cursor)
    return apiRequest<SavedPostsPage>('/api/posts/saved?' + query.toString(), { signal })
  },
  save: (postId: string) => apiRequest<void>(`/api/posts/${postId}/save`, { method: 'POST' }),
  removeSaved: (postId: string) => apiRequest<void>(`/api/posts/${postId}/save`, { method: 'DELETE' }),
  share: (postId: string, details: CreatePostShareDetails) =>
    apiRequest<PostShare>(`/api/posts/${postId}/shares`, { method: 'POST', ...jsonBody(details) }),
  getHashtagPosts: (tag: string, cursor?: string, limit = 20, signal?: AbortSignal) => {
    const query = new URLSearchParams({ limit: String(limit) })
    if (cursor) query.set('cursor', cursor)
    return apiRequest<HashtagPostsPage>(`/api/hashtags/${encodeURIComponent(tag)}/posts?${query.toString()}`, { signal })
  },
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
  setCommentReaction: (commentId: string, type: string) =>
    apiRequest<Comment>(`/api/posts/comments/${commentId}/reaction`, {
      method: 'PUT',
      ...jsonBody({ type }),
    }),
  removeCommentReaction: (commentId: string) =>
    apiRequest<Comment>(`/api/posts/comments/${commentId}/reaction`, { method: 'DELETE' }),
  setReaction: (postId: string, type: string) =>
    apiRequest<Post>(`/api/posts/${postId}/reaction`, {
      method: 'PUT',
      ...jsonBody({ type }),
    }),
  removeReaction: (postId: string) =>
    apiRequest<Post>(`/api/posts/${postId}/reaction`, { method: 'DELETE' }),
  getReactions: (postId: string, type?: string, offset = 0, limit = 20) => {
    const query = new URLSearchParams({ offset: String(offset), limit: String(limit) })
    if (type) query.set('type', type)
    return apiRequest<PagedResponse<PostReaction>>(`/api/posts/${postId}/reactions?${query.toString()}`)
  },
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
