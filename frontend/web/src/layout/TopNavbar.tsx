import { useCallback, useEffect, useLayoutEffect, useRef, useState, type ReactNode } from 'react'
import { Link, NavLink, useLocation, useNavigate } from 'react-router-dom'
import { useAuth } from '../auth/useAuth'
import { useRealtime } from '../realtime/useRealtime'
import { PreferenceControls, usePreferences } from '../preferences'
import { resolveProfileImageUrl, usersApi, type UserProfile } from '../api/users'
import { messagesApi, type Conversation, type IncomingMessage, type Message } from '../api/messages'
import { getNotificationPresentation } from '../shared/notificationPresentation'
import { showToast } from '../shared/toastState'
import NotificationCenter from '../shared/components/NotificationCenter'
import GlobalSearch from '../pages/search/GlobalSearch'
import { Mascot } from 'page-mascot'
import { SidebarLinks } from './Sidebar'

interface NavItem {
  path: string
  icon: ReactNode
  label: string
}

interface TopNavbarMascotItem {
  id: string
  name: string
  directions: string
  reactions: string
}

const leftNavbarMascots: readonly TopNavbarMascotItem[] = [
  { id: 'frog', name: 'Ếch xanh', directions: '/mascots/frog-directions.webp', reactions: '/mascots/frog-reactions.webp' },
  { id: 'otter', name: 'Rái cá', directions: '/mascots/otter-directions.webp', reactions: '/mascots/otter-reactions.webp' },
  { id: 'dino', name: 'Khủng long', directions: '/mascots/dino-directions.webp', reactions: '/mascots/dino-reactions.webp' },
  { id: 'bear', name: 'Gấu nâu', directions: '/mascots/bear-directions.webp', reactions: '/mascots/bear-reactions.webp' },
  { id: 'sheep', name: 'Bé cừu', directions: '/mascots/sheep-directions.webp', reactions: '/mascots/sheep-reactions.webp' },
]

const rightNavbarMascots: readonly TopNavbarMascotItem[] = [
  { id: 'wizard', name: 'Phù thủy', directions: '/mascots/wizard-directions.webp', reactions: '/mascots/wizard-reactions.webp' },
  { id: 'skater', name: 'Skater', directions: '/mascots/skater-directions.webp', reactions: '/mascots/skater-reactions.webp' },
  { id: 'astronaut', name: 'Phi hành gia', directions: '/mascots/astronaut-directions.webp', reactions: '/mascots/astronaut-reactions.webp' },
  { id: 'hamster', name: 'Hamster', directions: '/mascots/hamster-directions.webp', reactions: '/mascots/hamster-reactions.webp' },
  { id: 'tiger', name: 'Hổ con', directions: '/mascots/tiger-directions.webp', reactions: '/mascots/tiger-reactions.webp' },
]

function HomeIcon() {
  return <svg viewBox="0 0 28 28" aria-hidden="true" className="h-7 w-7 fill-current"><path d="M25.825 12.29 14.743 2.47a1.12 1.12 0 0 0-1.486 0L2.175 12.29a1.12 1.12 0 0 0 .743 1.96h2.237v9.29c0 1.082.878 1.96 1.96 1.96h4.06v-6.227h5.65V25.5h4.06c1.082 0 1.96-.878 1.96-1.96v-9.29h2.237a1.12 1.12 0 0 0 .743-1.96Z" /></svg>
}

function ReelsIcon() {
  return <svg viewBox="0 0 28 28" aria-hidden="true" className="h-7 w-7 fill-none stroke-current" strokeWidth="2.3" strokeLinecap="round" strokeLinejoin="round"><rect x="3" y="4" width="22" height="20" rx="4" /><path d="m3.5 9.5 5-5M10 9.5l5-5M16.5 9.5l5-5M12 12.25l5.25 3.25L12 18.75v-6.5Z" /></svg>
}

function GroupsIcon() {
  return <svg viewBox="0 0 28 28" aria-hidden="true" className="h-7 w-7 fill-none stroke-current" strokeWidth="2.3" strokeLinecap="round" strokeLinejoin="round"><circle cx="14" cy="9" r="4" /><path d="M6 24c.55-4.12 3.12-6.5 8-6.5s7.45 2.38 8 6.5M4.5 12.75a3 3 0 1 1 3.08-5.99M23.5 12.75a3 3 0 1 0-3.08-5.99M3.25 22.25c.2-2.1 1.22-3.7 3.2-4.62M24.75 22.25c-.2-2.1-1.22-3.7-3.2-4.62" /></svg>
}

function GamesIcon() {
  return <svg viewBox="0 0 28 28" aria-hidden="true" className="h-7 w-7 fill-none stroke-current" strokeWidth="2.3" strokeLinecap="round" strokeLinejoin="round"><path d="M7.2 10.5h13.6c2.4 0 3.8 1.84 3.35 4.18l-1.05 5.43c-.42 2.14-2.48 3.22-4.37 2.28l-3.3-1.64a3.04 3.04 0 0 0-2.66 0l-3.3 1.64c-1.89.94-3.95-.14-4.37-2.28l-1.05-5.43C3.4 12.34 4.8 10.5 7.2 10.5Z" /><path d="M9 15h4M11 13v4M18.5 14.5h.01M21 17h.01" /></svg>
}

function ProfileIcon() {
  return <svg viewBox="0 0 28 28" aria-hidden="true" className="h-7 w-7 fill-none stroke-current" strokeWidth="2.3" strokeLinecap="round" strokeLinejoin="round"><circle cx="14" cy="9" r="4.25" /><path d="M5.25 24c.7-4.38 3.58-7 8.75-7s8.05 2.62 8.75 7" /></svg>
}

function MenuIcon() {
  return <svg viewBox="0 0 24 24" aria-hidden="true" className="h-5 w-5 fill-current"><circle cx="5" cy="5" r="2" /><circle cx="12" cy="5" r="2" /><circle cx="19" cy="5" r="2" /><circle cx="5" cy="12" r="2" /><circle cx="12" cy="12" r="2" /><circle cx="19" cy="12" r="2" /><circle cx="5" cy="19" r="2" /><circle cx="12" cy="19" r="2" /><circle cx="19" cy="19" r="2" /></svg>
}

function ZolaLightIcon() {
  return <svg viewBox="0 0 24 24" aria-hidden="true" className="h-5 w-5 fill-current"><path d="M12 2.5C6.53 2.5 2.1 6.66 2.1 11.8c0 2.93 1.44 5.54 3.69 7.24v3.97l3.74-2.06c.8.22 1.63.34 2.47.34 5.47 0 9.9-4.16 9.9-9.29C21.9 6.66 17.47 2.5 12 2.5Zm1.08 12.58-2.52-2.69-4.92 2.72 5.42-5.75 2.6 2.7 4.81-2.72-5.39 5.74Z" /></svg>
}

function BellIcon() {
  return <svg viewBox="0 0 24 24" aria-hidden="true" className="h-5 w-5 fill-current"><path d="M19.2 16.4v-5.1c0-3.68-2.2-6.16-5.2-6.75V3.5a2 2 0 1 0-4 0v1.05c-3 .59-5.2 3.07-5.2 6.75v5.1L3.2 18v1.5h17.6V18l-1.6-1.6ZM12 22a2.75 2.75 0 0 0 2.59-1.8H9.4A2.75 2.75 0 0 0 12 22Z" /></svg>
}

