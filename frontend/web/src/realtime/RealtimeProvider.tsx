import { HubConnectionBuilder, LogLevel } from '@microsoft/signalr'
import { useCallback, useEffect, useMemo, useState } from 'react'
import { apiBaseUrl } from '../api/client'
import { messagesApi } from '../api/messages'
import type { IncomingMessage } from '../api/messages'
import { useAuth } from '../auth/useAuth'
import { getAuthSession } from '../auth/session'
import { RealtimeContext } from './context'

export function RealtimeProvider({ children }: { children: React.ReactNode }) {
  const { session } = useAuth()
  const [incomingMessages, setIncomingMessages] = useState<IncomingMessage[]>([])
  const addIncomingMessages = useCallback((nextMessages: IncomingMessage[]) => {
    setIncomingMessages((current) => [
      ...current,
      ...nextMessages.filter((nextMessage) => !current.some((item) => item.message.id === nextMessage.message.id)),
    ])
  }, [])
  const markConversationRead = useCallback((conversationId: string) => {
    setIncomingMessages((current) => current.filter((item) => item.conversation.id !== conversationId))
  }, [])

  useEffect(() => {
    if (!session) return

    let active = true
    void messagesApi.getUnreadNotifications()
      .then((page) => {
        if (active) addIncomingMessages(page.items)
      })
      .catch(() => undefined)

    const connection = new HubConnectionBuilder()
      .withUrl(`${apiBaseUrl}/hubs/messages`, {
        accessTokenFactory: () => getAuthSession()?.accessToken ?? '',
      })
      .withAutomaticReconnect()
      .configureLogging(import.meta.env.DEV ? LogLevel.Warning : LogLevel.Error)
      .build()

    connection.on('MessageReceived', (incomingMessage: IncomingMessage) => {
      addIncomingMessages([incomingMessage])
    })
    void connection.start().catch(() => undefined)

    return () => {
      active = false
      connection.off('MessageReceived')
      void connection.stop()
    }
  }, [addIncomingMessages, session])

  const value = useMemo(() => ({
    incomingMessages,
    unreadMessageCount: incomingMessages.length,
    markConversationRead,
  }), [incomingMessages, markConversationRead])

  return <RealtimeContext.Provider value={value}>{children}</RealtimeContext.Provider>
}
