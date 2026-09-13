import { useCallback, useEffect, useRef, useState } from 'react'
import { Navigate, useParams } from 'react-router-dom'
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
    <div className="min-h-screen bg-bg px-4 py-6 sm:px-8">
      {isLoading && <p className="text-sm text-text-muted">{t('loadingProfile')}</p>}
      {error && <p className="mb-4 rounded-lg bg-[#e41e3f]/10 border border-[#e41e3f]/40 p-3 text-sm text-[#ff8a9b]">{error}</p>}
      {!profile && !isLoading && relationship?.status === 'blocked' && isBlockedByMe && (
        <div className="mx-auto flex max-w-3xl items-center justify-between gap-3 rounded-xl border border-border bg-surface p-4">
          <p className="text-sm text-text-muted">{t('relationshipUnavailable')}</p>
          <button type="button" onClick={() => void updateRelationship(() => friendsApi.unblock(userId), true)} disabled={isUpdating} className="rounded-lg border border-border bg-surface-2 px-4 py-2 text-sm font-semibold text-text disabled:opacity-60">{t('unblock')}</button>
        </div>
      )}
      {profile && (
        <div className="max-w-3xl mx-auto flex flex-col gap-5">
          <section className="bg-surface border border-border rounded-2xl overflow-hidden">
            <div className="h-40 bg-gradient-to-br from-primary/60 via-surface-2 to-surface-3">
              {profile.coverUrl && <img src={resolveProfileImageUrl(profile.coverUrl)} alt="" className="w-full h-full object-cover" />}
            </div>
            <div className="px-5 pb-5">
              <div className="w-24 h-24 -mt-12 rounded-full bg-primary text-white border-4 border-surface flex items-center justify-center overflow-hidden text-2xl font-bold">
                {profile.avatarUrl ? <img src={resolveProfileImageUrl(profile.avatarUrl)} alt="" className="w-full h-full object-cover" /> : profile.displayName.slice(0, 2).toUpperCase()}
              </div>
              <div className="mt-3 flex flex-col sm:flex-row sm:items-start sm:justify-between gap-3">
                <div>
                  <h1 className="font-heading font-bold text-2xl text-text">{profile.displayName}</h1>
                  <p className="text-sm text-text-muted">@{profile.username}</p>
                  {profile.bio && <p className="mt-3 whitespace-pre-wrap text-sm text-text">{profile.bio}</p>}
                  <p className="mt-3 text-sm text-text-muted">{profile.currentCity ?? t('noCityListed')} · {mutualFriendCount} {t('mutualFriends')}</p>
                  <div className="mt-2 flex flex-wrap gap-x-3 gap-y-1 text-sm text-text-muted">
                    <span><strong className="text-text">{profile.followerCount}</strong> {t('followers')}</span>
                    <span><strong className="text-text">{profile.followingCount}</strong> {t('followingCount')}</span>
                    {profile.isFollowedBy && <span>{t('followsYou')}</span>}
                  </div>
                </div>
                <div className="flex flex-wrap gap-2">
                  {renderRelationshipAction()}
                  {relationship?.status !== 'blocked' && profile.isFollowing !== null && (
                    <button type="button" onClick={() => void updateFollow()} disabled={isUpdating} className="px-4 py-2 rounded-lg bg-surface-2 hover:bg-surface-hover text-text border border-border font-semibold text-sm cursor-pointer disabled:opacity-60">
                      {profile.isFollowing ? t('following') : t('follow')}
                    </button>
                  )}
                  {relationship?.status !== 'blocked' && <button type="button" onClick={() => void blockProfile()} disabled={isUpdating} className="px-4 py-2 rounded-lg bg-surface-2 hover:bg-surface-hover text-text border border-border font-semibold text-sm cursor-pointer disabled:opacity-60">{t('block')}</button>}
                  <ReportButton targetType="user" targetId={userId} />
                </div>
              </div>
            </div>
          </section>

          <section className="flex flex-col gap-4">
            <h2 className="font-heading font-bold text-xl text-text">{t('posts')}</h2>
            {posts.length === 0 && <p className="text-sm text-text-muted">{t('noVisiblePosts')}</p>}
            {posts.map((post) => (
              <LivePostCard
                key={post.id}
                post={post}
                author={profile}
                currentUserId={session!.user.id}
                onPostUpdated={(updatedPost) => setPosts((currentPosts) => currentPosts.map((item) => item.id === updatedPost.id ? updatedPost : item))}
                onPostDeleted={(postId) => setPosts((currentPosts) => currentPosts.filter((item) => item.id !== postId))}
              />
            ))}
            <PaginationControls hasMore={postsOffset < postsTotal} isLoading={isLoadingMorePosts} error={postsPageError} label={t('loadMorePosts')} onLoadMore={() => void loadMorePosts()} />
          </section>
        </div>
      )}
    </div>
  )
}