function ChevronDownIcon() {
  return <svg viewBox="0 0 16 16" aria-hidden="true" className="h-3 w-3 fill-current"><path d="m4.1 5.9 3.9 3.9 3.9-3.9 1.1 1.1L8 11.1 3 7l1.1-1.1Z" /></svg>
}

function ProfileMenuIcon() {
  return <svg viewBox="0 0 24 24" aria-hidden="true" className="h-5 w-5 fill-none stroke-current" strokeWidth="2.3"><circle cx="12" cy="8" r="3.5" /><path d="M5.5 20c.65-4 2.9-6 6.5-6s5.85 2 6.5 6" /></svg>
}

function SettingsIcon() {
  return <svg viewBox="0 0 24 24" aria-hidden="true" className="h-5 w-5 fill-none stroke-current" strokeWidth="2.2" strokeLinecap="round" strokeLinejoin="round"><circle cx="12" cy="12" r="3" /><path d="M19.4 15a1.7 1.7 0 0 0 .34 1.88l.06.06-2.18 2.18-.06-.06a1.7 1.7 0 0 0-1.88-.34 1.7 1.7 0 0 0-1.03 1.55V20.4h-3.08v-.13a1.7 1.7 0 0 0-1.03-1.55 1.7 1.7 0 0 0-1.88.34l-.06.06-2.18-2.18.06-.06A1.7 1.7 0 0 0 6.86 15a1.7 1.7 0 0 0-1.55-1.03h-.13v-3.08h.13A1.7 1.7 0 0 0 6.86 9.86 1.7 1.7 0 0 0 6.52 8l-.06-.06 2.18-2.18.06.06a1.7 1.7 0 0 0 1.88.34 1.7 1.7 0 0 0 1.03-1.55v-.13h3.08v.13a1.7 1.7 0 0 0 1.03 1.55 1.7 1.7 0 0 0 1.88-.34l.06-.06 2.18 2.18-.06.06a1.7 1.7 0 0 0-.34 1.88 1.7 1.7 0 0 0 1.55 1.03h.13v3.08h-.13A1.7 1.7 0 0 0 19.4 15Z" /></svg>
}

function HelpIcon() {
  return <svg viewBox="0 0 24 24" aria-hidden="true" className="h-5 w-5 fill-none stroke-current" strokeWidth="2.3" strokeLinecap="round"><circle cx="12" cy="12" r="8.5" /><path d="M9.8 9.25a2.35 2.35 0 1 1 3.95 1.7c-.94.88-1.75 1.22-1.75 2.55M12 16.7h.01" /></svg>
}

function MoonIcon() {
  return <svg viewBox="0 0 24 24" aria-hidden="true" className="h-5 w-5 fill-current"><path d="M20.65 15.42A8.75 8.75 0 0 1 8.58 3.35 8.75 8.75 0 1 0 20.65 15.42Z" /></svg>
}

function LanguageIcon() {
  return <svg viewBox="0 0 24 24" aria-hidden="true" className="h-5 w-5 fill-none stroke-current" strokeWidth="2.1" strokeLinecap="round" strokeLinejoin="round"><path d="M4 5h10M9 3v2c0 5-2.25 8.5-5 10.5M5.5 10.5c1.3 1.55 3.1 2.85 5.5 3.75M14 19l3.25-9L20.5 19M15.3 16h3.9" /></svg>
}

function LogoutIcon() {
  return <svg viewBox="0 0 24 24" aria-hidden="true" className="h-5 w-5 fill-none stroke-current" strokeWidth="2.2" strokeLinecap="round" strokeLinejoin="round"><path d="M10 4H5.7A1.7 1.7 0 0 0 4 5.7v12.6A1.7 1.7 0 0 0 5.7 20H10" /><path d="m14 8 4 4-4 4M18 12H9" /></svg>
}

function BackIcon() {
  return <svg viewBox="0 0 24 24" aria-hidden="true" className="h-6 w-6 fill-none stroke-current" strokeWidth="2.4" strokeLinecap="round" strokeLinejoin="round"><path d="m14.5 5-7 7 7 7" /></svg>
}

type MessageFilter = 'all' | 'unread' | 'group'

function conversationName(conversation: Conversation, profiles: ReadonlyMap<string, UserProfile>) {
  if (conversation.type === 'group') return conversation.title ?? 'Nhóm không tên'
  return conversation.participantUserId ? profiles.get(conversation.participantUserId)?.displayName ?? 'Người dùng' : 'Cuộc trò chuyện'
}

function conversationPreview(conversation: Conversation) {
  const message = conversation.lastMessage
  if (!message) return 'Bắt đầu trò chuyện'
  if (message.deletedAtUtc) return 'Tin nhắn đã gỡ'
  return message.content ?? (message.attachments?.length ? 'Đã gửi tệp đính kèm' : 'Đã gửi tin nhắn')
}

function formatConversationTime(value: string) {
  const minutes = Math.max(0, Math.floor((Date.now() - Date.parse(value)) / 60_000))
  if (minutes < 1) return 'Vừa xong'
  if (minutes < 60) return `${minutes} phút`
  const hours = Math.floor(minutes / 60)
  if (hours < 24) return `${hours} giờ`
  return `${Math.floor(hours / 24)} ngày`
}

