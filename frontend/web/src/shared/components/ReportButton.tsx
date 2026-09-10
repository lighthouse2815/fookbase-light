import { useState, type FormEvent } from 'react'
import { ApiError } from '../../api/client'
import { reportsApi, type ReportReason } from '../../api/reports'

type ReportTargetType = 'user' | 'post'

interface ReportButtonProps {
  targetType: ReportTargetType
  targetId: string
  className?: string
}

const reasons: { value: ReportReason; label: string }[] = [
  { value: 'spam', label: 'Spam' },
  { value: 'harassment', label: 'Harassment or bullying' },
  { value: 'hateSpeech', label: 'Hate speech' },
  { value: 'nudity', label: 'Nudity or sexual content' },
  { value: 'violence', label: 'Violence or dangerous content' },
  { value: 'scam', label: 'Scam or fraud' },
  { value: 'other', label: 'Other' },
]

export default function ReportButton({ targetType, targetId, className = '' }: ReportButtonProps) {
  const [isOpen, setIsOpen] = useState(false)
  const [reason, setReason] = useState<ReportReason>('spam')
  const [details, setDetails] = useState('')
  const [error, setError] = useState<string | null>(null)
  const [isSubmitting, setIsSubmitting] = useState(false)
  const [hasReported, setHasReported] = useState(false)

  const submit = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault()
    setError(null)
    setIsSubmitting(true)

    try {
      const payload = { reason, details: details.trim() || undefined }
      if (targetType === 'user') {
        await reportsApi.reportUser(targetId, payload)
      } else {
        await reportsApi.reportPost(targetId, payload)
      }
      setHasReported(true)
      setIsOpen(false)
    } catch (requestError) {
      setError(requestError instanceof ApiError ? requestError.message : 'Không thể gửi báo cáo.')
    } finally {
      setIsSubmitting(false)
    }
  }

  return (
    <>
      <button
        type="button"
        onClick={() => setIsOpen(true)}
        disabled={hasReported}
        className={className || 'rounded-lg border border-border bg-surface-2 px-3 py-2 text-sm font-semibold text-text-muted hover:bg-surface-hover disabled:cursor-not-allowed disabled:opacity-60'}
      >
        {hasReported ? 'Reported' : 'Report'}
      </button>

      {isOpen && (
        <div className="fixed inset-0 z-[70] flex items-center justify-center bg-black/65 p-4" role="presentation" onMouseDown={() => !isSubmitting && setIsOpen(false)}>
          <form onSubmit={(event) => void submit(event)} onMouseDown={(event) => event.stopPropagation()} role="dialog" aria-modal="true" aria-labelledby="report-title" className="w-full max-w-md rounded-2xl border border-border bg-surface p-5 shadow-2xl">
            <div className="flex items-start justify-between gap-4">
              <div>
                <h2 id="report-title" className="font-heading text-xl font-bold text-text">Report {targetType === 'user' ? 'user' : 'post'}</h2>
                <p className="mt-1 text-sm leading-5 text-text-muted">Your report is private and will be reviewed by the Fookbase team.</p>
              </div>
              <button type="button" onClick={() => setIsOpen(false)} disabled={isSubmitting} className="rounded-lg px-2 py-1 text-text-muted hover:bg-surface-2 hover:text-text disabled:opacity-60" aria-label="Close report dialog">×</button>
            </div>

            {error && <p role="alert" className="mt-4 rounded-lg border border-[#e41e3f]/40 bg-[#e41e3f]/10 px-3 py-2 text-sm text-[#ff8a9b]">{error}</p>}

            <label className="mt-4 flex flex-col gap-1.5 text-sm font-semibold text-text">Reason
              <select value={reason} onChange={(event) => setReason(event.target.value as ReportReason)} className="rounded-lg border border-border bg-surface-2 px-3 py-2 text-text outline-none focus:border-primary">
                {reasons.map((item) => <option key={item.value} value={item.value}>{item.label}</option>)}
              </select>
            </label>
            <label className="mt-4 flex flex-col gap-1.5 text-sm font-semibold text-text">Additional details <span className="font-normal text-text-light">(optional)</span>
              <textarea value={details} onChange={(event) => setDetails(event.target.value)} maxLength={500} rows={4} placeholder="Tell us what happened" className="resize-y rounded-lg border border-border bg-surface-2 px-3 py-2 text-text outline-none placeholder:text-text-light focus:border-primary" />
            </label>
            <div className="mt-5 flex justify-end gap-2">
              <button type="button" onClick={() => setIsOpen(false)} disabled={isSubmitting} className="rounded-lg border border-border bg-surface-2 px-3 py-2 text-sm font-semibold text-text disabled:opacity-60">Cancel</button>
              <button disabled={isSubmitting} className="rounded-lg bg-[#d9465f] px-3 py-2 text-sm font-semibold text-white hover:bg-[#c9344d] disabled:opacity-60">{isSubmitting ? 'Sending...' : 'Send report'}</button>
            </div>
          </form>
        </div>
      )}
    </>
  )
}
