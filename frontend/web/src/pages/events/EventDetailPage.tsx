import { useCallback, useEffect, useState } from 'react'
import { Link, useParams } from 'react-router-dom'
import { ApiError } from '../../api/client'
import { eventsApi } from '../../api/events'
import type { Event, EventParticipant } from '../../api/events'
import type { Post } from '../../api/posts'

export default function EventDetailPage() {
  const { eventId = '' } = useParams()
  const [event, setEvent] = useState<Event | null>(null)
  const [participants, setParticipants] = useState<EventParticipant[]>([])
  const [posts, setPosts] = useState<Post[]>([])
  const [content, setContent] = useState('')
  const [error, setError] = useState<string | null>(null)

  const load = useCallback(async () => {
    try {
      const [detail, people, discussion] = await Promise.all([eventsApi.get(eventId), eventsApi.participants(eventId), eventsApi.posts(eventId)])
      setEvent(detail)
      setParticipants(people.items)
      setPosts(discussion.items)
    } catch (reason) {
      setError(reason instanceof ApiError ? reason.message : 'Không thể tải sự kiện.')
    }
  }, [eventId])

  useEffect(() => {
    const timer = window.setTimeout(() => { void load() }, 0)
    return () => window.clearTimeout(timer)
  }, [load])

  const rsvp = async (status: 'going' | 'interested') => { await eventsApi.rsvp(eventId, status); await load() }
  const removeRsvp = async () => { await eventsApi.removeRsvp(eventId); await load() }
  const createDiscussionPost = async (formEvent: React.FormEvent) => {
    formEvent.preventDefault()
    if (!content.trim()) return
    await eventsApi.createPost(eventId, content)
    setContent('')
    await load()
  }

  if (error) return <main className="mx-auto max-w-3xl p-5 text-red-400">{error} <Link to="/events">Quay lại sự kiện</Link></main>
  if (!event) return <main className="p-5 text-text-muted">Đang tải sự kiện…</main>

  return <main className="mx-auto min-h-screen max-w-3xl px-3 py-5">
    <Link to="/events" className="text-sm text-primary">← Sự kiện</Link>
    {event.coverUrl && <img src={event.coverUrl} alt="" className="mt-4 h-64 w-full rounded-xl object-cover" />}
    <section className="mt-4 rounded-xl border border-border bg-surface p-5">
      <div className="flex flex-wrap items-start justify-between gap-3"><div><p className="text-xs font-semibold uppercase text-primary">{event.status} · {event.privacy}</p><h1 className="font-heading text-3xl font-bold text-text">{event.name}</h1><p className="mt-2 text-text-muted">Được tổ chức bởi {event.displayHost.name}</p></div>{event.canManage && <div className="flex gap-2">{event.status === 'draft' && <button type="button" onClick={() => void eventsApi.publish(event.id).then(load)} className="rounded bg-primary px-3 py-2 text-sm text-white">Công bố</button>}{event.status === 'published' && <button type="button" onClick={() => void eventsApi.cancel(event.id).then(load)} className="rounded border border-red-500/50 px-3 py-2 text-sm text-red-400">Hủy sự kiện</button>}</div>}</div>
      <p className="mt-4 text-text">{new Date(event.startsAtUtc).toLocaleString()}{event.endsAtUtc && ` – ${new Date(event.endsAtUtc).toLocaleString()}`}</p>
      {event.locationType === 'online' ? <a className="mt-2 block text-primary" href={event.onlineUrl ?? undefined} target="_blank" rel="noreferrer">Tham gia sự kiện trực tuyến</a> : <p className="mt-2 text-text-muted">{event.locationName || event.address || 'Địa điểm sẽ được cập nhật sau'}</p>}
      {event.description && <p className="mt-4 whitespace-pre-wrap text-text">{event.description}</p>}
      <div className="mt-5 flex flex-wrap gap-2"><button type="button" onClick={() => void rsvp('going')} className="rounded bg-primary px-3 py-2 text-sm text-white">{event.viewerRsvpStatus === 'going' ? 'Sẽ tham gia ✓' : 'Sẽ tham gia'} ({event.goingCount})</button><button type="button" onClick={() => void rsvp('interested')} className="rounded border border-border px-3 py-2 text-sm text-text">Quan tâm ({event.interestedCount})</button>{event.viewerRsvpStatus && <button type="button" onClick={() => void removeRsvp()} className="rounded border border-border px-3 py-2 text-sm text-text-muted">Xóa phản hồi</button>}</div>
    </section>
    <section className="mt-5 rounded-xl border border-border bg-surface p-5"><h2 className="font-heading text-xl font-bold text-text">Người tham gia</h2><div className="mt-3 flex flex-wrap gap-2">{participants.map((participant) => <span key={participant.userId} className="rounded-full bg-surface-2 px-3 py-1 text-sm text-text">{participant.displayName} · {participant.status}</span>)}{participants.length === 0 && <p className="text-sm text-text-muted">Chưa có phản hồi nào.</p>}</div></section>
    <section className="mt-5 rounded-xl border border-border bg-surface p-5"><h2 className="font-heading text-xl font-bold text-text">Thảo luận</h2>{event.canPost && <form onSubmit={createDiscussionPost} className="mt-3 flex gap-2"><input value={content} onChange={(change) => setContent(change.target.value)} placeholder="Viết bài thảo luận…" className="min-w-0 flex-1 rounded border border-border bg-surface-2 p-2 text-text" /><button className="rounded bg-primary px-3 text-white">Đăng</button></form>}<div className="mt-4 grid gap-3">{posts.map((post) => <article key={post.id} className="rounded-lg bg-surface-2 p-3"><p className="text-sm text-text">{post.content}</p><p className="mt-2 text-xs text-text-muted">{new Date(post.createdAtUtc).toLocaleString()}</p></article>)}{posts.length === 0 && <p className="text-sm text-text-muted">Chưa có bài thảo luận nào.</p>}</div></section>
  </main>
}
