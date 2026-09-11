import { apiRequest } from './client'
import type { Post } from './posts'

export interface CursorPage<T> {
  items: T[]
  nextCursor: string | null
}

export interface Page {
  id: string
  name: string
  username: string
  category: string
  bio: string | null
  status: 'published' | 'unpublished'
  avatarUrl: string | null
  coverUrl: string | null
  followerCount: number
  isFollowing: boolean
  viewerRole: 'owner' | 'admin' | 'editor' | 'moderator' | null
  createdAtUtc: string
  updatedAtUtc: string | null
}

export interface PageMember {
  userId: string
  username: string
  displayName: string
  role: 'owner' | 'admin' | 'editor' | 'moderator'
  joinedAtUtc: string
}

export interface PageInvitation {
  id: string
  pageId: string
  inviterUserId: string
  inviteeUserId: string
  role: 'admin' | 'editor' | 'moderator'
  status: string
  createdAtUtc: string
  respondedAtUtc: string | null
}

export interface PagePostPage extends CursorPage<Post> {}

export interface PageDetails {
  name: string
  username: string
  category: string
  bio?: string | null
}

const jsonBody = (value: unknown) => ({ body: JSON.stringify(value) })
const cursorQuery = (cursor?: string, limit = 20) => {
  const params = new URLSearchParams({ limit: String(limit) })
  if (cursor) params.set('cursor', cursor)
  return params.toString()
}

export const pagesApi = {
  create: (details: PageDetails) => apiRequest<Page>('/api/pages', { method: 'POST', ...jsonBody(details) }),
  get: (idOrUsername: string) => apiRequest<Page>(`/api/pages/${encodeURIComponent(idOrUsername)}`),
  update: (pageId: string, details: PageDetails) =>
    apiRequest<Page>(`/api/pages/${pageId}`, { method: 'PATCH', ...jsonBody(details) }),
  setMedia: (pageId: string, details: { avatarMediaId?: string | null; coverMediaId?: string | null; removeAvatar?: boolean; removeCover?: boolean }) =>
    apiRequest<Page>(`/api/pages/${pageId}/media`, { method: 'PATCH', ...jsonBody(details) }),
  delete: (pageId: string) => apiRequest<void>(`/api/pages/${pageId}`, { method: 'DELETE' }),
  publish: (pageId: string) => apiRequest<Page>(`/api/pages/${pageId}/publish`, { method: 'POST' }),
  unpublish: (pageId: string) => apiRequest<Page>(`/api/pages/${pageId}/unpublish`, { method: 'POST' }),
  follow: (pageId: string) => apiRequest<void>(`/api/pages/${pageId}/follow`, { method: 'POST' }),
  unfollow: (pageId: string) => apiRequest<void>(`/api/pages/${pageId}/follow`, { method: 'DELETE' }),
  mine: (cursor?: string) => apiRequest<CursorPage<Page>>(`/api/pages/mine?${cursorQuery(cursor)}`),
  following: (cursor?: string) => apiRequest<CursorPage<Page>>(`/api/pages/following?${cursorQuery(cursor)}`),
  discover: (query = '', cursor?: string) => {
    const params = new URLSearchParams(cursorQuery(cursor))
    if (query.trim()) params.set('query', query.trim())
    return apiRequest<CursorPage<Page>>('/api/pages/discover?' + params.toString())
  },
  members: (pageId: string, cursor?: string) => apiRequest<CursorPage<PageMember>>(`/api/pages/${pageId}/members?${cursorQuery(cursor)}`),
  invite: (pageId: string, userId: string, role: PageInvitation['role']) =>
    apiRequest<PageInvitation>(`/api/pages/${pageId}/invitations`, { method: 'POST', ...jsonBody({ userId, role }) }),
  invitationsMine: (cursor?: string) => apiRequest<CursorPage<PageInvitation>>(`/api/pages/invitations/mine?${cursorQuery(cursor)}`),
  acceptInvitation: (invitationId: string) => apiRequest<PageInvitation>(`/api/pages/invitations/${invitationId}/accept`, { method: 'POST' }),
  declineInvitation: (invitationId: string) => apiRequest<PageInvitation>(`/api/pages/invitations/${invitationId}/decline`, { method: 'POST' }),
  changeRole: (pageId: string, userId: string, role: PageInvitation['role']) =>
    apiRequest<PageMember>(`/api/pages/${pageId}/members/${userId}/role`, { method: 'PATCH', ...jsonBody({ role }) }),
  removeMember: (pageId: string, userId: string) => apiRequest<void>(`/api/pages/${pageId}/members/${userId}`, { method: 'DELETE' }),
  transferOwnership: (pageId: string, userId: string) =>
    apiRequest<void>(`/api/pages/${pageId}/transfer-ownership`, { method: 'POST', ...jsonBody({ userId }) }),
  posts: (pageId: string, cursor?: string) => apiRequest<PagePostPage>(`/api/pages/${pageId}/posts?${cursorQuery(cursor)}`),
  createPost: (pageId: string, content: string, mediaIds: string[]) =>
    apiRequest<Post>(`/api/pages/${pageId}/posts`, { method: 'POST', ...jsonBody({ content, privacy: 'public', mediaIds }) }),
}
