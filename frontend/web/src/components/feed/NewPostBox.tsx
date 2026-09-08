import { useState } from 'react'
import { CURRENT_USER } from '../../data/mockData'

interface NewPostBoxProps {
  onPost: (content: string) => void
}

const MAX_CHARS = 280

export default function NewPostBox({ onPost }: NewPostBoxProps) {
  const [content, setContent] = useState('')
  const remaining   = MAX_CHARS - content.length
  const isOverLimit = remaining < 0
  const isNearLimit = remaining <= 30 && !isOverLimit

  const handleSubmit = () => {
    if (!content.trim() || isOverLimit) return
    onPost(content.trim())
    setContent('')
  }

  // Circle progress for char counter
  const pct     = Math.min((content.length / MAX_CHARS) * 100, 100)
  const radius  = 10
  const circ    = 2 * Math.PI * radius
  const offset  = circ - (pct / 100) * circ

  return (
    <div className="bg-surface rounded-2xl card-shadow border border-border p-4 flex gap-3">
      {/* Avatar */}
      <div className={`w-10 h-10 rounded-full flex items-center justify-center text-[12px]
                       font-bold text-white shrink-0 ${CURRENT_USER.avatarColor}`}>
        {CURRENT_USER.avatar}
      </div>

      {/* Input area */}
      <div className="flex-1 flex flex-col gap-3">
        <textarea
          value={content}
          onChange={(e) => setContent(e.target.value)}
          placeholder="What's on your mind?"
          rows={3}
          className="w-full bg-transparent border-none outline-none resize-none
                     text-[15px] text-text leading-relaxed
                     placeholder:text-text-light"
          onKeyDown={(e) => {
            if (e.key === 'Enter' && (e.ctrlKey || e.metaKey)) handleSubmit()
          }}
        />

        {/* Divider + actions */}
        <div className="flex items-center justify-between pt-2 border-t border-border">
          {/* Media actions */}
          <div className="flex items-center gap-1 text-primary">
            {['🖼️', '😊', '📍', '🔗'].map((icon) => (
              <button key={icon} type="button"
                className="w-8 h-8 flex items-center justify-center rounded-full
                           hover:bg-purple-50 transition-colors cursor-pointer bg-transparent border-none text-base">
                {icon}
              </button>
            ))}
          </div>

          <div className="flex items-center gap-3">
            {/* Circular char counter */}
            {content.length > 0 && (
              <div className="relative w-6 h-6">
                <svg className="w-6 h-6 -rotate-90" viewBox="0 0 24 24">
                  <circle cx="12" cy="12" r={radius} fill="none"
                    stroke="#e2e8f0" strokeWidth="2.5" />
                  <circle cx="12" cy="12" r={radius} fill="none"
                    stroke={isOverLimit ? '#ef4444' : isNearLimit ? '#f59e0b' : '#6c63ff'}
                    strokeWidth="2.5"
                    strokeDasharray={circ}
                    strokeDashoffset={offset}
                    strokeLinecap="round" />
                </svg>
                {isNearLimit && (
                  <span className={`absolute inset-0 flex items-center justify-center text-[9px] font-bold
                                   ${isOverLimit ? 'text-danger' : 'text-warning'}`}>
                    {remaining}
                  </span>
                )}
              </div>
            )}

            <button
              type="button"
              onClick={handleSubmit}
              disabled={!content.trim() || isOverLimit}
              className="px-5 py-1.5 rounded-full text-[14px] font-semibold text-white
                         gradient-primary transition-all duration-200 cursor-pointer
                         disabled:opacity-40 disabled:cursor-not-allowed
                         hover:enabled:opacity-90 hover:enabled:shadow-[0_4px_12px_rgba(108,99,255,0.35)]
                         border-none"
            >
              Post
            </button>
          </div>
        </div>
      </div>
    </div>
  )
}
