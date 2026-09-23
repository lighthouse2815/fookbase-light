import { apiRequest } from './client'

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

function cursorQuery(cursor?: string, limit = 20) {
  const query = new URLSearchParams({ limit: String(limit) })
  if (cursor) query.set('cursor', cursor)
  return query.toString()
}

export const eventsApi = {
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
  acceptInvitation: (invitationId: string) =>
    apiRequest<EventInvitation>(`/api/events/invitations/${invitationId}/accept`, { method: 'POST' }),
  declineInvitation: (invitationId: string) =>
    apiRequest<EventInvitation>(`/api/events/invitations/${invitationId}/decline`, { method: 'POST' }),
}
