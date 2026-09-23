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

export interface PageInvitation {
  id: string
  pageId: string
  role: 'admin' | 'editor' | 'moderator'
  status: string
  eventName?: string | null
  page: Page | null
}

export interface PageMember {
  userId: string
  username: string
  displayName: string
  role: 'owner' | 'admin' | 'editor' | 'moderator'
  joinedAtUtc: string
}

export interface CreatePageDetails {
  name: string
  username: string
  category: string
  bio?: string | null
}

export interface UpdatePageDetails extends CreatePageDetails {}

export interface SetPageMediaDetails {
  avatarMediaId?: string | null
  coverMediaId?: string | null
  removeAvatar?: boolean
  removeCover?: boolean
}

export interface PageTimelinePage {
  items: Post[]
  nextCursor: string | null
}

function cursorQuery(cursor?: string, limit = 20) {
  const query = new URLSearchParams({ limit: String(limit) })
  if (cursor) query.set('cursor', cursor)
  return query.toString()
}

export const pagesApi = {
  create: (details: CreatePageDetails) =>
    apiRequest<Page>('/api/pages', { method: 'POST', body: JSON.stringify(details) }),
  get: (idOrUsername: string) => apiRequest<Page>(`/api/pages/${encodeURIComponent(idOrUsername)}`),
  update: (pageId: string, details: UpdatePageDetails) =>
    apiRequest<Page>(`/api/pages/${pageId}`, { method: 'PATCH', body: JSON.stringify(details) }),
  setMedia: (pageId: string, details: SetPageMediaDetails) =>
    apiRequest<Page>(`/api/pages/${pageId}/media`, { method: 'PATCH', body: JSON.stringify(details) }),
  delete: (pageId: string) => apiRequest<void>(`/api/pages/${pageId}`, { method: 'DELETE' }),
  publish: (pageId: string) => apiRequest<Page>(`/api/pages/${pageId}/publish`, { method: 'POST' }),
  unpublish: (pageId: string) => apiRequest<Page>(`/api/pages/${pageId}/unpublish`, { method: 'POST' }),
  discover: (search = '', cursor?: string, init?: RequestInit) => {
    const query = new URLSearchParams(cursorQuery(cursor))
    if (search.trim()) query.set('query', search.trim())
    return apiRequest<CursorPage<Page>>('/api/pages/discover?' + query.toString(), init)
  },
  mine: (cursor?: string, init?: RequestInit) =>
    apiRequest<CursorPage<Page>>('/api/pages/mine?' + cursorQuery(cursor), init),
  following: (cursor?: string, init?: RequestInit) =>
    apiRequest<CursorPage<Page>>('/api/pages/following?' + cursorQuery(cursor), init),
  invitationsMine: (cursor?: string, init?: RequestInit) =>
    apiRequest<CursorPage<PageInvitation>>('/api/pages/invitations/mine?' + cursorQuery(cursor), init),
  follow: (pageId: string) => apiRequest<void>(`/api/pages/${pageId}/follow`, { method: 'POST' }),
  unfollow: (pageId: string) => apiRequest<void>(`/api/pages/${pageId}/follow`, { method: 'DELETE' }),
  acceptInvitation: (invitationId: string) =>
    apiRequest<PageInvitation>(`/api/pages/invitations/${invitationId}/accept`, { method: 'POST' }),
  declineInvitation: (invitationId: string) =>
    apiRequest<PageInvitation>(`/api/pages/invitations/${invitationId}/decline`, { method: 'POST' }),
  members: (pageId: string, cursor?: string, limit = 20) =>
    apiRequest<CursorPage<PageMember>>(`/api/pages/${pageId}/members?${cursorQuery(cursor, limit)}`),
  invite: (pageId: string, userId: string, role: PageMember['role']) =>
    apiRequest<PageInvitation>(`/api/pages/${pageId}/invitations`, {
      method: 'POST',
      body: JSON.stringify({ userId, role }),
    }),
  changeMemberRole: (pageId: string, userId: string, role: Exclude<PageMember['role'], 'owner'>) =>
    apiRequest<PageMember>(`/api/pages/${pageId}/members/${userId}/role`, {
      method: 'PATCH',
      body: JSON.stringify({ role }),
    }),
  removeMember: (pageId: string, userId: string) =>
    apiRequest<void>(`/api/pages/${pageId}/members/${userId}`, { method: 'DELETE' }),
  transferOwnership: (pageId: string, userId: string) =>
    apiRequest<void>(`/api/pages/${pageId}/transfer-ownership`, {
      method: 'POST',
      body: JSON.stringify({ userId }),
    }),
  posts: (pageId: string, cursor?: string, limit = 20) =>
    apiRequest<PageTimelinePage>(`/api/pages/${pageId}/posts?${cursorQuery(cursor, limit)}`),
  createPost: (pageId: string, content: string, mediaIds?: string[]) =>
    apiRequest<Post>(`/api/pages/${pageId}/posts`, {
      method: 'POST',
      body: JSON.stringify({ content, mediaIds }),
    }),
}