function MessagesPopover({ conversations, profiles, filter, onFilterChange, query, onQueryChange, isLoading, error, onOpenConversation }: {
  conversations: Conversation[]
  profiles: ReadonlyMap<string, UserProfile>
  filter: MessageFilter
  onFilterChange: (filter: MessageFilter) => void
  query: string
  onQueryChange: (query: string) => void
  isLoading: boolean
  error: string | null
  onOpenConversation: (conversation: Conversation) => void
}) {
  const zolaLightUrl = import.meta.env.VITE_ZOLA_LIGHT_URL ?? 'http://localhost:5175'
  const normalizedQuery = query.trim().toLocaleLowerCase()
  const visibleConversations = conversations.filter((conversation) => {
    if (filter === 'unread' && conversation.unreadCount === 0) return false
    if (filter === 'group' && conversation.type !== 'group') return false
    const profile = conversation.participantUserId ? profiles.get(conversation.participantUserId) : null
    return !normalizedQuery || [conversationName(conversation, profiles), profile?.username].some((value) => value?.toLocaleLowerCase().includes(normalizedQuery))
  })

  return <div className="header-popover w-[min(25rem,calc(100vw-1rem))] rounded-2xl border border-border bg-surface shadow-2xl">
    <div className="px-4 pb-2 pt-3"><h2 className="font-heading text-2xl font-bold text-text">Đoạn chat</h2></div>
    <div className="px-4 pb-2"><label className="relative block"><span className="pointer-events-none absolute left-3 top-1/2 -translate-y-1/2 text-text-light">⌕</span><input type="search" value={query} onChange={(event) => onQueryChange(event.target.value)} placeholder="Tìm kiếm tin nhắn" className="w-full rounded-full border-0 bg-surface-2 py-2 pl-9 pr-4 text-sm text-text outline-none placeholder:text-text-light focus:ring-2 focus:ring-primary" /></label></div>
    <div className="flex gap-1 px-4 pb-2">{([['all', 'Tất cả'], ['unread', 'Chưa đọc'], ['group', 'Nhóm']] as const).map(([value, label]) => <button type="button" key={value} onClick={() => onFilterChange(value)} className={`rounded-full border-0 px-3 py-2 text-sm font-semibold transition-colors ${filter === value ? 'bg-primary/20 text-primary' : 'bg-transparent text-text hover:bg-surface-2'}`}>{label}</button>)}</div>
    <div className="max-h-[min(34rem,calc(100vh-10rem))] overflow-y-auto px-2 pb-2">
      {isLoading ? <p className="px-4 py-8 text-center text-sm text-text-muted">Đang tải đoạn chat…</p> : error ? <p className="px-4 py-8 text-center text-sm text-[#ff8a9b]">{error}</p> : visibleConversations.length === 0 ? <p className="px-4 py-8 text-center text-sm text-text-muted">Không có đoạn chat phù hợp.</p> : visibleConversations.map((conversation) => {
        const profile = conversation.participantUserId ? profiles.get(conversation.participantUserId) : null
        const name = conversationName(conversation, profiles)
        const initials = name.slice(0, 2).toUpperCase()
        return <button type="button" key={conversation.id} onClick={() => onOpenConversation(conversation)} className={`flex w-full items-center gap-3 rounded-xl border-0 px-2 py-2.5 text-left transition-colors hover:bg-surface-2 ${conversation.unreadCount > 0 ? 'bg-primary/5' : ''}`}>
          <span className="flex h-14 w-14 shrink-0 items-center justify-center overflow-hidden rounded-full bg-primary text-sm font-bold text-white">{profile?.avatarUrl ? <img src={resolveProfileImageUrl(profile.avatarUrl)} alt="" className="h-full w-full object-cover" /> : initials}</span>
          <span className="min-w-0 flex-1"><strong className="block truncate text-sm text-text">{name}</strong><span className={`mt-0.5 block truncate text-xs ${conversation.unreadCount > 0 ? 'font-semibold text-text' : 'text-text-muted'}`}>{conversationPreview(conversation)} · {formatConversationTime(conversation.lastMessageAtUtc)}</span></span>
          {conversation.unreadCount > 0 && <span className="h-3 w-3 shrink-0 rounded-full bg-primary" aria-label={`${conversation.unreadCount} tin chưa đọc`} />}
        </button>
      })}
    </div>
    <div className="border-t border-border px-3 py-2">
      <a href={zolaLightUrl} className="block rounded-lg px-3 py-2 text-center text-sm font-semibold text-primary no-underline transition-colors hover:bg-surface-2">Xem tất cả trong Zola</a>
    </div>
  </div>
}

function sameMessageDay(left: string, right: string) {
  return new Date(left).toDateString() === new Date(right).toDateString()
}

function formatMessageDay(value: string) {
  const date = new Date(value)
  const today = new Date()
  if (date.toDateString() === today.toDateString()) return 'Hôm nay'
  const yesterday = new Date(today)
  yesterday.setDate(yesterday.getDate() - 1)
  if (date.toDateString() === yesterday.toDateString()) return 'Hôm qua'
  return new Intl.DateTimeFormat('vi-VN', { weekday: 'long', day: '2-digit', month: '2-digit', year: 'numeric' }).format(date)
}

