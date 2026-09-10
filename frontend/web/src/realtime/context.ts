import { createContext } from 'react'
import type { IncomingMessage } from '../api/messages'
import type { FriendNotification } from '../api/friends'

export interface RealtimeContextValue {
  incomingMessages: IncomingMessage[]
  incomingFriendNotifications: FriendNotification[]
  unreadMessageCount: number
  typingConversationIds: ReadonlySet<string>
  readAtByConversation: ReadonlyMap<string, string>
  markConversationRead: (conversationId: string) => void
  markFriendNotificationRead: (notificationId: string) => void
  sendTyping: (conversationId: string) => void
}

export const RealtimeContext = createContext<RealtimeContextValue | null>(null)
