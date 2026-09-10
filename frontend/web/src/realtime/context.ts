import { createContext } from 'react'
import type { IncomingMessage } from '../api/messages'
import type { AppNotification } from '../api/notifications'

export interface RealtimeContextValue {
  incomingMessages: IncomingMessage[]
  notifications: AppNotification[]
  unreadMessageCount: number
  unreadNotificationCount: number
  hasMoreNotifications: boolean
  isLoadingMoreNotifications: boolean
  typingConversationIds: ReadonlySet<string>
  readAtByConversation: ReadonlyMap<string, string>
  markConversationRead: (conversationId: string, lastReadMessageId?: string) => void
  markNotificationRead: (notificationId: string) => void
  markAllNotificationsRead: () => void
  loadMoreNotifications: () => void
  sendTyping: (conversationId: string) => void
}

export const RealtimeContext = createContext<RealtimeContextValue | null>(null)
