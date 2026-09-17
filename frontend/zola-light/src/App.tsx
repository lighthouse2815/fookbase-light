import { HubConnectionBuilder, HubConnectionState, LogLevel } from '@microsoft/signalr'
import { useCallback, useEffect, useRef, useState } from 'react'
import {
  ApiError,
  apiBaseUrl,
  authApi,
  clearSession,
  getSession,
  messengerApi,
  resolveApiUrl,
  saveSession,
  type AuthSession,
  type Conversation,
  type Message,
  type Participant,
  type UserProfile,
} from './api'

const reactions = ['like', 'love', 'haha', 'wow', 'sad', 'angry']

function displayConversation(conversation: Conversation, profiles?: ReadonlyMap<string, UserProfile>) {
  if (conversation.type === 'group') return conversation.title ?? 'Nhóm không tên'
  return conversation.participantUserId
    ? profiles?.get(conversation.participantUserId)?.displayName ?? 'Người dùng'
    : 'Cuộc trò chuyện'
}

function formatTime(value: string) {
  return new Intl.DateTimeFormat(undefined, { hour: '2-digit', minute: '2-digit' }).format(new Date(value))
}

function messageSummary(message: Message | null) {
  if (!message) return 'Bắt đầu trò chuyện'
  if (message.deletedAtUtc) return 'Tin nhắn đã gỡ'
  if (message.story) return message.story.isAvailable ? 'Đã trả lời Story' : 'Story không khả dụng'
  return message.content ?? (message.attachments?.length ? 'Đã gửi media' : 'Bắt đầu trò chuyện')
}

function upsertMessage(items: Message[], message: Message) {
  const index = items.findIndex((item) => item.id === message.id)
  const next = index < 0 ? [...items, message] : items.map((item) => item.id === message.id ? message : item)
  return next.sort((left, right) => Date.parse(left.createdAtUtc) - Date.parse(right.createdAtUtc) || left.id.localeCompare(right.id))
}

function Modal({ title, onClose, children }: { title: string; onClose: () => void; children: React.ReactNode }) {
  useEffect(() => {
    const closeOnEscape = (event: KeyboardEvent) => { if (event.key === 'Escape') onClose() }
    window.addEventListener('keydown', closeOnEscape)
    return () => window.removeEventListener('keydown', closeOnEscape)
  }, [onClose])

  return <div className="modal-backdrop" role="presentation" onMouseDown={onClose}><section className="modal" role="dialog" aria-modal="true" aria-label={title} onMouseDown={(event) => event.stopPropagation()}><header><h2>{title}</h2><button type="button" aria-label={`Đóng ${title}`} onClick={onClose}>×</button></header>{children}</section></div>
}

