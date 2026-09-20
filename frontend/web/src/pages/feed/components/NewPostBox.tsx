import { useEffect, useRef, useState } from 'react'
import { resolveProfileImageUrl, usersApi } from '../../../api/users'
import { useAuth } from '../../../auth/useAuth'
import { usePreferences } from '../../../preferences'
import AppDialog from '../../../shared/components/AppDialog'

interface NewPostBoxProps {
  onPost: (
    content: string,
    files: readonly File[],
    onUploadProgress: (progress: number) => void,
  ) => Promise<void>
  identityName?: string
  postingLabel?: string
}

const MAX_CHARS = 280
const MAX_ATTACHMENTS = 10
const MAX_IMAGE_BYTES = 20 * 1024 * 1024
const MAX_VIDEO_BYTES = 500 * 1024 * 1024
const SUPPORTED_TYPES = new Set(['image/jpeg', 'image/png', 'image/webp', 'video/mp4', 'video/webm'])
const FEELINGS = ['😊', '🎉', '❤️', '💪', '🎵', '✈️']

interface Attachment {
  file: File
  previewUrl: string
}

function VideoCameraIcon() {
  return <svg viewBox="0 0 24 24" aria-hidden="true" className="h-6 w-6 fill-current"><path d="M4.5 5.5A2.5 2.5 0 0 0 2 8v8a2.5 2.5 0 0 0 2.5 2.5h9A2.5 2.5 0 0 0 16 16V8a2.5 2.5 0 0 0-2.5-2.5h-9ZM18 10.25l3.17-1.9c.37-.22.83.04.83.47v6.36c0 .43-.46.7-.83.47L18 13.75v-3.5Z" /></svg>
}

function PhotoIcon() {
  return <svg viewBox="0 0 24 24" aria-hidden="true" className="h-6 w-6 fill-none stroke-current" strokeWidth="2" strokeLinecap="round" strokeLinejoin="round"><rect x="3" y="4" width="18" height="16" rx="2" /><circle cx="8" cy="9" r="1.5" /><path d="m4.5 18 5.1-5.1 3.4 3.2 2.3-2.2 4.8 4.1" /></svg>
}

function FeelingIcon() {
  return <svg viewBox="0 0 24 24" aria-hidden="true" className="h-6 w-6 fill-none stroke-current" strokeWidth="2" strokeLinecap="round"><circle cx="12" cy="12" r="8.5" /><path d="M8.5 14.5c.9 1.05 2.07 1.58 3.5 1.58s2.6-.53 3.5-1.58M9 9.5h.01M15 9.5h.01" /></svg>
}

function validateFile(file: File, t: (key: string) => string) {
  if (!SUPPORTED_TYPES.has(file.type)) {
    return `${file.name}: ${t('onlyJpegPngWebpMp4Webm')}`
  }

  const maximumSize = file.type.startsWith('video/') ? MAX_VIDEO_BYTES : MAX_IMAGE_BYTES
  return file.size > maximumSize ? `${file.name}: ${t('fileTooLarge')}` : null
}

