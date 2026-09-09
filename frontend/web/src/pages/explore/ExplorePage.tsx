import { useEffect, useState } from 'react'
import { Link } from 'react-router-dom'
import { ApiError } from '../../api/client'
import { usersApi } from '../../api/users'
import type { UserProfile } from '../../api/users'
import { useAuth } from '../../auth/useAuth'
import { TRENDING_TOPICS, formatNumber } from '../../data/mockData'

export default function ExplorePage() {
  const [query, setQuery] = useState('')
  const { session } = useAuth()
  const [users, setUsers] = useState<UserProfile[]>([])
  const [userSearchError, setUserSearchError] = useState<string | null>(null)
  const [isSearchingUsers, setIsSearchingUsers] = useState(true)

  const trimmedQuery = query.trim().toLowerCase()

  const filteredTopics = TRENDING_TOPICS.filter((topic) =>
    !trimmedQuery || topic.tag.toLowerCase().includes(trimmedQuery)
  )

  useEffect(() => {
    const timeoutId = window.setTimeout(() => {
      setIsSearchingUsers(true)
      setUserSearchError(null)
      void usersApi.search(query)
        .then((page) => setUsers(page.items.filter((user) => user.userId !== session!.user.id)))
        .catch((error: unknown) => {
          setUserSearchError(error instanceof ApiError ? error.message : 'Không thể tìm người dùng.')
        })
        .finally(() => setIsSearchingUsers(false))
    }, 250)

    return () => window.clearTimeout(timeoutId)
  }, [query, session])

  return (
    <div
      className="p-4 xl:p-6 flex flex-col gap-6 min-h-screen bg-bg"
      style={{ animation: 'fade-in 0.25s ease both' }}
    >
      {/* ── Header ─────────────────────────────────────── */}
      <div className="flex items-center gap-3 pt-1">
        <h1 className="font-heading font-bold text-[22px] text-text">Explore</h1>
      </div>

      {/* ── Search bar ─────────────────────────────────── */}
      <div className="relative max-w-xl">
        <span className="absolute left-4 top-1/2 -translate-y-1/2 text-text-light text-base select-none pointer-events-none">
          🔍
        </span>
        <input
          type="text"
          value={query}
          onChange={(e) => setQuery(e.target.value)}
          placeholder="Search people, topics, posts..."
          className="w-full bg-surface-2 border border-border rounded-full
                     text-[14px] text-text pl-11 pr-10 py-2.5 outline-none
                     focus:input-focus transition-all placeholder:text-text-light"
        />
        {query && (
          <button
            type="button"
            onClick={() => setQuery('')}
            className="absolute right-3.5 top-1/2 -translate-y-1/2 text-text-light hover:text-text
                       text-xs cursor-pointer w-5 h-5 rounded-full flex items-center justify-center
                       bg-surface-3 hover:bg-surface border-none transition-colors"
            title="Clear search"
          >
            ✕
          </button>
        )}
      </div>

      <div className="grid lg:grid-cols-[1fr_340px] 2xl:grid-cols-[1fr_380px] gap-6 items-start">
        {/* ── Trending Topics ──────────────────────────── */}
        <section>
          <h2 className="font-heading font-bold text-[17px] text-text mb-4">Trending Topics</h2>
          <div className="bg-surface rounded-2xl border border-border overflow-hidden">
            {filteredTopics.length === 0 ? (
              <div className="p-8 text-center text-text-muted text-[14px]">
                No topics found matching &ldquo;{query}&rdquo;
              </div>
            ) : (
              filteredTopics.map((topic, i) => {
                return (
                  <div
                    key={topic.id}
                    className="group flex items-center gap-4 px-5 py-4 cursor-pointer
                               border-b border-border last:border-0 transition-colors
                               hover:bg-surface-2"
                    style={{ animation: `fade-in 0.3s ease ${i * 0.04}s both` }}
                  >
                    <span className="text-[15px] text-text-light font-medium w-5 shrink-0">
                      {i + 1}
                    </span>

                    <div className="flex-1 min-w-0">
                      <div className="text-[14px] font-semibold text-text group-hover:text-primary transition-colors truncate">
                        {topic.tag}
                      </div>
                      <div className="text-[12px] text-text-muted">
                        {formatNumber(topic.posts)} posts
                      </div>
                    </div>

                    <div className="flex items-center gap-3 shrink-0">
                      <span className="text-lg select-none">
                        {topic.trend === 'hot' ? '🔥' : topic.trend === 'up' ? '📈' : '📉'}
                      </span>
                    </div>
                  </div>
                )
              })
            )}
          </div>
        </section>

        {/* ── Suggested Users ──────────────────────────── */}
        <section>
          <h2 className="font-heading font-bold text-[17px] text-text mb-4">Who to follow</h2>
          <div className="flex flex-col gap-3">
            {userSearchError && <div className="bg-[#e41e3f]/10 border border-[#e41e3f]/40 rounded-2xl p-4 text-sm text-[#ff8a9b]">{userSearchError}</div>}
            {isSearchingUsers ? (
              <div className="bg-surface rounded-2xl border border-border p-6 text-center text-text-muted text-[14px]">Searching users...</div>
            ) : users.length === 0 && !userSearchError ? (
              <div className="bg-surface rounded-2xl border border-border p-6 text-center text-text-muted text-[14px]">
                No users found matching &ldquo;{query}&rdquo;
              </div>
            ) : (
              users.map((user, i) => (
                  <div
                    key={user.userId}
                    className="bg-surface rounded-2xl border border-border p-4
                               flex items-start gap-3 transition-all duration-200 hover:card-shadow-hover"
                    style={{ animation: `slide-in-left 0.3s ease ${i * 0.06}s both` }}
                  >
                    <div className="w-11 h-11 rounded-full overflow-hidden flex items-center justify-center text-[12px] font-bold text-white shrink-0 bg-primary">
                      {user.avatarUrl ? <img src={user.avatarUrl} alt="" className="w-full h-full object-cover" /> : user.displayName.slice(0, 2).toUpperCase()}
                    </div>

                    <div className="flex-1 min-w-0">
                      <div className="flex items-center gap-1.5 flex-wrap mb-0.5">
                        <span className="text-[13px] font-semibold text-text truncate">
                        {user.displayName}
                      </span>
                      </div>
                      <p className="text-[12px] text-text-muted truncate">@{user.username}</p>
                      {user.currentCity && <p className="text-[12px] text-text-muted">{user.currentCity}</p>}
                    </div>

                    <Link to={`/profile/${user.userId}`} className="px-3 py-1.5 rounded-full text-[12px] font-semibold transition-all duration-200 shrink-0 no-underline bg-primary text-white hover:bg-primary-dark">View</Link>
                  </div>
              ))
            )}
          </div>
        </section>
      </div>
    </div>
  )
}