function Login({ onSession }: { onSession: (session: AuthSession) => void }) {
  const googleQuery = new URLSearchParams(window.location.search)
  const googleCompletionCode = googleQuery.get('provider') === 'google' ? googleQuery.get('code') : null
  const googleLinkMode = googleQuery.get('mode') === 'link'
  const [email, setEmail] = useState(() => googleLinkMode ? googleQuery.get('email') ?? '' : '')
  const [password, setPassword] = useState('')
  const [error, setError] = useState<string | null>(null)
  const [busy, setBusy] = useState(false)
  const [challenge, setChallenge] = useState<string | null>(null)
  const [code, setCode] = useState('')
  const [googleEnabled, setGoogleEnabled] = useState(false)
  const [googleLink] = useState(() =>
    googleLinkMode && googleCompletionCode ? { code: googleCompletionCode, email: googleQuery.get('email') ?? '' } : null)
  const completedGoogleCodes = useRef(new Set<string>())
  const isEmbeddedBrowser = /\b(Zalo|FBAN|FBAV|Messenger)\b/i.test(navigator.userAgent)

  useEffect(() => {
    let active = true
    void authApi.providers().then((providers) => {
      if (active) setGoogleEnabled(providers.google)
    }).catch(() => {
      if (active) setGoogleEnabled(false)
    })
    return () => { active = false }
  }, [])

  useEffect(() => {
    if (!googleCompletionCode || completedGoogleCodes.current.has(googleCompletionCode)) return
    completedGoogleCodes.current.add(googleCompletionCode)
    window.history.replaceState({}, document.title, window.location.pathname)
    if (googleLinkMode) return

    let active = true
    void authApi.completeGoogle(googleCompletionCode).then((result) => {
      if (!active) return
      if ('twoFactorRequired' in result) setChallenge(result.challenge)
      else { saveSession(result); onSession(result) }
    }).catch((reason) => {
      if (active) setError(reason instanceof ApiError ? reason.message : 'Không thể hoàn tất đăng nhập Google.')
    })
    return () => { active = false }
  }, [googleCompletionCode, googleLinkMode, onSession])

  const submit = async (event: React.FormEvent) => {
    event.preventDefault()
    setBusy(true)
    setError(null)
    try {
      const session = googleLink
        ? await authApi.linkGoogle(googleLink.code, password)
        : await authApi.login(email, password)
      if ('twoFactorRequired' in session) setChallenge(session.challenge)
      else { saveSession(session); onSession(session) }
    } catch (reason) {
      setError(reason instanceof ApiError ? reason.message : 'Không thể đăng nhập.')
    } finally {
      setBusy(false)
    }
  }
  return <main className="login-shell"><form className="login-card" onSubmit={submit}>
    <div className="brand-mark">z</div><h1>Zola Light</h1><p>{googleLink ? `Xác nhận mật khẩu Fookbase cho ${googleLink.email}.` : 'Đăng nhập bằng tài khoản Fookbase của bạn.'}</p>
    {error && <p className="alert">{error}</p>}
    {challenge ? <>
      <label>Mã xác thực<input autoFocus autoComplete="one-time-code" value={code} onChange={(event) => setCode(event.target.value)} required /></label>
      <button type="button" className="primary" disabled={busy || !code} onClick={() => { setBusy(true); void authApi.verifyTwoFactor(challenge, code).then((next) => { saveSession(next); onSession(next) }).catch((reason) => setError(reason instanceof ApiError ? reason.message : 'Mã không hợp lệ.')).finally(() => setBusy(false)) }}>Xác minh</button>
    </> : <>
      <label>Email<input autoComplete="email" type="email" value={email} onChange={(event) => setEmail(event.target.value)} readOnly={Boolean(googleLink)} required /></label>
      <label>Mật khẩu<input autoComplete="current-password" type="password" value={password} onChange={(event) => setPassword(event.target.value)} required /></label>
      <button className="primary" disabled={busy}>{busy ? 'Đang đăng nhập…' : googleLink ? 'Tiếp tục với Google' : 'Đăng nhập'}</button>
      {!googleLink && googleEnabled && <>
        <div className="login-separator"><span />HOẶC<span /></div>
        {isEmbeddedBrowser ? <p className="google-webview-notice">Hãy mở trang này bằng Chrome hoặc Safari để đăng nhập Google.</p> : <button type="button" className="google-login" onClick={() => window.location.assign(`${apiBaseUrl}/api/auth/google/start?client=zola-light`)}><span>G</span>Tiếp tục với Google</button>}
      </>}
    </>}
    <a href={import.meta.env.VITE_WEB_URL ?? 'http://localhost:5173'}>Quay lại Fookbase</a>
  </form></main>
}

interface ComposerProps {
  conversationId: string
  replyTo: Message | null
  onCancelReply: () => void
  onSend: (content: string, files: File[]) => Promise<void>
  onTyping: () => void
}

function Composer({ conversationId, replyTo, onCancelReply, onSend, onTyping }: ComposerProps) {
  const [content, setContent] = useState('')
  const [files, setFiles] = useState<File[]>([])
  const [busy, setBusy] = useState(false)
  const inputRef = useRef<HTMLInputElement>(null)
  const send = async () => {
    if (!content.trim() && files.length === 0) return
    setBusy(true)
    try {
      await onSend(content.trim(), files)
      setContent('')
      setFiles([])
    } finally {
      setBusy(false)
    }
  }
  return <div className="composer">
    {replyTo && <div className="replying">Đang trả lời: {replyTo.content ?? 'Tin nhắn đã gỡ'} <button onClick={onCancelReply}>×</button></div>}
    {files.length > 0 && <div className="pending-files">{files.map((file) => <span key={file.name}>{file.name}</span>)} <button onClick={() => setFiles([])}>Xóa</button></div>}
    <div className="composer-row">
      <input ref={inputRef} type="file" accept="image/jpeg,image/png,image/webp,video/mp4,video/webm" multiple hidden onChange={(event) => setFiles(Array.from(event.target.files ?? []))} />
      <button className="icon-button" title="Đính kèm ảnh hoặc video" onClick={() => inputRef.current?.click()}>＋</button>
      <textarea value={content} rows={1} disabled={busy} placeholder="Nhập tin nhắn" onChange={(event) => { setContent(event.target.value); onTyping() }} onKeyDown={(event) => { if (event.key === 'Enter' && !event.shiftKey) { event.preventDefault(); void send() } }} />
      <button className="primary send" disabled={busy || (!content.trim() && files.length === 0)} onClick={() => void send()}>Gửi</button>
    </div>
    <span className="sr-only">Conversation {conversationId}</span>
  </div>
}

