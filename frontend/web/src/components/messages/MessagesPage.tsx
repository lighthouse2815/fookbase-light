import { useState, useRef, useEffect } from 'react'
import { CONVERSATIONS, CURRENT_USER, getUserById, formatTimestamp } from '../../data/mockData'
import type { Message, Conversation } from '../../data/mockData'

let nextMsgId = 200

export default function MessagesPage() {
  const [conversations, setConversations] = useState(CONVERSATIONS)
  const [activeConvId, setActiveConvId]   = useState<string>(CONVERSATIONS[0].id)
  const [draft, setDraft]                 = useState('')
  const [isTyping, setIsTyping]           = useState(false)
  const messagesEndRef = useRef<HTMLDivElement>(null)
  const typingTimeout  = useRef<ReturnType<typeof setTimeout> | null>(null)

  const activeConv = conversations.find((c) => c.id === activeConvId)!
  const partner    = getUserById(activeConv.participantId)!

  useEffect(() => {
    messagesEndRef.current?.scrollIntoView({ behavior: 'smooth' })
  }, [activeConv.messages])

  const simulateReply = (convId: string) => {
    setIsTyping(true)
    setTimeout(() => {
      setIsTyping(false)
      const replies = [
        'Got it! 👍',
        'Sounds good, thanks!',
        'Interesting, tell me more.',
        'On it, will check shortly.',
        'Haha nice one! 😄',
        'Yeah totally agree.',
        'Let me look into that.',
      ]
      const content = replies[Math.floor(Math.random() * replies.length)]
      const newMsg: Message = {
        id: `msg-${nextMsgId++}`,
        senderId: activeConv.participantId,
        content,
        timestamp: new Date(),
        isRead: true,
      }
      setConversations((prev) =>
        prev.map((c) =>
          c.id === convId
            ? { ...c, messages: [...c.messages, newMsg], lastMessageTime: new Date() }
            : c
        )
      )
    }, 1200 + Math.random() * 800)
  }

  const handleSend = () => {
    if (!draft.trim()) return
    const newMsg: Message = {
      id: `msg-${nextMsgId++}`,
      senderId: CURRENT_USER.id,
      content: draft.trim(),
      timestamp: new Date(),
      isRead: true,
    }
    setConversations((prev) =>
      prev.map((c) =>
        c.id === activeConvId
          ? { ...c, messages: [...c.messages, newMsg], lastMessageTime: new Date() }
          : c
      )
    )
    setDraft('')
    simulateReply(activeConvId)
  }

  const handleKeyDown = (e: React.KeyboardEvent<HTMLTextAreaElement>) => {
    if (typingTimeout.current) clearTimeout(typingTimeout.current)
    if (e.key === 'Enter' && !e.shiftKey) { e.preventDefault(); handleSend() }
  }

  const unreadCount = (conv: Conversation) =>
    conv.messages.filter((m) => m.senderId !== CURRENT_USER.id && !m.isRead).length

  return (
    <div className="flex h-[calc(100vh-56px)] bg-bg" style={{ animation: 'fade-in 0.25s ease both' }}>

      {/* ── Conversation List ─────────────────────────────── */}
      <aside className="w-[280px] shrink-0 border-r border-border bg-surface flex flex-col h-full max-sm:w-16">

        {/* Header */}
        <div className="px-4 py-4 border-b border-border flex items-center justify-between">
          <h1 className="font-heading font-bold text-[17px] text-text max-sm:hidden">Messages</h1>
          <button
            type="button"
            className="w-8 h-8 rounded-full bg-surface-2 flex items-center justify-center
                       text-text-muted hover:bg-surface-3 transition-colors cursor-pointer border-none"
            title="New message"
          >
            ✏️
          </button>
        </div>

        {/* Search */}
        <div className="px-3 py-2 border-b border-border max-sm:hidden">
          <input
            type="text"
            placeholder="Search messages..."
            className="w-full bg-surface-2 rounded-full text-[13px] text-text px-3.5 py-2
                       outline-none placeholder:text-text-light border border-border
                       focus:input-focus transition-all"
          />
        </div>

        {/* Conversations */}
        <div className="flex-1 scroll-smooth overflow-y-auto">
          {conversations.map((conv) => {
            const p      = getUserById(conv.participantId)!
            const unread = unreadCount(conv)
            const isActive = conv.id === activeConvId
            const lastMsg  = conv.messages[conv.messages.length - 1]

            return (
              <button
                key={conv.id}
                type="button"
                onClick={() => setActiveConvId(conv.id)}
                className={[
                  'w-full text-left flex items-center gap-3 px-4 py-3',
                  'border-b border-border transition-colors cursor-pointer',
                  isActive
                    ? 'bg-surface-2 border-l-2 border-l-primary'
                    : 'bg-transparent border-l-2 border-l-transparent hover:bg-surface-2',
                ].join(' ')}
              >
                <div
                  className={`w-10 h-10 rounded-full flex items-center justify-center text-[11px]
                             font-bold text-white shrink-0
                             ${p.isOnline ? 'avatar-online' : ''} ${p.avatarColor}`}
                >
                  {p.avatar}
                </div>
                <div className="flex-1 min-w-0 max-sm:hidden">
                  <div className="flex items-center justify-between mb-0.5">
                    <span
                      className={`text-[13px] truncate ${
                        unread > 0 ? 'font-semibold text-text' : 'font-medium text-text'
                      }`}
                    >
                      {p.displayName}
                    </span>
                    <span className="text-[11px] text-text-light shrink-0 ml-1">
                      {formatTimestamp(conv.lastMessageTime)}
                    </span>
                  </div>
                  <div className="flex items-center justify-between">
                    <p
                      className={`text-[12px] truncate ${
                        lastMsg?.isEncrypted
                          ? 'italic text-text-light'
                          : unread > 0
                          ? 'text-text font-medium'
                          : 'text-text-muted'
                      }`}
                    >
                      {lastMsg?.isEncrypted ? '🔒 Encrypted message' : lastMsg?.content}
                    </p>
                    {unread > 0 && (
                      <span
                        className="ml-1 min-w-[18px] h-[18px] rounded-full bg-[#e41e3f]
                                   text-[10px] font-bold text-white flex items-center justify-center px-1.5 shrink-0"
                      >
                        {unread}
                      </span>
                    )}
                  </div>
                </div>
              </button>
            )
          })}
        </div>
      </aside>

      {/* ── Chat Window ───────────────────────────────────── */}
      <main className="flex-1 flex flex-col h-full overflow-hidden bg-bg">

        {/* Chat header */}
        <div className="px-5 py-4 border-b border-border bg-surface flex items-center gap-3">
          <div
            className={`w-10 h-10 rounded-full flex items-center justify-center text-[11px]
                       font-bold text-white shrink-0
                       ${partner.isOnline ? 'avatar-online' : ''} ${partner.avatarColor}`}
          >
            {partner.avatar}
          </div>
          <div>
            <div className="text-[14px] font-semibold text-text">{partner.displayName}</div>
            <div className={`text-[12px] ${partner.isOnline ? 'text-[#31a24c] font-medium' : 'text-text-light'}`}>
              {partner.isOnline ? '● Active now' : '○ Offline'}
            </div>
          </div>
          <div className="ml-auto flex items-center gap-2">
            {['📞', '📹', 'ℹ️'].map((icon) => (
              <button
                key={icon}
                type="button"
                className="w-9 h-9 rounded-full bg-surface-2 flex items-center justify-center
                           hover:bg-surface-3 transition-colors cursor-pointer border-none text-base text-text"
              >
                {icon}
              </button>
            ))}
          </div>
        </div>

        {/* Messages */}
        <div className="flex-1 scroll-smooth overflow-y-auto p-5 flex flex-col gap-3">
          {activeConv.messages.map((msg, i) => {
            const isMine = msg.senderId === CURRENT_USER.id
            return (
              <div
                key={msg.id}
                className={`flex gap-2 ${isMine ? 'flex-row-reverse' : 'flex-row'}`}
                style={{ animation: `scale-in 0.2s ease ${i * 0.015}s both` }}
              >
                {!isMine && (
                  <div
                    className={`w-8 h-8 rounded-full flex items-center justify-center text-[10px]
                               font-bold text-white shrink-0 mt-1 ${partner.avatarColor}`}
                  >
                    {partner.avatar}
                  </div>
                )}

                <div className={`max-w-[65%] flex flex-col gap-1 ${isMine ? 'items-end' : 'items-start'}`}>
                  <div
                    className={[
                      'px-4 py-2.5 text-[14px] leading-relaxed',
                      isMine ? 'bubble-mine' : 'bubble-theirs',
                      msg.isEncrypted ? 'opacity-75 italic' : '',
                    ].join(' ')}
                  >
                    {msg.isEncrypted && (
                      <span className="text-[11px] block mb-1 not-italic font-medium opacity-80">
                        🔒 Encrypted
                      </span>
                    )}
                    {msg.content}
                  </div>
                  <span className="text-[11px] text-text-light px-1">
                    {formatTimestamp(msg.timestamp)}
                  </span>
                </div>
              </div>
            )
          })}

          {/* Typing indicator */}
          {isTyping && (
            <div className="flex items-end gap-2">
              <div
                className={`w-8 h-8 rounded-full flex items-center justify-center text-[10px]
                           font-bold text-white shrink-0 ${partner.avatarColor}`}
              >
                {partner.avatar}
              </div>
              <div className="bubble-theirs px-4 py-3 flex items-center gap-1">
                {[0, 1, 2].map((dot) => (
                  <span
                    key={dot}
                    className="w-2 h-2 rounded-full bg-text-muted inline-block"
                    style={{ animation: `pulse-dot 1.2s ease ${dot * 0.2}s infinite` }}
                  />
                ))}
              </div>
            </div>
          )}
          <div ref={messagesEndRef} />
        </div>

        {/* Input bar */}
        <div className="px-4 py-3 border-t border-border bg-surface">
          <div className="flex items-center gap-3 bg-surface-2 rounded-full px-4 py-2 border border-border focus-within:border-border-focus transition-colors">
            <button
              type="button"
              className="text-primary text-xl cursor-pointer bg-transparent border-none shrink-0 hover:opacity-80 transition-opacity"
              title="Add emoji"
            >
              😊
            </button>
            <textarea
              rows={1}
              value={draft}
              onChange={(e) => setDraft(e.target.value)}
              onKeyDown={handleKeyDown}
              placeholder="Message..."
              className="flex-1 bg-transparent border-none text-[14px] text-text outline-none
                         resize-none placeholder:text-text-light py-1 leading-normal"
              style={{ maxHeight: 120 }}
            />
            {draft.trim() ? (
              <button
                type="button"
                onClick={handleSend}
                className="w-8 h-8 rounded-full bg-primary flex items-center justify-center
                           text-white cursor-pointer border-none shrink-0
                           hover:bg-primary-dark transition-all"
                title="Send message"
              >
                ▶
              </button>
            ) : (
              <div className="flex items-center gap-1 shrink-0">
                {['🖼️', '🎙️', '❤️'].map((icon) => (
                  <button
                    key={icon}
                    type="button"
                    className="w-8 h-8 rounded-full flex items-center justify-center text-text-muted
                               hover:bg-surface-3 transition-colors cursor-pointer border-none text-base"
                  >
                    {icon}
                  </button>
                ))}
              </div>
            )}
          </div>
        </div>
      </main>
    </div>
  )
}
