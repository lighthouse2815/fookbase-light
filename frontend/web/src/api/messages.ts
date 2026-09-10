import { apiRequest } from './client'

export interface Message {
  id: string
  conversationId: string
  senderUserId: string
  content: string
  createdAtUtc: string
  readAtUtc: string | null
}

export interface Conversation {
  id: string
  participantUserId: string
  createdAtUtc: string
  lastMessageAtUtc: string
  lastMessage: Message | null
  unreadCount: number
}

export interface PagedResponse<T> {
  items: T[]
  offset: number
  limit: number
  total: number
}

export interface MessageHistoryResponse {
  items: Message[]
  nextCursor: string | null
  hasMore: boolean
}

export interface IncomingMessage {
  conversation: Conversation
  message: Message
}

export const messagesApi = {
  getOrCreateConversation: (userId: string) =>
    apiRequest<Conversation>(`/api/messages/conversations/${userId}`, { method: 'POST' }),
  getConversations: (offset = 0, limit = 20) =>
    apiRequest<PagedResponse<Conversation>>(
      `/api/messages/conversations?${new URLSearchParams({ offset: String(offset), limit: String(limit) })}`,
    ),
  getUnreadNotifications: (offset = 0, limit = 100) =>
    apiRequest<PagedResponse<IncomingMessage>>(
      `/api/messages/notifications?${new URLSearchParams({ offset: String(offset), limit: String(limit) })}`,
    ),
  getMessages: (conversationId: string, before?: string | null, limit = 50) => {
    const query = new URLSearchParams({ limit: String(limit) })
    if (before) query.set('before', before)
    return apiRequest<MessageHistoryResponse>(
      `/api/messages/conversations/${conversationId}/messages?${query}`,
    )
  },
  markConversationRead: (conversationId: string, lastReadMessageId: string) =>
    apiRequest<void>(`/api/messages/conversations/${conversationId}/read`, {
      method: 'POST',
      body: JSON.stringify({ lastReadMessageId }),
    }),
  sendMessage: (conversationId: string, content: string) =>
    apiRequest<Message>(`/api/messages/conversations/${conversationId}/messages`, {
      method: 'POST',
      body: JSON.stringify({ content }),
    }),
}
