import { createContext } from 'react'
import type { IncomingMessage } from '../api/messages'

export interface RealtimeContextValue {
  incomingMessages: IncomingMessage[]
  unreadMessageCount: number
  markConversationRead: (conversationId: string) => void
}

export const RealtimeContext = createContext<RealtimeContextValue | null>(null)