function FloatingConversation({ conversation, profile, currentUserId, incomingMessages, isOnline, readAtUpdate, draft, sending, onSend, onDraftChange, onClose, onMinimize, onRead, onMessageSent }: {
  conversation: Conversation
  profile?: UserProfile
  currentUserId: string
  incomingMessages: IncomingMessage[]
  isOnline: boolean
  readAtUpdate?: string
  draft: string
  sending: boolean
  onSend: (content: string) => Promise<Message>
  onDraftChange: (draft: string, expectedDraft?: string) => void
  onClose: () => void
  onMinimize: () => void
  onRead: (conversationId: string, messageId?: string) => Promise<void>
  onMessageSent: (message: Message) => void
}) {
  const [messages, setMessages] = useState<Message[]>([])
  const [isLoading, setIsLoading] = useState(true)
  const [sendingLocally, setIsSending] = useState(false)
  const isSending = sendingLocally || sending
  const [pendingMessage, setPendingMessage] = useState<Message | null>(null)
  const [error, setError] = useState<string | null>(null)
  const viewportRef = useRef<HTMLDivElement>(null)
  const [nextCursor, setNextCursor] = useState<string | null>(null)
  const [isLoadingMore, setIsLoadingMore] = useState(false)
  const [hasNewMessages, setHasNewMessages] = useState(false)
  const requestRef = useRef(false)
  const sendRef = useRef(false)
  const mountedRef = useRef(false)
  const generationRef = useRef(0)
  const autoScrollRef = useRef(true)
  const prependScrollRef = useRef<{ top: number; height: number; anchorId?: string; anchorTop?: number } | null>(null)
  const messagesRef = useRef(messages)
  const readPendingRef = useRef(false)
  const readIdsRef = useRef(new Set<string>())
  const { messagesConnectionStatus, messagesRevision, reconnectMessages } = useRealtime()
  useLayoutEffect(() => { messagesRef.current = messages }, [messages])
  const name = conversationName(conversation, new Map(profile ? [[profile.userId, profile]] : []))
  const initials = name.slice(0, 2).toUpperCase()

  const loadMessages = useCallback(async (before?: string) => {
    if (requestRef.current) return
    requestRef.current = true
    const generation = generationRef.current
    setError(null)
    if (before) setIsLoadingMore(true)
    try {
      const page = await messagesApi.getMessages(conversation.id, before)
      if (!mountedRef.current || generation !== generationRef.current) return
      if (before && viewportRef.current) {
        const viewport = viewportRef.current
        const bounds = viewport.getBoundingClientRect()
        const anchor = [...viewport.querySelectorAll<HTMLElement>('[data-message-id]')].find(row => row.getBoundingClientRect().bottom > bounds.top)
        prependScrollRef.current = { top: viewport.scrollTop, height: viewport.scrollHeight, anchorId: anchor?.dataset.messageId, anchorTop: anchor?.getBoundingClientRect().top }
      }
      setMessages(current => [...current.filter(item => !page.items.some(next => next.id === item.id)), ...page.items]
        .sort((a, b) => Date.parse(a.createdAtUtc) - Date.parse(b.createdAtUtc) || a.id.localeCompare(b.id)))
      if (before || !messagesRef.current.length) setNextCursor(page.hasMore ? page.nextCursor : null)
    } catch {
      if (mountedRef.current && generation === generationRef.current) setError('Không thể tải tin nhắn. Hãy thử lại.')
    } finally {
      if (mountedRef.current && generation === generationRef.current) {
        requestRef.current = false
        setIsLoading(false)
        setIsLoadingMore(false)
      }
    }
  }, [conversation.id])

  useEffect(() => {
    mountedRef.current = true
    requestRef.current = false
    void Promise.resolve().then(() => { if (mountedRef.current) void loadMessages() })
    return () => { mountedRef.current = false; generationRef.current += 1 }
  }, [loadMessages])

  useEffect(() => {
    const received = incomingMessages.filter(item => item.conversation.id === conversation.id).map(item => item.message)
    if (!received.length) return
    void Promise.resolve().then(() => {
      if (!mountedRef.current) return
      const unseen = received.some(message => !messagesRef.current.some(item => item.id === message.id))
      if (!autoScrollRef.current && unseen) setHasNewMessages(true)
      setMessages(current => {
        const additions = received.filter(message => !current.some(item => item.id === message.id))
        return [...current, ...additions].sort((a, b) => Date.parse(a.createdAtUtc) - Date.parse(b.createdAtUtc) || a.id.localeCompare(b.id))
      })
    })
  }, [conversation.id, incomingMessages])

  useEffect(() => {
    if (readAtUpdate || messagesRevision > 0 || conversation.lastMessage?.id) void Promise.resolve().then(() => { if (mountedRef.current) void loadMessages() })
  }, [loadMessages, readAtUpdate, messagesRevision, conversation.lastMessage?.id])

  const readVisible = useCallback(function readVisibleMessages(): void {
    const viewport = viewportRef.current
    if (!viewport || document.visibilityState !== 'visible' || readPendingRef.current) return
    const bounds = viewport.getBoundingClientRect()
    const rows = [...viewport.querySelectorAll<HTMLElement>('[data-message-id]')]
    const latest = [...messagesRef.current].reverse().find(message => {
      if (message.senderUserId === currentUserId || readIdsRef.current.has(message.id)) return false
      const row = rows.find(element => element.dataset.messageId === message.id)
      if (!row) return false
      const rect = row.getBoundingClientRect()
      return rect.height > 0 && Math.min(rect.bottom, bounds.bottom, window.innerHeight) - Math.max(rect.top, bounds.top, 0) >= Math.min(rect.height, bounds.height) * 0.5
    })
    if (!latest) return
    readPendingRef.current = true
    void onRead(conversation.id, latest.id).then(() => {
      const index = messagesRef.current.findIndex(message => message.id === latest.id)
      messagesRef.current.slice(0, index + 1).forEach(message => readIdsRef.current.add(message.id))
      window.requestAnimationFrame(() => { if (mountedRef.current) readVisibleMessages() })
    }).catch(() => { if (mountedRef.current) setError('Chưa thể cập nhật trạng thái đã đọc. Hãy thử lại.') })
      .finally(() => { readPendingRef.current = false })
  }, [conversation.id, currentUserId, onRead])

  useLayoutEffect(() => {
    const viewport = viewportRef.current
    if (!viewport) return
    const previous = prependScrollRef.current
    if (previous) {
      const anchor = [...viewport.querySelectorAll<HTMLElement>('[data-message-id]')].find(row => row.dataset.messageId === previous.anchorId)
      if (anchor && previous.anchorTop !== undefined) viewport.scrollTop += anchor.getBoundingClientRect().top - previous.anchorTop
      else viewport.scrollTop = previous.top + viewport.scrollHeight - previous.height
      prependScrollRef.current = null
    } else if (autoScrollRef.current) viewport.scrollTop = viewport.scrollHeight
    readVisible()
  }, [messages, pendingMessage, readVisible])
  useEffect(() => {
    document.addEventListener('visibilitychange', readVisible)
    return () => document.removeEventListener('visibilitychange', readVisible)
  }, [readVisible])

  const visibleMessages = pendingMessage ? [...messages, pendingMessage] : messages
  const latestOwnMessageId = [...visibleMessages].reverse().find(message => message.senderUserId === currentUserId && !message.deletedAtUtc)?.id

  const send = async (event: React.FormEvent<HTMLFormElement>) => {
    event.preventDefault()
    const content = draft.trim()
    if (!content || sendRef.current || isSending) return
    sendRef.current = true
    autoScrollRef.current = true
    setIsSending(true)
    setError(null)
    const temporaryMessage: Message = {
      id: `pending-${crypto.randomUUID()}`, conversationId: conversation.id, senderUserId: currentUserId, content, createdAtUtc: new Date().toISOString(), readAtUtc: null, attachments: [],
    }
    setPendingMessage(temporaryMessage)
    try {
      const message = await onSend(content)
      setMessages((current) => [...current.filter(item => item.id !== message.id), message]
        .sort((a, b) => Date.parse(a.createdAtUtc) - Date.parse(b.createdAtUtc) || a.id.localeCompare(b.id)))
      setPendingMessage(null)
      onDraftChange('', draft)
      onMessageSent(message)
    } catch {
      setPendingMessage(null)
      setError('Không thể gửi tin nhắn.')
    } finally {
      sendRef.current = false
      setIsSending(false)
    }
  }

  return <section className="fixed bottom-0 right-3 z-[60] flex h-[min(34rem,calc(100dvh-var(--app-header-height)-0.5rem))] w-[min(23rem,calc(100vw-1rem))] flex-col overflow-hidden rounded-t-xl border border-border bg-surface shadow-2xl sm:right-5" aria-label={`Đoạn chat với ${name}`}>
    <header className="flex shrink-0 items-center gap-2 border-b border-border bg-surface px-3 py-2.5">
      <span className="flex h-9 w-9 shrink-0 items-center justify-center overflow-hidden rounded-full bg-primary text-xs font-bold text-white">{profile?.avatarUrl ? <img src={resolveProfileImageUrl(profile.avatarUrl)} alt="" className="h-full w-full object-cover" /> : initials}</span>
      <span className="min-w-0 flex-1"><strong className="block truncate text-sm text-text">{name}</strong><small className="block truncate text-xs text-text-muted">{isOnline ? 'Đang hoạt động' : 'Ngoại tuyến'}</small></span>
      <button type="button" disabled={isSending} onClick={onMinimize} className="grid h-8 w-8 place-items-center rounded-full border-0 bg-transparent text-lg text-text-muted hover:bg-surface-2" aria-label="Thu nhỏ đoạn chat">−</button>
      <button type="button" disabled={isSending} onClick={onClose} className="grid h-8 w-8 place-items-center rounded-full border-0 bg-transparent text-lg text-text-muted hover:bg-surface-2" aria-label="Đóng đoạn chat">×</button>
    </header>
    {messagesConnectionStatus !== 'connected' && <div role="status" className="flex items-center gap-2 bg-surface-2 px-3 py-2 text-xs text-text-muted">{messagesConnectionStatus === 'disconnected' ? 'Tin nhắn trực tiếp đang mất kết nối.' : 'Đang kết nối lại…'}{messagesConnectionStatus === 'disconnected' && <button type="button" onClick={() => void reconnectMessages()} className="font-semibold text-primary">Thử lại</button>}</div>}
    <div ref={viewportRef} data-chat-history className="flex min-h-0 flex-1 flex-col gap-2 overflow-y-auto bg-bg px-3 py-3" aria-busy={isLoading || isLoadingMore} onScroll={() => {
      const viewport = viewportRef.current
      if (!viewport) return
      autoScrollRef.current = viewport.scrollHeight - viewport.scrollTop - viewport.clientHeight < 64
      if (autoScrollRef.current) setHasNewMessages(false)
      readVisible()
    }}>
      {nextCursor && <button type="button" disabled={isLoadingMore} onClick={() => void loadMessages(nextCursor)} className="shrink-0 rounded-lg border border-border px-3 py-2 text-xs text-text">{isLoadingMore ? 'Đang tải…' : 'Tải tin cũ hơn'}</button>}
      {isLoading && <p className="my-auto text-center text-sm text-text-muted">Đang tải tin nhắn…</p>}
      {!isLoading && messages.length === 0 && !error && <p className="my-auto text-center text-sm text-text-muted">Chưa có tin nhắn. Hãy gửi lời chào.</p>}
      {visibleMessages.map((message, index) => {
        const isMine = message.senderUserId === currentUserId
        const status = message.id !== latestOwnMessageId ? null : message.id === pendingMessage?.id ? 'Đang gửi' : message.readAtUtc ? 'Đã xem' : 'Đã gửi'
        return <div key={message.id}>{(index === 0 || !sameMessageDay(visibleMessages[index - 1].createdAtUtc, message.createdAtUtc)) && <p className="my-4 text-center text-[11px] text-text-light">{formatMessageDay(message.createdAtUtc)}</p>}<div data-message-id={message.id} className={`flex items-end gap-1.5 ${isMine ? 'justify-end' : 'justify-start'}`} title={new Intl.DateTimeFormat('vi-VN', { dateStyle: 'medium', timeStyle: 'short' }).format(new Date(message.createdAtUtc))}>{!isMine && <span className="flex h-6 w-6 shrink-0 items-center justify-center overflow-hidden rounded-full bg-primary text-[9px] font-bold text-white">{profile?.avatarUrl ? <img src={resolveProfileImageUrl(profile.avatarUrl)} alt="" className="h-full w-full object-cover" /> : initials}</span>}<div className={`max-w-[82%] rounded-2xl px-3 py-2 text-sm ${isMine ? 'rounded-br-md bg-linear-to-br from-violet-600 to-blue-600 text-white' : 'rounded-bl-md bg-surface-2 text-text'}`}><p className="whitespace-pre-wrap break-words">{message.deletedAtUtc ? 'Tin nhắn đã gỡ' : message.content ?? 'Đã gửi tệp đính kèm'}</p>{status && <span className="mt-1 block text-right text-[10px] text-white/75">{status}</span>}</div></div></div>
      })}
    </div>
    {hasNewMessages && <button type="button" onClick={() => { if (viewportRef.current) viewportRef.current.scrollTop = viewportRef.current.scrollHeight; autoScrollRef.current = true; setHasNewMessages(false); readVisible() }} className="self-center rounded-full border border-border bg-surface-2 px-4 py-2 text-xs text-text">Có tin nhắn mới ↓</button>}
    {error && <p role="alert" className="border-t border-border bg-[#e41e3f]/10 px-3 py-2 text-xs text-[#ff8a9b]">{error} <button type="button" onClick={() => { readVisible(); void loadMessages() }} className="font-semibold underline">Thử lại</button></p>}
    <form onSubmit={(event) => void send(event)} className="flex shrink-0 gap-2 border-t border-border bg-surface p-2.5">
      <input value={draft} onChange={(event) => onDraftChange(event.target.value)} aria-label="Nhập tin nhắn" placeholder="Nhập tin nhắn" disabled={isSending} className="min-w-0 flex-1 rounded-full border-0 bg-surface-2 px-3 py-2 text-sm text-text outline-none placeholder:text-text-light focus:ring-2 focus:ring-primary disabled:opacity-60" />
      <button type="submit" disabled={!draft.trim() || isSending} className="rounded-full border-0 bg-primary px-3 py-2 text-sm font-semibold text-white disabled:cursor-not-allowed disabled:opacity-60">Gửi</button>
    </form>
  </section>
}

