import { HubConnectionBuilder, HubConnectionState, LogLevel } from '@microsoft/signalr'
import { useCallback, useEffect, useMemo, useRef, useState } from 'react'
import { apiBaseUrl } from '../api/client'
import { messagesApi } from '../api/messages'
import type { IncomingMessage } from '../api/messages'
import { notificationsApi } from '../api/notifications'
import type { AppNotification } from '../api/notifications'
import { useAuth } from '../auth/useAuth'
import { getAuthSession } from '../auth/session'
import { showToast } from '../shared/toastState'
import { RealtimeContext } from './context'
import {
  markNotificationRead as applyNotificationRead,
  mergeNotificationItems,
  receiveNotification as applyReceivedNotification,
  restoreNotificationReads,
} from './notificationState'
import type { NotificationState } from './notificationState'

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
  const [messagesConnectionStatus, setMessagesConnectionStatus] = useState<'connecting' | 'connected' | 'reconnecting' | 'disconnected'>('connecting')
  const [messagesRevision, setMessagesRevision] = useState(0)
  const [notificationState, setNotificationState] = useState<NotificationState>({ items: [], unreadCount: 0 })
  const [isLoadingNotifications, setIsLoadingNotifications] = useState(Boolean(session))
  const [notificationsError, setNotificationsError] = useState<string | null>(null)
  const [loadMoreNotificationsError, setLoadMoreNotificationsError] = useState<string | null>(null)
  const [isMarkingNotificationsRead, setIsMarkingNotificationsRead] = useState(false)
  const [isMarkingAllNotificationsRead, setIsMarkingAllNotificationsRead] = useState(false)
  const [latestNotification, setLatestNotification] = useState<AppNotification | null>(null)
  const [recentNotificationIds, setRecentNotificationIds] = useState<ReadonlySet<string>>(new Set())
  const [nextNotificationCursor, setNextNotificationCursor] = useState<string | null>(null)
  const [isLoadingMoreNotifications, setIsLoadingMoreNotifications] = useState(false)
  const [typingConversationIds, setTypingConversationIds] = useState<ReadonlySet<string>>(new Set())
  const [onlineUserIds, setOnlineUserIds] = useState<ReadonlySet<string>>(new Set())
  const [readAtByConversation, setReadAtByConversation] = useState<ReadonlyMap<string, string>>(new Map())
  const messagesConnectionRef = useRef<ReturnType<HubConnectionBuilder['build']> | null>(null)
  const notificationsConnectionRef = useRef<ReturnType<HubConnectionBuilder['build']> | null>(null)
  const typingTimeoutsRef = useRef<Map<string, number>>(new Map())
  const notificationStateRef = useRef(notificationState)
  const notificationSessionRef = useRef(0)
  const notificationSessionActiveRef = useRef(false)
  const notificationRevisionRef = useRef(0)
  const notificationPagePendingRef = useRef(false)
  const loadMorePendingRef = useRef(false)
  const notificationCursorRef = useRef<string | null>(null)
  const seenNotificationIdsRef = useRef(new Set<string>())
  const pendingNotificationReadsRef = useRef(new Set<string>())
  const pendingMarkAllRef = useRef(false)
  const reconcileAfterMarkAllRef = useRef(false)
  const notificationReadOverridesRef = useRef(new Map<string, Pick<AppNotification, 'isRead' | 'readAtUtc'>>())
  const notificationAnimationTimeoutsRef = useRef(new Map<string, number>())
  const countRequestRef = useRef<Promise<void> | null>(null)
  const countRefreshRequestedRef = useRef(false)
  const incomingRevisionRef = useRef(0)
  const unreadRequestRef = useRef(0)
  const incomingAtRevisionRef = useRef(new Map<string, { revision: number; incoming: IncomingMessage }>())
  const addIncomingMessages = useCallback((nextMessages: IncomingMessage[]) => {
    const revision = ++incomingRevisionRef.current
    for (const incoming of nextMessages) incomingAtRevisionRef.current.set(incoming.message.id, { revision, incoming })
    setIncomingMessages((current) => [
      ...current,
      ...nextMessages.filter((nextMessage) => !current.some((item) => item.message.id === nextMessage.message.id)),
    ])
  }, [])
  const refreshUnreadMessages = useCallback(async () => {
    const generation = notificationSessionRef.current
    const revision = incomingRevisionRef.current
    const request = ++unreadRequestRef.current
    const page = await messagesApi.getUnreadNotifications()
    if (generation !== notificationSessionRef.current || request !== unreadRequestRef.current) return
    const additions = [...incomingAtRevisionRef.current.values()]
      .filter(item => item.revision > revision && !page.items.some(existing => existing.message.id === item.incoming.message.id))
      .map(item => item.incoming)
    incomingAtRevisionRef.current = new Map([...page.items, ...additions].map(incoming => [incoming.message.id, { revision: incomingRevisionRef.current, incoming }]))
    setIncomingMessages([...page.items, ...additions])
  }, [])
  const markConversationRead = useCallback(async (conversationId: string, lastReadMessageId?: string) => {
    if (!lastReadMessageId) return
    const generation = notificationSessionRef.current
    await messagesApi.markConversationRead(conversationId, lastReadMessageId)
    if (generation !== notificationSessionRef.current) return
    // Reconcile with the server so messages arriving during the write keep their badge.
    await refreshUnreadMessages()
  }, [refreshUnreadMessages])
  const reconnectMessages = useCallback(async () => {
    const connection = messagesConnectionRef.current
    if (connection?.state !== HubConnectionState.Disconnected) return
    const generation = notificationSessionRef.current
    setMessagesConnectionStatus('connecting')
    try {
      await connection.start()
    } catch { if (generation === notificationSessionRef.current) setMessagesConnectionStatus('disconnected'); return }
    if (generation !== notificationSessionRef.current) return
    setMessagesConnectionStatus('connected')
    setMessagesRevision(current => current + 1)
    void refreshUnreadMessages().catch(() => undefined)
  }, [refreshUnreadMessages])
  const updateNotificationState = useCallback((update: (current: NotificationState) => NotificationState) => {
    const next = update(notificationStateRef.current)
    notificationStateRef.current = next
    setNotificationState(next)
  }, [])
  const mergeNotifications = useCallback((nextNotifications: AppNotification[]) => {
    for (const notification of nextNotifications) seenNotificationIdsRef.current.add(notification.id)
    updateNotificationState((current) => ({
      ...current,
      items: mergeNotificationItems(current.items, nextNotifications, notificationReadOverridesRef.current),
    }))
    for (const notification of nextNotifications) {
      if (notification.isRead && !pendingNotificationReadsRef.current.has(notification.id) && !pendingMarkAllRef.current) {
        notificationReadOverridesRef.current.delete(notification.id)
      }
    }
  }, [updateNotificationState])
  const refreshNotificationCount = useCallback(function refreshCount() {
    if (!notificationSessionActiveRef.current) return
    if (countRequestRef.current) {
      countRefreshRequestedRef.current = true
      return
    }
    if (pendingNotificationReadsRef.current.size || pendingMarkAllRef.current) return

    const generation = notificationSessionRef.current
    const request = (async () => {
      do {
        countRefreshRequestedRef.current = false
        const revision = notificationRevisionRef.current
        try {
          const count = await notificationsApi.getUnreadCount()
          if (generation !== notificationSessionRef.current) return
          if (pendingNotificationReadsRef.current.size || pendingMarkAllRef.current) return
          if (revision !== notificationRevisionRef.current) {
            countRefreshRequestedRef.current = true
            continue
          }
          updateNotificationState((current) => ({ ...current, unreadCount: count.unreadNotificationCount }))
        } catch {
          countRefreshRequestedRef.current = false
          return
        }
      } while (countRefreshRequestedRef.current)
    })()
    countRequestRef.current = request
    void request.finally(() => {
      if (generation !== notificationSessionRef.current) return
      countRequestRef.current = null
      if (countRefreshRequestedRef.current && !pendingNotificationReadsRef.current.size && !pendingMarkAllRef.current) {
        refreshCount()
      }
    })
  }, [updateNotificationState])
  const reloadNotifications = useCallback(() => {
    if (!notificationSessionActiveRef.current || notificationPagePendingRef.current || loadMorePendingRef.current || pendingMarkAllRef.current) return

    const generation = notificationSessionRef.current
    notificationPagePendingRef.current = true
    setIsLoadingNotifications(true)
    setNotificationsError(null)
    refreshNotificationCount()
    void (async () => {
      try {
        const page = await notificationsApi.getPage()
        if (generation !== notificationSessionRef.current) return
        mergeNotifications(page.items)
        notificationCursorRef.current = page.nextCursor
        setNextNotificationCursor(page.nextCursor)
        setLoadMoreNotificationsError(null)
      } catch {
        if (generation === notificationSessionRef.current) setNotificationsError('Không thể tải thông báo.')
      } finally {
        if (generation === notificationSessionRef.current) {
          notificationPagePendingRef.current = false
          setIsLoadingNotifications(false)
        }
      }
    })()
  }, [mergeNotifications, refreshNotificationCount])
  const receiveNotification = useCallback((notification: AppNotification) => {
    if (!notificationSessionActiveRef.current || seenNotificationIdsRef.current.has(notification.id)) return

    seenNotificationIdsRef.current.add(notification.id)
    notificationRevisionRef.current += 1
    updateNotificationState((current) => applyReceivedNotification(current, notification))
    setLatestNotification(notification)
    setRecentNotificationIds((current) => new Set(current).add(notification.id))
    const timeout = window.setTimeout(() => {
      setRecentNotificationIds((current) => {
        const next = new Set(current)
        next.delete(notification.id)
        return next
      })
      notificationAnimationTimeoutsRef.current.delete(notification.id)
    }, 650)
    notificationAnimationTimeoutsRef.current.set(notification.id, timeout)
    refreshNotificationCount()
  }, [refreshNotificationCount, updateNotificationState])
  const markNotificationRead = useCallback((notificationId: string) => {
    const notification = notificationStateRef.current.items.find((item) => item.id === notificationId)
    if (!notificationSessionActiveRef.current || !notification || notification.isRead || pendingNotificationReadsRef.current.has(notificationId)) return

    const generation = notificationSessionRef.current
    const readAtUtc = new Date().toISOString()
    const previousCount = notificationStateRef.current.unreadCount
    pendingNotificationReadsRef.current.add(notificationId)
    notificationReadOverridesRef.current.set(notificationId, { isRead: true, readAtUtc })
    notificationRevisionRef.current += 1
    setIsMarkingNotificationsRead(true)
    updateNotificationState((current) => applyNotificationRead(current, notificationId, readAtUtc))
    void notificationsApi.markRead(notificationId)
      .catch(() => {
        if (generation !== notificationSessionRef.current) return
        notificationReadOverridesRef.current.delete(notificationId)
        notificationRevisionRef.current += 1
        updateNotificationState((current) => restoreNotificationReads(current, [notification], previousCount > 0 ? 1 : 0))
        showToast('Không thể đánh dấu thông báo đã đọc. Vui lòng thử lại.', 'error', 'notification-read-error')
      })
      .finally(() => {
        if (generation !== notificationSessionRef.current) return
        pendingNotificationReadsRef.current.delete(notificationId)
        setIsMarkingNotificationsRead(pendingNotificationReadsRef.current.size > 0 || pendingMarkAllRef.current)
        if (!pendingMarkAllRef.current && pendingNotificationReadsRef.current.size === 0 && reconcileAfterMarkAllRef.current) {
          reconcileAfterMarkAllRef.current = false
          reloadNotifications()
        }
        refreshNotificationCount()
      })
  }, [refreshNotificationCount, reloadNotifications, updateNotificationState])
  const markAllNotificationsRead = useCallback(() => {
    if (!notificationSessionActiveRef.current || pendingMarkAllRef.current || pendingNotificationReadsRef.current.size || notificationPagePendingRef.current || loadMorePendingRef.current) return

    const snapshot = notificationStateRef.current
    if (!snapshot.unreadCount && snapshot.items.every((item) => item.isRead)) return

    const generation = notificationSessionRef.current
    const unreadItems = snapshot.items.filter((item) => !item.isRead)
    const readAtUtc = new Date().toISOString()
    pendingMarkAllRef.current = true
    reconcileAfterMarkAllRef.current = true
    notificationRevisionRef.current += 1
    setIsMarkingAllNotificationsRead(true)
    setIsMarkingNotificationsRead(true)
    for (const notification of unreadItems) notificationReadOverridesRef.current.set(notification.id, { isRead: true, readAtUtc })
    updateNotificationState((current) => ({
      items: current.items.map((item) => item.isRead ? item : { ...item, isRead: true, readAtUtc }),
      unreadCount: 0,
    }))
    void notificationsApi.markAllRead()
      .catch(() => {
        if (generation !== notificationSessionRef.current) return
        for (const notification of unreadItems) notificationReadOverridesRef.current.delete(notification.id)
        notificationRevisionRef.current += 1
        updateNotificationState((current) => restoreNotificationReads(current, unreadItems, snapshot.unreadCount))
        showToast('Không thể đánh dấu tất cả đã đọc. Vui lòng thử lại.', 'error', 'notification-read-all-error')
      })
      .finally(() => {
        if (generation !== notificationSessionRef.current) return
        pendingMarkAllRef.current = false
        setIsMarkingAllNotificationsRead(false)
        setIsMarkingNotificationsRead(pendingNotificationReadsRef.current.size > 0)
        // A notification received while read-all was running may also have been
        // included by the server. Reconcile its read flag after local writes settle.
        if (pendingNotificationReadsRef.current.size === 0) {
          reconcileAfterMarkAllRef.current = false
          reloadNotifications()
        }
        refreshNotificationCount()
      })
  }, [refreshNotificationCount, reloadNotifications, updateNotificationState])
  const loadMoreNotifications = useCallback(() => {
    const cursor = notificationCursorRef.current
    if (!notificationSessionActiveRef.current || !cursor || loadMorePendingRef.current || notificationPagePendingRef.current || pendingMarkAllRef.current) return

    const generation = notificationSessionRef.current
    loadMorePendingRef.current = true
    setIsLoadingMoreNotifications(true)
    setLoadMoreNotificationsError(null)
    void notificationsApi.getPage(cursor)
      .then((page) => {
        if (generation !== notificationSessionRef.current) return
        mergeNotifications(page.items)
        notificationCursorRef.current = page.nextCursor
        setNextNotificationCursor(page.nextCursor)
      })
      .catch(() => {
        if (generation === notificationSessionRef.current) setLoadMoreNotificationsError('Không thể tải thêm thông báo.')
      })
      .finally(() => {
        if (generation !== notificationSessionRef.current) return
        loadMorePendingRef.current = false
        setIsLoadingMoreNotifications(false)
      })
  }, [mergeNotifications])
  const sendTyping = useCallback((conversationId: string) => {
    const connection = messagesConnectionRef.current
    if (connection?.state === HubConnectionState.Connected) {
      void connection.invoke('Typing', conversationId).catch(() => undefined)
    }
  }, [])

  useEffect(() => {
    if (!session) return

    let active = true
    notificationSessionRef.current += 1
    notificationSessionActiveRef.current = true
    void refreshUnreadMessages().catch(() => undefined)
    reloadNotifications()

    const connection = new HubConnectionBuilder()
      .withUrl(`${apiBaseUrl}/hubs/messages`, {
        accessTokenFactory: () => getAuthSession()?.accessToken ?? '',
      })
      .withAutomaticReconnect()
      .configureLogging(import.meta.env.DEV ? LogLevel.Warning : LogLevel.Error)
      .build()
    const typingTimeouts = typingTimeoutsRef.current
    const seenNotificationIds = seenNotificationIdsRef.current
    const pendingNotificationReads = pendingNotificationReadsRef.current
    const notificationReadOverrides = notificationReadOverridesRef.current
    const notificationAnimationTimeouts = notificationAnimationTimeoutsRef.current
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
    connection.on('PresenceSnapshot', (snapshot: { userIds: string[] }) => {
      setOnlineUserIds(new Set(snapshot.userIds))
    })
    connection.on('PresenceChanged', (presence: { userId: string; isOnline: boolean }) => {
      setOnlineUserIds((current) => {
        const next = new Set(current)
        if (presence.isOnline) next.add(presence.userId)
        else next.delete(presence.userId)
        return next
      })
    })
    connection.on('MessagesRead', (read: MessagesRead) => {
      if (read.readerUserId === session.user.id) return

      setReadAtByConversation((current) => {
        const previousReadAt = current.get(read.conversationId)
        if (previousReadAt && Date.parse(previousReadAt) >= Date.parse(read.readAtUtc)) return current

        return new Map(current).set(read.conversationId, read.readAtUtc)
      })
    })
    connection.onreconnecting(() => { if (active) { setMessagesConnectionStatus('reconnecting'); setOnlineUserIds(new Set()) } })
    connection.onclose(() => { if (active) { setMessagesConnectionStatus('disconnected'); setOnlineUserIds(new Set()) } })
    connection.onreconnected(() => {
      if (!active) return
      setMessagesConnectionStatus('connected')
      setMessagesRevision(current => current + 1)
      void refreshUnreadMessages().catch(() => undefined)
    })
    void connection.start().then(() => { if (active) setMessagesConnectionStatus('connected') }).catch(() => { if (active) setMessagesConnectionStatus('disconnected') })

    const notificationsConnection = new HubConnectionBuilder()
      .withUrl(apiBaseUrl + '/hubs/notifications', {
        accessTokenFactory: () => getAuthSession()?.accessToken ?? '',
      })
      .withAutomaticReconnect()
      .configureLogging(import.meta.env.DEV ? LogLevel.Warning : LogLevel.Error)
      .build()
    notificationsConnectionRef.current = notificationsConnection
    notificationsConnection.on('NotificationReceived', (notification: AppNotification) => {
      if (active) receiveNotification(notification)
    })
    notificationsConnection.onreconnected(() => {
      if (active) reloadNotifications()
    })
    void notificationsConnection.start().catch(() => undefined)

    return () => {
      active = false
      notificationSessionActiveRef.current = false
      notificationSessionRef.current += 1
      notificationRevisionRef.current += 1
      connection.off('MessageReceived')
      connection.off('TypingStarted')
      connection.off('PresenceSnapshot')
      connection.off('PresenceChanged')
      connection.off('MessagesRead')
      if (messagesConnectionRef.current === connection) messagesConnectionRef.current = null
      notificationsConnection.off('NotificationReceived')
      if (notificationsConnectionRef.current === notificationsConnection) notificationsConnectionRef.current = null
      for (const timeoutId of typingTimeouts.values()) window.clearTimeout(timeoutId)
      typingTimeouts.clear()
      setTypingConversationIds(new Set())
      setOnlineUserIds(new Set())
      setIncomingMessages([])
      incomingAtRevisionRef.current.clear()
      incomingRevisionRef.current += 1
      notificationStateRef.current = { items: [], unreadCount: 0 }
      setNotificationState(notificationStateRef.current)
      notificationPagePendingRef.current = false
      loadMorePendingRef.current = false
      notificationCursorRef.current = null
      seenNotificationIds.clear()
      pendingNotificationReads.clear()
      pendingMarkAllRef.current = false
      reconcileAfterMarkAllRef.current = false
      notificationReadOverrides.clear()
      countRequestRef.current = null
      countRefreshRequestedRef.current = false
      for (const timeout of notificationAnimationTimeouts.values()) window.clearTimeout(timeout)
      notificationAnimationTimeouts.clear()
      setRecentNotificationIds(new Set())
      setLatestNotification(null)
      setIsLoadingNotifications(false)
      setIsLoadingMoreNotifications(false)
      setIsMarkingNotificationsRead(false)
      setIsMarkingAllNotificationsRead(false)
      setNotificationsError(null)
      setLoadMoreNotificationsError(null)
      setNextNotificationCursor(null)
      setReadAtByConversation(new Map())
      void connection.stop()
      void notificationsConnection.stop()
    }
  }, [addIncomingMessages, receiveNotification, reloadNotifications, refreshUnreadMessages, session])

  const value = useMemo(() => ({
    incomingMessages,
    messagesConnectionStatus,
    messagesRevision,
    reconnectMessages,
    notifications: notificationState.items,
    unreadMessageCount: incomingMessages.length,
    unreadNotificationCount: notificationState.unreadCount,
    hasMoreNotifications: nextNotificationCursor !== null,
    isLoadingNotifications,
    notificationsError,
    loadMoreNotificationsError,
    isLoadingMoreNotifications,
    isMarkingNotificationsRead,
    isMarkingAllNotificationsRead,
    latestNotification,
    recentNotificationIds,
    typingConversationIds,
    onlineUserIds,
    readAtByConversation,
    markConversationRead,
    markNotificationRead,
    markAllNotificationsRead,
    loadMoreNotifications,
    reloadNotifications,
    sendTyping,
  }), [messagesConnectionStatus, messagesRevision, reconnectMessages, incomingMessages, isLoadingNotifications, notificationsError, loadMoreNotificationsError, isLoadingMoreNotifications, isMarkingNotificationsRead, isMarkingAllNotificationsRead, latestNotification, recentNotificationIds, loadMoreNotifications, reloadNotifications, markAllNotificationsRead, markConversationRead, markNotificationRead, nextNotificationCursor, notificationState, onlineUserIds, readAtByConversation, sendTyping, typingConversationIds])

  return <RealtimeContext.Provider value={value}>{children}</RealtimeContext.Provider>
}
