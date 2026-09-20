import { apiRequest as request } from './client'
export interface MessageAttachment { mediaId: string; sortOrder: number }
export interface MessageReaction { userId: string; type: string }
export interface MessageReplyPreview { id: string; senderUserId: string; content: string | null; type: string; isDeleted: boolean }
export interface MessageStoryReference {
  storyId: string
  isAvailable: boolean
  caption: string | null
  mediaType: 'image' | 'video' | null
}
export interface Message {
  id: string
  conversationId: string
  senderUserId: string
  content: string | null
  createdAtUtc: string
  readAtUtc: string | null
  type: 'text' | 'media'
  replyToMessageId: string | null
  replyTo: MessageReplyPreview | null
  editedAtUtc: string | null
  deletedAtUtc: string | null
  attachments: MessageAttachment[]
  reactions: MessageReaction[]
  story: MessageStoryReference | null
}
export interface Participant {
  userId: string
  role: 'owner' | 'admin' | 'member'
  joinedAtUtc: string
  leftAtUtc: string | null
  lastReadMessageId: string | null
  lastReadAtUtc: string | null
  lastDeliveredMessageId: string | null
  nickname: string | null
}
export interface Conversation {
  id: string
  participantUserId: string | null
  createdAtUtc: string
  lastMessageAtUtc: string
  lastMessage: Message | null
  unreadCount: number
  type: 'direct' | 'group'
  title: string | null
  photoMediaId: string | null
  participants: Participant[]
  isMuted: boolean
  isArchived: boolean
}
export interface CursorPage<T> { items: T[]; limit: number; nextCursor: string | null }
export interface MessageHistory { items: Message[]; nextCursor: string | null; hasMore: boolean }
export interface UserProfile { userId: string; username: string; displayName: string; avatarUrl: string | null }

const query = (parameters: Record<string, string | undefined>) => new URLSearchParams(
  Object.entries(parameters).filter((entry): entry is [string, string] => entry[1] !== undefined),
).toString()

export const messengerApi = {
  conversations: (before?: string, includeArchived = false) => request<CursorPage<Conversation>>(
    `/api/messages/conversations?${query({ before, limit: '30', includeArchived: includeArchived ? 'true' : undefined })}`,
  ),
  conversation: (id: string) => request<Conversation>(`/api/messages/conversations/${id}`),
  direct: (userId: string) => request<Conversation>('/api/messages/conversations/direct', {
    method: 'POST', body: JSON.stringify({ userId }),
  }),
  group: (title: string, participantUserIds: string[], photoMediaId?: string) => request<Conversation>('/api/messages/conversations/group', {
    method: 'POST', body: JSON.stringify({ title, participantUserIds, photoMediaId }),
  }),
  updateConversation: (id: string, value: object) => request<Conversation>(`/api/messages/conversations/${id}`, {
    method: 'PATCH', body: JSON.stringify(value),
  }),
  messages: (id: string, before?: string) => request<MessageHistory>(`/api/messages/conversations/${id}/messages?${query({ before, limit: '50' })}`),
  send: (id: string, content: string, mediaIds: string[], replyToMessageId?: string) => request<Message>(`/api/messages/conversations/${id}/messages`, {
    method: 'POST', body: JSON.stringify({ content: content || null, mediaIds, replyToMessageId }),
  }),
  read: (id: string, lastReadMessageId: string) => request<void>(`/api/messages/conversations/${id}/read`, {
    method: 'POST', body: JSON.stringify({ lastReadMessageId }),
  }),
  edit: (id: string, content: string) => request<Message>(`/api/messages/${id}`, { method: 'PATCH', body: JSON.stringify({ content }) }),
  unsend: (id: string) => request<void>(`/api/messages/${id}`, { method: 'DELETE' }),
  react: (id: string, type: string) => request<void>(`/api/messages/${id}/reactions`, { method: 'POST', body: JSON.stringify({ type }) }),
  removeReaction: (id: string) => request<void>(`/api/messages/${id}/reactions`, { method: 'DELETE' }),
  addParticipants: (id: string, userIds: string[]) => request<Conversation>(`/api/messages/conversations/${id}/participants`, { method: 'POST', body: JSON.stringify({ userIds }) }),
  removeParticipant: (id: string, userId: string) => request<void>(`/api/messages/conversations/${id}/participants/${userId}`, { method: 'DELETE' }),
  changeRole: (id: string, userId: string, role: string) => request<void>(`/api/messages/conversations/${id}/participants/${userId}/role`, { method: 'PATCH', body: JSON.stringify({ role }) }),
  leave: (id: string) => request<void>(`/api/messages/conversations/${id}/leave`, { method: 'POST' }),
  transferOwnership: (id: string, userId: string) => request<void>(`/api/messages/conversations/${id}/transfer-ownership`, { method: 'POST', body: JSON.stringify({ userId }) }),
  currentUser: () => request<UserProfile>('/api/users/me'),
  user: (userId: string) => request<UserProfile>(`/api/users/${userId}`),
  searchUsers: (value: string) => request<{ items: UserProfile[] }>(`/api/users/search?${query({ query: value, offset: '0', limit: '10' })}`),
  mediaUrl: (mediaId: string) => request<{ url: string }>(`/api/messages/media/${mediaId}/read-url`).then((result) => result.url),
}
