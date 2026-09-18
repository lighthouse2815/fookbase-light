import { useEffect, useRef, useState } from 'react'
import { aiApi, type AiChatMessage } from '../../api/ai'
import { ApiError } from '../../api/client'

const suggestions = [
  'Giúp mình lên kế hoạch cho ngày hôm nay.',
  'Viết một caption ngắn cho bài đăng du lịch.',
  'Giải thích một chủ đề theo cách dễ hiểu.',
]

export default function AiChatPage() {
  const [messages, setMessages] = useState<AiChatMessage[]>([])
  const [draft, setDraft] = useState('')
  const [isSending, setIsSending] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const bottomRef = useRef<HTMLDivElement>(null)

  useEffect(() => {
    bottomRef.current?.scrollIntoView({ behavior: 'smooth', block: 'end' })
  }, [messages, isSending])

  const sendMessage = async (value = draft) => {
    const message = value.trim()
    if (!message || isSending) return

    setDraft('')
    setError(null)
    setIsSending(true)
    setMessages((current) => [...current, { role: 'user', content: message }])

    try {
      const response = await aiApi.chat(message, messages.slice(-10))
      setMessages((current) => [...current, { role: 'assistant', content: response.content }])
    } catch (cause) {
      setError(cause instanceof ApiError ? cause.message : 'Không thể gửi tin nhắn đến trợ lý AI.')
    } finally {
      setIsSending(false)
    }
  }

  return (
    <main className="min-h-screen bg-bg p-4 xl:p-6" style={{ animation: 'fade-in 0.25s ease both' }}>
      <div className="mx-auto flex min-h-[calc(100vh-7rem)] max-w-4xl flex-col overflow-hidden rounded-3xl border border-border bg-surface card-shadow">
        <header className="flex flex-wrap items-center justify-between gap-3 border-b border-border px-5 py-4 sm:px-7">
          <div className="flex items-center gap-3">
            <span className="grid h-11 w-11 place-items-center rounded-2xl bg-primary text-xl text-white" aria-hidden="true">✦</span>
            <div>
              <p className="font-heading text-xl font-bold text-text">Trợ lý AI</p>
              <p className="text-sm text-text-muted">Hỏi bất cứ điều gì, bằng tiếng Việt hoặc ngôn ngữ bạn muốn.</p>
            </div>
          </div>
          <button
            type="button"
            onClick={() => { setMessages([]); setError(null) }}
            disabled={messages.length === 0 || isSending}
            className="rounded-lg border border-border bg-surface-2 px-3 py-2 text-sm font-semibold text-text transition hover:bg-surface-hover disabled:opacity-50"
          >
            Cuộc trò chuyện mới
          </button>
        </header>

        <section className="flex-1 space-y-4 overflow-y-auto px-5 py-6 sm:px-7" aria-live="polite">
          {messages.length === 0 && <div className="mx-auto flex max-w-xl flex-col items-center py-12 text-center">
            <span className="mb-4 grid h-16 w-16 place-items-center rounded-3xl bg-primary/15 text-3xl text-primary" aria-hidden="true">✦</span>
            <h1 className="font-heading text-2xl font-bold text-text">Bạn cần hỗ trợ gì?</h1>
            <p className="mt-2 text-text-muted">Trợ lý AI không lưu nội dung trò chuyện tại dịch vụ AI.</p>
            <div className="mt-6 flex flex-wrap justify-center gap-2">
              {suggestions.map((suggestion) => <button
                key={suggestion}
                type="button"
                onClick={() => void sendMessage(suggestion)}
                disabled={isSending}
                className="rounded-full border border-border bg-surface-2 px-4 py-2 text-sm text-text transition hover:border-primary hover:bg-primary/10 disabled:opacity-50"
              >
                {suggestion}
              </button>)}
            </div>
          </div>}

          {messages.map((chatMessage, index) => <article key={`${chatMessage.role}-${index}`} className={`flex ${chatMessage.role === 'user' ? 'justify-end' : 'justify-start'}`}>
            <div className={`max-w-[85%] whitespace-pre-wrap rounded-2xl px-4 py-3 leading-6 ${chatMessage.role === 'user' ? 'rounded-br-md bg-primary text-white' : 'rounded-bl-md bg-surface-2 text-text'}`}>
              {chatMessage.content}
            </div>
          </article>)}

          {isSending && <div className="flex justify-start"><div className="rounded-2xl rounded-bl-md bg-surface-2 px-4 py-3 text-sm text-text-muted">Trợ lý AI đang trả lời…</div></div>}
          {error && <p role="alert" className="rounded-xl border border-danger/40 bg-danger/10 px-4 py-3 text-sm text-danger">{error}</p>}
          <div ref={bottomRef} />
        </section>

        <form
          className="border-t border-border p-4 sm:px-7"
          onSubmit={(event) => { event.preventDefault(); void sendMessage() }}
        >
          <label className="sr-only" htmlFor="ai-chat-message">Tin nhắn cho trợ lý AI</label>
          <div className="flex items-end gap-3 rounded-2xl border border-border bg-bg p-2 focus-within:border-primary focus-within:ring-2 focus-within:ring-primary/25">
            <textarea
              id="ai-chat-message"
              value={draft}
              onChange={(event) => setDraft(event.target.value)}
              onKeyDown={(event) => {
                if (event.key === 'Enter' && !event.shiftKey) {
                  event.preventDefault()
                  event.currentTarget.form?.requestSubmit()
                }
              }}
              rows={1}
              maxLength={4000}
              disabled={isSending}
              placeholder="Nhắn cho Trợ lý AI…"
              className="max-h-40 min-h-11 flex-1 resize-y border-0 bg-transparent px-3 py-2 text-text outline-none placeholder:text-text-light disabled:opacity-60"
            />
            <button type="submit" disabled={!draft.trim() || isSending} className="rounded-xl bg-primary px-4 py-2.5 text-sm font-bold text-white transition hover:bg-primary-dark disabled:cursor-not-allowed disabled:opacity-50">
              Gửi
            </button>
          </div>
          <p className="mt-2 px-2 text-xs text-text-light">Enter để gửi · Shift + Enter để xuống dòng</p>
        </form>
      </div>
    </main>
  )
}