function MessageBubble({ message, mine, onReply, onEdit, onDelete, onReact }: {
  message: Message; mine: boolean; onReply: () => void; onEdit: () => void; onDelete: () => void; onReact: (type: string) => void
}) {
  const [showActions, setShowActions] = useState(false)
  return <article className={`message-row ${mine ? 'mine' : ''}`} onMouseEnter={() => setShowActions(true)} onMouseLeave={() => setShowActions(false)}>
    <div className="message-stack">
      {message.replyTo && <div className="reply-preview">↪ {message.replyTo.isDeleted ? 'Tin nhắn đã gỡ' : message.replyTo.content ?? 'Media'}</div>}
      {message.story && <div className={`story-preview ${message.story.isAvailable ? '' : 'unavailable'}`}>
        <span>{message.story.isAvailable ? '◉' : '◌'}</span>
        <span><b>{message.story.isAvailable ? 'Story' : 'Story không khả dụng'}</b><small>{message.story.isAvailable ? message.story.caption ?? (message.story.mediaType === 'video' ? 'Video Story' : 'Ảnh Story') : 'Story này đã hết hạn hoặc bạn không còn quyền xem.'}</small></span>
      </div>}
      <div className={`bubble ${message.deletedAtUtc ? 'deleted' : ''}`}>
        {message.deletedAtUtc ? 'Tin nhắn đã được gỡ' : <>
          {message.content && <span>{message.content}</span>}
          {message.attachments?.length > 0 && <div className="attachment-list">{message.attachments.map((attachment) => <Attachment key={attachment.mediaId} mediaId={attachment.mediaId} />)}</div>}
        </>}
      </div>
      {message.reactions?.length > 0 && <div className="reactions">{message.reactions.map((reaction) => <span key={`${reaction.userId}-${reaction.type}`}>{reaction.type}</span>)}</div>}
      <small>{formatTime(message.createdAtUtc)}{message.editedAtUtc && ' · đã chỉnh sửa'}</small>
    </div>
    {showActions && !message.deletedAtUtc && <div className="message-actions">
      <button title="Trả lời" onClick={onReply}>↪</button>
      <select aria-label="Phản ứng" defaultValue="" onChange={(event) => { if (event.target.value) onReact(event.target.value); event.currentTarget.value = '' }}><option value="">☺</option>{reactions.map((reaction) => <option key={reaction} value={reaction}>{reaction}</option>)}</select>
      {mine && <><button title="Sửa" onClick={onEdit}>✎</button><button title="Gỡ" onClick={onDelete}>⋯</button></>}
    </div>}
  </article>
}

function Attachment({ mediaId }: { mediaId: string }) {
  const [url, setUrl] = useState<string | null>(null)
  useEffect(() => { let alive = true; void messengerApi.mediaUrl(mediaId).then((value) => { if (alive) setUrl(value) }).catch(() => undefined); return () => { alive = false } }, [mediaId])
  return url ? <a href={url} target="_blank" rel="noreferrer"><img src={url} alt="Tệp đính kèm" /></a> : <span className="attachment-loading">Đang tải media…</span>
}

function CreateConversation({ onClose, onDirect, onGroup }: { onClose: () => void; onDirect: (userId: string) => Promise<void>; onGroup: (title: string, ids: string[]) => Promise<void> }) {
  const [mode, setMode] = useState<'direct' | 'group'>('direct')
  const [search, setSearch] = useState('')
  const [users, setUsers] = useState<UserProfile[]>([])
  const [selected, setSelected] = useState<UserProfile[]>([])
  const [title, setTitle] = useState('')
  const [busy, setBusy] = useState(false)
  useEffect(() => {
    if (!search.trim()) { setUsers([]); return }
    const timeout = window.setTimeout(() => void messengerApi.searchUsers(search).then((result) => setUsers(result.items)).catch(() => setUsers([])), 250)
    return () => window.clearTimeout(timeout)
  }, [search])
  const select = async (user: UserProfile) => {
    if (mode === 'direct') { setBusy(true); try { await onDirect(user.userId); onClose() } finally { setBusy(false) }; return }
    setSelected((current) => current.some((item) => item.userId === user.userId) ? current : [...current, user])
    setSearch('')
  }
  const create = async () => { if (!title.trim() || selected.length === 0) return; setBusy(true); try { await onGroup(title.trim(), selected.map((user) => user.userId)); onClose() } finally { setBusy(false) } }
  return <Modal title="Tạo cuộc trò chuyện" onClose={onClose}>
    <div className="tabs"><button className={mode === 'direct' ? 'active' : ''} onClick={() => setMode('direct')}>Tin nhắn mới</button><button className={mode === 'group' ? 'active' : ''} onClick={() => setMode('group')}>Nhóm mới</button></div>
    {mode === 'group' && <label>Tên nhóm<input value={title} onChange={(event) => setTitle(event.target.value)} maxLength={120} /></label>}
    <label>Tìm người dùng<input autoFocus value={search} onChange={(event) => setSearch(event.target.value)} placeholder="Tên hoặc username" /></label>
    {selected.length > 0 && <div className="selected-users">{selected.map((user) => <button key={user.userId} onClick={() => setSelected((current) => current.filter((item) => item.userId !== user.userId))}>{user.displayName} ×</button>)}</div>}
    <div className="search-results">{users.map((user) => <button key={user.userId} disabled={busy} onClick={() => void select(user)}><Avatar name={user.displayName} url={user.avatarUrl} /><span>{user.displayName}</span></button>)}</div>
    {mode === 'group' && <button className="primary" disabled={busy || !title.trim() || selected.length === 0} onClick={() => void create()}>Tạo nhóm ({selected.length + 1})</button>}
  </Modal>
}

