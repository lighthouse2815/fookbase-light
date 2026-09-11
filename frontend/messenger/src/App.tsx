import { HubConnectionBuilder, HubConnectionState, LogLevel } from '@microsoft/signalr'
import { useCallback, useEffect, useRef, useState } from 'react'
import {
  ApiError,
  apiBaseUrl,
  authApi,
  clearSession,
  getSession,
  messengerApi,
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
    ? profiles?.get(conversation.participantUserId)?.displayName ?? `@${conversation.participantUserId.slice(0, 8)}`
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

function Login({ onSession }: { onSession: (session: AuthSession) => void }) {
  const [email, setEmail] = useState('')
  const [password, setPassword] = useState('')
  const [error, setError] = useState<string | null>(null)
  const [busy, setBusy] = useState(false)
  const submit = async (event: React.FormEvent) => {
    event.preventDefault()
    setBusy(true)
    setError(null)
    try {
      const session = await authApi.login(email, password)
      saveSession(session)
      onSession(session)
    } catch (reason) {
      setError(reason instanceof ApiError ? reason.message : 'Không thể đăng nhập.')
    } finally {
      setBusy(false)
    }
  }
  return <main className="login-shell"><form className="login-card" onSubmit={submit}>
    <div className="brand-mark">f</div><h1>Fookbase Messenger</h1><p>Đăng nhập bằng tài khoản Fookbase của bạn.</p>
    {error && <p className="alert">{error}</p>}
    <label>Email<input autoComplete="email" type="email" value={email} onChange={(event) => setEmail(event.target.value)} required /></label>
    <label>Mật khẩu<input autoComplete="current-password" type="password" value={password} onChange={(event) => setPassword(event.target.value)} required /></label>
    <button className="primary" disabled={busy}>{busy ? 'Đang đăng nhập…' : 'Đăng nhập'}</button>
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
  return <div className="modal-backdrop" role="presentation"><section className="modal" role="dialog" aria-modal="true"><header><h2>Tạo cuộc trò chuyện</h2><button onClick={onClose}>×</button></header>
    <div className="tabs"><button className={mode === 'direct' ? 'active' : ''} onClick={() => setMode('direct')}>Tin nhắn mới</button><button className={mode === 'group' ? 'active' : ''} onClick={() => setMode('group')}>Nhóm mới</button></div>
    {mode === 'group' && <label>Tên nhóm<input value={title} onChange={(event) => setTitle(event.target.value)} maxLength={120} /></label>}
    <label>Tìm người dùng<input autoFocus value={search} onChange={(event) => setSearch(event.target.value)} placeholder="Tên hoặc username" /></label>
    {selected.length > 0 && <div className="selected-users">{selected.map((user) => <button key={user.userId} onClick={() => setSelected((current) => current.filter((item) => item.userId !== user.userId))}>{user.displayName} ×</button>)}</div>}
    <div className="search-results">{users.map((user) => <button key={user.userId} disabled={busy} onClick={() => void select(user)}><Avatar name={user.displayName} url={user.avatarUrl} /><span>{user.displayName}<small>@{user.username}</small></span></button>)}</div>
    {mode === 'group' && <button className="primary" disabled={busy || !title.trim() || selected.length === 0} onClick={() => void create()}>Tạo nhóm ({selected.length + 1})</button>}
  </section></div>
}

function Avatar({ name, url }: { name: string; url?: string | null }) {
  return url ? <img className="avatar" src={url} alt="" /> : <span className="avatar">{name.slice(0, 2).toUpperCase()}</span>
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
  const avatarUrl = directProfile?.avatarUrl?.startsWith('/') ? `${apiBaseUrl}${directProfile.avatarUrl}` : directProfile?.avatarUrl
  return <Avatar name={displayConversation(conversation, profiles)} url={url ?? avatarUrl} />
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
  const [profileLookups, setProfileLookups] = useState<ReadonlySet<string>>(new Set())
  const [error, setError] = useState<string | null>(null)
  const [showCreate, setShowCreate] = useState(false)
  const connectionRef = useRef<ReturnType<HubConnectionBuilder['build']> | null>(null)
  const typingTimeout = useRef<number | null>(null)
  const groupPhotoInputRef = useRef<HTMLInputElement>(null)
  const active = conversations.find((conversation) => conversation.id === activeId) ?? null

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
    const unknownIds = conversations.filter((conversation) => conversation.type === 'direct' && conversation.participantUserId && !profiles.has(conversation.participantUserId) && !profileLookups.has(conversation.participantUserId)).map((conversation) => conversation.participantUserId!)
    if (unknownIds.length === 0) return
    setProfileLookups((current) => new Set([...current, ...unknownIds]))
    let isCurrent = true
    void Promise.all(unknownIds.map((userId) => messengerApi.user(userId).catch(() => null))).then((users) => {
      if (!isCurrent) return
      setProfiles((current) => {
        const next = new Map(current)
        users.forEach((user) => { if (user) next.set(user.userId, user) })
        return next
      })
    })
    return () => { isCurrent = false }
  }, [conversations, profiles, profileLookups])
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
  const updateGroupTitle = async () => { if (!active || active.type !== 'group') return; const title = window.prompt('Tên nhóm', active.title ?? ''); if (title?.trim()) { const updated = await messengerApi.updateConversation(active.id, { title: title.trim() }); setConversations((current) => current.map((item) => item.id === updated.id ? updated : item)) } }
  const updateGroupPhoto = async (file: File) => {
    if (!active || active.type !== 'group') return
    const photoMediaId = await messengerApi.upload(file)
    const updated = await messengerApi.updateConversation(active.id, { photoMediaId })
    setConversations((current) => current.map((item) => item.id === updated.id ? updated : item))
  }
  const leave = async () => { if (!active || !window.confirm('Rời khỏi cuộc trò chuyện này?')) return; await messengerApi.leave(active.id); setConversations((current) => current.filter((item) => item.id !== active.id)); setActiveId(null) }
  const addParticipants = async () => { if (!active) return; const value = window.prompt('Nhập các UUID, cách nhau bởi dấu phẩy'); const ids = value?.split(',').map((id) => id.trim()).filter(Boolean) ?? []; if (ids.length) { await messengerApi.addParticipants(active.id, ids); const updated = await messengerApi.conversation(active.id); setConversations((current) => current.map((item) => item.id === active.id ? updated : item)) } }
  const myParticipant = active?.participants.find((participant) => participant.userId === session.user.id)

  return <main className="messenger-shell">
    <aside className="conversation-pane"><header className="pane-header"><div><strong>Messenger</strong><small>@{session.user.username}</small></div><button className="icon-button" title="Tin nhắn mới" onClick={() => setShowCreate(true)}>✎</button></header>
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
        {messages.map((message) => <MessageBubble key={message.id} message={message} mine={message.senderUserId === session.user.id} onReply={() => setReplyTo(message)} onReact={(type) => void messengerApi.react(message.id, type).catch((reason) => setError(reason.message))} onEdit={() => { const content = window.prompt('Chỉnh sửa tin nhắn', message.content ?? ''); if (content?.trim()) void messengerApi.edit(message.id, content.trim()).then((updated) => setMessages((current) => upsertMessage(current, updated))).catch((reason) => setError(reason.message)) }} onDelete={() => { if (window.confirm('Gỡ tin nhắn này?')) void messengerApi.unsend(message.id).then(() => setMessages((current) => current.map((item) => item.id === message.id ? { ...item, content: null, attachments: [], reactions: [], deletedAtUtc: new Date().toISOString() } : item))).catch((reason) => setError(reason.message)) }} />)}
      </div>
      <Composer conversationId={active.id} replyTo={replyTo} onCancelReply={() => setReplyTo(null)} onSend={send} onTyping={sendTyping} />
    </> : <div className="empty-chat"><div>💬</div><h1>Chọn một cuộc trò chuyện</h1><p>Hoặc tạo tin nhắn mới để bắt đầu.</p><button className="primary" onClick={() => setShowCreate(true)}>Tin nhắn mới</button></div>}</section>
    <aside className="details-pane">{active ? <><header><button className="close-details" onClick={() => document.body.classList.remove('details-open')}>×</button><ConversationAvatar conversation={active} profiles={profiles} /><h2>{displayConversation(active, profiles)}</h2>{active.type === 'group' && <><input ref={groupPhotoInputRef} className="sr-only" type="file" accept="image/jpeg,image/png,image/webp" onChange={(event) => { const file = event.target.files?.[0]; event.currentTarget.value = ''; if (file) void updateGroupPhoto(file).catch((reason) => setError(reason.message)) }} /><button onClick={() => void updateGroupTitle()}>Đổi tên</button><button onClick={() => groupPhotoInputRef.current?.click()}>Đổi ảnh</button></>}</header>
      <section><h3>Thành viên ({active.participants.length})</h3>{active.participants.map((participant) => <ParticipantRow key={participant.userId} participant={participant} currentUserId={session.user.id} isOnline={onlineIds.has(participant.userId)} canManage={myParticipant?.role === 'owner' || myParticipant?.role === 'admin'} conversation={active} onRefresh={async () => { const updated = await messengerApi.conversation(active.id); setConversations((current) => current.map((item) => item.id === updated.id ? updated : item)) }} />)}
      {active.type === 'group' && (myParticipant?.role === 'owner' || myParticipant?.role === 'admin') && <button className="secondary" onClick={() => void addParticipants()}>Thêm thành viên</button>}</section>
      <section><h3>Cài đặt</h3><button className="secondary" onClick={() => void messengerApi.updateConversation(active.id, { archived: !active.isArchived }).then(() => loadConversations())}>{active.isArchived ? 'Bỏ lưu trữ' : 'Lưu trữ'}</button><button className="secondary" onClick={() => void messengerApi.updateConversation(active.id, { mutedUntilUtc: new Date(Date.now() + 8 * 60 * 60 * 1000).toISOString() }).then(() => loadConversations())}>Tắt thông báo 8 giờ</button>{active.type === 'group' && myParticipant?.role !== 'owner' && <button className="danger" onClick={() => void leave()}>Rời nhóm</button>}</section>
    </> : <p className="details-placeholder">Thông tin cuộc trò chuyện sẽ hiển thị ở đây.</p>}</aside>
    {showCreate && <CreateConversation onClose={() => setShowCreate(false)} onDirect={createDirect} onGroup={createGroup} />}
  </main>
}

