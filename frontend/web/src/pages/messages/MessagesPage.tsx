import { useCallback, useEffect, useMemo, useRef, useState, type KeyboardEvent } from 'react'
import { useSearchParams } from 'react-router-dom'
import { ApiError } from '../../api/client'
import { messagesApi } from '../../api/messages'
import type { Conversation, Message } from '../../api/messages'
import { resolveProfileImageUrl, usersApi } from '../../api/users'
import type { UserProfile } from '../../api/users'
import { useAuth } from '../../auth/useAuth'
import { useRealtime } from '../../realtime/useRealtime'
import { usePreferences } from '../../preferences'
import PaginationControls from '../../shared/components/PaginationControls'

function formatTimestamp(value: string, locale: string, nowLabel: string) {
  const date = new Date(value)
  const diffMinutes = Math.floor((Date.now() - date.getTime()) / 60_000)
  if (diffMinutes < 1) return nowLabel
  if (diffMinutes < 60) return `${diffMinutes}m`
  if (diffMinutes < 1_440) return `${Math.floor(diffMinutes / 60)}h`
  if (diffMinutes < 10_080) return `${Math.floor(diffMinutes / 1_440)}d`
  return new Intl.DateTimeFormat(locale, { month: 'short', day: 'numeric' }).format(date)
}

function avatarLabel(profile: UserProfile) {
  return profile.displayName.slice(0, 2).toUpperCase() || profile.username.slice(0, 2).toUpperCase()
}

function mergeMessagesChronologically(current: Message[], next: Message[]) {
  return [...current, ...next]
    .filter((message, index, items) => items.findIndex((item) => item.id === message.id) === index)
    .sort((left, right) =>
      Date.parse(left.createdAtUtc) - Date.parse(right.createdAtUtc) ||
      left.id.localeCompare(right.id),
    )
}