function AddParticipantsDialog({ conversation, onClose, onAdd }: { conversation: Conversation; onClose: () => void; onAdd: (userIds: string[]) => Promise<void> }) {
  const [search, setSearch] = useState('')
  const [users, setUsers] = useState<UserProfile[]>([])
  const [selected, setSelected] = useState<UserProfile[]>([])
  const [busy, setBusy] = useState(false)
  const existingUserIds = new Set(conversation.participants.map((participant) => participant.userId))
  useEffect(() => {
    if (!search.trim()) { setUsers([]); return }
    const timeout = window.setTimeout(() => void messengerApi.searchUsers(search).then((result) => setUsers(result.items.filter((user) => !existingUserIds.has(user.userId)))).catch(() => setUsers([])), 250)
    return () => window.clearTimeout(timeout)
  }, [search, conversation.id])
  const add = async () => {
    if (selected.length === 0) return
    setBusy(true)
    try { await onAdd(selected.map((user) => user.userId)); onClose() } finally { setBusy(false) }
  }
  return <Modal title="Thêm thành viên" onClose={onClose}>
    <label>Tìm người dùng<input autoFocus value={search} onChange={(event) => setSearch(event.target.value)} placeholder="Tên hoặc username" /></label>
    {selected.length > 0 && <div className="selected-users">{selected.map((user) => <button type="button" key={user.userId} onClick={() => setSelected((current) => current.filter((item) => item.userId !== user.userId))}>{user.displayName} ×</button>)}</div>}
    <div className="search-results">{users.map((user) => <button type="button" key={user.userId} disabled={busy} onClick={() => { setSelected((current) => current.some((item) => item.userId === user.userId) ? current : [...current, user]); setSearch('') }}><Avatar name={user.displayName} url={user.avatarUrl} /><span>{user.displayName}</span></button>)}</div>
    <button type="button" className="primary" disabled={busy || selected.length === 0} onClick={() => void add()}>Thêm {selected.length || ''} thành viên</button>
  </Modal>
}

function RenameGroupDialog({ currentTitle, onClose, onSave }: { currentTitle: string; onClose: () => void; onSave: (title: string) => Promise<void> }) {
  const [title, setTitle] = useState(currentTitle)
  const [busy, setBusy] = useState(false)
  const save = async () => { if (!title.trim()) return; setBusy(true); try { await onSave(title.trim()); onClose() } finally { setBusy(false) } }
  return <Modal title="Đổi tên nhóm" onClose={onClose}><label>Tên nhóm<input autoFocus value={title} maxLength={120} onChange={(event) => setTitle(event.target.value)} /></label><button type="button" className="primary" disabled={busy || !title.trim()} onClick={() => void save()}>Lưu</button></Modal>
}

function EditMessageDialog({ message, onClose, onSave }: { message: Message; onClose: () => void; onSave: (content: string) => Promise<void> }) {
  const [content, setContent] = useState(message.content ?? '')
  const [busy, setBusy] = useState(false)
  const save = async () => { if (!content.trim()) return; setBusy(true); try { await onSave(content.trim()); onClose() } finally { setBusy(false) } }
  return <Modal title="Chỉnh sửa tin nhắn" onClose={onClose}><label>Nội dung<textarea autoFocus value={content} rows={4} onChange={(event) => setContent(event.target.value)} /></label><button type="button" className="primary" disabled={busy || !content.trim()} onClick={() => void save()}>Lưu</button></Modal>
}

function ConfirmDialog({ title, description, confirmLabel, onClose, onConfirm }: { title: string; description: string; confirmLabel: string; onClose: () => void; onConfirm: () => Promise<void> }) {
  const [busy, setBusy] = useState(false)
  const confirm = async () => { setBusy(true); try { await onConfirm(); onClose() } finally { setBusy(false) } }
  return <Modal title={title} onClose={onClose}><p className="modal-description">{description}</p><div className="modal-actions"><button type="button" className="secondary" disabled={busy} onClick={onClose}>Hủy</button><button type="button" className="danger" disabled={busy} onClick={() => void confirm()}>{confirmLabel}</button></div></Modal>
}

function Avatar({ name, url }: { name: string; url?: string | null }) {
  return url ? <img className="avatar" src={resolveApiUrl(url)} alt="" /> : <span className="avatar">{name.slice(0, 2).toUpperCase()}</span>
}

function ConversationAvatar({ conversation, profiles }: { conversation: Conversation; profiles?: ReadonlyMap<string, UserProfile> }) {
  const [url, setUrl] = useState<string | null>(null)
  useEffect(() => {
    if (!conversation.photoMediaId) { setUrl(null); return }
    let isCurrent = true
    void messengerApi.mediaUrl(conversation.photoMediaId).then((value) => {
      if (isCurrent) setUrl(value)
    }).catch(() => { if (isCurrent) setUrl(null) })
    return () => { isCurrent = false }
  }, [conversation.photoMediaId])
  const directProfile = conversation.participantUserId ? profiles?.get(conversation.participantUserId) : null
  return <Avatar name={displayConversation(conversation, profiles)} url={url ?? directProfile?.avatarUrl} />
}

