import { useEffect, useRef, useState, type FormEvent, type AriaRole } from 'react'
import { ApiError } from '../../api/client'
import { reportsApi, type ReportReason } from '../../api/reports'
import { usePreferences } from '../../preferences'
import AppDialog from './AppDialog'
import { showToast } from '../toastState'

type ReportTargetType = 'user' | 'post'

interface ReportButtonProps {
  targetType: ReportTargetType
  targetId: string
  className?: string
  role?: AriaRole
}

export default function ReportButton({ targetType, targetId, className = '', role }: ReportButtonProps) {
  const { t } = usePreferences()
  const reasons: { value: ReportReason; label: string }[] = [
    { value: 'spam', label: t('spam') }, { value: 'harassment', label: t('harassment') },
    { value: 'hateSpeech', label: t('hateSpeech') }, { value: 'nudity', label: t('nudity') },
    { value: 'violence', label: t('violence') }, { value: 'scam', label: t('scam') }, { value: 'other', label: t('other') },
  ]
  const [isOpen, setIsOpen] = useState(false)
  const [reason, setReason] = useState<ReportReason>('spam')
  const [details, setDetails] = useState('')
  const [error, setError] = useState<string | null>(null)
  const [isSubmitting, setIsSubmitting] = useState(false)
  const [hasReported, setHasReported] = useState(false)
  const inFlightRef = useRef(false)
  const mountedRef = useRef(false)
  useEffect(() => { mountedRef.current = true; return () => { mountedRef.current = false } }, [])

  const submit = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault()
    if (inFlightRef.current) return
    inFlightRef.current = true
    setError(null)
    setIsSubmitting(true)

    try {
      const payload = { reason, details: details.trim() || undefined }
      if (targetType === 'user') {
        await reportsApi.reportUser(targetId, payload)
      } else {
        await reportsApi.reportPost(targetId, payload)
      }
      if (mountedRef.current) { setHasReported(true); setIsOpen(false) }
    } catch (requestError) {
      const message = requestError instanceof ApiError ? requestError.message : t('unableSendReport')
      if (mountedRef.current) setError(message)
      showToast(message)
    } finally {
      inFlightRef.current = false
      if (mountedRef.current) setIsSubmitting(false)
    }
  }

  return (
    <>
      <button
        role={role}
        type="button"
        onClick={() => setIsOpen(true)}
        disabled={hasReported}
        className={className || 'rounded-lg border border-border bg-surface-2 px-3 py-2 text-sm font-semibold text-text-muted hover:bg-surface-hover disabled:cursor-not-allowed disabled:opacity-60'}
      >
        {hasReported ? t('reported') : t('report')}
      </button>

      {isOpen && <AppDialog title={targetType === 'user' ? t('reportUser') : t('reportPost')} onClose={() => { if (!inFlightRef.current) setIsOpen(false) }}>
          <form onSubmit={(event) => void submit(event)}>
            <div className="flex items-start justify-between gap-4">
              <div>
                <p className="mt-1 text-sm leading-5 text-text-muted">{t('reportPrivate')}</p>
              </div>
              <button type="button" onClick={() => setIsOpen(false)} disabled={isSubmitting} className="absolute right-4 top-4 rounded-lg px-2 py-1 text-text-muted hover:bg-surface-2 hover:text-text focus-visible:outline-2 focus-visible:outline-primary disabled:opacity-60" aria-label={t('closeReportDialog')}>×</button>
            </div>

            {error && <p role="alert" className="mt-4 rounded-lg border border-[#e41e3f]/40 bg-[#e41e3f]/10 px-3 py-2 text-sm text-[#ff8a9b]">{error}</p>}

            <label className="mt-4 flex flex-col gap-1.5 text-sm font-semibold text-text">{t('reason')}
              <select data-dialog-initial-focus disabled={isSubmitting} value={reason} onChange={(event) => setReason(event.target.value as ReportReason)} className="rounded-lg border border-border bg-surface-2 px-3 py-2 text-text outline-none focus:border-primary">
                {reasons.map((item) => <option key={item.value} value={item.value}>{item.label}</option>)}
              </select>
            </label>
            <label className="mt-4 flex flex-col gap-1.5 text-sm font-semibold text-text">{t('additionalDetails')} <span className="font-normal text-text-light">({t('optional')})</span>
              <textarea disabled={isSubmitting} value={details} onChange={(event) => setDetails(event.target.value)} maxLength={500} rows={4} placeholder={t('tellUsWhatHappened')} className="resize-y rounded-lg border border-border bg-surface-2 px-3 py-2 text-text outline-none placeholder:text-text-light focus:border-primary" />
            </label>
            <div className="mt-5 flex justify-end gap-2">
              <button type="button" onClick={() => setIsOpen(false)} disabled={isSubmitting} className="rounded-lg border border-border bg-surface-2 px-3 py-2 text-sm font-semibold text-text disabled:opacity-60">{t('cancel')}</button>
              <button disabled={isSubmitting} className="rounded-lg bg-[#d9465f] px-3 py-2 text-sm font-semibold text-white hover:bg-[#c9344d] disabled:opacity-60">{isSubmitting ? t('sending') : t('sendReport')}</button>
            </div>
          </form>
        </AppDialog>}
    </>
  )
}
