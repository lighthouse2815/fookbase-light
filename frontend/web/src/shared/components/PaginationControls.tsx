import { usePreferences } from '../../preferences'

interface PaginationControlsProps {
  hasMore: boolean
  isLoading: boolean
  error: string | null
  label: string
  onLoadMore: () => void
}

export default function PaginationControls({ hasMore, isLoading, error, label, onLoadMore }: PaginationControlsProps) {
  const { t } = usePreferences()

  if (!hasMore && !error) return null

  return (
    <div className="flex flex-col gap-2">
      {error && <div role="alert" className="flex items-center justify-between gap-3 rounded-lg border border-danger/40 bg-danger/10 px-3 py-2 text-sm text-danger"><span>{error}</span><button type="button" onClick={onLoadMore} disabled={isLoading} className="font-semibold underline disabled:opacity-60">{t('retry')}</button></div>}
      {hasMore && <button type="button" onClick={onLoadMore} disabled={isLoading} className="rounded-lg border border-border bg-surface-2 py-2.5 text-sm font-semibold text-text transition hover:bg-surface-hover disabled:cursor-not-allowed disabled:opacity-60">{isLoading ? t('loading') : label}</button>}
    </div>
  )
}
