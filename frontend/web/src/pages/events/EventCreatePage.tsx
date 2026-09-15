import { useState } from 'react'
import { useNavigate } from 'react-router-dom'
import { eventsApi } from '../../api/events'
import { ApiError } from '../../api/client'

export default function EventCreatePage() {
  const navigate = useNavigate()
  const [name, setName] = useState('')
  const [description, setDescription] = useState('')
  const [privacy, setPrivacy] = useState<'public' | 'private'>('public')
  const [locationType, setLocationType] = useState<'physical' | 'online'>('physical')
  const [locationName, setLocationName] = useState('')
  const [onlineUrl, setOnlineUrl] = useState('')
  const [startsAtUtc, setStartsAtUtc] = useState('')
  const [endsAtUtc, setEndsAtUtc] = useState('')
  const [status, setStatus] = useState<'draft' | 'published'>('published')
  const [error, setError] = useState<string | null>(null)
  const [saving, setSaving] = useState(false)

  const submit = async (event: React.FormEvent) => {
    event.preventDefault()
    setSaving(true)
    setError(null)
    try {
      const created = await eventsApi.create({ hostType: 'user', name, description, privacy, locationType, locationName, onlineUrl, startsAtUtc: new Date(startsAtUtc).toISOString(), endsAtUtc: endsAtUtc ? new Date(endsAtUtc).toISOString() : undefined, status })
      navigate(`/events/${created.id}`)
    } catch (reason) {
      setError(reason instanceof ApiError ? reason.message : 'Không thể tạo sự kiện.')
    } finally {
      setSaving(false)
    }
  }

  return <main className="mx-auto min-h-screen max-w-2xl px-3 py-5">
    <h1 className="font-heading text-3xl font-bold text-text">Tạo sự kiện</h1>
    {error && <p className="mt-4 rounded bg-red-500/10 p-3 text-red-400">{error}</p>}
    <form onSubmit={submit} className="mt-5 grid gap-4 rounded-xl border border-border bg-surface p-5">
      <label className="grid gap-1 text-sm text-text">Tên sự kiện<input required value={name} onChange={(event) => setName(event.target.value)} className="rounded border border-border bg-surface-2 p-2 text-text" /></label>
      <label className="grid gap-1 text-sm text-text">Mô tả<textarea value={description} onChange={(event) => setDescription(event.target.value)} className="rounded border border-border bg-surface-2 p-2 text-text" /></label>
      <div className="grid gap-4 sm:grid-cols-2"><label>Quyền riêng tư<select value={privacy} onChange={(event) => setPrivacy(event.target.value as typeof privacy)} className="ml-2 rounded border border-border bg-surface-2 p-2 text-text"><option value="public">Công khai</option><option value="private">Riêng tư</option></select></label><label>Hình thức<select value={locationType} onChange={(event) => setLocationType(event.target.value as typeof locationType)} className="ml-2 rounded border border-border bg-surface-2 p-2 text-text"><option value="physical">Trực tiếp</option><option value="online">Trực tuyến</option></select></label></div>
      {locationType === 'physical' ? <label className="grid gap-1 text-sm text-text">Địa điểm<input value={locationName} onChange={(event) => setLocationName(event.target.value)} className="rounded border border-border bg-surface-2 p-2 text-text" /></label> : <label className="grid gap-1 text-sm text-text">Liên kết cuộc họp an toàn<input required type="url" value={onlineUrl} onChange={(event) => setOnlineUrl(event.target.value)} className="rounded border border-border bg-surface-2 p-2 text-text" /></label>}
      <label className="grid gap-1 text-sm text-text">Bắt đầu<input required type="datetime-local" value={startsAtUtc} onChange={(event) => setStartsAtUtc(event.target.value)} className="rounded border border-border bg-surface-2 p-2 text-text" /></label>
      <label className="grid gap-1 text-sm text-text">Kết thúc (không bắt buộc)<input type="datetime-local" value={endsAtUtc} onChange={(event) => setEndsAtUtc(event.target.value)} className="rounded border border-border bg-surface-2 p-2 text-text" /></label>
      <div className="flex gap-2"><button disabled={saving} type="submit" onClick={() => setStatus('published')} className="rounded bg-primary px-4 py-2 text-white">Xuất bản</button><button disabled={saving} type="submit" onClick={() => setStatus('draft')} className="rounded border border-border px-4 py-2 text-text">Lưu nháp</button></div>
    </form>
  </main>
}
