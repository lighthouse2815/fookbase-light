import { HubConnectionBuilder, HubConnectionState, LogLevel } from '@microsoft/signalr'
import { useCallback, useEffect, useMemo, useRef, useState } from 'react'
import { apiBaseUrl } from '../api/client'
import { messagesApi } from '../api/messages'
import type { IncomingMessage } from '../api/messages'
import { notificationsApi } from '../api/notifications'
import type { AppNotification } from '../api/notifications'
import { useAuth } from '../auth/useAuth'
import { getAuthSession } from '../auth/session'
import { RealtimeContext } from './context'

interface MessageTyping {
  conversationId: string
  senderUserId: string
}

interface MessagesRead {
  conversationId: string
  readerUserId: string
  readAtUtc: string
}

export function RealtimeProvider({ children }: { children: React.ReactNode }) {
  const { session } = useAuth()
  const [incomingMessages, setIncomingMessages] = useState<IncomingMessage[]>([])
  const [notifications, setNotifications] = useState<AppNotification[]>([])
  const [unreadNotificationCount, setUnreadNotificationCount] = useState(0)
  const [nextNotificationCursor, setNextNotificationCursor] = useState<string | null>(null)
  const [isLoadingMoreNotifications, setIsLoadingMoreNotifications] = useState(false)
  const [typingConversationIds, setTypingConversationIds] = useState<ReadonlySet<string>>(new Set())
  const [readAtByConversation, setReadAtByConversation] = useState<ReadonlyMap<string, string>>(new Map())
  const messagesConnectionRef = useRef<ReturnType<HubConnectionBuilder['build']> | null>(null)
  const notificationsConnectionRef = useRef<ReturnType<HubConnectionBuilder['build']> | null>(null)
  const typingTimeoutsRef = useRef<Map<string, number>>(new Map())
  const addIncomingMessages = useCallback((nextMessages: IncomingMessage[]) => {
    setIncomingMessages((current) => [
      ...current,
      ...nextMessages.filter((nextMessage) => !current.some((item) => item.message.id === nextMessage.message.id)),
    ])
  }, [])
  const markConversationRead = useCallback((conversationId: string, lastReadMessageId?: string) => {
    setIncomingMessages((current) => current.filter((item) => item.conversation.id !== conversationId))
    if (lastReadMessageId) {
      void messagesApi.markConversationRead(conversationId, lastReadMessageId).catch(() => undefined)
    }
  }, [])
  const mergeNotifications = useCallback((nextNotifications: AppNotification[]) => {
    setNotifications((current) => {
      const byId = new Map(current.map((item) => [item.id, item]))
      for (const notification of nextNotifications) byId.set(notification.id, notification)
      return [...byId.values()].sort((left, right) =>
        Date.parse(right.createdAtUtc) - Date.parse(left.createdAtUtc) ||
        right.id.localeCompare(left.id))
    })
  }, [])
  const refreshNotificationCount = useCallback(() => {
    void notificationsApi.getUnreadCount()
      .then((count) => setUnreadNotificationCount(count.unreadNotificationCount))
      .catch(() => undefined)
  }, [])
  const reloadNotifications = useCallback(() => {
    void (async () => {
      try {
        const page = await notificationsApi.getPage()
        const count = await notificationsApi.getUnreadCount()
        setNotifications(page.items)
        setNextNotificationCursor(page.nextCursor)
        setUnreadNotificationCount(count.unreadNotificationCount)
      } catch {
        // The persisted API remains available for the next reconnect or reload.
      }
    })()
  }, [])
  const receiveNotification = useCallback((notification: AppNotification) => {
    mergeNotifications([notification])
    refreshNotificationCount()
  }, [mergeNotifications, refreshNotificationCount])
  const markNotificationRead = useCallback((notificationId: string) => {
    setNotifications((current) => current.map((notification) =>
      notification.id === notificationId
        ? { ...notification, isRead: true, readAtUtc: new Date().toISOString() }
        : notification))
    void notificationsApi.markRead(notificationId)
      .then(refreshNotificationCount)
      .catch(refreshNotificationCount)
  }, [refreshNotificationCount])
  const markAllNotificationsRead = useCallback(() => {
    setNotifications((current) => current.map((notification) => ({
      ...notification,
      isRead: true,
      readAtUtc: notification.readAtUtc ?? new Date().toISOString(),
    })))
    setUnreadNotificationCount(0)
    void notificationsApi.markAllRead()
      .then(refreshNotificationCount)
      .catch(refreshNotificationCount)
  }, [refreshNotificationCount])
  const loadMoreNotifications = useCallback(() => {
    if (!nextNotificationCursor || isLoadingMoreNotifications) return

    setIsLoadingMoreNotifications(true)
    void notificationsApi.getPage(nextNotificationCursor)
      .then((page) => {
        mergeNotifications(page.items)
        setNextNotificationCursor(page.nextCursor)
      })
      .catch(() => undefined)
      .finally(() => setIsLoadingMoreNotifications(false))
  }, [isLoadingMoreNotifications, mergeNotifications, nextNotificationCursor])
  const sendTyping = useCallback((conversationId: string) => {
    const connection = messagesConnectionRef.current
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
    reloadNotifications()

    const connection = new HubConnectionBuilder()
      .withUrl(`${apiBaseUrl}/hubs/messages`, {
        accessTokenFactory: () => getAuthSession()?.accessToken ?? '',
      })
      .withAutomaticReconnect()
      .configureLogging(import.meta.env.DEV ? LogLevel.Warning : LogLevel.Error)
      .build()
    const typingTimeouts = typingTimeoutsRef.current
    messagesConnectionRef.current = connection

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
    connection.on('MessagesRead', (read: MessagesRead) => {
      if (read.readerUserId === session.user.id) return

      setReadAtByConversation((current) => {
        const previousReadAt = current.get(read.conversationId)
        if (previousReadAt && Date.parse(previousReadAt) >= Date.parse(read.readAtUtc)) return current

        return new Map(current).set(read.conversationId, read.readAtUtc)
      })
    })
    void connection.start().catch(() => undefined)

    const notificationsConnection = new HubConnectionBuilder()
      .withUrl(apiBaseUrl + '/hubs/notifications', {
        accessTokenFactory: () => getAuthSession()?.accessToken ?? '',
      })
      .withAutomaticReconnect()
      .configureLogging(import.meta.env.DEV ? LogLevel.Warning : LogLevel.Error)
      .build()
    notificationsConnectionRef.current = notificationsConnection
    notificationsConnection.on('NotificationReceived', (notification: AppNotification) => {
      receiveNotification(notification)
    })
    notificationsConnection.onreconnected(() => {
      reloadNotifications()
    })
    void notificationsConnection.start().catch(() => undefined)

    return () => {
      active = false
      connection.off('MessageReceived')
      connection.off('TypingStarted')
      connection.off('MessagesRead')
      if (messagesConnectionRef.current === connection) messagesConnectionRef.current = null
      notificationsConnection.off('NotificationReceived')
      if (notificationsConnectionRef.current === notificationsConnection) notificationsConnectionRef.current = null
      for (const timeoutId of typingTimeouts.values()) window.clearTimeout(timeoutId)
      typingTimeouts.clear()
      setTypingConversationIds(new Set())
      setIncomingMessages([])
      setNotifications([])
      setUnreadNotificationCount(0)
      setNextNotificationCursor(null)
      setReadAtByConversation(new Map())
      void connection.stop()
      void notificationsConnection.stop()
    }
  }, [addIncomingMessages, receiveNotification, reloadNotifications, session])

  const value = useMemo(() => ({
    incomingMessages,
    notifications,
    unreadMessageCount: incomingMessages.length,
    unreadNotificationCount,
    hasMoreNotifications: nextNotificationCursor !== null,
    isLoadingMoreNotifications,
    typingConversationIds,
    readAtByConversation,
    markConversationRead,
    markNotificationRead,
    markAllNotificationsRead,
    loadMoreNotifications,
    sendTyping,
  }), [incomingMessages, isLoadingMoreNotifications, loadMoreNotifications, markAllNotificationsRead, markConversationRead, markNotificationRead, nextNotificationCursor, notifications, readAtByConversation, sendTyping, typingConversationIds, unreadNotificationCount])

  return <RealtimeContext.Provider value={value}>{children}</RealtimeContext.Provider>
}
