import { useEffect, useState } from 'react'
import { ApiError } from '../../api/client'
import { memoriesApi } from '../../api/memories'
import type { MemoryToday } from '../../api/memories'
import { useAuth } from '../../auth/useAuth'
import LivePostCard from '../feed/components/LivePostCard'

export default function MemoriesPage() {
  const { session } = useAuth()
  const [memories, setMemories] = useState<MemoryToday | null>(null)
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    void memoriesApi.getToday().then(setMemories).catch((reason) => setError(reason instanceof ApiError ? reason.message : 'Không thể tải kỷ niệm.'))
  }, [])

  if (error) return <main className="mx-auto max-w-3xl p-6 text-sm text-[#ff8a9b]">{error}</main>
  if (!memories) return <main className="mx-auto max-w-3xl p-6 text-sm text-text-muted">Đang tải kỷ niệm…</main>
  return <main className="mx-auto max-w-3xl p-4 sm:p-6"><h1 className="font-heading text-2xl font-bold text-text">Ngày này năm xưa</h1><p className="mt-1 text-sm text-text-muted">{memories.date}</p>{memories.years.length === 0 ? <div className="mt-5 rounded-xl border border-border bg-surface p-5 text-sm text-text-muted">Chưa có kỷ niệm nào cho ngày hôm nay.</div> : <div className="mt-5 flex flex-col gap-6">{memories.years.map((year) => <section key={year.year}><h2 className="mb-3 text-lg font-semibold text-text">{year.yearsAgo} năm trước</h2><div className="flex flex-col gap-4">{year.items.map((post) => <LivePostCard key={post.id} post={post} currentUserId={session!.user.id} onPostUpdated={() => undefined} onPostDeleted={() => undefined} />)}</div></section>)}</div>}</main>
}