export default function NewPostBox({ onPost, identityName, postingLabel }: NewPostBoxProps) {
  const { session } = useAuth()
  const { t } = usePreferences()
  const fallbackDisplayName = session!.user.username.includes('@') ? 'bạn' : session!.user.username
  const fileInputRef = useRef<HTMLInputElement>(null)
  const [isExpanded, setIsExpanded] = useState(false)
  const [content, setContent] = useState('')
  const [attachments, setAttachments] = useState<Attachment[]>([])
  const [isSubmitting, setIsSubmitting] = useState(false)
  const [uploadProgress, setUploadProgress] = useState(0)
  const [error, setError] = useState<string | null>(null)
  const [isFeelingPickerOpen, setIsFeelingPickerOpen] = useState(false)
  const [avatarUrl, setAvatarUrl] = useState<string | null>(null)
  const [profileName, setProfileName] = useState<string | null>(null)
  const [isDiscardConfirmationOpen, setIsDiscardConfirmationOpen] = useState(false)
  const displayName = identityName ?? profileName ?? fallbackDisplayName
  const initials = displayName.slice(0, 2).toUpperCase()
  const previewUrlsRef = useRef(new Set<string>())
  const remaining = MAX_CHARS - content.length
  const isOverLimit = remaining < 0
  const isNearLimit = remaining <= 30 && !isOverLimit

  useEffect(() => () => {
    previewUrlsRef.current.forEach((url) => URL.revokeObjectURL(url))
  }, [])

  useEffect(() => {
    if (identityName) return
    let isCurrent = true
    void usersApi.getCurrent()
      .then((profile) => {
        if (!isCurrent) return
        setAvatarUrl(profile.avatarUrl)
        setProfileName(profile.displayName)
      })
      .catch(() => undefined)
    return () => { isCurrent = false }
  }, [identityName, session?.user.id])

  const selectFiles = (selectedFiles: FileList | null) => {
    if (!selectedFiles) return

    const nextFiles = Array.from(selectedFiles)
    const invalidMessage = nextFiles.map((file) => validateFile(file, t)).find((message) => message !== null)
    if (invalidMessage) {
      setError(invalidMessage)
      return
    }

    const availableSlots = MAX_ATTACHMENTS - attachments.length
    if (availableSlots <= 0) {
      setError(`${t('maxAttachments')} ${MAX_ATTACHMENTS} ${t('attachments')}`)
      return
    }

    const newAttachments = nextFiles.slice(0, availableSlots).map((file) => {
      const previewUrl = URL.createObjectURL(file)
      previewUrlsRef.current.add(previewUrl)
      return { file, previewUrl }
    })
    setError(nextFiles.length > availableSlots ? `${t('onlyFirstAttachments')} ${availableSlots} ${t('attachmentsWereAdded')}` : null)
    setAttachments((currentAttachments) => [...currentAttachments, ...newAttachments])
  }

  const resetComposer = () => {
    setContent('')
    attachments.forEach((attachment) => {
      URL.revokeObjectURL(attachment.previewUrl)
      previewUrlsRef.current.delete(attachment.previewUrl)
    })
    setAttachments([])
    setUploadProgress(0)
    setError(null)
    setIsFeelingPickerOpen(false)
  }

  const handleSubmit = async () => {
    if ((!content.trim() && attachments.length === 0) || isOverLimit) return

    setIsSubmitting(true)
    setUploadProgress(0)
    setError(null)

    try {
      await onPost(content.trim(), attachments.map((attachment) => attachment.file), setUploadProgress)
      resetComposer()
      setIsExpanded(false)
    } catch (requestError) {
      setError(requestError instanceof Error ? requestError.message : t('unableCreatePost'))
    } finally {
      setIsSubmitting(false)
    }
  }

  const addFeeling = (feeling: string) => {
    setContent((current) => `${current}${current && !current.endsWith(' ') ? ' ' : ''}${feeling}`)
    setIsFeelingPickerOpen(false)
  }

  const pct = Math.min((content.length / MAX_CHARS) * 100, 100)
  const radius = 10
  const circ = 2 * Math.PI * radius
  const offset = circ - (pct / 100) * circ
  const openComposer = () => setIsExpanded(true)

  return (
    <div className="bg-surface rounded-xl border border-border p-3 transition-all shadow-sm">
      {!isExpanded ? (
        <div className="grid grid-cols-[2.5rem_minmax(0,1fr)] items-center gap-2 sm:flex">
          <span className="flex h-10 w-10 shrink-0 items-center justify-center overflow-hidden rounded-full bg-primary text-[12px] font-bold text-white">{avatarUrl ? <img src={resolveProfileImageUrl(avatarUrl)} alt="" className="h-full w-full object-cover" /> : initials}</span>
          <button type="button" onClick={openComposer} className="h-10 min-w-0 flex-1 truncate bg-surface-2 hover:bg-surface-3 text-text-muted text-left rounded-full px-4 text-[14px] cursor-pointer transition-colors border-none outline-none">{postingLabel ?? t('whatsOnMind')}, {displayName}?</button>
          <div className="col-span-2 grid grid-cols-3 items-center gap-1 sm:flex sm:shrink-0">
            <button type="button" onClick={openComposer} className="flex min-h-10 items-center justify-center gap-1.5 sm:w-10 rounded-lg border-0 bg-transparent text-[#f02849] cursor-pointer transition-colors hover:bg-surface-2" title={t('video')} aria-label={t('video')}><VideoCameraIcon /><span className="text-[11px] font-medium sm:hidden">{t('video')}</span></button>
            <button type="button" onClick={openComposer} className="flex min-h-10 items-center justify-center gap-1.5 sm:w-10 rounded-lg border-0 bg-transparent text-[#45bd62] cursor-pointer transition-colors hover:bg-surface-2" title={t('photoVideo')} aria-label={t('photoVideo')}><PhotoIcon /><span className="text-[11px] font-medium sm:hidden">{t('photoVideo')}</span></button>
            <button type="button" onClick={openComposer} className="flex min-h-10 items-center justify-center gap-1.5 sm:w-10 rounded-lg border-0 bg-transparent text-[#f7b928] cursor-pointer transition-colors hover:bg-surface-2" title={t('feelingActivity')} aria-label={t('feelingActivity')}><FeelingIcon /><span className="text-[11px] font-medium sm:hidden">{t('feelingActivity')}</span></button>
          </div>
        </div>
      ) : (
        <div className="flex flex-col gap-3">
          <div className="flex items-start justify-between gap-2 pb-1 border-b border-border">
            <div className="flex min-w-0 items-center gap-3"><div className="w-10 h-10 rounded-full flex items-center justify-center text-[12px] font-bold text-white shrink-0 bg-primary">{initials}</div><div><div className="text-[14px] font-semibold text-text break-words">{displayName}</div><div className="text-[12px] text-text-muted">{postingLabel ?? t('public')}</div></div></div>
            <button type="button" onClick={() => { if (!content.trim() && attachments.length === 0) setIsExpanded(false); else setIsDiscardConfirmationOpen(true) }} className="w-8 h-8 shrink-0 rounded-full bg-surface-2 hover:bg-surface-3 flex items-center justify-center text-text-muted hover:text-text cursor-pointer border-none transition-colors" title={t('close')} aria-label={t('close')}>✕</button>
          </div>

          <textarea autoFocus value={content} onChange={(event) => setContent(event.target.value)} placeholder={`${t('whatsOnMind')}, ${displayName}?`} rows={4} className="w-full bg-transparent border-none outline-none resize-none text-[15px] text-text leading-relaxed placeholder:text-text-light" onKeyDown={(event) => { if (event.key === 'Enter' && (event.ctrlKey || event.metaKey)) void handleSubmit() }} />

          {attachments.length > 0 && (
            <div className={`grid gap-2 ${attachments.length > 1 ? 'sm:grid-cols-2' : 'grid-cols-1'}`}>
              {attachments.map((attachment, index) => (
                <div key={attachment.previewUrl} className="relative rounded-lg border border-border bg-surface-2 overflow-hidden">
                  {attachment.file.type.startsWith('image/') ? <img src={attachment.previewUrl} alt={`${t('attachment')} ${index + 1}`} className="max-h-72 w-full object-cover" /> : <video src={attachment.previewUrl} controls className="max-h-72 w-full" />}
                  <button type="button" onClick={() => { URL.revokeObjectURL(attachment.previewUrl); previewUrlsRef.current.delete(attachment.previewUrl); setAttachments((currentAttachments) => currentAttachments.filter((_, currentIndex) => currentIndex !== index)) }} disabled={isSubmitting} className="absolute right-2 top-2 w-7 h-7 rounded-full bg-surface/90 hover:bg-surface text-text border border-border cursor-pointer disabled:opacity-60" title={t('removeAttachment')}>✕</button>
                </div>
              ))}
            </div>
          )}

          {isSubmitting && attachments.length > 0 && <div className="flex items-center gap-3 text-xs text-text-muted"><div className="h-2 flex-1 rounded-full bg-surface-3 overflow-hidden"><div className="h-full bg-primary transition-[width] duration-150" style={{ width: `${uploadProgress}%` }} /></div><span>{uploadProgress}%</span></div>}

          <div className="flex flex-wrap items-center justify-between gap-2 pt-2 border-t border-border">
            <div className="relative flex flex-wrap items-center gap-1">
              <button type="button" onClick={() => fileInputRef.current?.click()} disabled={isSubmitting} className="flex items-center gap-1.5 px-3 py-1.5 rounded-lg hover:bg-surface-2 text-text-muted hover:text-text transition-colors cursor-pointer bg-transparent border-none text-[13px] disabled:opacity-60"><span className="text-base">🎬</span><span>{t('video')}</span></button>
              <button type="button" onClick={() => fileInputRef.current?.click()} disabled={isSubmitting} className="flex items-center gap-1.5 px-3 py-1.5 rounded-lg hover:bg-surface-2 text-text-muted hover:text-text transition-colors cursor-pointer bg-transparent border-none text-[13px] disabled:opacity-60"><span className="text-base">🖼️</span><span>{t('photoVideo')} {attachments.length > 0 ? `(${attachments.length}/${MAX_ATTACHMENTS})` : ''}</span></button>
              <button type="button" onClick={() => setIsFeelingPickerOpen((current) => !current)} disabled={isSubmitting} className="flex items-center gap-1.5 px-3 py-1.5 rounded-lg hover:bg-surface-2 text-text-muted hover:text-text transition-colors cursor-pointer bg-transparent border-none text-[13px] disabled:opacity-60"><span className="text-base">😊</span><span>{t('feelingActivity')}</span></button>
              {isFeelingPickerOpen && <div className="absolute left-0 top-full z-10 mt-2 flex gap-1 rounded-xl border border-border bg-surface p-2 shadow-xl">{FEELINGS.map((feeling) => <button key={feeling} type="button" onClick={() => addFeeling(feeling)} className="h-8 w-8 rounded-lg bg-surface-2 text-base hover:bg-surface-3">{feeling}</button>)}</div>}
              <input ref={fileInputRef} type="file" accept="image/jpeg,image/png,image/webp,video/mp4,video/webm" multiple className="hidden" onChange={(event) => { selectFiles(event.target.files); event.target.value = '' }} />
            </div>

            {content.length > 0 && <div className="relative w-6 h-6 shrink-0"><svg className="w-6 h-6 -rotate-90" viewBox="0 0 24 24"><circle cx="12" cy="12" r={radius} fill="none" stroke="#3e4042" strokeWidth="2.5" /><circle cx="12" cy="12" r={radius} fill="none" stroke={isOverLimit ? '#e15f5f' : isNearLimit ? '#e7a33e' : '#2374e1'} strokeWidth="2.5" strokeDasharray={circ} strokeDashoffset={offset} strokeLinecap="round" /></svg>{isNearLimit && <span className={`absolute inset-0 flex items-center justify-center text-[9px] font-bold ${isOverLimit ? 'text-danger' : 'text-warning'}`}>{remaining}</span>}</div>}
          </div>

          {error && <p className="text-xs text-[#ff8a9b]">{error}</p>}
          <button type="button" onClick={() => void handleSubmit()} disabled={(!content.trim() && attachments.length === 0) || isOverLimit || isSubmitting} className="w-full py-2 rounded-lg text-[14px] font-semibold text-white bg-primary hover:brightness-110 transition-all duration-200 cursor-pointer disabled:opacity-40 disabled:cursor-not-allowed border-none">{isSubmitting ? t('posting') : t('post')}</button>
        </div>
      )}
      {isDiscardConfirmationOpen && <AppDialog title="Bỏ bài viết?" onClose={() => setIsDiscardConfirmationOpen(false)}><p className="mt-3 text-sm text-text-muted">Nội dung và tệp đính kèm chưa đăng sẽ bị xóa.</p><div className="mt-5 flex justify-end gap-2"><button type="button" onClick={() => setIsDiscardConfirmationOpen(false)} className="rounded-lg border-0 bg-surface-2 px-4 py-2 text-sm font-semibold text-text hover:bg-surface-3">Hủy</button><button type="button" onClick={() => { resetComposer(); setIsExpanded(false); setIsDiscardConfirmationOpen(false) }} className="rounded-lg border-0 bg-[#e41e3f] px-4 py-2 text-sm font-semibold text-white hover:brightness-110">Bỏ bài viết</button></div></AppDialog>}
    </div>
  )
}
