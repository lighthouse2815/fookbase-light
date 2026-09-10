import { HubConnectionBuilder, HubConnectionState, LogLevel } from '@microsoft/signalr'
import { useCallback, useEffect, useMemo, useRef, useState } from 'react'
import { apiBaseUrl } from '../api/client'
import { messagesApi } from '../api/messages'
import type { IncomingMessage } from '../api/messages'
import { useAuth } from '../auth/useAuth'
import { getAuthSession } from '../auth/session'
import { RealtimeContext } from './context'

interface MessageTyping {
  conversationId: string
  senderUserId: string
}

export function RealtimeProvider({ children }: { children: React.ReactNode }) {
  const { session } = useAuth()
  const [incomingMessages, setIncomingMessages] = useState<IncomingMessage[]>([])
  const [typingConversationIds, setTypingConversationIds] = useState<ReadonlySet<string>>(new Set())
  const connectionRef = useRef<ReturnType<HubConnectionBuilder['build']> | null>(null)
  const typingTimeoutsRef = useRef<Map<string, number>>(new Map())
  const addIncomingMessages = useCallback((nextMessages: IncomingMessage[]) => {
    setIncomingMessages((current) => [
      ...current,
      ...nextMessages.filter((nextMessage) => !current.some((item) => item.message.id === nextMessage.message.id)),
    ])
  }, [])
  const markConversationRead = useCallback((conversationId: string) => {
    setIncomingMessages((current) => current.filter((item) => item.conversation.id !== conversationId))
  }, [])
  const sendTyping = useCallback((conversationId: string) => {
    const connection = connectionRef.current
    if (connection?.state === HubConnectionState.Connected) {
      void connection.invoke('Typing', conversationId).catch(() => undefined)
    }
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
    const typingTimeouts = typingTimeoutsRef.current
    connectionRef.current = connection

    connection.on('MessageReceived', (incomingMessage: IncomingMessage) => {
      addIncomingMessages([incomingMessage])
    })
    connection.on('TypingStarted', (typing: MessageTyping) => {
      if (typing.senderUserId === session.user.id) return

      const previousTimeout = typingTimeouts.get(typing.conversationId)
      if (previousTimeout) window.clearTimeout(previousTimeout)
      setTypingConversationIds((current) => new Set(current).add(typing.conversationId))
      const timeoutId = window.setTimeout(() => {
        setTypingConversationIds((current) => {
          const next = new Set(current)
          next.delete(typing.conversationId)
          return next
        })
        typingTimeouts.delete(typing.conversationId)
      }, 3_000)
      typingTimeouts.set(typing.conversationId, timeoutId)
    })
    void connection.start().catch(() => undefined)

    return () => {
      active = false
      connection.off('MessageReceived')
      connection.off('TypingStarted')
      if (connectionRef.current === connection) connectionRef.current = null
      for (const timeoutId of typingTimeouts.values()) window.clearTimeout(timeoutId)
      typingTimeouts.clear()
      setTypingConversationIds(new Set())
      void connection.stop()
    }
  }, [addIncomingMessages, session])

  const value = useMemo(() => ({
    incomingMessages,
    unreadMessageCount: incomingMessages.length,
    typingConversationIds,
    markConversationRead,
    sendTyping,
  }), [incomingMessages, markConversationRead, sendTyping, typingConversationIds])

  return <RealtimeContext.Provider value={value}>{children}</RealtimeContext.Provider>
}
