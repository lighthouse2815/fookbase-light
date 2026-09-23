import { apiRequest } from './client'

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

function cursorQuery(cursor?: string, limit = 20) {
  const query = new URLSearchParams({ limit: String(limit) })
  if (cursor) query.set('cursor', cursor)
  return query.toString()
}

export const pagesApi = {
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
}
