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
  getMessages: (conversationId: string, offset = 0, limit = 50) =>
    apiRequest<PagedResponse<Message>>(
      `/api/messages/conversations/${conversationId}/messages?${new URLSearchParams({ offset: String(offset), limit: String(limit) })}`,
    ),
  sendMessage: (conversationId: string, content: string) =>
    apiRequest<Message>(`/api/messages/conversations/${conversationId}/messages`, {
      method: 'POST',
      body: JSON.stringify({ content }),
    }),
}
