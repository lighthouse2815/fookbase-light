import { apiRequest } from './client'
import type { Post } from './posts'
import type { MediaReadUrl } from './media'

export interface Group {
  id: string
  name: string
  description: string | null
  privacy: 'public' | 'private'
  ownerUserId: string
  coverUrl: string | null
  memberCount: number
  viewerRole: 'owner' | 'admin' | 'moderator' | 'member' | null
  createdAtUtc: string
  updatedAtUtc: string | null
}

export interface CursorPage<T> {
  items: T[]
  nextCursor: string | null
}

export interface GroupMember {
  userId: string
  role: 'owner' | 'admin' | 'moderator' | 'member'
  joinedAtUtc: string
  username: string | null
  displayName: string | null
  avatarUrl: string | null
}

export interface GroupJoinRequest {
  id: string
  requesterUserId: string
  status: string
  createdAtUtc: string
  respondedAtUtc: string | null
  respondedByUserId: string | null
}

export interface GroupRule {
  id: string
  groupId: string
  title: string
  description: string | null
  sortOrder: number
}

export interface GroupInvite {
  id: string
  groupId: string
  inviterUserId: string
  inviteeUserId: string
  status: string
  createdAtUtc: string
  respondedAtUtc: string | null
  group: Group | null
}

export interface GroupFeedItem {
  group: Group
  post: Post
}

export interface CreateGroupDetails {
  name: string
  description?: string
  privacy: 'public' | 'private'
}

export interface UpdateGroupDetails extends CreateGroupDetails {
  coverMediaId?: string | null
  removeCover?: boolean
}

const jsonBody = (value: unknown) => ({ body: JSON.stringify(value) })

function cursorQuery(cursor?: string, limit = 20) {
  const query = new URLSearchParams({ limit: String(limit) })
  if (cursor) query.set('cursor', cursor)
  return query.toString()
}

export const groupsApi = {
  create: (details: CreateGroupDetails) =>
    apiRequest<Group>('/api/groups', { method: 'POST', ...jsonBody(details) }),
  get: (groupId: string) => apiRequest<Group>(`/api/groups/${groupId}`),
  getCoverAccess: (groupId: string) =>
    apiRequest<MediaReadUrl>(`/api/groups/${groupId}/cover/access`),
  update: (groupId: string, details: UpdateGroupDetails) =>
    apiRequest<Group>(`/api/groups/${groupId}`, { method: 'PATCH', ...jsonBody(details) }),
  delete: (groupId: string) => apiRequest<void>(`/api/groups/${groupId}`, { method: 'DELETE' }),
  getMine: (cursor?: string) =>
    apiRequest<CursorPage<Group>>(`/api/groups/mine?${cursorQuery(cursor)}`),
  getFeed: (cursor?: string) =>
    apiRequest<CursorPage<GroupFeedItem>>(`/api/groups/feed?${cursorQuery(cursor)}`),
  getMyInvites: (cursor?: string) =>
    apiRequest<CursorPage<GroupInvite>>(`/api/groups/invites/mine?${cursorQuery(cursor)}`),
  discover: (queryText = '', cursor?: string) => {
    const query = new URLSearchParams(cursorQuery(cursor))
    if (queryText.trim()) query.set('query', queryText.trim())
    return apiRequest<CursorPage<Group>>('/api/groups/discover?' + query.toString())
  },
  join: (groupId: string) =>
    apiRequest<GroupJoinRequest | null>(`/api/groups/${groupId}/join`, { method: 'POST' }),
  leave: (groupId: string) =>
    apiRequest<void>(`/api/groups/${groupId}/leave`, { method: 'POST' }),
  getMembers: (groupId: string, cursor?: string) =>
    apiRequest<CursorPage<GroupMember>>(`/api/groups/${groupId}/members?${cursorQuery(cursor)}`),
  changeMemberRole: (groupId: string, userId: string, role: GroupMember['role']) =>
    apiRequest<GroupMember>(`/api/groups/${groupId}/members/${userId}/role`, {
      method: 'PATCH',
      ...jsonBody({ role }),
    }),
  removeMember: (groupId: string, userId: string) =>
    apiRequest<void>(`/api/groups/${groupId}/members/${userId}`, { method: 'DELETE' }),
  getJoinRequests: (groupId: string, cursor?: string) =>
    apiRequest<CursorPage<GroupJoinRequest>>(`/api/groups/${groupId}/join-requests?${cursorQuery(cursor)}`),
  approveJoinRequest: (groupId: string, requestId: string) =>
    apiRequest<GroupJoinRequest>(`/api/groups/${groupId}/join-requests/${requestId}/approve`, { method: 'POST' }),
  declineJoinRequest: (groupId: string, requestId: string) =>
    apiRequest<GroupJoinRequest>(`/api/groups/${groupId}/join-requests/${requestId}/decline`, { method: 'POST' }),
  invite: (groupId: string, userId: string) =>
    apiRequest<GroupInvite>(`/api/groups/${groupId}/invites`, { method: 'POST', ...jsonBody({ userId }) }),
  acceptInvite: (groupId: string, inviteId: string) =>
    apiRequest<GroupInvite>(`/api/groups/${groupId}/invites/${inviteId}/accept`, { method: 'POST' }),
  declineInvite: (groupId: string, inviteId: string) =>
    apiRequest<GroupInvite>(`/api/groups/${groupId}/invites/${inviteId}/decline`, { method: 'POST' }),
  getRules: (groupId: string) => apiRequest<GroupRule[]>(`/api/groups/${groupId}/rules`),
  createRule: (groupId: string, title: string, description: string, sortOrder: number) =>
    apiRequest<GroupRule>(`/api/groups/${groupId}/rules`, {
      method: 'POST',
      ...jsonBody({ title, description, sortOrder }),
    }),
  updateRule: (groupId: string, ruleId: string, title: string, description: string, sortOrder: number) =>
    apiRequest<GroupRule>(`/api/groups/${groupId}/rules/${ruleId}`, {
      method: 'PATCH',
      ...jsonBody({ title, description, sortOrder }),
    }),
  deleteRule: (groupId: string, ruleId: string) =>
    apiRequest<void>(`/api/groups/${groupId}/rules/${ruleId}`, { method: 'DELETE' }),
  getPosts: (groupId: string, cursor?: string) =>
    apiRequest<CursorPage<Post>>(`/api/groups/${groupId}/posts?${cursorQuery(cursor)}`),
  createPost: (groupId: string, content: string, mediaIds: string[]) =>
    apiRequest<Post>(`/api/groups/${groupId}/posts`, {
      method: 'POST',
      ...jsonBody({ content, privacy: 'public', mediaIds }),
    }),
  removePost: (groupId: string, postId: string) =>
    apiRequest<void>(`/api/groups/${groupId}/posts/${postId}`, { method: 'DELETE' }),
}
