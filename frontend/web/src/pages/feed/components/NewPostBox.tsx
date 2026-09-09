import { useEffect, useState } from 'react'
import { CURRENT_USER } from '../../../data/mockData'

interface NewPostBoxProps {
  onPost: (
    content: string,
    file: File | null,
    onUploadProgress: (progress: number) => void,
  ) => Promise<void>
}

const MAX_CHARS = 280

export default function NewPostBox({ onPost }: NewPostBoxProps) {
  const [isExpanded, setIsExpanded] = useState(false)
  const [content, setContent] = useState('')
  const [file, setFile] = useState<File | null>(null)
  const [previewUrl, setPreviewUrl] = useState<string | null>(null)
  const [isSubmitting, setIsSubmitting] = useState(false)
  const [uploadProgress, setUploadProgress] = useState(0)
  const [error, setError] = useState<string | null>(null)
  const remaining = MAX_CHARS - content.length
  const isOverLimit = remaining < 0
  const isNearLimit = remaining <= 30 && !isOverLimit

  useEffect(() => {
    if (!file) return

    const reader = new FileReader()
    reader.addEventListener('load', () => {
      if (typeof reader.result === 'string') setPreviewUrl(reader.result)
    })
    reader.readAsDataURL(file)
    return () => reader.abort()
  }, [file])

  const selectFile = (nextFile: File | null) => {
    setPreviewUrl(null)
    setFile(nextFile)
  }

  const handleSubmit = async () => {
    if (!content.trim() || isOverLimit) return
    setIsSubmitting(true)
    setUploadProgress(0)
    setError(null)

    try {
      await onPost(content.trim(), file, setUploadProgress)
      setContent('')
      setFile(null)
      setUploadProgress(0)
      setIsExpanded(false)
    } catch (requestError) {
      setError(requestError instanceof Error ? requestError.message : 'Không thể tạo bài viết.')
    } finally {
      setIsSubmitting(false)
    }
  }

  // Circle progress for char counter
  const pct = Math.min((content.length / MAX_CHARS) * 100, 100)
  const radius = 10
  const circ = 2 * Math.PI * radius
  const offset = circ - (pct / 100) * circ

  return (
    <div className="bg-surface rounded-xl border border-border p-3 sm:p-4 transition-all">
      {!isExpanded ? (
        <>
          {/* Collapsed state: Facebook single-line look */}
          <div className="flex items-center gap-3">
            <div
              className={`w-10 h-10 rounded-full flex items-center justify-center text-[12px]
                          font-bold text-white shrink-0 ${CURRENT_USER.avatarColor}`}
            >
              {CURRENT_USER.avatar}
            </div>
            <button
              type="button"
              onClick={() => setIsExpanded(true)}
              className="flex-1 bg-surface-2 hover:bg-surface-3 text-text-muted text-left
                         rounded-full px-4 py-2.5 text-[14px] cursor-pointer transition-colors
                         border-none outline-none"
            >
              What's on your mind, {CURRENT_USER.displayName}?
            </button>
          </div>

          <div className="border-t border-border my-2.5" />

          {/* 3 action buttons in a row */}
          <div className="flex items-center justify-between">
            <button
              type="button"
              onClick={() => setIsExpanded(true)}
              className="flex-1 flex items-center justify-center gap-2 py-2 rounded-lg
                         hover:bg-surface-2 transition-colors cursor-pointer border-none
                         bg-transparent text-text-muted hover:text-text text-[13px] sm:text-[14px] font-medium"
            >
              <span className="text-lg">📹</span>
              <span>Live video</span>
            </button>
            <button
              type="button"
              onClick={() => setIsExpanded(true)}
              className="flex-1 flex items-center justify-center gap-2 py-2 rounded-lg
                         hover:bg-surface-2 transition-colors cursor-pointer border-none
                         bg-transparent text-text-muted hover:text-text text-[13px] sm:text-[14px] font-medium"
            >
              <span className="text-lg">🖼️</span>
              <span>Photo/video</span>
            </button>
            <button
              type="button"
              onClick={() => setIsExpanded(true)}
              className="flex-1 flex items-center justify-center gap-2 py-2 rounded-lg
                         hover:bg-surface-2 transition-colors cursor-pointer border-none
                         bg-transparent text-text-muted hover:text-text text-[13px] sm:text-[14px] font-medium"
            >
              <span className="text-lg">😊</span>
              <span>Feeling/activity</span>
            </button>
          </div>
        </>
      ) : (
        <div className="flex flex-col gap-3">
          {/* Expanded header */}
          <div className="flex items-center justify-between pb-1 border-b border-border">
            <div className="flex items-center gap-3">
              <div
                className={`w-10 h-10 rounded-full flex items-center justify-center text-[12px]
                            font-bold text-white shrink-0 ${CURRENT_USER.avatarColor}`}
              >
                {CURRENT_USER.avatar}
              </div>
              <div>
                <div className="text-[14px] font-semibold text-text">{CURRENT_USER.displayName}</div>
                <div className="text-[12px] text-text-muted">Public</div>
              </div>
            </div>
            <button
              type="button"
              onClick={() => {
                if (!content.trim()) {
                  setIsExpanded(false)
                } else if (confirm('Discard post?')) {
                  setContent('')
                  setIsExpanded(false)
                }
              }}
              className="w-8 h-8 rounded-full bg-surface-2 hover:bg-surface-3 flex items-center
                         justify-center text-text-muted hover:text-text cursor-pointer border-none
                         transition-colors"
              title="Close"
            >
              ✕
            </button>
          </div>

          {/* Textarea */}
          <textarea
            autoFocus
            value={content}
            onChange={(e) => setContent(e.target.value)}
            placeholder={`What's on your mind, ${CURRENT_USER.displayName}?`}
            rows={4}
            className="w-full bg-transparent border-none outline-none resize-none
                       text-[15px] text-text leading-relaxed placeholder:text-text-light"
            onKeyDown={(e) => {
              if (e.key === 'Enter' && (e.ctrlKey || e.metaKey)) void handleSubmit()
            }}
          />

          {file && (
            <div className="relative rounded-lg border border-border bg-surface-2 overflow-hidden">
              {previewUrl && file.type.startsWith('image/') && <img src={previewUrl} alt="Attachment preview" className="max-h-72 w-full object-cover" />}
              {previewUrl && file.type.startsWith('video/') && <video src={previewUrl} controls className="max-h-72 w-full" />}
              {!file.type.startsWith('image/') && !file.type.startsWith('video/') && <p className="p-3 text-sm text-text">{file.name}</p>}
              <button
                type="button"
                onClick={() => setFile(null)}
                disabled={isSubmitting}
                className="absolute right-2 top-2 w-7 h-7 rounded-full bg-surface/90 hover:bg-surface text-text border border-border cursor-pointer disabled:opacity-60"
                title="Remove attachment"
              >
                ✕
              </button>
            </div>
          )}

          {isSubmitting && file && (
            <div className="flex items-center gap-3 text-xs text-text-muted">
              <div className="h-2 flex-1 rounded-full bg-surface-3 overflow-hidden">
                <div className="h-full bg-primary transition-[width] duration-150" style={{ width: `${uploadProgress}%` }} />
              </div>
              <span>{uploadProgress}%</span>
            </div>
          )}

          {/* Action buttons + Char counter row */}
          <div className="flex items-center justify-between pt-2 border-t border-border">
            <div className="flex items-center gap-1">
              <button
                type="button"
                className="flex items-center gap-1.5 px-3 py-1.5 rounded-lg hover:bg-surface-2
                           text-text-muted hover:text-text transition-colors cursor-pointer
                           bg-transparent border-none text-[13px]"
              >
                <span className="text-base">📹</span>
                <span className="hidden sm:inline">Live video</span>
              </button>
              <label
                className="flex items-center gap-1.5 px-3 py-1.5 rounded-lg hover:bg-surface-2
                           text-text-muted hover:text-text transition-colors cursor-pointer
                           bg-transparent border-none text-[13px]"
              >
                <span className="text-base">🖼️</span>
                <span className="hidden sm:inline">{file ? file.name : 'Photo/video'}</span>
                <input type="file" accept="image/*,video/*" className="hidden" onChange={(event) => selectFile(event.target.files?.[0] ?? null)} />
              </label>
              <button
                type="button"
                className="flex items-center gap-1.5 px-3 py-1.5 rounded-lg hover:bg-surface-2
                           text-text-muted hover:text-text transition-colors cursor-pointer
                           bg-transparent border-none text-[13px]"
              >
                <span className="text-base">😊</span>
                <span className="hidden sm:inline">Feeling/activity</span>
              </button>
            </div>

            {/* Circular char counter */}
            {content.length > 0 && (
              <div className="relative w-6 h-6 shrink-0">
                <svg className="w-6 h-6 -rotate-90" viewBox="0 0 24 24">
                  <circle cx="12" cy="12" r={radius} fill="none"
                    stroke="#3e4042" strokeWidth="2.5" />
                  <circle cx="12" cy="12" r={radius} fill="none"
                    stroke={isOverLimit ? '#e15f5f' : isNearLimit ? '#e7a33e' : '#2374e1'}
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
          </div>

          {error && <p className="text-xs text-[#ff8a9b]">{error}</p>}

          {/* Post button */}
          <button
            type="button"
            onClick={() => void handleSubmit()}
            disabled={!content.trim() || isOverLimit || isSubmitting}
            className="w-full py-2 rounded-lg text-[14px] font-semibold text-white
                       bg-primary hover:brightness-110 transition-all duration-200 cursor-pointer
                       disabled:opacity-40 disabled:cursor-not-allowed border-none"
          >
            {isSubmitting ? 'Posting...' : 'Post'}
          </button>
        </div>
      )}
    </div>
  )
}
