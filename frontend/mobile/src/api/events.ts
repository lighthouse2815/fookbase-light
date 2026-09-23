import { apiRequest } from './client'
import type { Post } from './posts'

export interface CursorPage<T> {
  items: T[]
  nextCursor: string | null
}

export interface EventHost {
  type: 'user' | 'group' | 'page'
  id: string
  name: string
  username?: string | null
  avatarUrl?: string | null
}

export interface Event {
  id: string
  name: string
  description: string | null
  displayHost: EventHost
  privacy: 'public' | 'private'
  locationType: 'physical' | 'online'
  locationName: string | null
  address: string | null
  onlineUrl: string | null
  startsAtUtc: string
  endsAtUtc: string | null
  status: 'draft' | 'published' | 'cancelled'
  coverUrl: string | null
  goingCount: number
  interestedCount: number
  viewerRsvpStatus: 'going' | 'interested' | null
  canManage: boolean
  canPost: boolean
  createdAtUtc: string
  updatedAtUtc: string | null
}

export interface EventInvitation {
  id: string
  eventId: string
  status: string
  event: Event | null
}

export interface EventParticipant {
  userId: string
  username: string
  displayName: string
  avatarUrl: string | null
  status: string
  respondedAtUtc: string
}

export interface CreateEventDetails {
  hostType: 'user' | 'group' | 'page'
  hostId?: string | null
  name: string
  description?: string | null
  privacy: 'public' | 'private'
  locationType: 'physical' | 'online'
  locationName?: string | null
  address?: string | null
  onlineUrl?: string | null
  startsAtUtc: string
  endsAtUtc?: string | null
  coverMediaId?: string | null
  status?: 'draft' | 'published'
}

export interface UpdateEventDetails extends Omit<CreateEventDetails, 'hostType' | 'hostId' | 'status'> {
  removeCover?: boolean
}

export interface CreateEventPostDetails {
  content: string
  mediaIds?: string[]
}

function cursorQuery(cursor?: string, limit = 20) {
  const query = new URLSearchParams({ limit: String(limit) })
  if (cursor) query.set('cursor', cursor)
  return query.toString()
}

export const eventsApi = {
  create: (details: CreateEventDetails) =>
    apiRequest<Event>('/api/events', { method: 'POST', body: JSON.stringify(details) }),
  get: (eventId: string) => apiRequest<Event>(`/api/events/${eventId}`),
  update: (eventId: string, details: UpdateEventDetails) =>
    apiRequest<Event>(`/api/events/${eventId}`, { method: 'PATCH', body: JSON.stringify(details) }),
  delete: (eventId: string) => apiRequest<void>(`/api/events/${eventId}`, { method: 'DELETE' }),
  publish: (eventId: string) =>
    apiRequest<Event>(`/api/events/${eventId}/publish`, { method: 'POST' }),
  cancel: (eventId: string) =>
    apiRequest<Event>(`/api/events/${eventId}/cancel`, { method: 'POST' }),
  discover: (search = '', cursor?: string, init?: RequestInit) => {
    const query = new URLSearchParams(cursorQuery(cursor))
    if (search.trim()) query.set('query', search.trim())
    return apiRequest<CursorPage<Event>>('/api/events/discover?' + query.toString(), init)
  },
  mine: (cursor?: string, init?: RequestInit) =>
    apiRequest<CursorPage<Event>>('/api/events/mine?' + cursorQuery(cursor), init),
  upcoming: (cursor?: string, init?: RequestInit) =>
    apiRequest<CursorPage<Event>>('/api/events/upcoming?' + cursorQuery(cursor), init),
  invitations: (cursor?: string, init?: RequestInit) =>
    apiRequest<CursorPage<EventInvitation>>('/api/events/invitations/mine?' + cursorQuery(cursor), init),
  rsvp: (eventId: string, status: 'going' | 'interested') =>
    apiRequest<Event>(`/api/events/${eventId}/rsvp`, { method: 'POST', body: JSON.stringify({ status }) }),
  removeRsvp: (eventId: string) => apiRequest<void>(`/api/events/${eventId}/rsvp`, { method: 'DELETE' }),
  participants: (eventId: string, cursor?: string, limit = 20) =>
    apiRequest<CursorPage<EventParticipant>>(`/api/events/${eventId}/participants?${cursorQuery(cursor, limit)}`),
  invite: (eventId: string, userId: string) =>
    apiRequest<EventInvitation>(`/api/events/${eventId}/invites`, {
      method: 'POST',
      body: JSON.stringify({ userId }),
    }),
  posts: (eventId: string, cursor?: string, limit = 20) =>
    apiRequest<CursorPage<Post>>(`/api/events/${eventId}/posts?${cursorQuery(cursor, limit)}`),
  createPost: (eventId: string, details: CreateEventPostDetails) =>
    apiRequest<Post>(`/api/events/${eventId}/posts`, { method: 'POST', body: JSON.stringify(details) }),
  acceptInvitation: (invitationId: string) =>
    apiRequest<EventInvitation>(`/api/events/invitations/${invitationId}/accept`, { method: 'POST' }),
  declineInvitation: (invitationId: string) =>
    apiRequest<EventInvitation>(`/api/events/invitations/${invitationId}/decline`, { method: 'POST' }),
}
