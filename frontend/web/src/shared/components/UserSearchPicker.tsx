import { useEffect, useState } from 'react'
import { usersApi, resolveProfileImageUrl } from '../../api/users'
import type { UserProfile } from '../../api/users'

interface UserSearchPickerProps {
  excludedUserIds?: readonly string[]
  onSelect: (user: UserProfile) => void
}

export default function UserSearchPicker({ excludedUserIds = [], onSelect }: UserSearchPickerProps) {
  const [query, setQuery] = useState('')
  const [users, setUsers] = useState<UserProfile[]>([])

  useEffect(() => {
    if (!query.trim()) return
    const excluded = new Set(excludedUserIds)
    const timer = window.setTimeout(() => {
      void usersApi.search(query, 0, 10)
        .then((page) => setUsers(page.items.filter((user) => !excluded.has(user.userId))))
        .catch(() => setUsers([]))
    }, 250)
    return () => window.clearTimeout(timer)
  }, [query, excludedUserIds])

  return <div className="relative">
    <input value={query} onChange={(event) => setQuery(event.target.value)} placeholder="Tìm theo tên hoặc username" className="w-full rounded border border-border bg-surface-2 px-2 py-1.5 text-sm text-text" />
    {query.trim() && users.length > 0 && <div className="absolute z-20 mt-1 w-full overflow-hidden rounded-lg border border-border bg-surface shadow-xl">
      {users.map((user) => <button key={user.userId} type="button" onClick={() => { onSelect(user); setQuery(''); setUsers([]) }} className="flex w-full items-center gap-2 px-2 py-2 text-left text-sm text-text hover:bg-surface-2">
        <span className="grid h-7 w-7 shrink-0 place-items-center overflow-hidden rounded-full bg-primary text-[10px] font-bold text-white">{user.avatarUrl ? <img src={resolveProfileImageUrl(user.avatarUrl)} alt="" loading="lazy" className="h-full w-full object-cover" /> : user.displayName.slice(0, 2).toUpperCase()}</span>
        <span className="min-w-0"><span className="block truncate font-semibold">{user.displayName}</span><span className="block truncate text-xs text-text-muted">@{user.username}</span></span>
      </button>)}
    </div>}
  </div>
}
