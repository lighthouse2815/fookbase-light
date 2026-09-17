import { useCallback, useEffect, useRef, useState } from 'react'
import { Link, Navigate, useParams } from 'react-router-dom'
import { ApiError } from '../../api/client'
import { friendsApi } from '../../api/friends'
import type { RelationshipStatus } from '../../api/friends'
import { postsApi } from '../../api/posts'
import type { Post } from '../../api/posts'
import { resolveProfileImageUrl, usersApi } from '../../api/users'
import type { UserProfile } from '../../api/users'
import { useAuth } from '../../auth/useAuth'
import { usePreferences } from '../../preferences'
import ReportButton from '../../shared/components/ReportButton'
import PaginationControls from '../../shared/components/PaginationControls'
import LivePostCard from '../feed/components/LivePostCard'

export default function UserProfilePage() {
  const { userId } = useParams()
  const { session } = useAuth()
  const { t } = usePreferences()
  const [profile, setProfile] = useState<UserProfile | null>(null)
  const [relationship, setRelationship] = useState<RelationshipStatus | null>(null)
  const [isBlockedByMe, setIsBlockedByMe] = useState(false)
  const [mutualFriendCount, setMutualFriendCount] = useState(0)
  const [posts, setPosts] = useState<Post[]>([])
  const [postsTotal, setPostsTotal] = useState(0)
  const [postsOffset, setPostsOffset] = useState(0)
  const [isLoading, setIsLoading] = useState(true)
  const [isLoadingMorePosts, setIsLoadingMorePosts] = useState(false)
  const [isUpdating, setIsUpdating] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [postsPageError, setPostsPageError] = useState<string | null>(null)
  const profileRequestGenerationRef = useRef(0)

  const loadProfile = useCallback(async () => {
    if (!userId) return
    const requestGeneration = ++profileRequestGenerationRef.current
    setIsLoading(true)
    setError(null)

    const [profileResult, statusResult, mutualFriendsResult, postsResult, blockedUsersResult] = await Promise.allSettled([
      usersApi.getById(userId),
      friendsApi.getStatus(userId),
      friendsApi.getMutualFriends(userId),
      postsApi.getByUser(userId),
      friendsApi.getBlockedUsers(),
    ])
    if (requestGeneration !== profileRequestGenerationRef.current) return

    if (profileResult.status === 'rejected') {
      setProfile(null)
      setRelationship(statusResult.status === 'fulfilled' ? statusResult.value : null)
      setMutualFriendCount(0)
      setPosts([])
      setPostsTotal(0)
      setPostsOffset(0)
      setPostsPageError(null)
      setIsBlockedByMe(blockedUsersResult.status === 'fulfilled' && blockedUsersResult.value.items.some((blockedUser) => blockedUser.userId === userId))
      setError(profileResult.reason instanceof ApiError ? profileResult.reason.message : t('unableLoadProfile'))
      setIsLoading(false)
      return
    }

    setProfile(profileResult.value)
    if (statusResult.status === 'fulfilled') setRelationship(statusResult.value)
    if (mutualFriendsResult.status === 'fulfilled') setMutualFriendCount(mutualFriendsResult.value.count)
    if (postsResult.status === 'fulfilled') {
      setPosts(postsResult.value.items)
      setPostsTotal(postsResult.value.total)
      setPostsOffset(postsResult.value.offset + postsResult.value.items.length)
      setPostsPageError(null)
    }
    if (blockedUsersResult.status === 'fulfilled') {
      setIsBlockedByMe(blockedUsersResult.value.items.some((blockedUser) => blockedUser.userId === userId))
    }

    const failedResult = [statusResult, mutualFriendsResult, postsResult, blockedUsersResult].find((result) => result.status === 'rejected')
    if (failedResult?.status === 'rejected') {
      setError(failedResult.reason instanceof ApiError ? failedResult.reason.message : t('unableLoadProfile'))
    }
    setIsLoading(false)
  }, [t, userId])

  const refreshProfileRelationshipState = useCallback(async () => {
    if (!userId) return
    const requestGeneration = ++profileRequestGenerationRef.current
    const [profileResult, statusResult, mutualFriendsResult] = await Promise.allSettled([
      usersApi.getById(userId),
      friendsApi.getStatus(userId),
      friendsApi.getMutualFriends(userId),
    ])
    if (requestGeneration !== profileRequestGenerationRef.current) return

    if (profileResult.status === 'fulfilled') {
      setProfile(profileResult.value)
    } else if (profileResult.reason instanceof ApiError && profileResult.reason.status === 404) {
      setProfile(null)
      setPosts([])
      setPostsTotal(0)
      setPostsOffset(0)
      setPostsPageError(null)
    }
    if (statusResult.status === 'fulfilled') setRelationship(statusResult.value)
    if (mutualFriendsResult.status === 'fulfilled') setMutualFriendCount(mutualFriendsResult.value.count)
  }, [userId])

  useEffect(() => {
    const timeoutId = window.setTimeout(() => {
      void loadProfile()
    }, 0)

    return () => window.clearTimeout(timeoutId)
  }, [loadProfile])

  if (!userId || userId === session!.user.id) return <Navigate to="/profile" replace />

  const updateRelationship = async (action: () => Promise<unknown>, reloadPosts = false) => {
    setIsUpdating(true)
    setError(null)

    try {
      await action()
      if (reloadPosts) {
        await loadProfile()
      } else {
        await refreshProfileRelationshipState()
      }
    } catch (requestError) {
      setError(requestError instanceof ApiError ? requestError.message : t('unableUpdateRelationship'))
    } finally {
      setIsUpdating(false)
    }
  }

  const updateFollow = async () => {
    if (!profile) return

    setIsUpdating(true)
    setError(null)

    try {
      if (profile.isFollowing) {
        await usersApi.unfollow(profile.userId)
      } else {
        await usersApi.follow(profile.userId)
      }
      setProfile((currentProfile) => currentProfile && currentProfile.userId === profile.userId
        ? {
            ...currentProfile,
            isFollowing: !profile.isFollowing,
            followerCount: currentProfile.followerCount + (profile.isFollowing ? -1 : 1),
          }
        : currentProfile)
      await refreshProfileRelationshipState()
    } catch (requestError) {
      setError(requestError instanceof ApiError ? requestError.message : t('unableUpdateRelationship'))
    } finally {
      setIsUpdating(false)
    }
  }

  const blockProfile = async () => {
    setIsUpdating(true)
    setError(null)

    try {
      await friendsApi.block(userId)
      ++profileRequestGenerationRef.current
      setProfile(null)
      setRelationship({ userId, status: 'blocked', requestId: null })
      setIsBlockedByMe(true)
      setMutualFriendCount(0)
      setPosts([])
      setPostsTotal(0)
      setPostsOffset(0)
      setPostsPageError(null)
      setIsLoading(false)
    } catch (requestError) {
      setError(requestError instanceof ApiError ? requestError.message : t('unableUpdateRelationship'))
    } finally {
      setIsUpdating(false)
    }
  }

  const loadMorePosts = async () => {
    if (!userId) return
    setIsLoadingMorePosts(true)
    setPostsPageError(null)
    try {
      const page = await postsApi.getByUser(userId, postsOffset)
      setPosts((current) => [...current, ...page.items.filter((post) => !current.some((item) => item.id === post.id))])
      setPostsTotal(page.total)
      setPostsOffset(page.offset + page.items.length)
    } catch (requestError) {
      setPostsPageError(requestError instanceof ApiError ? requestError.message : t('unableLoadPosts'))
    } finally {
      setIsLoadingMorePosts(false)
    }
  }

  const renderRelationshipAction = () => {
    if (!relationship) return null

    const actionClass = 'px-4 py-2 rounded-lg font-semibold text-sm border-none cursor-pointer disabled:opacity-60'
    switch (relationship.status) {
      case 'none':
        return <button type="button" onClick={() => void updateRelationship(() => friendsApi.sendRequest(userId))} disabled={isUpdating} className={`${actionClass} bg-primary hover:bg-primary-dark text-white`}>{t('addFriend')}</button>
      case 'request_sent':
        return <button type="button" onClick={() => void updateRelationship(() => friendsApi.cancelRequest(relationship.requestId!))} disabled={isUpdating} className={`${actionClass} bg-surface-2 hover:bg-surface-hover text-text border border-border`}>{t('cancelRequest')}</button>
      case 'request_received':
        return (
          <div className="flex gap-2">
            <button type="button" onClick={() => void updateRelationship(() => friendsApi.acceptRequest(relationship.requestId!), true)} disabled={isUpdating} className={`${actionClass} bg-primary hover:bg-primary-dark text-white`}>{t('accept')}</button>
            <button type="button" onClick={() => void updateRelationship(() => friendsApi.declineRequest(relationship.requestId!))} disabled={isUpdating} className={`${actionClass} bg-surface-2 hover:bg-surface-hover text-text border border-border`}>{t('decline')}</button>
          </div>
        )
      case 'friends':
        return <button type="button" onClick={() => void updateRelationship(() => friendsApi.unfriend(userId), true)} disabled={isUpdating} className={`${actionClass} bg-surface-2 hover:bg-surface-hover text-text border border-border`}>{t('unfriend')}</button>
      case 'blocked':
        return isBlockedByMe
          ? <button type="button" onClick={() => void updateRelationship(() => friendsApi.unblock(userId), true)} disabled={isUpdating} className={`${actionClass} bg-surface-2 hover:bg-surface-hover text-text border border-border`}>{t('unblock')}</button>
          : <span className="text-sm text-text-muted">{t('relationshipUnavailable')}</span>
      default:
        return <span className="text-sm text-text-muted">{t('relationshipUnavailable')}</span>
    }
  }

  return (
    <div className="min-h-screen bg-bg" style={{ animation: 'fade-in 0.25s ease both' }}>
      {isLoading && <p className="mx-auto max-w-[1120px] px-4 py-6 text-sm text-text-muted sm:px-8">{t('loadingProfile')}</p>}
      {error && <p className="mx-auto mt-4 max-w-[1120px] rounded-lg border border-[#e41e3f]/40 bg-[#e41e3f]/10 px-3 py-2 text-sm text-[#ff8a9b] sm:px-8">{error}</p>}
      {!profile && !isLoading && relationship?.status === 'blocked' && isBlockedByMe && (
        <div className="mx-auto mt-6 flex max-w-3xl items-center justify-between gap-3 rounded-xl border border-border bg-surface p-4">
          <p className="text-sm text-text-muted">{t('relationshipUnavailable')}</p>
          <button type="button" onClick={() => void updateRelationship(() => friendsApi.unblock(userId), true)} disabled={isUpdating} className="rounded-lg border border-border bg-surface-2 px-4 py-2 text-sm font-semibold text-text disabled:opacity-60">{t('unblock')}</button>
        </div>
      )}
      {profile && (
        <>
          <section className="border-b border-border bg-surface shadow-sm">
            <div className="relative mx-auto h-[280px] w-full max-w-[1120px] sm:h-[320px] md:h-[380px]">
              <div className="absolute inset-0 overflow-hidden rounded-b-2xl bg-gradient-to-b from-surface-3 via-surface-2 to-surface-3">
                {profile.coverUrl && <img src={resolveProfileImageUrl(profile.coverUrl)} alt="" className="absolute inset-0 h-full w-full object-cover" />}
                <div className="absolute inset-0 bg-gradient-to-t from-surface/50 via-transparent to-transparent" />
              </div>
            </div>

            <div className="mx-auto max-w-[1120px] px-4 sm:px-8">
              <div className="flex flex-col items-center gap-4 py-4 md:flex-row md:items-center">
                <div className="flex h-[168px] w-[168px] shrink-0 items-center justify-center overflow-hidden rounded-full border-4 border-surface bg-primary text-5xl font-bold text-white shadow-2xl">
                  {profile.avatarUrl ? <img src={resolveProfileImageUrl(profile.avatarUrl)} alt="" className="h-full w-full object-cover" /> : profile.displayName.slice(0, 2).toUpperCase()}
                </div>
                <div className="min-w-0 flex-1">
                  <div className="flex flex-col gap-4 xl:flex-row xl:items-end xl:justify-between">
                    <div className="text-center md:text-left">
                      <h1 className="font-heading text-2xl font-bold leading-tight text-text sm:text-3xl">{profile.displayName}</h1>
                      {profile.bio && <p className="mt-2 max-w-xl whitespace-pre-line text-[14px] leading-relaxed text-text">{profile.bio}</p>}
                      <div className="mt-2.5 flex flex-wrap items-center justify-center gap-2 text-[14px] text-text-muted md:justify-start">
                        <span><strong className="font-semibold text-text">{profile.followerCount.toLocaleString()}</strong> {t('followers')}</span>
                        <span className="font-bold text-text-light">•</span>
                        <span><strong className="font-semibold text-text">{postsTotal.toLocaleString()}</strong> {t('posts').toLowerCase()}</span>
                        {mutualFriendCount > 0 && <><span className="font-bold text-text-light">•</span><span>{mutualFriendCount} {t('mutualFriends')}</span></>}
                      </div>
                      {(profile.currentCity || profile.education || profile.workplace) && <p className="mt-2 flex flex-wrap items-center justify-center gap-x-2 gap-y-1 text-sm text-text-muted md:justify-start"><span>{profile.currentCity ? `⌖ ${profile.currentCity}` : null}</span>{profile.education && <span>· {profile.education}</span>}{profile.workplace && <span>· {profile.workplace}</span>}</p>}
                    </div>
                    <div className="flex flex-wrap items-center justify-center gap-2.5 md:justify-start">
                      {renderRelationshipAction()}
                      {relationship?.status !== 'blocked' && profile.isFollowing !== null && (
                        <button type="button" onClick={() => void updateFollow()} disabled={isUpdating} className="rounded-lg border border-border bg-surface-2 px-4 py-2 text-sm font-semibold text-text transition-colors hover:bg-surface-hover disabled:opacity-60">
                          {profile.isFollowing ? t('following') : t('follow')}
                        </button>
                      )}
                      {relationship?.status !== 'blocked' && <button type="button" onClick={() => void blockProfile()} disabled={isUpdating} className="rounded-lg border border-border bg-surface-2 px-4 py-2 text-sm font-semibold text-text transition-colors hover:bg-surface-hover disabled:opacity-60">{t('block')}</button>}
                      <ReportButton targetType="user" targetId={userId} />
                    </div>
                  </div>
                </div>
              </div>
              <div className="mt-2 border-t border-border" />
              <div className="flex items-center gap-1 overflow-x-auto pt-1">
                <span className="relative whitespace-nowrap px-4 py-3.5 text-[15px] font-semibold text-primary">{t('posts')}<span className="absolute bottom-0 left-0 right-0 h-[3px] rounded-t-sm bg-primary" /></span>
                <Link to={`/photos?userId=${profile.userId}`} className="whitespace-nowrap rounded-lg px-4 py-3.5 text-[15px] font-semibold text-text-muted no-underline hover:bg-surface-2 hover:text-text">{t('photos')}</Link>
              </div>
            </div>
          </section>

          <main className="mx-auto grid max-w-[1120px] grid-cols-1 items-start gap-4 px-4 py-5 sm:px-8 lg:grid-cols-12">
            <aside className="flex flex-col gap-4 lg:col-span-5 xl:col-span-4">
              <section className="flex flex-col gap-3.5 rounded-xl border border-border bg-surface p-4 card-shadow">
                <h2 className="font-heading text-[18px] font-bold text-text">Thông tin công khai</h2>
                {profile.bio && <p className="whitespace-pre-wrap text-center text-[14px] leading-relaxed text-text">{profile.bio}</p>}
                {(profile.currentCity || profile.hometown || profile.workplace || profile.education || profile.birthday || profile.website) ? <div className="flex flex-col gap-3 border-t border-border pt-3 text-sm text-text">
                  {profile.currentCity && <p>📍 Sống tại <strong>{profile.currentCity}</strong></p>}
                  {profile.hometown && <p>🏠 Đến từ <strong>{profile.hometown}</strong></p>}
                  {profile.workplace && <p>💼 Làm việc tại <strong>{profile.workplace}</strong></p>}
                  {profile.education && <p>🎓 Học tại <strong>{profile.education}</strong></p>}
                  {profile.birthday && <p>🎂 {profile.birthday.day}/{profile.birthday.month}</p>}
                  {profile.website && <a href={profile.website} target="_blank" rel="noopener noreferrer" className="break-all text-primary hover:underline">🔗 {profile.website}</a>}
                </div> : <p className="text-sm text-text-muted">Chưa có thông tin công khai.</p>}
                <div className="flex flex-wrap gap-x-3 gap-y-1 border-t border-border pt-3 text-sm text-text-muted">
                  <span><strong className="text-text">{profile.followerCount.toLocaleString()}</strong> {t('followers')}</span>
                  <span><strong className="text-text">{profile.followingCount.toLocaleString()}</strong> {t('followingCount')}</span>
                  {profile.isFollowedBy && <span>{t('followsYou')}</span>}
                </div>
              </section>
            </aside>

            <section className="flex flex-col gap-3 lg:col-span-7 xl:col-span-8">
              <div className="flex items-center justify-between rounded-xl border border-border bg-surface px-4 py-3.5 card-shadow"><h2 className="font-heading text-[17px] font-bold text-text">{t('posts')}</h2><Link to={`/photos?userId=${profile.userId}`} className="text-sm font-medium text-primary no-underline hover:underline">{t('photos')}</Link></div>
              {posts.length === 0 && <p className="rounded-xl border border-border bg-surface p-4 text-sm text-text-muted">{t('noVisiblePosts')}</p>}
              {posts.map((post) => (
                <LivePostCard
                  key={post.id}
                  post={post}
                  author={profile}
                  currentUserId={session!.user.id}
                  onPostUpdated={(updatedPost) => setPosts((currentPosts) => currentPosts
                    .map((item) => item.id === updatedPost.id ? updatedPost : item)
                    .sort((left, right) => Number(right.isPinned) - Number(left.isPinned)))}
                  onPostDeleted={(postId) => setPosts((currentPosts) => currentPosts.filter((item) => item.id !== postId))}
                />
              ))}
              <PaginationControls hasMore={postsOffset < postsTotal} isLoading={isLoadingMorePosts} error={postsPageError} label={t('loadMorePosts')} onLoadMore={() => void loadMorePosts()} />
            </section>
          </main>
        </>
      )}
    </div>
  )
}