export default function MessagesPage() {
  const { session } = useAuth()
  const { language, t } = usePreferences()
  const locale = language === 'vi' ? 'vi-VN' : 'en-US'
  const [searchParams] = useSearchParams()
  const requestedConversationId = searchParams.get('conversation')
  const { incomingMessages, markConversationRead, readAtByConversation, sendTyping, typingConversationIds } = useRealtime()
  const [conversations, setConversations] = useState<Conversation[]>([])
  const [conversationTotal, setConversationTotal] = useState(0)
  const [conversationOffset, setConversationOffset] = useState(0)
  const [profiles, setProfiles] = useState<Record<string, UserProfile>>({})
  const [activeConversationId, setActiveConversationId] = useState<string | null>(null)
  const [messages, setMessages] = useState<Message[]>([])
  const [nextMessageCursor, setNextMessageCursor] = useState<string | null>(null)
  const [hasMoreMessages, setHasMoreMessages] = useState(false)
  const [draft, setDraft] = useState('')
  const [search, setSearch] = useState('')
  const [newMessageQuery, setNewMessageQuery] = useState('')
  const [newMessageResults, setNewMessageResults] = useState<UserProfile[]>([])
  const [isCreatingConversation, setIsCreatingConversation] = useState(false)
  const [isLoading, setIsLoading] = useState(true)
  const [isSending, setIsSending] = useState(false)
  const [isLoadingMoreConversations, setIsLoadingMoreConversations] = useState(false)
  const [isLoadingMoreMessages, setIsLoadingMoreMessages] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [conversationsPageError, setConversationsPageError] = useState<string | null>(null)
  const [messagesPageError, setMessagesPageError] = useState<string | null>(null)
  const messagesEndRef = useRef<HTMLDivElement>(null)
  const shouldScrollToBottomRef = useRef(false)
  const lastTypingSentAtRef = useRef(0)

  const loadConversations = useCallback(async (offset = 0, append = false) => {
    const page = await messagesApi.getConversations(offset)
    setConversations((current) => append
      ? [...current, ...page.items.filter((conversation) => !current.some((item) => item.id === conversation.id))]
      : page.items)
    setConversationTotal(page.total)
    setConversationOffset(page.offset + page.items.length)
    setActiveConversationId((current) => page.items.some((item) => item.id === requestedConversationId)
      ? requestedConversationId
      : current ?? page.items[0]?.id ?? null)

    const participantUserIds = [...new Set(page.items.map((item) => item.participantUserId))]
    if (participantUserIds.length === 0) return

    const settledProfiles = await Promise.allSettled(participantUserIds.map((userId) => usersApi.getById(userId)))
    const loadedProfiles = settledProfiles.flatMap((result) => result.status === 'fulfilled' ? [result.value] : [])
    if (loadedProfiles.length > 0) {
      setProfiles((current) => ({
        ...current,
        ...Object.fromEntries(loadedProfiles.map((profile) => [profile.userId, profile])),
      }))
    }
  }, [requestedConversationId])

  const loadMoreConversations = async () => {
    setIsLoadingMoreConversations(true)
    setConversationsPageError(null)
    try {
      await loadConversations(conversationOffset, true)
    } catch (requestError) {
      setConversationsPageError(requestError instanceof ApiError ? requestError.message : t('unableLoadConversations'))
    } finally {
      setIsLoadingMoreConversations(false)
    }
  }

  useEffect(() => {
    let active = true
    const timeoutId = window.setTimeout(() => {
      void loadConversations()
        .catch((requestError: unknown) => {
          if (active) setError(requestError instanceof ApiError ? requestError.message : t('unableLoadConversations'))
        })
        .finally(() => {
          if (active) setIsLoading(false)
        })
    }, 0)

    return () => {
      active = false
      window.clearTimeout(timeoutId)
    }
  }, [loadConversations, t])

  useEffect(() => {
    if (!activeConversationId) return

    let active = true
    void messagesApi.getMessages(activeConversationId)
      .then((page) => {
        if (!active) return
        setError(null)
        setMessages(page.items)
        setNextMessageCursor(page.nextCursor)
        setHasMoreMessages(page.hasMore)
        setMessagesPageError(null)
        shouldScrollToBottomRef.current = true
        markConversationRead(activeConversationId, page.items.at(-1)?.id)
        setConversations((current) => current.map((conversation) =>
          conversation.id === activeConversationId ? { ...conversation, unreadCount: 0 } : conversation,
        ))
      })
      .catch((requestError: unknown) => {
        if (active) setError(requestError instanceof ApiError ? requestError.message : t('unableLoadMessages'))
      })

    return () => { active = false }
  }, [activeConversationId, markConversationRead, t])

  useEffect(() => {
    if (incomingMessages.length === 0) return

    const timeoutId = window.setTimeout(() => {
      setConversations((current) => incomingMessages.reduce<Conversation[]>((updated, incomingMessage) => {
        const existing = updated.find((conversation) => conversation.id === incomingMessage.conversation.id)
        const nextConversation = existing
          ? { ...existing, ...incomingMessage.conversation }
          : incomingMessage.conversation
        return [
          nextConversation,
          ...updated.filter((conversation) => conversation.id !== nextConversation.id),
        ]
      }, current).sort((left, right) => Date.parse(right.lastMessageAtUtc) - Date.parse(left.lastMessageAtUtc)))
    }, 0)

    const profileIds = [...new Set(incomingMessages.map((item) => item.conversation.participantUserId))]
    void Promise.allSettled(profileIds.map((userId) => usersApi.getById(userId)))
      .then((results) => {
        const loadedProfiles = results.flatMap((result) => result.status === 'fulfilled' ? [result.value] : [])
        if (loadedProfiles.length > 0) {
          setProfiles((current) => ({
            ...current,
            ...Object.fromEntries(loadedProfiles.map((profile) => [profile.userId, profile])),
          }))
        }
      })

    const activeIncomingMessages = incomingMessages.filter((item) => item.conversation.id === activeConversationId)
    if (activeIncomingMessages.length === 0 || !activeConversationId) {
      return () => window.clearTimeout(timeoutId)
    }

    void messagesApi.getMessages(activeConversationId)
      .then((page) => {
        setMessages((current) => mergeMessagesChronologically(current, page.items))
        shouldScrollToBottomRef.current = true
        markConversationRead(activeConversationId, page.items.at(-1)?.id)
      })
      .catch(() => undefined)
    return () => window.clearTimeout(timeoutId)
  }, [activeConversationId, incomingMessages, markConversationRead])

  useEffect(() => {
    if (!activeConversationId) return

    const readAtUtc = readAtByConversation.get(activeConversationId)
    if (!readAtUtc) return

    const timeoutId = window.setTimeout(() => {
      setMessages((current) => current.map((message) =>
        message.senderUserId === session!.user.id
          && !message.readAtUtc
          && Date.parse(message.createdAtUtc) <= Date.parse(readAtUtc)
          ? { ...message, readAtUtc }
          : message,
      ))
    }, 0)
    return () => window.clearTimeout(timeoutId)
  }, [activeConversationId, readAtByConversation, session])

  useEffect(() => {
    if (!shouldScrollToBottomRef.current) return

    messagesEndRef.current?.scrollIntoView({ behavior: 'smooth' })
    shouldScrollToBottomRef.current = false
  }, [messages])

  useEffect(() => {
    if (!isCreatingConversation || !newMessageQuery.trim()) return

    let active = true
    const timeoutId = window.setTimeout(() => {
      void usersApi.search(newMessageQuery)
        .then((page) => {
          if (active) setNewMessageResults(page.items.filter((profile) => profile.userId !== session!.user.id))
        })
        .catch(() => {
          if (active) setNewMessageResults([])
        })
    }, 250)
    return () => {
      active = false
      window.clearTimeout(timeoutId)
    }
  }, [isCreatingConversation, newMessageQuery, session])

  const activeConversation = conversations.find((item) => item.id === activeConversationId) ?? null
  const partner = activeConversation ? profiles[activeConversation.participantUserId] : null
  const visibleConversations = useMemo(() => {
    const normalizedSearch = search.trim().toLowerCase()
    if (!normalizedSearch) return conversations
    return conversations.filter((conversation) => {
      const profile = profiles[conversation.participantUserId]
      return profile?.displayName.toLowerCase().includes(normalizedSearch)
        || profile?.username.toLowerCase().includes(normalizedSearch)
    })
  }, [conversations, profiles, search])

  const startConversation = async (profile: UserProfile) => {
    setError(null)
    try {
      const conversation = await messagesApi.getOrCreateConversation(profile.userId)
      setProfiles((current) => ({ ...current, [profile.userId]: profile }))
      setConversations((current) => {
        const existing = current.find((item) => item.id === conversation.id)
        return existing
          ? current.map((item) => item.id === conversation.id
            ? { ...item, ...conversation, lastMessage: conversation.lastMessage ?? item.lastMessage }
            : item)
          : [conversation, ...current]
      })
      setActiveConversationId(conversation.id)
      setIsCreatingConversation(false)
      setNewMessageQuery('')
      setNewMessageResults([])
    } catch (requestError) {
      setError(requestError instanceof ApiError ? requestError.message : t('unableCreateConversation'))
    }
  }

  const loadMoreMessages = async () => {
    if (!activeConversationId || !hasMoreMessages || !nextMessageCursor) return
    setIsLoadingMoreMessages(true)
    setMessagesPageError(null)
    try {
      const page = await messagesApi.getMessages(activeConversationId, nextMessageCursor)
      setMessages((current) => mergeMessagesChronologically(current, page.items))
      setNextMessageCursor(page.nextCursor)
      setHasMoreMessages(page.hasMore)
    } catch (requestError) {
      setMessagesPageError(requestError instanceof ApiError ? requestError.message : t('unableLoadMessages'))
    } finally {
      setIsLoadingMoreMessages(false)
    }
  }

  const handleSend = async () => {
    if (!activeConversationId || !draft.trim() || isSending) return
    setIsSending(true)
    setError(null)
    try {
      const message = await messagesApi.sendMessage(activeConversationId, draft.trim())
      setMessages((current) => [...current, message])
      shouldScrollToBottomRef.current = true
      setConversations((current) => current
        .map((conversation) => conversation.id === activeConversationId
          ? { ...conversation, lastMessage: message, lastMessageAtUtc: message.createdAtUtc }
          : conversation)
        .sort((left, right) => Date.parse(right.lastMessageAtUtc) - Date.parse(left.lastMessageAtUtc)))
      setDraft('')
    } catch (requestError) {
      setError(requestError instanceof ApiError ? requestError.message : t('unableSendMessage'))
    } finally {
      setIsSending(false)
    }
  }

  const handleKeyDown = (event: KeyboardEvent<HTMLTextAreaElement>) => {
    if (event.key === 'Enter' && !event.shiftKey) {
      event.preventDefault()
      void handleSend()
    }
  }

  const handleDraftChange = (value: string) => {
    setDraft(value)
    if (!activeConversationId || !value.trim()) return

    const now = Date.now()
    if (now - lastTypingSentAtRef.current >= 1_500) {
      lastTypingSentAtRef.current = now
      sendTyping(activeConversationId)
    }
  }

  return (
    <div className="flex h-[calc(100vh-56px)] bg-bg" style={{ animation: 'fade-in 0.25s ease both' }}>
      <aside className="w-[280px] shrink-0 border-r border-border bg-surface flex flex-col h-full max-sm:w-16">
        <div className="px-4 py-4 border-b border-border flex items-center justify-between">
          <h1 className="font-heading font-bold text-[17px] text-text max-sm:hidden">{t('messages')}</h1>
          <button type="button" onClick={() => setIsCreatingConversation((current) => !current)} className="w-8 h-8 rounded-full bg-surface-2 flex items-center justify-center text-text-muted hover:bg-surface-3 transition-colors cursor-pointer border-none" title={t('newMessage')}>✏️</button>
        </div>

        <div className="px-3 py-2 border-b border-border max-sm:hidden">
          <input type="text" value={search} onChange={(event) => setSearch(event.target.value)} placeholder={t('searchMessages')} className="w-full bg-surface-2 rounded-full text-[13px] text-text px-3.5 py-2 outline-none placeholder:text-text-light border border-border focus:input-focus transition-all" />
        </div>

        {isCreatingConversation && (
          <div className="p-3 border-b border-border max-sm:hidden">
            <input autoFocus type="search" value={newMessageQuery} onChange={(event) => setNewMessageQuery(event.target.value)} placeholder={t('findPerson')} className="w-full bg-surface-2 rounded-lg text-[13px] text-text px-3 py-2 outline-none placeholder:text-text-light border border-border focus:input-focus" />
            {newMessageResults.map((profile) => (
              <button key={profile.userId} type="button" onClick={() => void startConversation(profile)} className="w-full text-left flex gap-2 items-center px-1 py-2 hover:bg-surface-2 rounded-lg cursor-pointer border-none bg-transparent">
                <Avatar profile={profile} size="small" />
                <span className="min-w-0"><span className="block truncate text-sm text-text">{profile.displayName}</span><span className="block truncate text-xs text-text-muted">@{profile.username}</span></span>
              </button>
            ))}
          </div>
        )}

        <div className="flex-1 scroll-smooth overflow-y-auto">
          {isLoading && <p className="px-4 py-3 text-sm text-text-muted max-sm:hidden">{t('loadingConversations')}</p>}
          {!isLoading && visibleConversations.length === 0 && <p className="px-4 py-3 text-sm text-text-muted max-sm:hidden">{t('noConversations')}</p>}
          {visibleConversations.map((conversation) => {
            const profile = profiles[conversation.participantUserId]
            const isActive = conversation.id === activeConversationId
            return (
              <button key={conversation.id} type="button" onClick={() => setActiveConversationId(conversation.id)} className={['w-full text-left flex items-center gap-3 px-4 py-3 border-b border-border transition-colors cursor-pointer', isActive ? 'bg-surface-2 border-l-2 border-l-primary' : 'bg-transparent border-l-2 border-l-transparent hover:bg-surface-2'].join(' ')}>
                {profile ? <Avatar profile={profile} /> : <div className="w-10 h-10 rounded-full bg-surface-3 shrink-0" />}
                <div className="flex-1 min-w-0 max-sm:hidden">
                  <div className="flex items-center justify-between mb-0.5"><span className={`text-[13px] truncate ${conversation.unreadCount > 0 ? 'font-semibold' : 'font-medium'} text-text`}>{profile?.displayName ?? t('unknownUser')}</span><span className="text-[11px] text-text-light shrink-0 ml-1">{formatTimestamp(conversation.lastMessageAtUtc, locale, t('now'))}</span></div>
                  <div className="flex items-center justify-between"><p className={`text-[12px] truncate ${conversation.unreadCount > 0 ? 'text-text font-medium' : 'text-text-muted'}`}>{conversation.lastMessage?.content ?? t('startConversation')}</p>{conversation.unreadCount > 0 && <span className="ml-1 min-w-[18px] h-[18px] rounded-full bg-[#e41e3f] text-[10px] font-bold text-white flex items-center justify-center px-1.5 shrink-0">{conversation.unreadCount}</span>}</div>
                </div>
              </button>
            )
          })}
          <div className="p-3 max-sm:hidden"><PaginationControls hasMore={!search.trim() && conversationOffset < conversationTotal} isLoading={isLoadingMoreConversations} error={conversationsPageError} label={t('loadMoreConversations')} onLoadMore={() => void loadMoreConversations()} /></div>
        </div>
      </aside>

      <main className="flex-1 flex flex-col h-full overflow-hidden bg-bg">
        {!activeConversation || !partner ? (
          <div className="flex-1 flex items-center justify-center text-sm text-text-muted">{t('selectConversation')}</div>
        ) : (
          <>
            <div className="px-5 py-4 border-b border-border bg-surface flex items-center gap-3"><Avatar profile={partner} /><div><div className="text-[14px] font-semibold text-text">{partner.displayName}</div><div className="text-[12px] text-text-light">@{partner.username}</div></div><button type="button" onClick={() => void loadConversations().catch(() => undefined)} className="ml-auto text-sm text-primary bg-transparent border-none cursor-pointer">{t('refresh')}</button></div>
            <div className="flex-1 scroll-smooth overflow-y-auto p-5 flex flex-col gap-3">
              <PaginationControls hasMore={hasMoreMessages} isLoading={isLoadingMoreMessages} error={messagesPageError} label={t('loadMoreMessages')} onLoadMore={() => void loadMoreMessages()} />
              {messages.map((message, index) => {
                const isMine = message.senderUserId === session!.user.id
                const isLatestReadMessage = isMine
                  && message.readAtUtc
                  && !messages.some((candidate) =>
                    candidate.senderUserId === session!.user.id
                    && candidate.readAtUtc
                    && candidate.createdAtUtc > message.createdAtUtc)
                return <div key={message.id} className={`flex gap-2 ${isMine ? 'flex-row-reverse' : 'flex-row'}`} style={{ animation: `scale-in 0.2s ease ${index * 0.015}s both` }}>
                  {!isMine && <Avatar profile={partner} size="small" />}
                  <div className={`max-w-[65%] flex flex-col gap-1 ${isMine ? 'items-end' : 'items-start'}`}><div className={`px-4 py-2.5 text-[14px] leading-relaxed ${isMine ? 'bubble-mine' : 'bubble-theirs'}`}>{message.content}</div><span className="text-[11px] text-text-light px-1">{formatTimestamp(message.createdAtUtc, locale, t('now'))}{isLatestReadMessage ? ` · ${t('seen')}` : ''}</span></div>
                </div>
              })}
              {typingConversationIds.has(activeConversation.id) && (
                <div className="flex items-end gap-2">
                  <Avatar profile={partner} size="small" />
                  <div className="bubble-theirs px-4 py-3 flex items-center gap-1" aria-label={`${partner.displayName} ${t('isTyping')}`}>
                    {[0, 1, 2].map((dot) => <span key={dot} className="w-2 h-2 rounded-full bg-text-muted inline-block" style={{ animation: `pulse-dot 1.2s ease ${dot * 0.2}s infinite` }} />)}
                  </div>
                </div>
              )}
              {messages.length === 0 && <p className="text-sm text-text-muted">{t('noMessagesSayHello')}</p>}
              <div ref={messagesEndRef} />
            </div>
            <div className="px-4 py-3 border-t border-border bg-surface"><div className="flex items-center gap-3 bg-surface-2 rounded-full px-4 py-2 border border-border focus-within:border-border-focus transition-colors"><textarea rows={1} maxLength={5000} value={draft} disabled={isSending} onChange={(event) => handleDraftChange(event.target.value)} onKeyDown={handleKeyDown} placeholder={t('message')} className="flex-1 bg-transparent border-none text-[14px] text-text outline-none resize-none placeholder:text-text-light py-1 leading-normal disabled:opacity-50" />{draft.trim() && <button type="button" onClick={() => void handleSend()} disabled={isSending} className="w-8 h-8 rounded-full bg-primary flex items-center justify-center text-white cursor-pointer border-none shrink-0 hover:bg-primary-dark transition-all disabled:opacity-50" title={t('sendMessage')}>▶</button>}</div></div>
          </>
        )}
        {error && <p className="absolute bottom-4 right-4 max-w-sm rounded-lg bg-[#e41e3f]/10 border border-[#e41e3f]/40 p-3 text-sm text-[#ff8a9b]">{error}</p>}
      </main>
    </div>
  )
}

function Avatar({ profile, size = 'normal' }: { profile: UserProfile; size?: 'normal' | 'small' }) {
  const dimensions = size === 'small' ? 'w-8 h-8 text-[10px]' : 'w-10 h-10 text-[11px]'
  return <div className={`${dimensions} rounded-full bg-primary flex items-center justify-center font-bold text-white shrink-0 overflow-hidden`}>{profile.avatarUrl ? <img src={resolveProfileImageUrl(profile.avatarUrl)} alt="" className="w-full h-full object-cover" /> : avatarLabel(profile)}</div>
}