export default function TopNavbar() {
  const { session, signOut } = useAuth()
  const {
    incomingMessages,
    markNotificationRead,
    latestNotification,
    unreadMessageCount,
    unreadNotificationCount,
    markConversationRead,
    onlineUserIds,
    readAtByConversation,
  } = useRealtime()
  const { language, setLanguage, setTheme, t, theme } = usePreferences()
  const location = useLocation()
  const navigate = useNavigate()
  const [activeHeaderPopup, setActiveHeaderPopup] = useState<'menu' | 'messages' | 'notifications' | 'profile' | null>(null)
  const lastToastedNotificationRef = useRef<string | null>(null)
  const [avatarUrl, setAvatarUrl] = useState<string | null>(null)
  const fallbackDisplayName = session!.user.username.includes('@') ? 'Tài khoản của bạn' : session!.user.username
  const [displayName, setDisplayName] = useState(fallbackDisplayName)
  const [isAppearanceOpen, setIsAppearanceOpen] = useState(false)
  const [conversations, setConversations] = useState<Conversation[]>([])
  const [messageProfiles, setMessageProfiles] = useState<ReadonlyMap<string, UserProfile>>(new Map())
  const [messageFilter, setMessageFilter] = useState<MessageFilter>('all')
  const [messageQuery, setMessageQuery] = useState('')
  const [isLoadingMessages, setIsLoadingMessages] = useState(false)
  const [messagesError, setMessagesError] = useState<string | null>(null)
  const [openConversation, setOpenConversation] = useState<Conversation | null>(null)
  const [isConversationMinimized, setIsConversationMinimized] = useState(false)
  const [chatDrafts, setChatDrafts] = useState<ReadonlyMap<string, string>>(new Map())
  const [sendingConversationIds, setSendingConversationIds] = useState<ReadonlySet<string>>(new Set())
  const sendingConversationsRef = useRef(new Set<string>())
  const menuDropdownRef = useRef<HTMLDivElement>(null)
  const messagesDropdownRef = useRef<HTMLDivElement>(null)
  const notificationDropdownRef = useRef<HTMLDivElement>(null)
  const profileDropdownRef = useRef<HTMLDivElement>(null)
  const initials = session!.user.username.slice(0, 2).toUpperCase()
  const isNotificationsPage = location.pathname === '/notifications'
  const isMenuOpen = activeHeaderPopup === 'menu'
  const isMessagesOpen = activeHeaderPopup === 'messages'
  const isNotificationsOpen = activeHeaderPopup === 'notifications'
  const isProfileOpen = activeHeaderPopup === 'profile'
  const navItems: NavItem[] = [
    { path: '/feed', icon: <HomeIcon />, label: t('home') },
    { path: '/reels', icon: <ReelsIcon />, label: 'Reels' },
    { path: '/groups', icon: <GroupsIcon />, label: t('groups') },
    { path: '/games', icon: <GamesIcon />, label: t('games') },
    { path: '/profile', icon: <ProfileIcon />, label: t('profile') },
  ]

  useEffect(() => {
    if (!latestNotification || lastToastedNotificationRef.current === latestNotification.id) return
    lastToastedNotificationRef.current = latestNotification.id
    const presentation = getNotificationPresentation(latestNotification)
    if (isNotificationsOpen || isNotificationsPage || !presentation.canToast || presentation.destination === location.pathname + location.hash || presentation.destination.split('#')[0] === location.pathname) return
    showToast(presentation.text, 'info', `notification:${latestNotification.id}`, {
      item: latestNotification,
      onOpen: () => { markNotificationRead(latestNotification.id); setActiveHeaderPopup(null); navigate(presentation.destination) },
    })
  }, [latestNotification, isNotificationsOpen, isNotificationsPage, location.pathname, location.hash, markNotificationRead, navigate])

  useEffect(() => {
    let isCurrent = true
    void usersApi.getCurrent()
      .then((profile) => {
        if (isCurrent) {
          setAvatarUrl(profile.avatarUrl)
          setDisplayName(profile.displayName)
        }
      })
      .catch(() => {
        if (isCurrent) {
          setAvatarUrl(null)
          setDisplayName(fallbackDisplayName)
        }
      })

    return () => { isCurrent = false }
  }, [fallbackDisplayName, session?.user.id, session?.user.username])

  useEffect(() => {
    if (!isMessagesOpen) return
    let isCurrent = true
    void messagesApi.getConversations().then(async (page) => {
      const userIds = [...new Set(page.items.flatMap((conversation) => conversation.type === 'direct' && conversation.participantUserId ? [conversation.participantUserId] : []))]
      const profiles = await Promise.all(userIds.map((userId) => usersApi.getById(userId).catch(() => null)))
      if (!isCurrent) return
      setConversations(page.items)
      setMessageProfiles((current) => {
        const next = new Map(current)
        profiles.forEach((profile) => { if (profile) next.set(profile.userId, profile) })
        return next
      })
    }).catch(() => {
      if (isCurrent) setMessagesError(t('unableLoadConversations'))
    }).finally(() => { if (isCurrent) setIsLoadingMessages(false) })
    return () => { isCurrent = false }
  }, [incomingMessages, isMessagesOpen, t])

  useEffect(() => {
    if (!activeHeaderPopup) return

    const activePopupRef = activeHeaderPopup === 'menu'
      ? menuDropdownRef
      : activeHeaderPopup === 'messages'
        ? messagesDropdownRef
        : activeHeaderPopup === 'notifications'
          ? notificationDropdownRef
          : profileDropdownRef
    const closePopupWhenClickingOutside = (event: PointerEvent) => {
      if (!activePopupRef.current?.contains(event.target as Node)) {
        setActiveHeaderPopup(null)
        setIsAppearanceOpen(false)
      }
    }

    const closePopupOnEscape = (event: KeyboardEvent) => {
      if (event.key !== 'Escape' || event.defaultPrevented) return
      setActiveHeaderPopup(null)
      setIsAppearanceOpen(false)
      activePopupRef.current?.querySelector<HTMLButtonElement>('button')?.focus()
    }
    const closeNotificationWhenFocusLeaves = (event: FocusEvent) => {
      if (activeHeaderPopup === 'notifications' && !activePopupRef.current?.contains(event.target as Node)) setActiveHeaderPopup(null)
    }
    document.addEventListener('keydown', closePopupOnEscape)
    document.addEventListener('pointerdown', closePopupWhenClickingOutside, true)
    document.addEventListener('focusin', closeNotificationWhenFocusLeaves)
    return () => {
      document.removeEventListener('pointerdown', closePopupWhenClickingOutside, true)
      document.removeEventListener('keydown', closePopupOnEscape)
      document.removeEventListener('focusin', closeNotificationWhenFocusLeaves)
    }
  }, [activeHeaderPopup])

  const openFloatingConversation = useCallback((conversation: Conversation) => {
    setOpenConversation(conversation)
    setIsConversationMinimized(false)
    setActiveHeaderPopup(null)
  }, [])

  const updateFloatingConversation = useCallback((message: Message) => {
    setConversations((current) => current.map((item) => item.id === message.conversationId
      ? { ...item, lastMessage: message, lastMessageAtUtc: message.createdAtUtc }
      : item))
    setOpenConversation(current => current?.id === message.conversationId ? { ...current, lastMessage: message, lastMessageAtUtc: message.createdAtUtc } : current)
  }, [])

  const sendFloatingMessage = useCallback(async (conversationId: string, content: string) => {
    if (sendingConversationsRef.current.has(conversationId)) throw new Error('Tin nhắn đang được gửi.')
    sendingConversationsRef.current.add(conversationId)
    setSendingConversationIds(current => new Set(current).add(conversationId))
    try { return await messagesApi.sendMessage(conversationId, content) }
    finally {
      sendingConversationsRef.current.delete(conversationId)
      setSendingConversationIds(current => { const next = new Set(current); next.delete(conversationId); return next })
    }
  }, [])

  return (
    <>
      <header className="app-header fixed top-0 left-0 right-0 bg-surface border-b border-border flex flex-wrap items-center px-2 sm:px-4 z-50 lg:flex-nowrap">
        <div className="flex h-14 min-w-0 flex-1 items-center gap-2 lg:w-[280px] lg:flex-none">
          <Link
            to="/feed"
            className="w-10 h-10 rounded-full bg-primary flex items-center justify-center shrink-0 cursor-pointer border-none hover:brightness-110 transition no-underline"
            title={t('home')}
          >
            <span className="text-white text-xl font-bold">f</span>
          </Link>
          <div className="hidden items-center shrink-0 2xl:flex" title="Fookbase Kitty">
            <Mascot
              directions="/mascots/cat-directions.webp"
              reactions="/mascots/cat-reactions.webp"
              size={38}
              label="Navbar Cat Mascot"
            />
          </div>
          <GlobalSearch key={session!.user.id} userId={session!.user.id} onOpen={() => setActiveHeaderPopup(null)} />
        </div>

        {/* Left mascot squad (5 characters) */}
        <div className="hidden 2xl:flex items-center gap-1.5 shrink-0 px-2" aria-label="Mascots squad left">
          {leftNavbarMascots.map((item) => (
            <div key={item.id} title={item.name} className="flex items-center shrink-0">
              <Mascot
                directions={item.directions}
                reactions={item.reactions}
                size={34}
                label={item.name}
                className="transition-transform hover:scale-125 cursor-pointer drop-shadow-xs"
              />
            </div>
          ))}
        </div>

        <nav aria-label="Điều hướng chính" className="order-last flex h-12 w-full min-w-0 items-center justify-center gap-1 border-t border-border lg:order-none lg:h-full lg:w-auto lg:flex-1 lg:border-0 lg:px-2 lg:max-w-[680px] lg:mx-auto">
          {navItems.map((item) => (
            <NavLink
              key={item.path}
              to={item.path}
              className={({ isActive }) => [
                'flex flex-1 items-center justify-center self-stretch rounded-lg transition-colors duration-200 cursor-pointer relative max-w-[120px] no-underline',
                isActive ? 'text-primary' : 'text-text-muted hover:bg-surface-2',
              ].join(' ')}
              title={item.label}
              aria-label={item.label}
            >
              {({ isActive }) => (
                <>
                  {item.icon}
                  {isActive && <div className="absolute bottom-0 left-1 right-1 h-[3px] bg-primary rounded-t-full" />}
                </>
              )}
            </NavLink>
          ))}
        </nav>

        {/* Right mascot squad (5 characters) */}
        <div className="hidden 2xl:flex items-center gap-1.5 shrink-0 px-2" aria-label="Mascots squad right">
          {rightNavbarMascots.map((item) => (
            <div key={item.id} title={item.name} className="flex items-center shrink-0">
              <Mascot
                directions={item.directions}
                reactions={item.reactions}
                size={34}
                label={item.name}
                className="transition-transform hover:scale-125 cursor-pointer drop-shadow-xs"
              />
            </div>
          ))}
        </div>

        <div className="flex h-14 shrink-0 items-center justify-end gap-1 sm:gap-2 lg:w-[280px]">
          <div ref={menuDropdownRef} className="relative">
            <button
              type="button"
              onClick={() => {
                setActiveHeaderPopup((current) => current === 'menu' ? null : 'menu')
              }}
              className="w-10 h-10 rounded-full bg-surface-2 flex items-center justify-center text-text hover:bg-[#4e4f50] transition-colors cursor-pointer border-none focus-visible:ring-2 focus-visible:ring-primary focus-visible:ring-offset-2 focus-visible:ring-offset-surface"
              title="Menu"
              aria-label="Menu"
              aria-expanded={isMenuOpen}
            >
              <MenuIcon />
            </button>
            {isMenuOpen && <div className="header-popover w-[min(22rem,calc(100vw-1rem))] rounded-2xl border border-border bg-surface p-3 shadow-2xl">
              <div className="flex items-center justify-between pb-2"><h2 className="text-lg font-bold text-text">Menu</h2><button type="button" aria-label={t('close')} onClick={() => setActiveHeaderPopup(null)} className="grid h-9 w-9 place-items-center rounded-full bg-surface-2 text-text">×</button></div>
              <nav aria-label="Lối tắt" onClick={(event) => { if ((event.target as HTMLElement).closest('a')) setActiveHeaderPopup(null) }} className="mb-3"><SidebarLinks expanded /></nav>
              <div className="border-t border-border pt-3"><PreferenceControls /></div>
              <button type="button" onClick={() => void signOut()} className="mt-3 flex w-full items-center gap-2 rounded-lg border-0 bg-surface-2 px-3 py-2 text-left text-sm font-semibold text-text cursor-pointer transition-colors hover:bg-surface-hover">
                <span aria-hidden="true">↪</span>{t('signOut')}
              </button>
            </div>}
          </div>
          <div ref={messagesDropdownRef} className="relative">
            <button
              type="button"
              onClick={() => {
                if (!isMessagesOpen) {
                  setIsLoadingMessages(true)
                  setMessagesError(null)
                }
                setActiveHeaderPopup((current) => current === 'messages' ? null : 'messages')
                setIsAppearanceOpen(false)
              }}
              className={`relative flex h-10 w-10 items-center justify-center rounded-full border-0 transition-colors ${isMessagesOpen ? 'bg-primary text-white' : 'bg-surface-2 text-text hover:bg-[#4e4f50]'}`}
              title={t('messages')}
              aria-label={t('messages')}
              aria-expanded={isMessagesOpen}
            >
              <ZolaLightIcon />
              {unreadMessageCount > 0 && <span className="absolute -top-1 -right-1 min-w-5 h-5 rounded-full bg-[#e41e3f] text-[10px] font-bold text-white flex items-center justify-center px-1">{unreadMessageCount > 99 ? '99+' : unreadMessageCount}</span>}
            </button>
            {isMessagesOpen && <MessagesPopover conversations={conversations} profiles={messageProfiles} filter={messageFilter} onFilterChange={setMessageFilter} query={messageQuery} onQueryChange={setMessageQuery} isLoading={isLoadingMessages} error={messagesError} onOpenConversation={openFloatingConversation} />}
          </div>
          <div ref={notificationDropdownRef} className="relative">
            <button
              type="button"
              onClick={() => setActiveHeaderPopup((current) => current === 'notifications' ? null : 'notifications')}
              onKeyDown={(event) => { if (event.key === 'ArrowDown') { event.preventDefault(); setActiveHeaderPopup('notifications') } }}
              className={`notification-bell notification-focus relative flex h-10 w-10 items-center justify-center rounded-full border-0 text-sm ${isNotificationsOpen || isNotificationsPage ? 'bg-primary text-white' : 'bg-surface-2 text-text hover:bg-surface-hover'}`}
              title="Thông báo"
              aria-label={`Thông báo, ${unreadNotificationCount} chưa đọc`}
              aria-expanded={isNotificationsOpen}
              aria-haspopup="dialog"
            >
              <span key={`bell:${latestNotification?.id ?? 'initial'}`} className={latestNotification ? 'notification-bell-ring' : 'flex'}><BellIcon /></span>
              {unreadNotificationCount > 0 && <span key={`badge:${latestNotification?.id ?? 'initial'}`} aria-hidden="true" className={`absolute -right-0.5 -top-0.5 flex h-5 min-w-5 items-center justify-center rounded-full bg-[#e41e3f] px-1 text-[10px] font-bold text-white ${latestNotification ? 'notification-badge-pop' : ''}`}>{unreadNotificationCount > 99 ? '99+' : unreadNotificationCount}</span>}
            </button>
            {isNotificationsOpen && <NotificationCenter popover onOpen={() => setActiveHeaderPopup(null)} />}
          </div>
          <div ref={profileDropdownRef} className="relative">
            <button
              type="button"
              onClick={() => {
                setActiveHeaderPopup((current) => current === 'profile' ? null : 'profile')
                setIsAppearanceOpen(false)
              }}
              className={`relative flex h-10 w-10 shrink-0 items-center justify-center overflow-visible rounded-full border-none text-[11px] font-bold text-white transition ${isProfileOpen ? 'ring-2 ring-primary ring-offset-2 ring-offset-surface' : 'hover:brightness-110 cursor-pointer'} bg-primary`}
              title={t('profile')}
              aria-label={t('profile')}
              aria-expanded={isProfileOpen}
            >
              <span className="flex h-full w-full items-center justify-center overflow-hidden rounded-full">{avatarUrl ? <img src={resolveProfileImageUrl(avatarUrl)} alt="" className="h-full w-full object-cover" /> : initials}</span>
              <span className="absolute -bottom-0.5 -right-0.5 flex h-4 w-4 items-center justify-center rounded-full border-2 border-surface bg-surface-2 text-text"><ChevronDownIcon /></span>
            </button>
            {isProfileOpen && <div className="header-popover w-[min(24rem,calc(100vw-1rem))] rounded-2xl border border-border bg-surface shadow-2xl">
              <div className={`flex w-[200%] transition-transform duration-300 ease-out ${isAppearanceOpen ? '-translate-x-1/2' : 'translate-x-0'}`}>
                <section className="w-1/2 shrink-0 p-3">
                  <Link to="/profile" onClick={() => setActiveHeaderPopup(null)} className="block rounded-xl p-1.5 no-underline hover:bg-surface-2">
                    <div className="flex items-center gap-3 rounded-xl border-2 border-primary p-2">
                      <span className="flex h-12 w-12 shrink-0 items-center justify-center overflow-hidden rounded-full bg-primary text-sm font-bold text-white">{avatarUrl ? <img src={resolveProfileImageUrl(avatarUrl)} alt="" className="h-full w-full object-cover" /> : initials}</span>
                      <span className="min-w-0"><span className="block truncate text-base font-bold text-text">{displayName}</span>{!session!.user.username.includes('@') && <span className="block truncate text-sm text-text-muted">@{session!.user.username}</span>}</span>
                    </div>
                    <span className="mt-2 flex items-center justify-center gap-2 rounded-lg bg-surface-2 px-3 py-2 text-center text-sm font-semibold text-text"><ProfileMenuIcon />Xem trang cá nhân</span>
                  </Link>
                  <div className="my-2 border-t border-border" />
                  <Link to="/settings/privacy" onClick={() => setActiveHeaderPopup(null)} className="flex items-center gap-3 rounded-xl px-2 py-2.5 text-sm text-text no-underline hover:bg-surface-2"><span className="grid h-9 w-9 place-items-center rounded-full bg-surface-2"><SettingsIcon /></span><span className="flex-1 font-medium">Cài đặt và quyền riêng tư</span><span className="text-2xl text-text-muted">›</span></Link>
                  <Link to="/settings/security" onClick={() => setActiveHeaderPopup(null)} className="flex items-center gap-3 rounded-xl px-2 py-2.5 text-sm text-text no-underline hover:bg-surface-2"><span className="grid h-9 w-9 place-items-center rounded-full bg-surface-2"><HelpIcon /></span><span className="flex-1 font-medium">Trợ giúp và bảo mật</span><span className="text-2xl text-text-muted">›</span></Link>
                  <button type="button" onClick={() => setIsAppearanceOpen(true)} className="flex w-full items-center gap-3 rounded-xl border-0 bg-transparent px-2 py-2.5 text-left text-sm text-text cursor-pointer hover:bg-surface-2"><span className="grid h-9 w-9 place-items-center rounded-full bg-surface-2"><MoonIcon /></span><span className="flex-1 font-medium">Màn hình và trợ năng</span><span className="text-2xl text-text-muted">›</span></button>
                  <button type="button" onClick={() => void signOut()} className="flex w-full items-center gap-3 rounded-xl border-0 bg-transparent px-2 py-2.5 text-left text-sm text-text cursor-pointer hover:bg-surface-2"><span className="grid h-9 w-9 place-items-center rounded-full bg-surface-2"><LogoutIcon /></span><span className="font-medium">{t('signOut')}</span></button>
                  <p className="px-2 pt-2 text-xs leading-4 text-text-light">Quyền riêng tư · Điều khoản · Quảng cáo · Cookie · Thêm</p>
                </section>
                <section className="w-1/2 shrink-0 p-3">
                  <div className="mb-3 flex items-center gap-2"><button type="button" onClick={() => setIsAppearanceOpen(false)} className="grid h-10 w-10 place-items-center rounded-full border-2 border-primary bg-surface-2 text-text cursor-pointer hover:bg-surface-3" aria-label="Quay lại"><BackIcon /></button><h2 className="text-2xl font-bold text-text">Màn hình và trợ năng</h2></div>
                  <div className="flex gap-3 px-1 py-2"><span className="grid h-10 w-10 shrink-0 place-items-center rounded-full bg-surface-2"><MoonIcon /></span><div><h3 className="font-bold text-text">Chế độ tối</h3><p className="mt-1 text-sm leading-5 text-text-muted">Điều chỉnh giao diện để giảm độ chói và cho đôi mắt được nghỉ ngơi.</p></div></div>
                  <div className="mt-2 space-y-1 px-1">
                    <button type="button" onClick={() => setTheme('light')} className="flex w-full items-center justify-between rounded-lg border-0 bg-transparent px-3 py-2.5 text-left text-sm text-text cursor-pointer hover:bg-surface-2"><span>Tắt</span><span className={`h-5 w-5 rounded-full border-2 ${theme === 'light' ? 'border-primary bg-primary shadow-[inset_0_0_0_3px_var(--color-surface)]' : 'border-text-light'}`} /></button>
                    <button type="button" onClick={() => setTheme('dark')} className="flex w-full items-center justify-between rounded-lg border-0 bg-transparent px-3 py-2.5 text-left text-sm text-text cursor-pointer hover:bg-surface-2"><span>Bật</span><span className={`h-5 w-5 rounded-full border-2 ${theme === 'dark' ? 'border-primary bg-primary shadow-[inset_0_0_0_3px_var(--color-surface)]' : 'border-text-light'}`} /></button>
                  </div>
                  <div className="my-3 border-t border-border" />
                  <div className="flex gap-3 px-1 py-2"><span className="grid h-10 w-10 shrink-0 place-items-center rounded-full bg-surface-2"><LanguageIcon /></span><div><h3 className="font-bold text-text">Ngôn ngữ</h3><p className="mt-1 text-sm leading-5 text-text-muted">Chọn ngôn ngữ hiển thị của Fookbase.</p></div></div>
                  <div className="mt-2 space-y-1 px-1">
                    <button type="button" onClick={() => setLanguage('vi')} className="flex w-full items-center justify-between rounded-lg border-0 bg-transparent px-3 py-2.5 text-left text-sm text-text cursor-pointer hover:bg-surface-2"><span>Tiếng Việt</span><span className={`h-5 w-5 rounded-full border-2 ${language === 'vi' ? 'border-primary bg-primary shadow-[inset_0_0_0_3px_var(--color-surface)]' : 'border-text-light'}`} /></button>
                    <button type="button" onClick={() => setLanguage('en')} className="flex w-full items-center justify-between rounded-lg border-0 bg-transparent px-3 py-2.5 text-left text-sm text-text cursor-pointer hover:bg-surface-2"><span>English</span><span className={`h-5 w-5 rounded-full border-2 ${language === 'en' ? 'border-primary bg-primary shadow-[inset_0_0_0_3px_var(--color-surface)]' : 'border-text-light'}`} /></button>
                  </div>
                </section>
              </div>
            </div>}
          </div>
        </div>
      </header>
      {openConversation && (isConversationMinimized ? (
        <div className="fixed bottom-4 right-3 z-[60] flex flex-col items-center gap-3 sm:right-5">
          <button type="button" onClick={() => setIsConversationMinimized(false)} className="flex h-12 w-12 items-center justify-center overflow-hidden rounded-full border-0 bg-primary text-sm font-bold text-white shadow-2xl ring-2 ring-surface transition-transform hover:scale-105" title={conversationName(openConversation, messageProfiles)} aria-label={`Mở đoạn chat với ${conversationName(openConversation, messageProfiles)}`}>
            {messageProfiles.get(openConversation.participantUserId ?? '')?.avatarUrl ? <img src={resolveProfileImageUrl(messageProfiles.get(openConversation.participantUserId ?? '')!.avatarUrl!)} alt="" className="h-full w-full object-cover" /> : conversationName(openConversation, messageProfiles).slice(0, 2).toUpperCase()}
          </button>
          <button type="button" onClick={() => { setActiveHeaderPopup('messages'); setIsLoadingMessages(true); setMessagesError(null) }} className="grid h-12 w-12 place-items-center rounded-full border-0 bg-surface-2 text-2xl text-text shadow-2xl transition-colors hover:bg-surface-hover" title="Tin nhắn mới" aria-label="Tin nhắn mới">✎</button>
        </div>
      ) : (
        <FloatingConversation
          key={openConversation.id}
          conversation={openConversation}
          profile={openConversation.participantUserId ? messageProfiles.get(openConversation.participantUserId) : undefined}
          currentUserId={session!.user.id}
          incomingMessages={incomingMessages}
          isOnline={openConversation.participantUserId !== null && onlineUserIds.has(openConversation.participantUserId)}
          readAtUpdate={readAtByConversation.get(openConversation.id)}
          draft={chatDrafts.get(openConversation.id) ?? ''}
          sending={sendingConversationIds.has(openConversation.id)}
          onSend={content => sendFloatingMessage(openConversation.id, content)}
          onDraftChange={(draft, expectedDraft) => setChatDrafts(current => {
            if (expectedDraft !== undefined && current.get(openConversation.id) !== expectedDraft) return current
            return new Map(current).set(openConversation.id, draft)
          })}
          onClose={() => setOpenConversation(null)}
          onMinimize={() => setIsConversationMinimized(true)}
          onRead={markConversationRead}
          onMessageSent={updateFloatingConversation}
        />
      ))}
    </>
  )
}
