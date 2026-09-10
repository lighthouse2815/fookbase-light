import { useEffect, useState } from 'react'
import { Link, useSearchParams } from 'react-router-dom'
import { ApiError } from '../../api/client'
import { friendsApi } from '../../api/friends'
import type { RelationshipStatus } from '../../api/friends'
import { postsApi } from '../../api/posts'
import type { Post } from '../../api/posts'
import { resolveProfileImageUrl, usersApi } from '../../api/users'
import type { UserProfile } from '../../api/users'
import { useAuth } from '../../auth/useAuth'
import { usePreferences } from '../../preferences'
import PaginationControls from '../../shared/components/PaginationControls'

function relativeDate(value: string, locale: string) {
  return new Intl.RelativeTimeFormat(locale, { numeric: 'auto' }).format(
    Math.round((new Date(value).getTime() - Date.now()) / 60_000),
    'minute',
  )
}

export default function ExplorePage() {
  const { session } = useAuth()
  const { language, t } = usePreferences()
  const [searchParams, setSearchParams] = useSearchParams()
  const query = searchParams.get('q') ?? ''
  const [users, setUsers] = useState<UserProfile[]>([])
  const [posts, setPosts] = useState<Post[]>([])
  const [totalUsers, setTotalUsers] = useState(0)
  const [totalPosts, setTotalPosts] = useState(0)
  const [nextUserOffset, setNextUserOffset] = useState(0)
  const [nextPostOffset, setNextPostOffset] = useState(0)
  const [userSearchError, setUserSearchError] = useState<string | null>(null)
  const [postSearchError, setPostSearchError] = useState<string | null>(null)
  const [isSearchingUsers, setIsSearchingUsers] = useState(true)
  const [isSearchingPosts, setIsSearchingPosts] = useState(true)
  const [isLoadingMoreUsers, setIsLoadingMoreUsers] = useState(false)
  const [isLoadingMorePosts, setIsLoadingMorePosts] = useState(false)
  const [userLoadMoreError, setUserLoadMoreError] = useState<string | null>(null)
  const [postLoadMoreError, setPostLoadMoreError] = useState<string | null>(null)
  const [relationships, setRelationships] = useState<Record<string, RelationshipStatus>>({})
  const [relationshipActionError, setRelationshipActionError] = useState<string | null>(null)
  const [updatingRelationshipUserId, setUpdatingRelationshipUserId] = useState<string | null>(null)

  const loadRelationshipStatuses = async (profiles: readonly UserProfile[]) => {
    const results = await Promise.allSettled(profiles.map((profile) => friendsApi.getStatus(profile.userId)))

    setRelationships((currentRelationships) => {
      const nextRelationships = { ...currentRelationships }
      results.forEach((result, index) => {
        if (result.status === 'fulfilled') {
          nextRelationships[profiles[index].userId] = result.value
        }
      })
      return nextRelationships
    })
  }

  useEffect(() => {
    const timeoutId = window.setTimeout(() => {
      setIsSearchingUsers(true)
      setUserSearchError(null)
      void usersApi.search(query)
        .then((page) => {
          const nextUsers = page.items.filter((user) => user.userId !== session!.user.id)
          setUsers(nextUsers)
          setTotalUsers(page.total)
          setNextUserOffset(page.offset + page.items.length)
          void loadRelationshipStatuses(nextUsers)
        })
        .catch((error: unknown) => {
          setUserSearchError(error instanceof ApiError ? error.message : t('unableSearchUsers'))
        })
        .finally(() => setIsSearchingUsers(false))
    }, 250)

    return () => window.clearTimeout(timeoutId)
  }, [query, session, t])

  useEffect(() => {
    const timeoutId = window.setTimeout(() => {
      setIsSearchingPosts(true)
      setPostSearchError(null)
      void postsApi.search(query)
        .then((page) => {
          setPosts(page.items)
          setTotalPosts(page.total)
          setNextPostOffset(page.offset + page.items.length)
        })
        .catch((error: unknown) => {
          setPostSearchError(error instanceof ApiError ? error.message : t('unableSearchPosts'))
        })
        .finally(() => setIsSearchingPosts(false))
    }, 250)

    return () => window.clearTimeout(timeoutId)
  }, [query, t])

  const updateQuery = (nextQuery: string) => {
    setSearchParams(nextQuery.trim() ? { q: nextQuery } : {})
  }

  const loadMoreUsers = async () => {
    setIsLoadingMoreUsers(true)
    setUserLoadMoreError(null)

    try {
      const page = await usersApi.search(query, nextUserOffset)
      setUsers((currentUsers) => [
        ...currentUsers,
        ...page.items.filter((user) => user.userId !== session!.user.id && !currentUsers.some((item) => item.userId === user.userId)),
      ])
      void loadRelationshipStatuses(page.items.filter((user) => user.userId !== session!.user.id))
      setTotalUsers(page.total)
      setNextUserOffset(page.offset + page.items.length)
    } catch (error) {
      setUserLoadMoreError(error instanceof ApiError ? error.message : t('unableSearchUsers'))
    } finally {
      setIsLoadingMoreUsers(false)
    }
  }

  const loadMorePosts = async () => {
    setIsLoadingMorePosts(true)
    setPostLoadMoreError(null)

    try {
      const page = await postsApi.search(query, nextPostOffset)
      setPosts((currentPosts) => [
        ...currentPosts,
        ...page.items.filter((post) => !currentPosts.some((item) => item.id === post.id)),
      ])
      setTotalPosts(page.total)
      setNextPostOffset(page.offset + page.items.length)
    } catch (error) {
      setPostLoadMoreError(error instanceof ApiError ? error.message : t('unableSearchPosts'))
    } finally {
      setIsLoadingMorePosts(false)
    }
  }

  const hasQuery = Boolean(query.trim())

  const updateRelationship = async (userId: string, action: () => Promise<unknown>) => {
    setUpdatingRelationshipUserId(userId)
    setRelationshipActionError(null)

    try {
      await action()
      const status = await friendsApi.getStatus(userId)
      setRelationships((currentRelationships) => ({ ...currentRelationships, [userId]: status }))
    } catch (error) {
      setRelationshipActionError(error instanceof ApiError ? error.message : t('unableUpdateRelationship'))
    } finally {
      setUpdatingRelationshipUserId(null)
    }
  }

  const renderRelationshipAction = (userId: string) => {
    const relationship = relationships[userId]
    const isUpdating = updatingRelationshipUserId === userId
    const actionClass = 'rounded-full px-3 py-1.5 text-[12px] font-semibold transition-all disabled:opacity-60'

    if (relationship?.status === 'friends') {
      return <span className={`${actionClass} bg-surface-2 text-text-muted`}>{t('friends')}</span>
    }

    if (relationship?.status === 'request_sent' && relationship.requestId) {
      return <button type="button" onClick={() => void updateRelationship(userId, () => friendsApi.cancelRequest(relationship.requestId!))} disabled={isUpdating} className={`${actionClass} border border-border bg-surface-2 text-text hover:bg-surface-hover`}>{t('cancelRequest')}</button>
    }

    if (relationship?.status === 'request_received' && relationship.requestId) {
      return <button type="button" onClick={() => void updateRelationship(userId, () => friendsApi.acceptRequest(relationship.requestId!))} disabled={isUpdating} className={`${actionClass} bg-primary text-white hover:bg-primary-dark`}>{t('acceptRequest')}</button>
    }

    if (relationship?.status === 'blocked') {
      return <span className={`${actionClass} bg-surface-2 text-text-muted`}>{t('unavailable')}</span>
    }

    return <button type="button" onClick={() => void updateRelationship(userId, () => friendsApi.sendRequest(userId))} disabled={isUpdating} className={`${actionClass} bg-primary text-white hover:bg-primary-dark`}>{isUpdating ? t('sending') : t('addFriend')}</button>
  }

  return (
    <div className="p-4 xl:p-6 flex flex-col gap-6 min-h-screen bg-bg" style={{ animation: 'fade-in 0.25s ease both' }}>
      <div className="flex items-center gap-3 pt-1"><h1 className="font-heading font-bold text-[22px] text-text">{t('explore')}</h1></div>

      <div className="relative max-w-xl">
        <span className="absolute left-4 top-1/2 -translate-y-1/2 text-text-light text-base select-none pointer-events-none">🔍</span>
        <input type="search" value={query} onChange={(event) => updateQuery(event.target.value)} placeholder={t('searchPeoplePosts')} className="w-full bg-surface-2 border border-border rounded-full text-[14px] text-text pl-11 pr-10 py-2.5 outline-none focus:input-focus transition-all placeholder:text-text-light" />
        {query && <button type="button" onClick={() => updateQuery('')} className="absolute right-3.5 top-1/2 -translate-y-1/2 text-text-light hover:text-text text-xs cursor-pointer w-5 h-5 rounded-full flex items-center justify-center bg-surface-3 hover:bg-surface border-none transition-colors" title={t('clearSearch')}>✕</button>}
      </div>

      <div className="grid lg:grid-cols-[minmax(0,1fr)_340px] 2xl:grid-cols-[minmax(0,1fr)_380px] gap-6 items-start">
        <section>
          <h2 className="font-heading font-bold text-[17px] text-text mb-4">{hasQuery ? t('posts') : t('latestPosts')}</h2>
          <div className="flex flex-col gap-3">
            {postSearchError && <div className="bg-[#e41e3f]/10 border border-[#e41e3f]/40 rounded-2xl p-4 text-sm text-[#ff8a9b]">{postSearchError}</div>}
            {isSearchingPosts ? <div className="bg-surface rounded-2xl border border-border p-6 text-center text-text-muted text-[14px]">{t('searchingPosts')}</div> : posts.length === 0 && !postSearchError ? <div className="bg-surface rounded-2xl border border-border p-8 text-center text-text-muted text-[14px]">{hasQuery ? `${t('noPostsMatching')} “${query}”.` : t('noVisiblePostsYet')}</div> : posts.map((post) => (
              <article key={post.id} className="bg-surface rounded-2xl border border-border p-4 flex flex-col gap-2">
                <div className="flex items-center justify-between gap-3 text-xs text-text-muted"><Link to={`/profile/${post.authorUserId}`} className="font-semibold text-text hover:underline no-underline">{t('viewAuthor')}</Link><span>{relativeDate(post.createdAtUtc, language === 'vi' ? 'vi-VN' : 'en-US')}</span></div>
                <p className="text-sm leading-relaxed whitespace-pre-wrap text-text">{post.content || `${post.mediaIds.length} ${t('attachments')}`}</p>
                <div className="text-xs text-text-light">{post.commentCount} {t('comments')} · {Object.values(post.reactionCounts).reduce((sum, count) => sum + count, 0)} {t('reactions')}</div>
              </article>
            ))}
            <PaginationControls hasMore={nextPostOffset < totalPosts} isLoading={isLoadingMorePosts} error={postLoadMoreError} label={t('loadMorePosts')} onLoadMore={() => void loadMorePosts()} />
          </div>
        </section>

        <section>
          <h2 className="font-heading font-bold text-[17px] text-text mb-4">{hasQuery ? t('people') : t('peopleToDiscover')}</h2>
          <div className="flex flex-col gap-3">
            {userSearchError && <div className="bg-[#e41e3f]/10 border border-[#e41e3f]/40 rounded-2xl p-4 text-sm text-[#ff8a9b]">{userSearchError}</div>}
            {relationshipActionError && <div className="bg-[#e41e3f]/10 border border-[#e41e3f]/40 rounded-2xl p-4 text-sm text-[#ff8a9b]">{relationshipActionError}</div>}
            {isSearchingUsers ? <div className="bg-surface rounded-2xl border border-border p-6 text-center text-text-muted text-[14px]">{t('searchingUsers')}</div> : users.length === 0 && !userSearchError ? <div className="bg-surface rounded-2xl border border-border p-6 text-center text-text-muted text-[14px]">{hasQuery ? `${t('noPeopleMatching')} “${query}”.` : t('noUsersDiscover')}</div> : users.map((user, index) => (
              <div key={user.userId} className="bg-surface rounded-2xl border border-border p-4 flex items-start gap-3 transition-all duration-200 hover:card-shadow-hover" style={{ animation: `slide-in-left 0.3s ease ${index * 0.06}s both` }}>
                <div className="w-11 h-11 rounded-full overflow-hidden flex items-center justify-center text-[12px] font-bold text-white shrink-0 bg-primary">{user.avatarUrl ? <img src={resolveProfileImageUrl(user.avatarUrl)} alt="" className="w-full h-full object-cover" /> : user.displayName.slice(0, 2).toUpperCase()}</div>
                <div className="flex-1 min-w-0"><p className="text-[13px] font-semibold text-text truncate">{user.displayName}</p><p className="text-[12px] text-text-muted truncate">@{user.username}</p>{user.currentCity && <p className="text-[12px] text-text-muted">{user.currentCity}</p>}</div>
                <div className="flex shrink-0 items-center gap-2">
                  {renderRelationshipAction(user.userId)}
                  <Link to={`/profile/${user.userId}`} className="px-3 py-1.5 rounded-full text-[12px] font-semibold transition-all duration-200 no-underline bg-surface-2 text-text hover:bg-surface-hover">{t('view')}</Link>
                </div>
              </div>
            ))}
            <PaginationControls hasMore={nextUserOffset < totalUsers} isLoading={isLoadingMoreUsers} error={userLoadMoreError} label={t('loadMorePeople')} onLoadMore={() => void loadMoreUsers()} />
          </div>
        </section>
      </div>
    </div>
  )
}
