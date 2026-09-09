import { useEffect, useState } from 'react'
import { ApiError } from '../../api/client'
import { friendsApi } from '../../api/friends'
import type { BlockedUser, Friend, FriendRequest, PagedResponse, RelationshipStatus } from '../../api/friends'
import { usersApi } from '../../api/users'
import type { UserProfile } from '../../api/users'
import { useAuth } from '../../auth/useAuth'
import { CURRENT_USER, POSTS, getUserById, formatNumber } from '../../data/mockData'
import type { Post } from '../../data/mockData'
import PostCard from '../feed/components/PostCard'

type ProfileTab = 'posts' | 'about' | 'friends' | 'photos'

const badgeClass: Record<string, string> = {
  root: 'badge-root',
  anon: 'badge-anon',
  cyborg: 'badge-cyborg',
  neural: 'badge-neural',
  ghost: 'badge-ghost',
}

const TABS: { id: ProfileTab; label: string }[] = [
  { id: 'posts', label: 'Posts' },
  { id: 'about', label: 'About' },
  { id: 'friends', label: 'Friends' },
  { id: 'photos', label: 'Photos' },
]

// Fallback sample posts authored by current user if none found in mock data
const defaultUserPosts: Post[] = [
  {
    id: 'user-post-1',
    authorId: 'u1',
    content:
      'Working on a new static analysis tool for uncovering memory safety bugs before they hit production. Excited to open source the first preview next week! 🚀💻',
    timestamp: new Date(Date.now() - 1000 * 60 * 60 * 4),
    likes: 342,
    reposts: 58,
    comments: 24,
    tags: ['#security', '#opensource', '#rust', '#dev'],
    isLiked: false,
    isReposted: false,
  },
  {
    id: 'user-post-2',
    authorId: 'u1',
    content:
      'Always sanitize your inputs and never assume memory state across context switches. Simplicity is the ultimate security patch.',
    timestamp: new Date(Date.now() - 1000 * 60 * 60 * 28),
    likes: 819,
    reposts: 172,
    comments: 63,
    tags: ['#kernel', '#infosec', '#bestpractices'],
    isLiked: true,
    isReposted: false,
  },
]

const emptyFriendPage: PagedResponse<Friend> = {
  items: [],
  offset: 0,
  limit: 100,
  total: 0,
}

function getInitials(profile: UserProfile | undefined) {
  if (!profile) return '?'

  return profile.displayName
    .split(' ')
    .filter(Boolean)
    .slice(0, 2)
    .map((part) => part[0])
    .join('')
    .toUpperCase()
}

function getProfileName(profile: UserProfile | undefined, userId: string) {
  return profile?.displayName || `User ${userId.slice(0, 8)}`
}

