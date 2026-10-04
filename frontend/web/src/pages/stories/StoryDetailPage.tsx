import { useCallback, useEffect, useMemo, useState } from 'react'
import { Link, useNavigate, useParams } from 'react-router-dom'
import { ApiError } from '../../api/client'
import { storiesApi, type Story, type StoryTrayAuthor } from '../../api/stories'
import StoryViewer, { type StoryUpdate } from '../feed/components/StoryViewer'

export default function StoryDetailPage() {
  const { storyId = '' } = useParams()
  const navigate = useNavigate()
  const [story, setStory] = useState<Story | null>(null)
  const [isLoading, setIsLoading] = useState(true)
  const [error, setError] = useState<'unavailable' | 'load' | null>(null)
  const [attempt, setAttempt] = useState(0)
  const close = useCallback(() => navigate('/feed', { replace: true }), [navigate])

  useEffect(() => {
    let active = true
    const timer = window.setTimeout(() => {
      setIsLoading(true)
      setError(null)
      setStory(null)
      void storiesApi.get(storyId)
        .then((loaded) => { if (active) setStory(loaded) })
        .catch((reason: unknown) => {
          if (active) setError(reason instanceof ApiError && [403, 404, 410].includes(reason.status) ? 'unavailable' : 'load')
        })
        .finally(() => { if (active) setIsLoading(false) })
    }, 0)
    return () => { active = false; window.clearTimeout(timer) }
  }, [storyId, attempt])

  const groups = useMemo<StoryTrayAuthor[]>(() => story ? [{
    author: story.author,
    hasUnseenStories: !story.isViewed && !story.canManage,
    stories: [story],
  }] : [], [story])
  const updateStory = useCallback((updated: StoryUpdate) => {
    setStory((current) => current?.id === updated.id ? { ...current, ...updated } : current)
  }, [])

  if (story) return <StoryViewer groups={groups} initialAuthorIndex={0} initialStoryIndex={0} onClose={close} onStoriesChanged={updateStory} />

  return <main className="mx-auto flex min-h-[calc(100dvh-var(--app-header-height))] w-full max-w-lg flex-col items-center justify-center px-4 py-6">
    {isLoading ? <div role="status" aria-label="Đang tải Story" className="w-full max-w-xs">
      <div aria-hidden="true" className="aspect-[9/14] rounded-2xl bg-surface-2 motion-safe:animate-pulse" />
      <span className="sr-only">Đang tải Story…</span>
    </div> : <section className="w-full rounded-2xl border border-border bg-surface p-6 text-center shadow-sm">
      <h1 className="font-heading text-xl font-bold text-text">{error === 'unavailable' ? 'Story không còn khả dụng' : 'Không thể tải Story'}</h1>
      <p className="mt-2 text-sm leading-6 text-text-muted">{error === 'unavailable'
        ? 'Story có thể đã hết hạn, bị xóa hoặc bạn không có quyền xem.'
        : 'Vui lòng thử lại để xem Story này.'}</p>
      <div className="mt-5 flex flex-wrap justify-center gap-3">
        {error === 'load' && <button type="button" onClick={() => setAttempt((current) => current + 1)} className="rounded-lg border-0 bg-primary px-4 py-2 text-sm font-semibold text-white cursor-pointer hover:opacity-90 focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-primary">Thử lại</button>}
        <Link to="/feed" replace className="rounded-lg bg-surface-2 px-4 py-2 text-sm font-semibold text-text no-underline hover:bg-surface-3 focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-primary">Về bảng tin</Link>
      </div>
    </section>}
  </main>
}
