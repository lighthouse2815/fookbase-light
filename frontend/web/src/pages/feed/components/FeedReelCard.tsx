import { useEffect, useState } from 'react'
import { Link } from 'react-router-dom'
import { apiRequest } from '../../../api/client'
import type { FeedItem } from '../../../api/feed'
import type { MediaReadUrl } from '../../../api/media'
import { resolveProfileImageUrl } from '../../../api/users'
import { usePreferences } from '../../../preferences'

export default function FeedReelCard({ item }: { item: FeedItem }) {
  const { language, t } = usePreferences()
  const [posterUrl, setPosterUrl] = useState<string | null>(null)
  const [previewFailed, setPreviewFailed] = useState(false)
  const posterAccessPath = item.video?.posterAccessPath
  const destination = `/reels?reel=${item.id}`

  useEffect(() => {
    if (!posterAccessPath) return
    const controller = new AbortController()
    void apiRequest<MediaReadUrl>(posterAccessPath, { signal: controller.signal })
      .then((poster) => { if (!controller.signal.aborted) setPosterUrl(poster.url) })
      .catch(() => { if (!controller.signal.aborted) setPreviewFailed(true) })
    return () => controller.abort()
  }, [posterAccessPath])

  return (
    <article className="flex flex-col gap-3 rounded-xl border border-border bg-surface p-4">
      <div className="flex items-center gap-3">
        <div className="flex h-10 w-10 shrink-0 items-center justify-center overflow-hidden rounded-full bg-primary font-bold text-white">
          {item.displayAuthor.avatarUrl ? <img src={resolveProfileImageUrl(item.displayAuthor.avatarUrl)} alt="" className="h-full w-full object-cover" /> : item.displayAuthor.name.slice(0, 2).toUpperCase()}
        </div>
        <div className="min-w-0 flex-1">
          <Link to={`/profile/${item.displayAuthor.id}`} className="block truncate text-sm font-semibold text-text no-underline hover:underline">{item.displayAuthor.name}</Link>
          <p className="text-xs text-text-muted">@{item.displayAuthor.username} · <time dateTime={item.createdAtUtc}>{new Intl.DateTimeFormat(language === 'vi' ? 'vi-VN' : 'en-US', { dateStyle: 'medium' }).format(new Date(item.createdAtUtc))}</time></p>
        </div>
        <span className="rounded-full bg-primary/10 px-2.5 py-1 text-xs font-semibold text-primary">{item.isSuggested ? t('suggestedReel') : 'Reel'}</span>
      </div>
      {item.content && <p className="whitespace-pre-wrap text-sm leading-relaxed text-text">{item.content}</p>}
      <Link to={destination} aria-label={`${t('openReel')} · ${item.displayAuthor.name}`} className="relative flex h-[400px] items-center justify-center overflow-hidden rounded-lg bg-black text-white no-underline sm:h-[480px]">
        {posterUrl && !previewFailed ? <img src={posterUrl} alt="" onError={() => setPreviewFailed(true)} className="h-full w-full object-contain" /> : <span className="px-6 text-center text-sm">{previewFailed || !posterAccessPath ? t('unableLoadReelPreview') : t('loading')}</span>}
        <span className="absolute inset-0 flex items-center justify-center bg-black/10" aria-hidden="true"><span className="flex h-14 w-14 items-center justify-center rounded-full bg-black/65 text-2xl">▶</span></span>
        <span className="absolute bottom-3 right-3 rounded-full bg-black/75 px-3 py-1.5 text-xs font-semibold">{t('openReel')}</span>
      </Link>
      <div className="flex items-center justify-between border-t border-border pt-2 text-xs text-text-muted">
        <span>{item.reactionCount} {t('reactions')}</span>
        <Link to={destination} className="text-text-muted no-underline hover:underline">{item.commentCount} {t('comments')}</Link>
      </div>
    </article>
  )
}