function AppShell({ session, onSignOut }: { session: AuthSession; onSignOut: () => Promise<void> }) {
  const [conversations, setConversations] = useState<Conversation[]>([])
  const [nextConversationCursor, setNextConversationCursor] = useState<string | null>(null)
  const [activeId, setActiveId] = useState<string | null>(null)
  const [messages, setMessages] = useState<Message[]>([])
  const [nextMessageCursor, setNextMessageCursor] = useState<string | null>(null)
  const [hasMoreMessages, setHasMoreMessages] = useState(false)
  const [replyTo, setReplyTo] = useState<Message | null>(null)
  const [typing, setTyping] = useState(false)
  const [onlineIds, setOnlineIds] = useState<ReadonlySet<string>>(new Set())
  const [profiles, setProfiles] = useState<ReadonlyMap<string, UserProfile>>(new Map())
  const [error, setError] = useState<string | null>(null)
  const [showCreate, setShowCreate] = useState(false)
  const [showAddParticipants, setShowAddParticipants] = useState(false)
  const [showRenameGroup, setShowRenameGroup] = useState(false)
  const [editingMessage, setEditingMessage] = useState<Message | null>(null)
  const [confirmation, setConfirmation] = useState<{ title: string; description: string; confirmLabel: string; onConfirm: () => Promise<void> } | null>(null)
  const connectionRef = useRef<ReturnType<HubConnectionBuilder['build']> | null>(null)
  const typingTimeout = useRef<number | null>(null)
  const groupPhotoInputRef = useRef<HTMLInputElement>(null)
  const active = conversations.find((conversation) => conversation.id === activeId) ?? null
  const currentUserProfile = profiles.get(session.user.id)

  useEffect(() => {
    let isCurrent = true
    void messengerApi.currentUser().then((profile) => {
      if (isCurrent) setProfiles((current) => new Map(current).set(profile.userId, profile))
    }).catch(() => undefined)
    return () => { isCurrent = false }
  }, [])

  const loadConversations = useCallback(async (before?: string) => {
    const page = await messengerApi.conversations(before)
    setConversations((current) => before ? [...current, ...page.items.filter((item) => !current.some((existing) => existing.id === item.id))] : page.items)
    setNextConversationCursor(page.nextCursor)
    if (!before) setActiveId((current) => current ?? page.items[0]?.id ?? null)
  }, [])
  const loadMessages = useCallback(async (conversationId: string, before?: string) => {
    const page = await messengerApi.messages(conversationId, before)
    setMessages((current) => before ? [...page.items, ...current.filter((item) => !page.items.some((newItem) => newItem.id === item.id))] : page.items)
    setNextMessageCursor(page.nextCursor)
    setHasMoreMessages(page.hasMore)
    const lastIncoming = [...page.items].reverse().find((message) => message.senderUserId !== session.user.id)
    if (!before && lastIncoming) void messengerApi.read(conversationId, lastIncoming.id).catch(() => undefined)
  }, [session.user.id])
  useEffect(() => { void loadConversations().catch((reason) => setError(reason instanceof ApiError ? reason.message : 'Không thể tải cuộc trò chuyện.')) }, [loadConversations])
  useEffect(() => {
    const unknownIds = [...new Set(conversations.flatMap((conversation) => conversation.type === 'direct' && conversation.participantUserId ? [conversation.participantUserId] : conversation.participants.map((participant) => participant.userId)).filter((userId) => !profiles.has(userId)))]
    if (unknownIds.length === 0) return
    let isCurrent = true
    void Promise.all(unknownIds.map((userId) => messengerApi.user(userId).catch(() => null))).then((users) => {
      if (!isCurrent) return
      const resolvedProfiles = users.filter((user): user is UserProfile => user !== null)
      if (resolvedProfiles.length === 0) return
      setProfiles((current) => {
        const next = new Map(current)
        resolvedProfiles.forEach((profile) => next.set(profile.userId, profile))
        return next
      })
    })
    return () => { isCurrent = false }
  }, [conversations, profiles])
  useEffect(() => { if (activeId) { setReplyTo(null); void loadMessages(activeId).catch((reason) => setError(reason instanceof ApiError ? reason.message : 'Không thể tải tin nhắn.')) } }, [activeId, loadMessages])
  useEffect(() => {
    const connection = new HubConnectionBuilder().withUrl(`${apiBaseUrl}/hubs/messages`, { accessTokenFactory: () => getSession()?.accessToken ?? '' }).withAutomaticReconnect().configureLogging(import.meta.env.DEV ? LogLevel.Warning : LogLevel.Error).build()
    connectionRef.current = connection
    connection.on('MessageCreated', (message: Message) => { if (message.conversationId === activeId) { setMessages((current) => upsertMessage(current, message)); void messengerApi.read(message.conversationId, message.id).catch(() => undefined) }; void loadConversations().catch(() => undefined) })
    connection.on('MessageEdited', (message: Message) => { if (message.conversationId === activeId) setMessages((current) => upsertMessage(current, message)) })
    connection.on('MessageDeleted', (event: { messageId: string; conversationId: string; deletedAtUtc: string }) => { if (event.conversationId === activeId) setMessages((current) => current.map((message) => message.id === event.messageId ? { ...message, content: null, attachments: [], reactions: [], deletedAtUtc: event.deletedAtUtc } : message)) })
    connection.on('MessageReactionChanged', () => { if (activeId) void loadMessages(activeId).catch(() => undefined) })
    connection.on('ConversationCreated', () => void loadConversations().catch(() => undefined))
    connection.on('ConversationUpdated', () => { void loadConversations().catch(() => undefined); if (activeId) void messengerApi.conversation(activeId).then((updated) => setConversations((current) => current.map((item) => item.id === updated.id ? updated : item))).catch(() => undefined) })
    connection.on('ParticipantRemoved', (event: { conversationId: string; userId: string }) => { if (event.userId === session.user.id) { setConversations((current) => current.filter((conversation) => conversation.id !== event.conversationId)); setActiveId((current) => current === event.conversationId ? null : current) } else void loadConversations().catch(() => undefined) })
    connection.on('TypingChanged', (event: { conversationId: string; senderUserId: string; isTyping: boolean }) => { if (event.conversationId === activeId && event.senderUserId !== session.user.id) { setTyping(event.isTyping); window.setTimeout(() => setTyping(false), 3000) } })
    connection.on('PresenceSnapshot', (snapshot: { userIds: string[] }) => setOnlineIds(new Set(snapshot.userIds)))
    connection.on('PresenceChanged', (event: { userId: string; isOnline: boolean }) => setOnlineIds((current) => {
      const next = new Set(current)
      if (event.isOnline) next.add(event.userId)
      else next.delete(event.userId)
      return next
    }))
    connection.onreconnected(() => { void loadConversations().catch(() => undefined); if (activeId) void loadMessages(activeId).catch(() => undefined) })
    void connection.start().catch(() => undefined)
    return () => { connection.stop().catch(() => undefined); connectionRef.current = null }
  }, [activeId, loadConversations, loadMessages, session.user.id])

  const send = async (content: string, files: File[]) => {
    if (!activeId) return
    const mediaIds = await Promise.all(files.map((file) => messengerApi.upload(file)))
    const message = await messengerApi.send(activeId, content, mediaIds, replyTo?.id)
    setMessages((current) => upsertMessage(current, message)); setReplyTo(null); await loadConversations()
  }
  const sendTyping = () => { const connection = connectionRef.current; if (connection?.state === HubConnectionState.Connected && activeId) void connection.invoke('Typing', activeId).catch(() => undefined); if (typingTimeout.current) window.clearTimeout(typingTimeout.current); typingTimeout.current = window.setTimeout(() => { typingTimeout.current = null }, 800) }
  const createDirect = async (userId: string) => { const conversation = await messengerApi.direct(userId); await loadConversations(); setActiveId(conversation.id) }
  const createGroup = async (title: string, userIds: string[]) => { const conversation = await messengerApi.group(title, userIds); await loadConversations(); setActiveId(conversation.id) }
  const updateGroupTitle = async (title: string) => { if (!active || active.type !== 'group') return; const updated = await messengerApi.updateConversation(active.id, { title }); setConversations((current) => current.map((item) => item.id === updated.id ? updated : item)) }
  const updateGroupPhoto = async (file: File) => {
    if (!active || active.type !== 'group') return
    const photoMediaId = await messengerApi.upload(file)
    const updated = await messengerApi.updateConversation(active.id, { photoMediaId })
    setConversations((current) => current.map((item) => item.id === updated.id ? updated : item))
  }
  const leave = async () => { if (!active) return; await messengerApi.leave(active.id); setConversations((current) => current.filter((item) => item.id !== active.id)); setActiveId(null) }
  const addParticipants = async (userIds: string[]) => { if (!active || userIds.length === 0) return; await messengerApi.addParticipants(active.id, userIds); const updated = await messengerApi.conversation(active.id); setConversations((current) => current.map((item) => item.id === active.id ? updated : item)) }
  const editMessage = async (message: Message, content: string) => { const updated = await messengerApi.edit(message.id, content); setMessages((current) => upsertMessage(current, updated)) }
  const deleteMessage = async (message: Message) => { await messengerApi.unsend(message.id); setMessages((current) => current.map((item) => item.id === message.id ? { ...item, content: null, attachments: [], reactions: [], deletedAtUtc: new Date().toISOString() } : item)) }
  const manageParticipant = async (participant: Participant, action: 'remove' | 'admin' | 'member' | 'owner') => {
    if (!active) return
    if (action === 'remove') await messengerApi.removeParticipant(active.id, participant.userId)
    if (action === 'admin' || action === 'member') await messengerApi.changeRole(active.id, participant.userId, action)
    if (action === 'owner') await messengerApi.transferOwnership(active.id, participant.userId)
    const updated = await messengerApi.conversation(active.id)
    setConversations((current) => current.map((item) => item.id === updated.id ? updated : item))
  }
  const myParticipant = active?.participants.find((participant) => participant.userId === session.user.id)

  return <main className="zola-light-shell">
    <aside className="conversation-pane"><header className="pane-header"><div className="current-user"><Avatar name={currentUserProfile?.displayName ?? 'Tài khoản của bạn'} url={currentUserProfile?.avatarUrl} /><div><strong>{currentUserProfile?.displayName ?? 'Tài khoản của bạn'}</strong></div></div><button className="icon-button" title="Tin nhắn mới" onClick={() => setShowCreate(true)}>✎</button></header>
      <input className="conversation-filter" placeholder="Tìm cuộc trò chuyện" onChange={(event) => { const value = event.target.value.toLowerCase(); document.querySelectorAll<HTMLElement>('[data-conversation]').forEach((node) => { node.hidden = !node.dataset.conversation?.includes(value) }) }} />
      <div className="conversation-list">{conversations.map((conversation) => <button key={conversation.id} data-conversation={displayConversation(conversation, profiles).toLowerCase()} hidden={false} className={`conversation-item ${conversation.id === activeId ? 'selected' : ''}`} onClick={() => setActiveId(conversation.id)}>
        <ConversationAvatar conversation={conversation} profiles={profiles} /><span><b>{displayConversation(conversation, profiles)}</b><small>{messageSummary(conversation.lastMessage)}</small></span>{conversation.unreadCount > 0 && <em>{conversation.unreadCount > 99 ? '99+' : conversation.unreadCount}</em>}</button>)}
      </div>
      {nextConversationCursor && <button className="load-more" onClick={() => void loadConversations(nextConversationCursor)}>Tải thêm</button>}
      <footer><button onClick={() => void onSignOut()}>Đăng xuất</button><a href={import.meta.env.VITE_WEB_URL ?? 'http://localhost:5173'}>Fookbase</a></footer>
    </aside>
    <section className="chat-pane">{error && <p className="alert">{error}</p>}{active ? <>
      <header className="chat-header"><button className="mobile-back" aria-label="Quay lại danh sách" onClick={() => setActiveId(null)}>‹</button><ConversationAvatar conversation={active} profiles={profiles} /><div><strong>{displayConversation(active, profiles)}</strong><small>{typing ? 'đang nhập…' : active.type === 'group' ? `${active.participants.length} thành viên` : onlineIds.has(active.participantUserId ?? '') ? 'Đang hoạt động' : 'Ngoại tuyến'}</small></div><button className="mobile-details" onClick={() => document.body.classList.toggle('details-open')}>ⓘ</button></header>
      <div className="message-list">{hasMoreMessages && <button className="load-more" onClick={() => activeId && void loadMessages(activeId, nextMessageCursor ?? undefined)}>Tải tin cũ hơn</button>}
        {messages.map((message) => <MessageBubble key={message.id} message={message} mine={message.senderUserId === session.user.id} onReply={() => setReplyTo(message)} onReact={(type) => void messengerApi.react(message.id, type).catch((reason) => setError(reason.message))} onEdit={() => setEditingMessage(message)} onDelete={() => setConfirmation({ title: 'Gỡ tin nhắn?', description: 'Tin nhắn sẽ bị gỡ khỏi cuộc trò chuyện.', confirmLabel: 'Gỡ tin nhắn', onConfirm: () => deleteMessage(message) })} />)}
      </div>
      <Composer conversationId={active.id} replyTo={replyTo} onCancelReply={() => setReplyTo(null)} onSend={send} onTyping={sendTyping} />
    </> : <div className="empty-chat"><div>💬</div><h1>Chọn một cuộc trò chuyện</h1><p>Hoặc tạo tin nhắn mới để bắt đầu.</p><button className="primary" onClick={() => setShowCreate(true)}>Tin nhắn mới</button></div>}</section>
    <aside className="details-pane">{active ? <><header><button className="close-details" onClick={() => document.body.classList.remove('details-open')}>×</button><ConversationAvatar conversation={active} profiles={profiles} /><h2>{displayConversation(active, profiles)}</h2>{active.type === 'group' && <><input ref={groupPhotoInputRef} className="sr-only" type="file" accept="image/jpeg,image/png,image/webp" onChange={(event) => { const file = event.target.files?.[0]; event.currentTarget.value = ''; if (file) void updateGroupPhoto(file).catch((reason) => setError(reason.message)) }} /><button onClick={() => setShowRenameGroup(true)}>Đổi tên</button><button onClick={() => groupPhotoInputRef.current?.click()}>Đổi ảnh</button></>}</header>
      <section><h3>Thành viên ({active.participants.length})</h3>{active.participants.map((participant) => <ParticipantRow key={participant.userId} participant={participant} profile={profiles.get(participant.userId)} currentUserId={session.user.id} isOnline={onlineIds.has(participant.userId)} canManage={myParticipant?.role === 'owner' || myParticipant?.role === 'admin'} onManage={(action) => {
        if (action === 'remove') setConfirmation({ title: 'Xóa thành viên?', description: `Xóa ${profiles.get(participant.userId)?.displayName ?? 'thành viên'} khỏi nhóm này.`, confirmLabel: 'Xóa thành viên', onConfirm: () => manageParticipant(participant, action) })
        else if (action === 'owner') setConfirmation({ title: 'Chuyển quyền sở hữu?', description: `Bạn sẽ chuyển quyền sở hữu nhóm cho ${profiles.get(participant.userId)?.displayName ?? 'thành viên này'}.`, confirmLabel: 'Chuyển quyền', onConfirm: () => manageParticipant(participant, action) })
        else void manageParticipant(participant, action).catch((reason) => setError(reason instanceof ApiError ? reason.message : 'Không thể cập nhật thành viên.'))
      }} />)}
      {active.type === 'group' && (myParticipant?.role === 'owner' || myParticipant?.role === 'admin') && <button className="secondary" onClick={() => setShowAddParticipants(true)}>Thêm thành viên</button>}</section>
      <section><h3>Cài đặt</h3><button className="secondary" onClick={() => void messengerApi.updateConversation(active.id, { archived: !active.isArchived }).then(() => loadConversations())}>{active.isArchived ? 'Bỏ lưu trữ' : 'Lưu trữ'}</button><button className="secondary" onClick={() => void messengerApi.updateConversation(active.id, { mutedUntilUtc: new Date(Date.now() + 8 * 60 * 60 * 1000).toISOString() }).then(() => loadConversations())}>Tắt thông báo 8 giờ</button>{active.type === 'group' && myParticipant?.role !== 'owner' && <button className="danger" onClick={() => setConfirmation({ title: 'Rời nhóm?', description: 'Bạn sẽ không còn nhận được tin nhắn từ nhóm này.', confirmLabel: 'Rời nhóm', onConfirm: leave })}>Rời nhóm</button>}</section>
    </> : <p className="details-placeholder">Thông tin cuộc trò chuyện sẽ hiển thị ở đây.</p>}</aside>
    {showCreate && <CreateConversation onClose={() => setShowCreate(false)} onDirect={createDirect} onGroup={createGroup} />}
    {showAddParticipants && active && <AddParticipantsDialog conversation={active} onClose={() => setShowAddParticipants(false)} onAdd={addParticipants} />}
    {showRenameGroup && active?.type === 'group' && <RenameGroupDialog currentTitle={active.title ?? ''} onClose={() => setShowRenameGroup(false)} onSave={updateGroupTitle} />}
    {editingMessage && <EditMessageDialog message={editingMessage} onClose={() => setEditingMessage(null)} onSave={(content) => editMessage(editingMessage, content)} />}
    {confirmation && <ConfirmDialog {...confirmation} onClose={() => setConfirmation(null)} onConfirm={async () => { try { await confirmation.onConfirm() } catch (reason) { setError(reason instanceof ApiError ? reason.message : 'Không thể thực hiện thao tác.'); throw reason } }} />}
  </main>
}

