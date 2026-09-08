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

  // Auto-scroll to bottom on new message
  useEffect(() => {
    messagesEndRef.current?.scrollIntoView({ behavior: 'smooth' })
  }, [activeConv.messages])

  // Simulate partner typing then replying
  const simulateReply = (convId: string) => {
    setIsTyping(true)
    setTimeout(() => {
      setIsTyping(false)
      const replies = [
        'ack. processing...',
        'copy that. standby.',
        '[ENCRYPTED_MSG:a4f2...] — key exchange pending',
        'interesting. tell me more.',
        'understood. initiating countermeasures.',
        'nice find. patching my exploit db.',
        'that\'s a clean PoC. respect.',
      ]
      const content = replies[Math.floor(Math.random() * replies.length)]
      const newMsg: Message = {
        id: `msg-${nextMsgId++}`,
        senderId: activeConv.participantId,
        content,
        timestamp: new Date(),
        isEncrypted: content.startsWith('[ENCRYPTED'),
        isRead: true,
      }
      setConversations((prev) =>
        prev.map((c) =>
          c.id === convId
            ? { ...c, messages: [...c.messages, newMsg], lastMessageTime: new Date() }
            : c
        )
      )
    }, 1500 + Math.random() * 1000)
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
    if (e.key === 'Enter' && !e.shiftKey) {
      e.preventDefault()
      handleSend()
    }
  }

  const unreadCount = (conv: Conversation) =>
    conv.messages.filter((m) => m.senderId !== CURRENT_USER.id && !m.isRead).length

  return (
    <div className="flex h-screen" style={{ animation: 'fade-in 0.3s ease both' }}>

      {/* ── Conversation List ──────────────────────────────── */}
      <aside className="w-[260px] shrink-0 border-r border-[rgba(0,255,255,0.12)]
                        flex flex-col h-full max-sm:w-[60px]">

        {/* Header */}
        <div className="px-4 py-3 border-b border-[rgba(0,255,255,0.12)]
                        bg-[rgba(10,14,20,0.9)] backdrop-blur-md flex items-center gap-2">
          <span className="font-mono text-[10px] text-cyber-cyan tracking-widest">◎</span>
          <h1 className="font-mono text-[12px] text-text-bright tracking-widest max-sm:hidden">
            SECURE_COMMS
          </h1>
        </div>

        {/* Conversations */}
        <div className="flex-1 scroll-cyber overflow-y-auto">
          {conversations.map((conv) => {
            const p     = getUserById(conv.participantId)!
            const unread = unreadCount(conv)
            const isActive = conv.id === activeConvId
            const lastMsg = conv.messages[conv.messages.length - 1]

            return (
              <button
                key={conv.id}
                type="button"
                onClick={() => setActiveConvId(conv.id)}
                className={[
                  'w-full text-left flex items-center gap-3 px-3 py-3',
                  'border-b border-[rgba(0,255,255,0.07)] transition-all duration-200',
                  'cursor-pointer bg-transparent',
                  isActive
                    ? 'bg-[rgba(0,255,255,0.06)] border-l-2 border-l-cyber-cyan'
                    : 'hover:bg-[rgba(0,255,255,0.03)]',
                ].join(' ')}
              >
                {/* Avatar */}
                <div
                  className={`w-9 h-9 rounded-[2px] flex items-center justify-center font-mono
                              text-[10px] text-cyber-cyan border border-[rgba(0,255,255,0.3)] shrink-0
                              ${p.isOnline ? 'avatar-online' : ''}`}
                  style={{ background: p.avatarColor }}
                >
                  {p.avatar}
                </div>

                {/* Info */}
                <div className="flex-1 min-w-0 max-sm:hidden">
                  <div className="flex items-center justify-between mb-0.5">
                    <span className="font-mono text-[11px] text-text-bright truncate">
                      {p.displayName}
                    </span>
                    <span className="font-mono text-[9px] text-text-dim shrink-0 ml-1">
                      {formatTimestamp(conv.lastMessageTime)}
                    </span>
                  </div>
                  <div className="flex items-center justify-between">
                    <p className={`font-mono text-[10px] truncate ${
                      lastMsg?.isEncrypted ? 'text-cyber-danger opacity-60 italic' : 'text-text-dim'
                    }`}>
                      {lastMsg?.isEncrypted ? '[encrypted]' : lastMsg?.content}
                    </p>
                    {unread > 0 && (
                      <span className="ml-1 w-4 h-4 rounded-[2px] bg-cyber-danger font-mono text-[9px]
                                       text-white flex items-center justify-center shrink-0">
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

      {/* ── Chat Window ────────────────────────────────────── */}
      <main className="flex-1 flex flex-col h-full overflow-hidden">

        {/* Chat header */}
        <div className="px-4 py-3 border-b border-[rgba(0,255,255,0.12)]
                        bg-[rgba(10,14,20,0.9)] backdrop-blur-md flex items-center gap-3">
          <div
            className={`w-8 h-8 rounded-[2px] flex items-center justify-center font-mono text-[10px]
                        text-cyber-cyan border border-[rgba(0,255,255,0.3)]
                        ${partner.isOnline ? 'avatar-online' : ''}`}
            style={{ background: partner.avatarColor }}
          >
            {partner.avatar}
          </div>
          <div>
            <div className="font-mono text-[12px] text-text-bright">{partner.displayName}</div>
            <div className={`font-mono text-[9px] ${partner.isOnline ? 'text-cyber-green neon-green' : 'text-text-dim'}`}>
              {partner.isOnline ? '● ONLINE' : '○ OFFLINE'}
            </div>
          </div>
          <div className="ml-auto flex items-center gap-2">
            <span className="font-mono text-[9px] text-text-dim tracking-widest">E2E_ENCRYPTED ✓</span>
            <span className="w-1.5 h-1.5 rounded-full bg-cyber-green shadow-[0_0_6px_rgba(0,255,65,0.5)]" />
          </div>
        </div>

        {/* Messages */}
        <div className="flex-1 scroll-cyber overflow-y-auto p-4 flex flex-col gap-3">
          {activeConv.messages.map((msg, i) => {
            const isMine = msg.senderId === CURRENT_USER.id
            return (
              <div
                key={msg.id}
                className={`flex gap-2 ${isMine ? 'flex-row-reverse' : 'flex-row'}`}
                style={{ animation: `fade-in 0.2s ease ${i * 0.02}s both` }}
              >
                {/* Avatar */}
                {!isMine && (
                  <div
                    className="w-7 h-7 rounded-[2px] flex items-center justify-center font-mono
                               text-[9px] text-cyber-cyan border border-[rgba(0,255,255,0.3)] shrink-0 mt-1"
                    style={{ background: partner.avatarColor }}
                  >
                    {partner.avatar}
                  </div>
                )}

                {/* Bubble */}
                <div className={`max-w-[65%] ${isMine ? 'items-end' : 'items-start'} flex flex-col gap-1`}>
                  <div className={[
                    'px-3 py-2 rounded-[3px] font-mono text-[12px] leading-relaxed',
                    isMine
                      ? 'bg-[rgba(0,255,255,0.08)] border border-[rgba(0,255,255,0.25)] text-text-bright'
                      : 'bg-bg-card border border-[rgba(0,255,255,0.12)] text-text-mid',
                    msg.isEncrypted ? 'msg-encrypted' : '',
                  ].join(' ')}>
                    {msg.isEncrypted && (
                      <span className="text-cyber-danger text-[9px] tracking-widest block mb-1">
                        🔒 ENCRYPTED
                      </span>
                    )}
                    {msg.content}
                  </div>
                  <span className="font-mono text-[9px] text-text-dim">
                    {formatTimestamp(msg.timestamp)}
                  </span>
                </div>
              </div>
            )
          })}

          {/* Typing indicator */}
          {isTyping && (
            <div className="flex items-center gap-2">
              <div
                className="w-7 h-7 rounded-[2px] flex items-center justify-center font-mono
                           text-[9px] text-cyber-cyan border border-[rgba(0,255,255,0.3)] shrink-0"
                style={{ background: partner.avatarColor }}
              >
                {partner.avatar}
              </div>
              <div className="px-3 py-2 bg-bg-card border border-[rgba(0,255,255,0.12)] rounded-[3px]">
                <span className="font-mono text-[11px] text-text-dim tracking-widest">
                  encrypting<span style={{ animation: 'blink 1s step-end infinite' }}>...</span>
                </span>
              </div>
            </div>
          )}

          <div ref={messagesEndRef} />
        </div>

        {/* Input */}
        <div className="p-3 border-t border-[rgba(0,255,255,0.12)]
                        bg-[rgba(10,14,20,0.8)] backdrop-blur-md">
          <div className="flex gap-2 items-end">
            <div className="flex-1 relative">
              <span className="absolute left-3 top-3 font-mono text-[11px] text-cyber-cyan">&gt;</span>
              <textarea
                rows={1}
                value={draft}
                onChange={(e) => setDraft(e.target.value)}
                onKeyDown={handleKeyDown}
                placeholder="type encrypted message..."
                className="w-full bg-bg-input border border-[rgba(0,255,255,0.2)] text-text-bright
                           font-mono text-[12px] pl-7 pr-3 py-2.5 rounded-[2px] outline-none resize-none
                           placeholder:text-text-dim transition-all duration-200
                           focus:border-cyber-cyan focus:shadow-[0_0_8px_rgba(0,255,255,0.15)]"
              />
            </div>
            <button
              type="button"
              onClick={handleSend}
              disabled={!draft.trim()}
              className="px-3 py-2.5 font-mono text-[11px] tracking-widest border rounded-[2px]
                         transition-all duration-200 cursor-pointer
                         disabled:opacity-30 disabled:cursor-not-allowed
                         border-[rgba(0,255,255,0.3)] text-cyber-cyan bg-[rgba(0,255,255,0.05)]
                         hover:enabled:border-cyber-cyan hover:enabled:bg-[rgba(0,255,255,0.12)]
                         hover:enabled:shadow-[0_0_12px_rgba(0,255,255,0.2)]"
            >
              ▶
            </button>
          </div>
          <p className="font-mono text-[9px] text-text-dim mt-1.5 tracking-wide">
            // enter to send · shift+enter for newline · end-to-end encrypted
          </p>
        </div>
      </main>
    </div>
  )
}
