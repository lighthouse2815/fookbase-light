import { useState } from 'react'
import { CURRENT_USER } from '../../data/mockData'

interface NewPostBoxProps {
  onPost: (content: string) => void
}

const MAX_CHARS = 280

export default function NewPostBox({ onPost }: NewPostBoxProps) {
  const [content, setContent] = useState('')
  const remaining = MAX_CHARS - content.length
  const isOverLimit = remaining < 0
  const isNearLimit = remaining <= 30 && !isOverLimit

  const handleSubmit = () => {
    if (!content.trim() || isOverLimit) return
    onPost(content.trim())
    setContent('')
  }

  return (
    <div className="border border-[rgba(0,255,255,0.15)] bg-bg-card rounded-[4px]
                    transition-all duration-200 hover:border-[rgba(0,255,255,0.25)]
                    hover:shadow-[0_0_16px_rgba(0,255,255,0.06)]
                    p-4 flex gap-3" style={{ animation: 'fade-in 0.3s ease both' }}>

      {/* Avatar */}
      <div
        className="avatar-online w-10 h-10 rounded-[2px] flex items-center justify-center
                   text-[11px] font-mono text-cyber-cyan border border-[rgba(0,255,255,0.3)] shrink-0"
        style={{ background: CURRENT_USER.avatarColor }}
      >
        {CURRENT_USER.avatar}
      </div>

      {/* Input area */}
      <div className="flex-1 flex flex-col gap-3">
        {/* Terminal header */}
        <div className="flex items-center gap-2 font-mono text-[10px] text-text-dim tracking-widest">
          <span className="text-cyber-green">&gt;</span>
          <span>BROADCAST_INPUT</span>
          <span className="cursor-blink" />
        </div>

        <textarea
          value={content}
          onChange={(e) => setContent(e.target.value)}
          placeholder="// enter your signal..."
          rows={3}
          className="w-full bg-transparent border-none outline-none resize-none
                     font-cyber text-[14px] text-text-bright leading-relaxed
                     placeholder:text-text-dim placeholder:font-mono placeholder:text-[12px]"
          onKeyDown={(e) => {
            if (e.key === 'Enter' && (e.ctrlKey || e.metaKey)) handleSubmit()
          }}
        />

        {/* Footer */}
        <div className="flex items-center justify-between pt-2 border-t border-[rgba(0,255,255,0.1)]">
          <div className="flex items-center gap-3">
            {/* Tag hint */}
            <span className="font-mono text-[10px] text-text-dim tracking-wide">
              #tags  [ctrl+enter]
            </span>
          </div>

          <div className="flex items-center gap-3">
            {/* Char counter */}
            <span className={`font-mono text-[11px] tabular-nums ${
              isOverLimit ? 'text-cyber-danger' :
              isNearLimit ? 'text-cyber-warning' :
              'text-text-dim'
            }`}>
              {remaining}
            </span>

            <button
              type="button"
              onClick={handleSubmit}
              disabled={!content.trim() || isOverLimit}
              className="flex items-center gap-1.5 px-4 py-1.5 font-mono text-[11px]
                         tracking-[0.12em] uppercase rounded-[2px] border transition-all duration-200
                         disabled:opacity-30 disabled:cursor-not-allowed
                         border-[rgba(0,255,255,0.3)] text-cyber-cyan bg-[rgba(0,255,255,0.05)]
                         hover:enabled:border-cyber-cyan hover:enabled:bg-[rgba(0,255,255,0.1)]
                         hover:enabled:shadow-[0_0_16px_rgba(0,255,255,0.2)] cursor-pointer"
            >
              ▶ BROADCAST
            </button>
          </div>
        </div>
      </div>
    </div>
  )
}