export default function ProfilePage() {
  const { session } = useAuth()
  const [tab, setTab] = useState<ProfileTab>('posts')
  const [friends, setFriends] = useState<PagedResponse<Friend>>(emptyFriendPage)
  const [incomingRequests, setIncomingRequests] = useState<FriendRequest[]>([])
  const [outgoingRequests, setOutgoingRequests] = useState<FriendRequest[]>([])
  const [blockedUsers, setBlockedUsers] = useState<BlockedUser[]>([])
  const [friendProfiles, setFriendProfiles] = useState<Record<string, UserProfile>>({})
  const [isRelationshipsLoading, setIsRelationshipsLoading] = useState(true)
  const [isLoadingMoreFriends, setIsLoadingMoreFriends] = useState(false)
  const [relationshipError, setRelationshipError] = useState<string | null>(null)
  const [actionId, setActionId] = useState<string | null>(null)
  const [relationshipUserId, setRelationshipUserId] = useState('')
  const [relationshipStatus, setRelationshipStatus] = useState<RelationshipStatus | null>(null)
  const [mutualFriendCount, setMutualFriendCount] = useState<number | null>(null)
  const [profile, setProfile] = useState<UserProfile | null>(null)
  const [profileError, setProfileError] = useState<string | null>(null)
  const [isProfileEditing, setIsProfileEditing] = useState(false)
  const [displayNameDraft, setDisplayNameDraft] = useState('')
  const [bioDraft, setBioDraft] = useState('')
  const [cityDraft, setCityDraft] = useState('')
  const user = profile ? {
    ...CURRENT_USER,
    id: profile.userId,
    handle: profile.username,
    displayName: profile.displayName,
    avatar: profile.displayName.slice(0, 2).toUpperCase(),
    bio: profile.bio ?? '',
    location: profile.currentCity ?? 'Not set',
    joinDate: new Date(profile.createdAt).toLocaleDateString(),
  } : CURRENT_USER

  const myPosts = POSTS.filter((p) => p.authorId === user.id)
  const postsToShow = myPosts.length > 0 ? myPosts : defaultUserPosts

  const loadRelationships = async (showLoading = true) => {
    if (showLoading) {
      setIsRelationshipsLoading(true)
      setRelationshipError(null)
    }

    try {
      const [friendsPage, incomingPage, outgoingPage, blockedPage] = await Promise.all([
        friendsApi.getFriends(0, 20),
        friendsApi.getIncomingRequests(),
        friendsApi.getOutgoingRequests(),
        friendsApi.getBlockedUsers(),
      ])
      const profileIds = [...new Set([
        ...friendsPage.items.map((friend) => friend.userId),
        ...incomingPage.items.map((request) => request.senderUserId),
        ...outgoingPage.items.map((request) => request.receiverUserId),
        ...blockedPage.items.map((blockedUser) => blockedUser.userId),
      ])]
      const profileResults = await Promise.allSettled(
        profileIds.map((userId) => usersApi.getById(userId)),
      )
      const profiles: Record<string, UserProfile> = {}

      profileResults.forEach((result, index) => {
        if (result.status === 'fulfilled') {
          profiles[profileIds[index]] = result.value
        }
      })

      setFriends(friendsPage)
      setIncomingRequests(incomingPage.items)
      setOutgoingRequests(outgoingPage.items)
      setBlockedUsers(blockedPage.items)
      setFriendProfiles(profiles)
    } catch (error) {
      setRelationshipError(
        error instanceof ApiError ? error.message : 'Không thể tải dữ liệu bạn bè.',
      )
    } finally {
      setIsRelationshipsLoading(false)
    }
  }

  useEffect(() => {
    const timeoutId = window.setTimeout(() => {
      void loadRelationships(false)
    }, 0)

    return () => window.clearTimeout(timeoutId)
  }, [])

  useEffect(() => {
    const timeoutId = window.setTimeout(() => {
      void usersApi.getCurrent()
        .then(setProfile)
        .catch((error: unknown) => {
          setProfileError(error instanceof ApiError ? error.message : 'Không thể tải hồ sơ.')
        })
    }, 0)

    return () => window.clearTimeout(timeoutId)
  }, [])

  const runRelationshipAction = async (id: string, action: () => Promise<unknown>) => {
    setActionId(id)
    setRelationshipError(null)

    try {
      await action()
      await loadRelationships()
    } catch (error) {
      setRelationshipError(
        error instanceof ApiError ? error.message : 'Không thể cập nhật mối quan hệ.',
      )
    } finally {
      setActionId(null)
    }
  }

  const loadMoreFriends = async () => {
    setIsLoadingMoreFriends(true)
    setRelationshipError(null)

    try {
      const page = await friendsApi.getFriends(friends.items.length, 20)
      const profileResults = await Promise.allSettled(
        page.items.map((friend) => usersApi.getById(friend.userId)),
      )
      const profiles: Record<string, UserProfile> = {}
      profileResults.forEach((result, index) => {
        if (result.status === 'fulfilled') profiles[page.items[index].userId] = result.value
      })

      setFriends((currentFriends) => ({
        ...page,
        items: [
          ...currentFriends.items,
          ...page.items.filter((friend) => !currentFriends.items.some((item) => item.userId === friend.userId)),
        ],
      }))
      setFriendProfiles((currentProfiles) => ({ ...currentProfiles, ...profiles }))
    } catch (error) {
      setRelationshipError(error instanceof ApiError ? error.message : 'Không thể tải thêm bạn bè.')
    } finally {
      setIsLoadingMoreFriends(false)
    }
  }

  const lookupRelationship = async () => {
    if (!relationshipUserId.trim()) return
    setRelationshipError(null)

    try {
      const [status, mutualFriends] = await Promise.all([
        friendsApi.getStatus(relationshipUserId.trim()),
        friendsApi.getMutualFriends(relationshipUserId.trim()),
      ])
      setRelationshipStatus(status)
      setMutualFriendCount(mutualFriends.count)
    } catch (error) {
      setRelationshipError(error instanceof ApiError ? error.message : 'Không thể tra cứu quan hệ.')
    }
  }

  const openProfileEditor = () => {
    setDisplayNameDraft(profile?.displayName ?? session!.user.username)
    setBioDraft(profile?.bio ?? '')
    setCityDraft(profile?.currentCity ?? '')
    setIsProfileEditing(true)
  }

  const saveProfile = async (event: React.FormEvent<HTMLFormElement>) => {
    event.preventDefault()
    setProfileError(null)

    try {
      setProfile(await usersApi.updateCurrent({
        displayName: displayNameDraft,
        bio: bioDraft,
        currentCity: cityDraft,
      }))
      setIsProfileEditing(false)
    } catch (error) {
      setProfileError(error instanceof ApiError ? error.message : 'Không thể cập nhật hồ sơ.')
    }
  }

  return (
    <div className="min-h-screen bg-bg" style={{ animation: 'fade-in 0.25s ease both' }}>
      {/* ── Top Section: Cover + Header + Tabs ─────────────────────── */}
      <div className="bg-surface border-b border-border shadow-sm">
        {/* 1. Cover photo area: Full-width dark gradient banner */}
        <div className="relative w-full h-[260px] sm:h-[300px] md:h-[340px] bg-gradient-to-b from-surface-3 via-surface-2 to-surface-3">
          {/* Subtle dark texture overlay */}
          <div className="absolute inset-0 opacity-20 bg-[radial-gradient(#3e4042_1px,transparent_1px)] [background-size:16px_16px]" />
          <div className="absolute inset-0 bg-gradient-to-t from-surface/50 via-transparent to-transparent" />

          {/* 2. Avatar: Large circle overlapping the bottom of cover photo */}
          <div className="absolute -bottom-[84px] left-1/2 -translate-x-1/2 md:translate-x-0 md:left-8 z-20">
            <div className="relative group">
              <div
                className={`w-[168px] h-[168px] rounded-full flex items-center justify-center text-5xl
                           font-bold text-white border-4 border-surface shadow-2xl
                           ${user.avatarColor || 'bg-surface-2'}`}
              >
                {user.avatar}
              </div>
              {/* Camera icon button */}
              <button
                type="button"
                title="Update profile picture"
                className="absolute bottom-2 right-2 w-9 h-9 rounded-full bg-surface-2 hover:bg-surface-hover
                           flex items-center justify-center text-text border border-border cursor-pointer
                           shadow-md transition-colors text-sm"
              >
                📷
              </button>
            </div>
          </div>

          {/* Edit cover photo button */}
          <button
            type="button"
            className="absolute right-4 sm:right-8 bottom-4 px-3.5 py-1.5 rounded-lg bg-surface/85 hover:bg-surface
                       text-text text-[13px] font-semibold flex items-center gap-2
                       border border-border/60 transition-colors cursor-pointer shadow-md backdrop-blur-sm z-10"
          >
            <span>📷</span>
            <span className="hidden sm:inline">Edit cover photo</span>
          </button>
        </div>

        {/* 3. Below cover: Profile info section */}
        <div className="max-w-[1250px] mx-auto px-4 sm:px-8">
          <div className="pt-[96px] md:pt-4 md:pl-[216px] pb-4 flex flex-col xl:flex-row xl:items-end justify-between gap-4">
            {/* User Details */}
            <div className="flex flex-col items-center md:items-start text-center md:text-left">
              {/* Name & Badges */}
              <div className="flex flex-wrap items-center justify-center md:justify-start gap-2.5">
                <h1 className="font-heading font-bold text-2xl sm:text-3xl text-text leading-tight">
                  {user.displayName}
                </h1>
                <div className="flex flex-wrap gap-1.5">
                  {user.badges.map((b) => (
                    <span key={b} className={`badge-pill ${badgeClass[b] ?? ''}`}>{b}</span>
                  ))}
                </div>
              </div>

              {/* Handle */}
              <p className="text-[14px] text-text-muted font-medium mt-0.5">@{user.handle}</p>

              {/* Bio text */}
              <p className="text-[14px] text-text mt-2 max-w-xl leading-relaxed whitespace-pre-line">
                {user.bio}
              </p>

              {/* Stats row: followers, following, posts - inline with dot separators */}
              <div className="flex flex-wrap items-center justify-center md:justify-start gap-2 text-[14px] text-text-muted mt-2.5">
                <span>
                  <strong className="font-semibold text-text">{formatNumber(user.followers)}</strong> followers
                </span>
                <span className="text-text-light font-bold">•</span>
                <span>
                  <strong className="font-semibold text-text">{formatNumber(user.following)}</strong> following
                </span>
                <span className="text-text-light font-bold">•</span>
                <span>
                  <strong className="font-semibold text-text">{formatNumber(user.posts)}</strong> posts
                </span>
              </div>
            </div>

            {/* Action buttons */}
            <div className="flex flex-wrap items-center justify-center md:justify-start gap-2.5 shrink-0 mt-2 xl:mt-0">
              {/* Add Friend button */}
              <button
                type="button"
                className="px-4 py-2 bg-primary hover:bg-primary-dark text-white rounded-lg font-semibold text-sm flex items-center gap-1.5 transition-colors cursor-pointer border-none shadow-sm"
              >
                <span>👥</span>
                <span>Add Friend</span>
              </button>

              {/* Message button */}
              <button
                type="button"
                onClick={openProfileEditor}
                className="px-4 py-2 bg-surface-2 hover:bg-surface-hover text-text rounded-lg font-semibold text-sm flex items-center gap-1.5 transition-colors cursor-pointer border border-border"
              >
                <span>💬</span>
                <span>Message</span>
              </button>

              {/* Edit profile button: bg-surface-2 text-text rounded-lg, not a pill button */}
              <button
                type="button"
                className="px-4 py-2 bg-surface-2 hover:bg-surface-hover text-text rounded-lg font-semibold text-sm flex items-center gap-1.5 transition-colors cursor-pointer border border-border"
              >
                <span>✏️</span>
                <span>Edit profile</span>
              </button>

              {/* More options button */}
              <button
                type="button"
                title="More options"
                className="w-9 h-9 bg-surface-2 hover:bg-surface-hover text-text rounded-lg font-semibold text-sm flex items-center justify-center transition-colors cursor-pointer border border-border"
              >
                <span>•••</span>
              </button>
            </div>
          </div>

          {profileError && <p className="mb-3 rounded-lg bg-[#e41e3f]/10 border border-[#e41e3f]/40 px-3 py-2 text-sm text-[#ff8a9b]">{profileError}</p>}
          {isProfileEditing && (
            <form onSubmit={(event) => void saveProfile(event)} className="mb-4 grid grid-cols-1 sm:grid-cols-2 gap-3 rounded-xl border border-border bg-surface-2/60 p-4">
              <label className="flex flex-col gap-1 text-sm text-text">Display name
                <input value={displayNameDraft} onChange={(event) => setDisplayNameDraft(event.target.value)} required className="rounded-lg border border-border bg-surface px-3 py-2 text-text outline-none" />
              </label>
              <label className="flex flex-col gap-1 text-sm text-text">Current city
                <input value={cityDraft} onChange={(event) => setCityDraft(event.target.value)} className="rounded-lg border border-border bg-surface px-3 py-2 text-text outline-none" />
              </label>
              <label className="sm:col-span-2 flex flex-col gap-1 text-sm text-text">Bio
                <textarea value={bioDraft} onChange={(event) => setBioDraft(event.target.value)} rows={3} className="rounded-lg border border-border bg-surface px-3 py-2 text-text outline-none resize-y" />
              </label>
              <div className="sm:col-span-2 flex justify-end gap-2">
                <button type="button" onClick={() => setIsProfileEditing(false)} className="px-3 py-2 rounded-lg bg-surface hover:bg-surface-hover border border-border text-text text-sm cursor-pointer">Cancel</button>
                <button className="px-3 py-2 rounded-lg bg-primary hover:bg-primary-dark border-none text-white text-sm cursor-pointer">Save profile</button>
              </div>
            </form>
          )}

          {/* 4. Tabs below: Posts | About | Friends | Photos */}
          <div className="border-t border-border mt-2" />
          <div className="flex items-center gap-1 overflow-x-auto scroll-smooth pt-1">
            {TABS.map((t) => {
              const isActive = tab === t.id
              return (
                <button
                  key={t.id}
                  type="button"
                  onClick={() => setTab(t.id)}
                  className={[
                    'px-4 py-3.5 text-[15px] font-semibold transition-all duration-150 cursor-pointer relative border-none bg-transparent whitespace-nowrap',
                    isActive
                      ? 'text-primary'
                      : 'text-text-muted hover:text-text hover:bg-surface-2/60 rounded-lg',
                  ].join(' ')}
                >
                  {t.label}
                  {isActive && (
                    <span className="absolute bottom-0 left-0 right-0 h-[3px] bg-primary rounded-t-sm" />
                  )}
                </button>
              )
            })}
          </div>
        </div>
      </div>

      {/* ── Body: Two-column layout below tabs ─────────────────────── */}
      <div className="max-w-[1250px] mx-auto px-4 sm:px-8 py-5">
        {tab === 'posts' && (
          <div className="grid grid-cols-1 lg:grid-cols-12 gap-4 items-start">
            {/* ── Left column (posts) ── */}
            <div className="order-2 lg:order-1 lg:col-span-7 xl:col-span-7 2xl:col-span-8 flex flex-col gap-4">
              {/* Create post box */}
              <div className="bg-surface rounded-xl card-shadow border border-border p-4 flex flex-col gap-3">
                <div className="flex items-center gap-3">
                  <div
                    className={`w-10 h-10 rounded-full flex items-center justify-center font-bold text-white text-sm shrink-0 ${user.avatarColor || 'bg-surface-2'}`}
                  >
                    {user.avatar}
                  </div>
                  <div className="flex-1 bg-surface-2 hover:bg-surface-hover transition-colors rounded-full px-4 py-2.5 text-text-muted text-sm cursor-pointer select-none">
                    What's on your mind?
                  </div>
                </div>
                <div className="border-t border-border pt-2.5 flex items-center justify-around text-xs sm:text-[13px] font-semibold text-text-muted">
                  <button
                    type="button"
                    className="flex items-center gap-2 py-1.5 px-3 rounded-lg hover:bg-surface-2 transition-colors cursor-pointer border-none text-text-muted hover:text-text"
                  >
                    <span className="text-base">🎥</span>
                    <span>Live video</span>
                  </button>
                  <button
                    type="button"
                    className="flex items-center gap-2 py-1.5 px-3 rounded-lg hover:bg-surface-2 transition-colors cursor-pointer border-none text-text-muted hover:text-text"
                  >
                    <span className="text-base">🖼️</span>
                    <span>Photo/video</span>
                  </button>
                  <button
                    type="button"
                    className="flex items-center gap-2 py-1.5 px-3 rounded-lg hover:bg-surface-2 transition-colors cursor-pointer border-none text-text-muted hover:text-text"
                  >
                    <span className="text-base">😊</span>
                    <span>Feeling/activity</span>
                  </button>
                </div>
              </div>

              {/* Manage posts header */}
              <div className="bg-surface rounded-xl card-shadow border border-border p-3.5 px-4 flex items-center justify-between">
                <h2 className="font-heading font-bold text-[17px] text-text">Posts</h2>
                <div className="flex items-center gap-2">
                  <button
                    type="button"
                    className="px-3 py-1.5 bg-surface-2 hover:bg-surface-hover text-text text-xs font-semibold rounded-lg transition-colors cursor-pointer border-none flex items-center gap-1.5"
                  >
                    <span>⚙️</span>
                    <span>Manage posts</span>
                  </button>
                </div>
              </div>

              {/* 5. Post grid below tabs - show the user's posts using PostCard */}
              <div className="flex flex-col gap-3">
                {postsToShow.map((post, i) => {
                  const author = getUserById(post.authorId) || user
                  return (
                    <PostCard
                      key={post.id}
                      post={post}
                      author={author}
                      style={{ animation: `fade-in 0.3s ease ${i * 0.05}s both` }}
                    />
                  )
                })}
              </div>
            </div>

            {/* ── Right column (Intro card with bio, location, join date) ── */}
            <div className="order-1 lg:order-2 lg:col-span-5 xl:col-span-5 2xl:col-span-4 flex flex-col gap-4">
              {/* 6. Intro Card */}
              <div className="bg-surface rounded-xl card-shadow border border-border p-4 flex flex-col gap-3.5">
                <h2 className="font-heading font-bold text-[18px] text-text">Intro</h2>

                {/* Bio text */}
                <p className="text-[14px] text-text text-center py-0.5 leading-relaxed whitespace-pre-wrap">
                  {user.bio}
                </p>
                <button
                  type="button"
                  onClick={openProfileEditor}
                  className="w-full py-2 px-3 bg-surface-2 hover:bg-surface-hover text-text text-sm font-semibold rounded-lg transition-colors cursor-pointer border-none"
                >
                  Edit bio
                </button>

                <div className="border-t border-border my-0.5" />

                {/* Details */}
                <div className="flex flex-col gap-3 text-sm">
                  <div className="flex items-center gap-3 text-text">
                    <span className="text-text-muted text-base shrink-0">📍</span>
                    <span>Lives in <strong className="font-semibold text-text">{user.location}</strong></span>
                  </div>
                  <div className="flex items-center gap-3 text-text">
                    <span className="text-text-muted text-base shrink-0">📅</span>
                    <span>Joined <strong className="font-semibold text-text">{user.joinDate}</strong></span>
                  </div>
                  <div className="flex items-center gap-3 text-text">
                    <span className="text-text-muted text-base shrink-0">👥</span>
                    <span>Followed by <strong className="font-semibold text-text">{formatNumber(user.followers)}</strong> people</span>
                  </div>
                  {user.isOnline && (
                    <div className="flex items-center gap-3 text-text">
                      <span className="w-2.5 h-2.5 rounded-full bg-[#31a24c] shrink-0 ml-0.5" />
                      <span className="text-[#31a24c] font-medium text-[13px]">Active now</span>
                    </div>
                  )}
                </div>

                {/* Badges */}
                {user.badges && user.badges.length > 0 && (
                  <div className="flex flex-col gap-2 pt-2 border-t border-border">
                    <span className="text-xs font-semibold text-text-muted uppercase tracking-wider">
                      Badges
                    </span>
                    <div className="flex flex-wrap gap-1.5">
                      {user.badges.map((b) => (
                        <span key={b} className={`badge-pill ${badgeClass[b] ?? ''}`}>{b}</span>
                      ))}
                    </div>
                  </div>
                )}

                <button
                  type="button"
                  onClick={openProfileEditor}
                  className="w-full py-2 px-3 bg-surface-2 hover:bg-surface-hover text-text text-sm font-semibold rounded-lg transition-colors cursor-pointer border-none mt-1"
                >
                  Edit details
                </button>
              </div>

              {/* Photos Preview Card */}
              <div className="bg-surface rounded-xl card-shadow border border-border p-4 flex flex-col gap-3">
                <div className="flex items-center justify-between">
                  <h2 className="font-heading font-bold text-[18px] text-text">Photos</h2>
                  <button
                    type="button"
                    onClick={() => setTab('photos')}
                    className="text-primary hover:underline text-sm font-medium cursor-pointer border-none bg-transparent"
                  >
                    See all photos
                  </button>
                </div>
                <div className="grid grid-cols-3 gap-1.5 rounded-lg overflow-hidden">
                  {[
                    'bg-surface-3',
                    'bg-surface-2',
                    'bg-surface-3',
                    'bg-surface-2',
                    'bg-surface-3',
                    'bg-surface-2',
                  ].map((bg, idx) => (
                    <div
                      key={idx}
                      className={`${bg} aspect-square flex items-center justify-center text-text-muted hover:opacity-90 cursor-pointer transition-opacity text-xl`}
                    >
                      {['💻', '⚡', '🔐', '🌐', '🛡️', '⚙️'][idx]}
                    </div>
                  ))}
                </div>
              </div>

              {/* Friends Preview Card */}
              <div className="bg-surface rounded-xl card-shadow border border-border p-4 flex flex-col gap-3">
                <div className="flex items-center justify-between">
                  <div>
                    <h2 className="font-heading font-bold text-[18px] text-text">Friends</h2>
                    <p className="text-xs text-text-muted">{formatNumber(friends.total)} friends</p>
                  </div>
                  <button
                    type="button"
                    onClick={() => setTab('friends')}
                    className="text-primary hover:underline text-sm font-medium cursor-pointer border-none bg-transparent"
                  >
                    See all friends
                  </button>
                </div>
                <div className="grid grid-cols-3 gap-2">
                  {friends.items.slice(0, 6).map((friend) => {
                    const profile = friendProfiles[friend.userId]

                    return (
                      <div key={friend.userId} className="flex flex-col items-center text-center group">
                        <div className="w-full aspect-square rounded-lg overflow-hidden flex items-center justify-center text-base font-bold text-white mb-1.5 shadow-sm transition-transform group-hover:scale-[1.02] bg-primary">
                          {profile?.avatarUrl ? (
                            <img
                              src={profile.avatarUrl}
                              alt=""
                              className="w-full h-full object-cover"
                            />
                          ) : getInitials(profile)}
                        </div>
                        <span className="text-[12px] font-medium text-text truncate w-full group-hover:underline">
                          {getProfileName(profile, friend.userId)}
                        </span>
                      </div>
                    )
                  })}
                  {!isRelationshipsLoading && friends.items.length === 0 && !relationshipError && (
                    <p className="col-span-3 text-xs text-text-muted">Chưa có bạn bè.</p>
                  )}
                </div>
              </div>
            </div>
          </div>
        )}

        {/* ── About Tab ── */}
        {tab === 'about' && (
          <div className="bg-surface rounded-xl card-shadow border border-border p-6 flex flex-col gap-6">
            <h2 className="font-heading font-bold text-xl text-text border-b border-border pb-3">About</h2>
            <div className="grid grid-cols-1 md:grid-cols-2 gap-6">
              <div className="flex flex-col gap-4">
                <h3 className="font-semibold text-text-muted text-xs uppercase tracking-wider">Overview</h3>
                <div className="flex items-start gap-3">
                  <span className="text-xl">📍</span>
                  <div>
                    <p className="text-sm font-semibold text-text">Lives in {user.location}</p>
                    <p className="text-xs text-text-muted">Current City</p>
                  </div>
                </div>
                <div className="flex items-start gap-3">
                  <span className="text-xl">📅</span>
                  <div>
                    <p className="text-sm font-semibold text-text">Joined {user.joinDate}</p>
                    <p className="text-xs text-text-muted">Member Since</p>
                  </div>
                </div>
                <div className="flex items-start gap-3">
                  <span className="text-xl">💼</span>
                  <div>
                    <p className="text-sm font-semibold text-text">Security Researcher</p>
                    <p className="text-xs text-text-muted">Specialization</p>
                  </div>
                </div>
              </div>
              <div className="flex flex-col gap-4">
                <h3 className="font-semibold text-text-muted text-xs uppercase tracking-wider">Bio & Badges</h3>
                <p className="text-sm text-text leading-relaxed whitespace-pre-line bg-surface-2 p-3.5 rounded-lg border border-border">
                  {user.bio}
                </p>
                <div className="flex flex-wrap gap-2">
                  {user.badges.map((b) => (
                    <span key={b} className={`badge-pill ${badgeClass[b] ?? ''} text-xs py-1 px-3`}>{b}</span>
                  ))}
                </div>
              </div>
            </div>
          </div>
        )}

        {/* ── Friends Tab ── */}
        {tab === 'friends' && (
          <div className="bg-surface rounded-xl card-shadow border border-border p-6 flex flex-col gap-5">
            <div className="flex items-center justify-between border-b border-border pb-3">
              <div>
                <h2 className="font-heading font-bold text-xl text-text">Friends</h2>
                <p className="text-sm text-text-muted">{formatNumber(friends.total)} friends</p>
              </div>
              <button
                type="button"
                onClick={() => void loadRelationships()}
                disabled={isRelationshipsLoading}
                className="px-3 py-1.5 bg-surface-2 hover:bg-surface-hover disabled:opacity-60 text-text rounded-lg text-xs font-semibold border border-border cursor-pointer transition-colors"
              >
                {isRelationshipsLoading ? 'Loading...' : 'Refresh'}
              </button>
            </div>

            {relationshipError && (
              <div className="rounded-lg border border-[#e41e3f]/40 bg-[#e41e3f]/10 px-3 py-2 text-sm text-[#ff8a9b]">
                {relationshipError}
              </div>
            )}

            <section className="rounded-xl border border-border bg-surface-2/40 p-4 flex flex-col gap-3">
              <div>
                <h3 className="font-heading font-bold text-base text-text">Manage relationship</h3>
                <p className="text-xs text-text-muted">Enter a user ID to send a request, check status, mutual friends, or block.</p>
              </div>
              <div className="flex flex-col sm:flex-row gap-2">
                <input
                  value={relationshipUserId}
                  onChange={(event) => {
                    setRelationshipUserId(event.target.value)
                    setRelationshipStatus(null)
                    setMutualFriendCount(null)
                  }}
                  placeholder="User ID (UUID)"
                  className="flex-1 rounded-lg border border-border bg-surface px-3 py-2 text-sm text-text outline-none"
                />
                <button type="button" onClick={() => void lookupRelationship()} className="px-3 py-2 rounded-lg bg-surface hover:bg-surface-hover border border-border text-text text-sm cursor-pointer">Check</button>
              </div>
              {relationshipStatus && (
                <div className="flex flex-wrap items-center gap-2 text-sm text-text">
                  <span>Status: <strong>{relationshipStatus.status}</strong></span>
                  {mutualFriendCount !== null && <span className="text-text-muted">· {mutualFriendCount} mutual friends</span>}
                  <button type="button" onClick={() => void runRelationshipAction(relationshipUserId, () => friendsApi.sendRequest(relationshipUserId))} className="px-2.5 py-1 rounded-lg bg-primary text-white border-none cursor-pointer text-xs">Add friend</button>
                  <button type="button" onClick={() => void runRelationshipAction(relationshipUserId, () => friendsApi.block(relationshipUserId))} className="px-2.5 py-1 rounded-lg bg-surface border border-border text-text cursor-pointer text-xs">Block</button>
                </div>
              )}
            </section>

            {incomingRequests.length > 0 && (
              <section className="flex flex-col gap-3">
                <h3 className="font-heading font-bold text-lg text-text">Friend requests</h3>
                <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
                  {incomingRequests.map((request) => {
                    const profile = friendProfiles[request.senderUserId]
                    const isPending = actionId === request.id

                    return (
                      <div key={request.id} className="flex items-center justify-between gap-3 p-3 rounded-lg bg-surface-2/60 border border-border">
                        <div className="min-w-0">
                          <p className="font-semibold text-text text-sm truncate">{getProfileName(profile, request.senderUserId)}</p>
                          <p className="text-xs text-text-muted truncate">@{profile?.username ?? request.senderUserId.slice(0, 8)}</p>
                        </div>
                        <div className="flex gap-2 shrink-0">
                          <button
                            type="button"
                            onClick={() => void runRelationshipAction(request.id, () => friendsApi.acceptRequest(request.id))}
                            disabled={isPending}
                            className="px-3 py-1.5 bg-primary hover:bg-primary-dark disabled:opacity-60 text-white rounded-lg text-xs font-semibold border-none cursor-pointer transition-colors"
                          >
                            Accept
                          </button>
                          <button
                            type="button"
                            onClick={() => void runRelationshipAction(request.id, () => friendsApi.declineRequest(request.id))}
                            disabled={isPending}
                            className="px-3 py-1.5 bg-surface-2 hover:bg-surface-hover disabled:opacity-60 text-text rounded-lg text-xs font-semibold border border-border cursor-pointer transition-colors"
                          >
                            Decline
                          </button>
                        </div>
                      </div>
                    )
                  })}
                </div>
              </section>
            )}

            {outgoingRequests.length > 0 && (
              <section className="flex flex-col gap-3">
                <h3 className="font-heading font-bold text-lg text-text">Sent requests</h3>
                <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
                  {outgoingRequests.map((request) => {
                    const profile = friendProfiles[request.receiverUserId]
                    const isPending = actionId === request.id

                    return (
                      <div key={request.id} className="flex items-center justify-between gap-3 p-3 rounded-lg bg-surface-2/60 border border-border">
                        <div className="min-w-0">
                          <p className="font-semibold text-text text-sm truncate">{getProfileName(profile, request.receiverUserId)}</p>
                          <p className="text-xs text-text-muted truncate">@{profile?.username ?? request.receiverUserId.slice(0, 8)}</p>
                        </div>
                        <button
                          type="button"
                          onClick={() => void runRelationshipAction(request.id, () => friendsApi.cancelRequest(request.id))}
                          disabled={isPending}
                          className="px-3 py-1.5 bg-surface-2 hover:bg-surface-hover disabled:opacity-60 text-text rounded-lg text-xs font-semibold border border-border cursor-pointer transition-colors shrink-0"
                        >
                          Cancel
                        </button>
                      </div>
                    )
                  })}
                </div>
              </section>
            )}

            {blockedUsers.length > 0 && (
              <section className="flex flex-col gap-3">
                <h3 className="font-heading font-bold text-lg text-text">Blocked users</h3>
                <div className="grid grid-cols-1 sm:grid-cols-2 gap-3">
                  {blockedUsers.map((blockedUser) => {
                    const profile = friendProfiles[blockedUser.userId]
                    const isPending = actionId === blockedUser.userId

                    return (
                      <div key={blockedUser.userId} className="flex items-center justify-between gap-3 rounded-lg bg-surface-2/60 border border-border p-3">
                        <div className="min-w-0">
                          <p className="font-semibold text-sm text-text truncate">{getProfileName(profile, blockedUser.userId)}</p>
                          <p className="text-xs text-text-muted truncate">@{profile?.username ?? blockedUser.userId.slice(0, 8)}</p>
                        </div>
                        <button type="button" onClick={() => void runRelationshipAction(blockedUser.userId, () => friendsApi.unblock(blockedUser.userId))} disabled={isPending} className="px-3 py-1.5 rounded-lg bg-surface border border-border text-text text-xs cursor-pointer disabled:opacity-60">Unblock</button>
                      </div>
                    )
                  })}
                </div>
              </section>
            )}

            {!isRelationshipsLoading && friends.items.length === 0 && !relationshipError && (
              <p className="text-sm text-text-muted">Chưa có bạn bè.</p>
            )}

            <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
              {friends.items.map((friend) => {
                const profile = friendProfiles[friend.userId]
                const isPending = actionId === friend.userId

                return (
                <div
                  key={friend.userId}
                  className="flex items-center justify-between p-3 rounded-lg bg-surface-2/60 border border-border hover:bg-surface-2 transition-colors"
                >
                  <div className="flex items-center gap-3">
                    <div className="w-14 h-14 rounded-lg overflow-hidden flex items-center justify-center font-bold text-white text-lg bg-primary">
                      {profile?.avatarUrl ? (
                        <img src={profile.avatarUrl} alt="" className="w-full h-full object-cover" />
                      ) : getInitials(profile)}
                    </div>
                    <div>
                      <p className="font-semibold text-text text-sm hover:underline cursor-pointer">
                        {getProfileName(profile, friend.userId)}
                      </p>
                      <p className="text-xs text-text-muted">@{profile?.username ?? friend.userId.slice(0, 8)}</p>
                      <p className="text-xs text-text-light mt-0.5">
                        Friends since {new Date(friend.friendsSinceUtc).toLocaleDateString()}
                      </p>
                    </div>
                  </div>
                  <button
                    type="button"
                    onClick={() => void runRelationshipAction(friend.userId, () => friendsApi.unfriend(friend.userId))}
                    disabled={isPending}
                    className="px-3 py-1.5 bg-surface-2 hover:bg-surface-hover disabled:opacity-60 text-text rounded-lg text-xs font-semibold border border-border cursor-pointer transition-colors"
                  >
                    {isPending ? 'Updating...' : 'Unfriend'}
                  </button>
                </div>
                )
              })}
            </div>
            {friends.items.length < friends.total && (
              <button
                type="button"
                onClick={() => void loadMoreFriends()}
                disabled={isLoadingMoreFriends}
                className="rounded-lg bg-surface-2 hover:bg-surface-hover disabled:opacity-60 border border-border py-2.5 text-sm font-semibold text-text cursor-pointer"
              >
                {isLoadingMoreFriends ? 'Loading...' : 'Load more friends'}
              </button>
            )}
          </div>
        )}

        {/* ── Photos Tab ── */}
        {tab === 'photos' && (
          <div className="bg-surface rounded-xl card-shadow border border-border p-6 flex flex-col gap-5">
            <div className="border-b border-border pb-3">
              <h2 className="font-heading font-bold text-xl text-text">Photos</h2>
              <p className="text-sm text-text-muted">All media and photos</p>
            </div>
            <div className="grid grid-cols-2 sm:grid-cols-3 md:grid-cols-4 gap-3">
              {[
                { icon: '💻', title: 'Kernel Exploit PoC' },
                { icon: '⚡', title: 'Assembly Debug' },
                { icon: '🔐', title: 'Cryptographic Hash' },
                { icon: '🌐', title: 'Network Topology' },
                { icon: '🛡️', title: 'Firewall Policy' },
                { icon: '⚙️', title: 'Buffer Analysis' },
                { icon: '📡', title: 'Packet Capture' },
                { icon: '🔍', title: 'Memory Dump' },
              ].map((item, idx) => (
                <div
                  key={idx}
                  className="aspect-square bg-surface-2 rounded-lg border border-border flex flex-col items-center justify-center p-3 gap-2 hover:bg-surface-hover cursor-pointer transition-colors"
                >
                  <span className="text-4xl">{item.icon}</span>
                  <span className="text-xs font-medium text-text-muted text-center">{item.title}</span>
                </div>
              ))}
            </div>
          </div>
        )}
      </div>
    </div>
  )
}
