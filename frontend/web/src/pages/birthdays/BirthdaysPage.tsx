import { useEffect, useState } from 'react'
import { Link } from 'react-router-dom'
import { birthdaysApi } from '../../api/users'
import type { BirthdayFriend } from '../../api/users'
import { Mascot } from 'page-mascot'

function BirthdayList({ title, items }: { title: string; items: BirthdayFriend[] }) {
  return <section className="rounded-xl border border-border bg-surface p-4"><h2 className="text-lg font-semibold text-text">{title}</h2>{items.length === 0 ? <p className="mt-3 text-sm text-text-muted">Không có sinh nhật nào.</p> : <div className="mt-3 divide-y divide-border">{items.map((item) => <Link key={item.userId} to={`/profile/${item.userId}`} className="flex items-center justify-between gap-3 py-3 no-underline"><span className="font-medium text-text">{item.displayName}</span><span className="text-sm text-text-muted">{item.day}/{item.month}</span></Link>)}</div>}</section>
}

export default function BirthdaysPage() {
  const [today, setToday] = useState<BirthdayFriend[]>([])
  const [upcoming, setUpcoming] = useState<BirthdayFriend[]>([])
  const [loading, setLoading] = useState(true)
  useEffect(() => { void Promise.all([birthdaysApi.getToday(), birthdaysApi.getUpcoming()]).then(([a, b]) => { setToday(a); setUpcoming(b); setLoading(false) }).catch(() => setLoading(false)) }, [])
  return (
    <main className="mx-auto max-w-3xl p-4 sm:p-6">
      <div className="mb-6 flex items-center gap-4">
        <Mascot
          directions="/mascots/bunny-directions.webp"
          reactions="/mascots/bunny-reactions.webp"
          size={76}
          label="Birthday bunny"
        />
        <div>
          <h1 className="font-heading text-2xl font-bold text-text">Sinh nhật</h1>
          <p className="mt-0.5 text-sm text-text-muted">Chúc mừng sinh nhật những người bạn yêu quý! 🎂</p>
        </div>
      </div>
      {loading
        ? <p className="mt-4 text-sm text-text-muted">Đang tải…</p>
        : <div className="grid gap-4"><BirthdayList title="Hôm nay" items={today} /><BirthdayList title="Sắp tới" items={upcoming} /></div>}
    </main>
  )
}

