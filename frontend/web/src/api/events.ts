import { apiRequest } from './client'
import type { CursorPage } from './groups'
import type { Post } from './posts'

export interface EventHost { type: 'user' | 'group' | 'page'; id: string; name: string; username?: string | null; avatarUrl?: string | null }
export interface Event { id: string; name: string; description: string | null; displayHost: EventHost; privacy: 'public' | 'private'; locationType: 'physical' | 'online'; locationName: string | null; address: string | null; onlineUrl: string | null; startsAtUtc: string; endsAtUtc: string | null; status: 'draft' | 'published' | 'cancelled'; coverUrl: string | null; goingCount: number; interestedCount: number; viewerRsvpStatus: 'going' | 'interested' | null; canManage: boolean; canPost: boolean; createdAtUtc: string; updatedAtUtc: string | null }
export interface EventInvitation { id: string; eventId: string; inviterUserId: string; inviteeUserId: string; status: string; createdAtUtc: string; respondedAtUtc: string | null; event: Event | null }
export interface EventParticipant { userId: string; username: string; displayName: string; avatarUrl: string | null; status: string; respondedAtUtc: string }
export interface EventDetails { hostType: 'user' | 'group' | 'page'; hostId?: string; name: string; description?: string; privacy: 'public' | 'private'; locationType: 'physical' | 'online'; locationName?: string; address?: string; onlineUrl?: string; startsAtUtc: string; endsAtUtc?: string; coverMediaId?: string; status?: 'draft' | 'published' }
const body = (value: unknown) => ({ body: JSON.stringify(value) })
const query = (cursor?: string) => { const value = new URLSearchParams({ limit: '20' }); if (cursor) value.set('cursor', cursor); return value.toString() }
export const eventsApi = {
  create: (value: EventDetails) => apiRequest<Event>('/api/events', { method: 'POST', ...body(value) }),
  get: (id: string) => apiRequest<Event>(`/api/events/${id}`), update: (id: string, value: EventDetails) => apiRequest<Event>(`/api/events/${id}`, { method: 'PATCH', ...body(value) }),
  publish: (id: string) => apiRequest<Event>(`/api/events/${id}/publish`, { method: 'POST' }), cancel: (id: string) => apiRequest<Event>(`/api/events/${id}/cancel`, { method: 'POST' }), delete: (id: string) => apiRequest<void>(`/api/events/${id}`, { method: 'DELETE' }),
  rsvp: (id: string, status: 'going' | 'interested') => apiRequest<Event>(`/api/events/${id}/rsvp`, { method: 'POST', ...body({ status }) }), removeRsvp: (id: string) => apiRequest<void>(`/api/events/${id}/rsvp`, { method: 'DELETE' }),
  mine: (cursor?: string) => apiRequest<CursorPage<Event>>(`/api/events/mine?${query(cursor)}`), upcoming: (cursor?: string) => apiRequest<CursorPage<Event>>(`/api/events/upcoming?${query(cursor)}`), discover: (search = '', cursor?: string) => apiRequest<CursorPage<Event>>(`/api/events/discover?${query(cursor)}${search.trim() ? `&query=${encodeURIComponent(search.trim())}` : ''}`),
  invitations: (cursor?: string) => apiRequest<CursorPage<EventInvitation>>(`/api/events/invitations/mine?${query(cursor)}`), acceptInvite: (id: string) => apiRequest<EventInvitation>(`/api/events/invitations/${id}/accept`, { method: 'POST' }), declineInvite: (id: string) => apiRequest<EventInvitation>(`/api/events/invitations/${id}/decline`, { method: 'POST' }), invite: (eventId: string, userId: string) => apiRequest<EventInvitation>(`/api/events/${eventId}/invites`, { method: 'POST', ...body({ userId }) }),
  participants: (id: string, cursor?: string) => apiRequest<CursorPage<EventParticipant>>(`/api/events/${id}/participants?${query(cursor)}`), posts: (id: string, cursor?: string) => apiRequest<CursorPage<Post>>(`/api/events/${id}/posts?${query(cursor)}`), createPost: (id: string, content: string, mediaIds: string[] = []) => apiRequest<Post>(`/api/events/${id}/posts`, { method: 'POST', ...body({ content, mediaIds }) }),
}