function ParticipantRow({ participant, profile, currentUserId, isOnline, canManage, onManage }: { participant: Participant; profile?: UserProfile; currentUserId: string; isOnline: boolean; canManage: boolean; onManage: (action: 'remove' | 'admin' | 'member' | 'owner') => void }) {
  const name = participant.userId === currentUserId ? 'Bạn' : participant.nickname ?? profile?.displayName ?? 'Thành viên'
  const avatarUrl = profile?.avatarUrl
  const role = participant.role === 'owner' ? 'Chủ nhóm' : participant.role === 'admin' ? 'Quản trị viên' : 'Thành viên'
  return <div className="participant"><span className={`presence ${isOnline ? 'online' : ''}`} /><Avatar name={name} url={avatarUrl} /><div><b>{name}</b><small>{role}{participant.lastReadAtUtc ? ` · đã xem ${formatTime(participant.lastReadAtUtc)}` : ''}</small></div>
    {canManage && participant.userId !== currentUserId && participant.role !== 'owner' && <select aria-label={`Quản lý ${name}`} defaultValue="" onChange={(event) => { const action = event.target.value as 'remove' | 'admin' | 'member' | 'owner'; if (action) onManage(action); event.currentTarget.value = '' }}><option value="">⋯</option><option value="admin">Đặt quản trị viên</option><option value="member">Đặt thành viên</option><option value="owner">Chuyển quyền sở hữu</option><option value="remove">Xóa</option></select>}
  </div>
}

export function App() {
  const [session, setSession] = useState<AuthSession | null>(() => getSession())
  const signOut = async () => { try { if (session) await authApi.logout(session.refreshToken) } finally { clearSession(); setSession(null) } }
  return session ? <AppShell session={session} onSignOut={signOut} /> : <Login onSession={setSession} />
}