function ParticipantRow({ participant, currentUserId, isOnline, canManage, conversation, onRefresh }: { participant: Participant; currentUserId: string; isOnline: boolean; canManage: boolean; conversation: Conversation; onRefresh: () => Promise<void> }) {
  const manage = async (action: 'remove' | 'admin' | 'member' | 'owner') => {
    if (action === 'remove') await messengerApi.removeParticipant(conversation.id, participant.userId)
    if (action === 'admin' || action === 'member') await messengerApi.changeRole(conversation.id, participant.userId, action)
    if (action === 'owner') await messengerApi.transferOwnership(conversation.id, participant.userId)
    await onRefresh()
  }
  return <div className="participant"><span className={`presence ${isOnline ? 'online' : ''}`} /><Avatar name={participant.nickname ?? participant.userId.slice(0, 8)} /><div><b>{participant.userId === currentUserId ? 'Bạn' : participant.nickname ?? participant.userId.slice(0, 8)}</b><small>{participant.role}{participant.lastReadAtUtc ? ` · đã xem ${formatTime(participant.lastReadAtUtc)}` : ''}</small></div>
    {canManage && participant.userId !== currentUserId && participant.role !== 'owner' && <select aria-label="Quản lý thành viên" defaultValue="" onChange={(event) => { const action = event.target.value as 'remove' | 'admin' | 'member' | 'owner'; if (action) void manage(action); event.currentTarget.value = '' }}><option value="">⋯</option><option value="admin">Đặt quản trị viên</option><option value="member">Đặt thành viên</option><option value="owner">Chuyển quyền sở hữu</option><option value="remove">Xóa</option></select>}
  </div>
}

export function App() {
  const [session, setSession] = useState<AuthSession | null>(() => getSession())
  const signOut = async () => { try { if (session) await authApi.logout(session.refreshToken) } finally { clearSession(); setSession(null) } }
  return session ? <AppShell session={session} onSignOut={signOut} /> : <Login onSession={setSession} />
}
