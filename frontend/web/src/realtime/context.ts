import { createContext } from 'react'
import type { IncomingMessage } from '../api/messages'

export interface RealtimeContextValue {
  incomingMessages: IncomingMessage[]
  unreadMessageCount: number
  typingConversationIds: ReadonlySet<string>
  readAtByConversation: ReadonlyMap<string, string>
  markConversationRead: (conversationId: string) => void
  sendTyping: (conversationId: string) => void
}

export const RealtimeContext = createContext<RealtimeContextValue | null>(null)
