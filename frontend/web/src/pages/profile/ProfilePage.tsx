import { useCallback, useEffect, useRef, useState } from 'react'
import { Link } from 'react-router-dom'
import { ApiError } from '../../api/client'
import { authApi } from '../../api/auth'
import { friendsApi } from '../../api/friends'
import type { BlockedUser, Friend, FriendRequest, FriendSuggestion, PagedResponse } from '../../api/friends'
import { mediaApi } from '../../api/media'
import { postsApi } from '../../api/posts'
import type { Post as ApiPost } from '../../api/posts'
import { resolveProfileImageUrl, usersApi } from '../../api/users'
import type { UserProfile } from '../../api/users'
import { useAuth } from '../../auth/useAuth'
import { formatNumber } from '../../shared/formatNumber'
import { usePreferences } from '../../preferences'
import PaginationControls from '../../shared/components/PaginationControls'
import LivePostCard from '../feed/components/LivePostCard'
import NewPostBox from '../feed/components/NewPostBox'

type ProfileTab = 'posts' | 'about' | 'friends' | 'photos'

interface ProfilePhoto {
  mediaId: string
  postId: string
  url: string
}

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
  const { session, changePassword } = useAuth()
  const { language, t } = usePreferences()
  const locale = language === 'vi' ? 'vi-VN' : 'en-US'
  const tabs: { id: ProfileTab; label: string }[] = [
    { id: 'posts', label: t('posts') }, { id: 'about', label: t('about') },
    { id: 'friends', label: t('friends') }, { id: 'photos', label: t('photos') },
  ]
  const [tab, setTab] = useState<ProfileTab>('posts')
  const [friends, setFriends] = useState<PagedResponse<Friend>>(emptyFriendPage)
  const [incomingRequests, setIncomingRequests] = useState<FriendRequest[]>([])
  const [outgoingRequests, setOutgoingRequests] = useState<FriendRequest[]>([])
  const [blockedUsers, setBlockedUsers] = useState<BlockedUser[]>([])
  const [friendSuggestions, setFriendSuggestions] = useState<FriendSuggestion[]>([])
  const [friendProfiles, setFriendProfiles] = useState<Record<string, UserProfile>>({})
  const [isRelationshipsLoading, setIsRelationshipsLoading] = useState(true)
  const [isLoadingMoreFriends, setIsLoadingMoreFriends] = useState(false)
  const [isSuggestionsLoading, setIsSuggestionsLoading] = useState(false)
  const [relationshipError, setRelationshipError] = useState<string | null>(null)
  const [suggestionsError, setSuggestionsError] = useState<string | null>(null)
  const [actionId, setActionId] = useState<string | null>(null)
  const [profile, setProfile] = useState<UserProfile | null>(null)
  const [profileError, setProfileError] = useState<string | null>(null)
  const [profilePosts, setProfilePosts] = useState<ApiPost[]>([])
  const [profilePostsTotal, setProfilePostsTotal] = useState(0)
  const [photos, setPhotos] = useState<ProfilePhoto[]>([])
  const [isPhotosLoading, setIsPhotosLoading] = useState(true)
  const [isProfilePostsLoading, setIsProfilePostsLoading] = useState(true)
  const [isLoadingMoreProfilePosts, setIsLoadingMoreProfilePosts] = useState(false)
  const [profilePostsPageError, setProfilePostsPageError] = useState<string | null>(null)
  const [loadMoreFriendsError, setLoadMoreFriendsError] = useState<string | null>(null)
  const [isProfileEditing, setIsProfileEditing] = useState(false)
  const [isAccountSecurityOpen, setIsAccountSecurityOpen] = useState(false)
  const [displayNameDraft, setDisplayNameDraft] = useState('')
  const [bioDraft, setBioDraft] = useState('')
  const [cityDraft, setCityDraft] = useState('')
  const [hometownDraft, setHometownDraft] = useState('')
  const [workplaceDraft, setWorkplaceDraft] = useState('')
  const [educationDraft, setEducationDraft] = useState('')
  const [websiteDraft, setWebsiteDraft] = useState('')
  const [birthdayDraft, setBirthdayDraft] = useState('')
  const [birthdayVisibilityDraft, setBirthdayVisibilityDraft] = useState('0')
  const [profileMediaUpload, setProfileMediaUpload] = useState<{ kind: 'avatar' | 'cover'; progress: number } | null>(null)
  const [currentPasswordDraft, setCurrentPasswordDraft] = useState('')
  const [newPasswordDraft, setNewPasswordDraft] = useState('')
  const [confirmPasswordDraft, setConfirmPasswordDraft] = useState('')
  const [accountSecurityError, setAccountSecurityError] = useState<string | null>(null)
  const [accountSecurityNotice, setAccountSecurityNotice] = useState<string | null>(null)
  const [isChangingPassword, setIsChangingPassword] = useState(false)
  const [isResendingVerification, setIsResendingVerification] = useState(false)
  const avatarInputRef = useRef<HTMLInputElement>(null)
  const coverInputRef = useRef<HTMLInputElement>(null)
  const isMountedRef = useRef(false)
  const suggestionRequestGenerationRef = useRef(0)
  const suggestionAbortControllerRef = useRef<AbortController | null>(null)
  const displayName = profile?.displayName ?? ''
  const initials = displayName.slice(0, 2).toUpperCase() || '?'
  const bio = profile?.bio ?? ''
  const location = profile?.currentCity ?? t('notSet')
  const joinedDate = profile ? new Date(profile.createdAt).toLocaleDateString(locale) : '—'

  const loadProfilePosts = useCallback(async (
    userId: string,
    offset = 0,
    append = false,
  ) => {
    if (append) {
      setIsLoadingMoreProfilePosts(true)
      setProfilePostsPageError(null)
    } else {
      setIsProfilePostsLoading(true)
      setProfilePostsPageError(null)
    }

    try {
      const page = await postsApi.getByUser(userId, offset)
      setProfilePosts((currentPosts) => append
        ? [...currentPosts, ...page.items.filter((post) => !currentPosts.some((item) => item.id === post.id))]
        : page.items)
      setProfilePostsTotal(page.total)
    } catch (error) {
      setProfilePostsPageError(error instanceof ApiError ? error.message : t('unableLoadPosts'))
    } finally {
      if (append) {
        setIsLoadingMoreProfilePosts(false)
      } else {
        setIsProfilePostsLoading(false)
      }
    }
  }, [t])

  const loadProfilePhotos = useCallback(async (userId: string) => {
    setIsPhotosLoading(true)

    try {
      const page = await postsApi.getByUser(userId, 0, 100)
      const mediaItems = page.items.flatMap((post) => post.mediaIds.map((mediaId) => ({ postId: post.id, mediaId })))
      const results = await Promise.allSettled(
        mediaItems.map((item) => postsApi.getMediaAccess(item.postId, item.mediaId)),
      )
      setPhotos(results.flatMap((result, index) => (
        result.status === 'fulfilled' && result.value.mediaType === 'image'
          ? [{ ...mediaItems[index], url: result.value.url }]
          : []
      )))
    } catch {
      setPhotos([])
    } finally {
      setIsPhotosLoading(false)
    }
  }, [])

  const loadCurrentProfile = useCallback(async () => {
    try {
      const currentProfile = await usersApi.getCurrent()
      setProfile(currentProfile)
      await Promise.all([
        loadProfilePosts(currentProfile.userId),
        loadProfilePhotos(currentProfile.userId),
      ])
    } catch (error) {
      setProfileError(error instanceof ApiError ? error.message : t('unableLoadProfile'))
      setIsProfilePostsLoading(false)
    }
  }, [loadProfilePhotos, loadProfilePosts, t])

  const loadRelationships = useCallback(async (showLoading = true) => {
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
        error instanceof ApiError ? error.message : t('unableLoadFriends'),
      )
    } finally {
      setIsRelationshipsLoading(false)
    }
  }, [t])

  const cancelPendingSuggestionLoad = useCallback(() => {
    suggestionRequestGenerationRef.current += 1
    suggestionAbortControllerRef.current?.abort()
    suggestionAbortControllerRef.current = null
    if (isMountedRef.current) setIsSuggestionsLoading(false)
  }, [])

  const loadFriendSuggestions = useCallback(async () => {
    cancelPendingSuggestionLoad()
    const requestGeneration = ++suggestionRequestGenerationRef.current
    const controller = new AbortController()
    suggestionAbortControllerRef.current = controller
    if (isMountedRef.current) {
      setIsSuggestionsLoading(true)
      setSuggestionsError(null)
    }

    try {
      const page = await friendsApi.getSuggestions(undefined, 12, { signal: controller.signal })
      if (isMountedRef.current && requestGeneration === suggestionRequestGenerationRef.current) {
        setFriendSuggestions(page.items)
      }
    } catch (error) {
      if (isMountedRef.current && requestGeneration === suggestionRequestGenerationRef.current && !controller.signal.aborted) {
        setSuggestionsError(error instanceof ApiError ? error.message : t('unableLoadFriends'))
      }
    } finally {
      if (isMountedRef.current && requestGeneration === suggestionRequestGenerationRef.current) {
        suggestionAbortControllerRef.current = null
        setIsSuggestionsLoading(false)
      }
    }
  }, [cancelPendingSuggestionLoad, t])

  useEffect(() => {
    isMountedRef.current = true

    return () => {
      isMountedRef.current = false
      suggestionRequestGenerationRef.current += 1
      suggestionAbortControllerRef.current?.abort()
      suggestionAbortControllerRef.current = null
    }
  }, [])

  useEffect(() => {
    const timeoutId = window.setTimeout(() => {
      void loadRelationships(false)
    }, 0)

    return () => window.clearTimeout(timeoutId)
  }, [loadRelationships])

  useEffect(() => {
    const timeoutId = window.setTimeout(() => {
      void loadCurrentProfile()
    }, 0)

    return () => window.clearTimeout(timeoutId)
  }, [loadCurrentProfile])

  useEffect(() => {
    if (tab !== 'friends') return

    const timeoutId = window.setTimeout(() => {
      void loadFriendSuggestions()
    }, 0)

    return () => window.clearTimeout(timeoutId)
  }, [loadFriendSuggestions, tab])

  const runRelationshipAction = async (id: string, action: () => Promise<unknown>) => {
    setActionId(id)
    setRelationshipError(null)

    try {
      await action()
      await loadRelationships()
    } catch (error) {
      setRelationshipError(
        error instanceof ApiError ? error.message : t('unableUpdateRelationship'),
      )
    } finally {
      setActionId(null)
    }
  }

  const addSuggestedFriend = async (suggestion: FriendSuggestion) => {
    const id = `suggestion-friend-${suggestion.profile.userId}`
    cancelPendingSuggestionLoad()
    setActionId(id)
    setRelationshipError(null)
    setSuggestionsError(null)

    try {
      await friendsApi.sendRequest(suggestion.profile.userId)
      if (isMountedRef.current) {
        cancelPendingSuggestionLoad()
        setFriendSuggestions((current) => current.filter((item) => item.profile.userId !== suggestion.profile.userId))
      }
    } catch (error) {
      if (isMountedRef.current) setRelationshipError(error instanceof ApiError ? error.message : t('unableUpdateRelationship'))
    } finally {
      if (isMountedRef.current) setActionId(null)
    }
  }

  const toggleSuggestedFollow = async (suggestion: FriendSuggestion) => {
    const id = `suggestion-follow-${suggestion.profile.userId}`
    cancelPendingSuggestionLoad()
    setActionId(id)
    setRelationshipError(null)
    setSuggestionsError(null)

    try {
      if (suggestion.isFollowing) {
        await usersApi.unfollow(suggestion.profile.userId)
      } else {
        await usersApi.follow(suggestion.profile.userId)
      }
      if (isMountedRef.current) {
        cancelPendingSuggestionLoad()
        setFriendSuggestions((current) => current.map((item) => item.profile.userId === suggestion.profile.userId
          ? { ...item, isFollowing: !item.isFollowing }
          : item))
      }
    } catch (error) {
      if (isMountedRef.current) setRelationshipError(error instanceof ApiError ? error.message : t('unableUpdateRelationship'))
    } finally {
      if (isMountedRef.current) setActionId(null)
    }
  }

  const removeSuggestedFriend = (userId: string) => {
    cancelPendingSuggestionLoad()
    setSuggestionsError(null)
    setFriendSuggestions((current) => current.filter((item) => item.profile.userId !== userId))
  }

  const loadMoreFriends = async () => {
    setIsLoadingMoreFriends(true)
    setLoadMoreFriendsError(null)

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
      setLoadMoreFriendsError(error instanceof ApiError ? error.message : t('unableLoadMoreFriends'))
    } finally {
      setIsLoadingMoreFriends(false)
    }
  }

  const openProfileEditor = () => {
    setDisplayNameDraft(profile?.displayName ?? '')
    setBioDraft(profile?.bio ?? '')
    setCityDraft(profile?.currentCity ?? '')
    setHometownDraft(profile?.hometown ?? '')
    setWorkplaceDraft(profile?.workplace ?? '')
    setEducationDraft(profile?.education ?? '')
    setWebsiteDraft(profile?.website ?? '')
    setBirthdayDraft(profile?.dateOfBirth ?? '')
    setBirthdayVisibilityDraft(profile?.birthdayVisibility === 'friends' ? '1' : profile?.birthdayVisibility === 'public' ? '2' : '0')
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
        hometown: hometownDraft,
        workplace: workplaceDraft,
        education: educationDraft,
        website: websiteDraft,
        dateOfBirth: birthdayDraft || null,
        birthdayVisibility: Number(birthdayVisibilityDraft),
      }))
      setIsProfileEditing(false)
    } catch (error) {
      setProfileError(error instanceof ApiError ? error.message : t('unableUpdateProfile'))
    }
  }

  const createProfilePost = async (
    content: string,
    files: readonly File[],
    onUploadProgress: (progress: number) => void,
  ) => {
    const mediaIds = await mediaApi.uploadFiles(files, onUploadProgress)
    const post = await postsApi.create({ content, privacy: 'public', mediaIds })
    setProfilePosts((currentPosts) => [post, ...currentPosts])
    setProfilePostsTotal((currentTotal) => currentTotal + 1)
    if (profile) void loadProfilePhotos(profile.userId)
  }

  const uploadProfileMedia = async (kind: 'avatar' | 'cover', file: File) => {
    const allowedTypes = ['image/jpeg', 'image/png', 'image/webp']
    if (!allowedTypes.includes(file.type) || file.size > 20 * 1024 * 1024) {
      setProfileError(t('invalidProfilePhoto'))
      return
    }

    setProfileError(null)
    setProfileMediaUpload({ kind, progress: 0 })

    try {
      const mediaId = await mediaApi.uploadFile(file, (progress) => {
        setProfileMediaUpload({ kind, progress })
      })
      const updatedProfile = await usersApi.updateCurrent(
        kind === 'avatar' ? { avatarMediaId: mediaId } : { coverMediaId: mediaId },
      )
      setProfile(updatedProfile)
      await Promise.all([
        loadProfilePosts(updatedProfile.userId),
        loadProfilePhotos(updatedProfile.userId),
      ])
    } catch (error) {
      setProfileError(error instanceof ApiError ? error.message : t('unableUpdateProfilePhoto'))
    } finally {
      setProfileMediaUpload(null)
    }
  }

  const selectProfileMedia = (kind: 'avatar' | 'cover', event: React.ChangeEvent<HTMLInputElement>) => {
    const file = event.target.files?.[0]
    event.target.value = ''
    if (file) void uploadProfileMedia(kind, file)
  }

  const updatePassword = async (event: React.FormEvent<HTMLFormElement>) => {
    event.preventDefault()
    setAccountSecurityError(null)
    setAccountSecurityNotice(null)
    setIsChangingPassword(true)

    try {
      await changePassword({
        currentPassword: currentPasswordDraft,
        newPassword: newPasswordDraft,
        confirmPassword: confirmPasswordDraft,
      })
      setCurrentPasswordDraft('')
      setNewPasswordDraft('')
      setConfirmPasswordDraft('')
      setAccountSecurityNotice(t('passwordChanged'))
    } catch (error) {
      setAccountSecurityError(error instanceof ApiError ? error.message : t('unableChangePassword'))
    } finally {
      setIsChangingPassword(false)
    }
  }

  const resendVerificationEmail = async () => {
    setAccountSecurityError(null)
    setAccountSecurityNotice(null)
    setIsResendingVerification(true)

    try {
      await authApi.resendEmailVerification()
      setAccountSecurityNotice(t('verificationResent'))
    } catch (error) {
      setAccountSecurityError(error instanceof ApiError ? error.message : t('unableResendVerification'))
    } finally {
      setIsResendingVerification(false)
    }
  }

  return (
    <div className="min-h-screen bg-bg" style={{ animation: 'fade-in 0.25s ease both' }}>
      <input ref={avatarInputRef} type="file" accept="image/jpeg,image/png,image/webp" className="hidden" onChange={(event) => selectProfileMedia('avatar', event)} />
      <input ref={coverInputRef} type="file" accept="image/jpeg,image/png,image/webp" className="hidden" onChange={(event) => selectProfileMedia('cover', event)} />
      {/* ── Top Section: Cover + Header + Tabs ─────────────────────── */}
      <div className="bg-surface border-b border-border shadow-sm">
        {/* 1. Cover photo area: Full-width dark gradient banner */}
        <div className="relative mx-auto h-[280px] w-full max-w-[1120px] sm:h-[320px] md:h-[380px]">
          <div className="absolute inset-0 overflow-hidden rounded-b-2xl bg-gradient-to-b from-surface-3 via-surface-2 to-surface-3">
            {profile?.coverUrl && <img src={resolveProfileImageUrl(profile.coverUrl)} alt="" className="absolute inset-0 h-full w-full object-cover" />}
            {/* Subtle dark texture overlay */}
            <div className="absolute inset-0 opacity-20 bg-[radial-gradient(#3e4042_1px,transparent_1px)] [background-size:16px_16px]" />
            <div className="absolute inset-0 bg-gradient-to-t from-surface/50 via-transparent to-transparent" />
          </div>

          {/* Edit cover photo button */}
          <button
            type="button"
            onClick={() => coverInputRef.current?.click()}
            disabled={profileMediaUpload !== null}
            className="absolute right-4 sm:right-8 bottom-4 px-3.5 py-1.5 rounded-lg bg-surface/85 hover:bg-surface
                       text-text text-[13px] font-semibold flex items-center gap-2
                       border border-border/60 transition-colors cursor-pointer shadow-md backdrop-blur-sm z-10 disabled:opacity-60"
          >
            <span>📷</span>
            <span className="hidden sm:inline">{profileMediaUpload?.kind === 'cover' ? `${t('uploading')} ${profileMediaUpload.progress}%` : t('editCoverPhoto')}</span>
          </button>
        </div>

        {/* 3. Below cover: Profile info section */}
        <div className="mx-auto max-w-[1120px] px-4 sm:px-8">
          <div className="flex flex-col items-center gap-4 pb-4 pt-4 md:flex-row md:items-center">
            <div className="relative shrink-0 group">
              <div className="w-[168px] h-[168px] rounded-full flex items-center justify-center text-5xl font-bold text-white border-4 border-surface shadow-2xl bg-primary">
                {profile?.avatarUrl ? <img src={resolveProfileImageUrl(profile.avatarUrl)} alt="" className="w-full h-full rounded-full object-cover" /> : initials}
              </div>
              <button
                type="button"
                title={t('updateProfilePicture')}
                onClick={() => avatarInputRef.current?.click()}
                disabled={profileMediaUpload !== null}
                className="absolute bottom-2 right-2 w-9 h-9 rounded-full bg-surface-2 hover:bg-surface-hover flex items-center justify-center text-text border border-border cursor-pointer shadow-md transition-colors text-sm disabled:opacity-60"
              >
                {profileMediaUpload?.kind === 'avatar' ? `${profileMediaUpload.progress}%` : '📷'}
              </button>
            </div>

            <div className="min-w-0 flex-1">
              <div className="flex flex-col gap-4 xl:flex-row xl:items-end xl:justify-between">
                {/* User Details */}
                <div className="flex flex-col items-center md:items-start text-center md:text-left">
              {/* Name & Badges */}
              <div className="flex flex-wrap items-center justify-center md:justify-start gap-2.5">
                <h1 className="font-heading font-bold text-2xl sm:text-3xl text-text leading-tight">
                  {displayName}
                </h1>
              </div>

              {/* Bio text */}
              <p className="text-[14px] text-text mt-2 max-w-xl leading-relaxed whitespace-pre-line">
                {bio}
              </p>

              {/* Stats row: followers, following, posts - inline with dot separators */}
              <div className="flex flex-wrap items-center justify-center md:justify-start gap-2 text-[14px] text-text-muted mt-2.5">
                <span>
                  <strong className="font-semibold text-text">{formatNumber(friends.total)}</strong> {t('friendsCount')}
                </span>
                <span className="text-text-light font-bold">•</span>
                <span>
                  <strong className="font-semibold text-text">{formatNumber(profilePostsTotal)}</strong> {t('posts').toLowerCase()}
                </span>
              </div>
              {(profile?.currentCity || profile?.education || profile?.workplace) && <p className="mt-2 flex flex-wrap items-center justify-center gap-x-2 gap-y-1 text-sm text-text-muted md:justify-start"><span>{profile.currentCity ? `⌖ ${profile.currentCity}` : null}</span>{profile.education && <span>· {profile.education}</span>}{profile.workplace && <span>· {profile.workplace}</span>}</p>}
                </div>

                {/* Action buttons */}
                <div className="flex flex-wrap items-center justify-center md:justify-start gap-2.5 shrink-0 xl:mt-0">
              <button
                type="button"
                onClick={() => setTab('posts')}
                className="px-4 py-2 bg-primary hover:bg-primary-dark text-white rounded-lg font-semibold text-sm flex items-center gap-1.5 transition-colors cursor-pointer border-none shadow-sm"
              >
                <span>＋</span>
                <span>Thêm vào tin</span>
              </button>

              {/* Edit profile button: bg-surface-2 text-text rounded-lg, not a pill button */}
              <button
                type="button"
                onClick={openProfileEditor}
                className="px-4 py-2 bg-surface-2 hover:bg-surface-hover text-text rounded-lg font-semibold text-sm flex items-center gap-1.5 transition-colors cursor-pointer border border-border"
              >
                <span>✎</span>
                <span>{t('editProfile')}</span>
              </button>

              {/* More options button */}
              <button
                type="button"
                title={t('accountSecurity')}
                onClick={() => setIsAccountSecurityOpen((current) => !current)}
                className="h-9 bg-surface-2 hover:bg-surface-hover text-text rounded-lg font-semibold text-sm flex items-center justify-center px-3 transition-colors cursor-pointer border border-border"
              >
                <span>⌄</span>
              </button>
                </div>
              </div>
            </div>
          </div>

          {profileError && <p className="mb-3 rounded-lg bg-[#e41e3f]/10 border border-[#e41e3f]/40 px-3 py-2 text-sm text-[#ff8a9b]">{profileError}</p>}
          {isProfileEditing && (
            <form onSubmit={(event) => void saveProfile(event)} className="mb-4 grid grid-cols-1 sm:grid-cols-2 gap-3 rounded-xl border border-border bg-surface-2/60 p-4">
              <label className="flex flex-col gap-1 text-sm text-text">{t('displayName')}
                <input value={displayNameDraft} onChange={(event) => setDisplayNameDraft(event.target.value)} required className="rounded-lg border border-border bg-surface px-3 py-2 text-text outline-none" />
              </label>
              <label className="flex flex-col gap-1 text-sm text-text">{t('currentCity')}
                <input value={cityDraft} onChange={(event) => setCityDraft(event.target.value)} className="rounded-lg border border-border bg-surface px-3 py-2 text-text outline-none" />
              </label>
              <label className="flex flex-col gap-1 text-sm text-text">Quê quán<input value={hometownDraft} onChange={(event) => setHometownDraft(event.target.value)} className="rounded-lg border border-border bg-surface px-3 py-2 text-text outline-none" /></label>
              <label className="flex flex-col gap-1 text-sm text-text">Nơi làm việc<input value={workplaceDraft} onChange={(event) => setWorkplaceDraft(event.target.value)} className="rounded-lg border border-border bg-surface px-3 py-2 text-text outline-none" /></label>
              <label className="flex flex-col gap-1 text-sm text-text">Học vấn<input value={educationDraft} onChange={(event) => setEducationDraft(event.target.value)} className="rounded-lg border border-border bg-surface px-3 py-2 text-text outline-none" /></label>
              <label className="flex flex-col gap-1 text-sm text-text">Website<input type="url" value={websiteDraft} onChange={(event) => setWebsiteDraft(event.target.value)} className="rounded-lg border border-border bg-surface px-3 py-2 text-text outline-none" /></label>
              <label className="flex flex-col gap-1 text-sm text-text">Ngày sinh<input type="date" value={birthdayDraft} onChange={(event) => setBirthdayDraft(event.target.value)} className="rounded-lg border border-border bg-surface px-3 py-2 text-text outline-none" /></label>
              <label className="flex flex-col gap-1 text-sm text-text">Hiển thị ngày sinh<select value={birthdayVisibilityDraft} onChange={(event) => setBirthdayVisibilityDraft(event.target.value)} className="rounded-lg border border-border bg-surface px-3 py-2 text-text outline-none"><option value="0">Chỉ mình tôi</option><option value="1">Bạn bè</option><option value="2">Công khai</option></select></label>
              <label className="sm:col-span-2 flex flex-col gap-1 text-sm text-text">{t('bio')}
                <textarea value={bioDraft} onChange={(event) => setBioDraft(event.target.value)} rows={3} className="rounded-lg border border-border bg-surface px-3 py-2 text-text outline-none resize-y" />
              </label>
              <div className="sm:col-span-2 flex justify-end gap-2">
                <button type="button" onClick={() => setIsProfileEditing(false)} className="px-3 py-2 rounded-lg bg-surface hover:bg-surface-hover border border-border text-text text-sm cursor-pointer">{t('cancel')}</button>
                <button className="px-3 py-2 rounded-lg bg-primary hover:bg-primary-dark border-none text-white text-sm cursor-pointer">{t('saveProfile')}</button>
              </div>
            </form>
          )}

          {isAccountSecurityOpen && (
            <section className="mb-4 rounded-xl border border-border bg-surface-2/60 p-4">
              <div className="flex flex-wrap items-start justify-between gap-3">
                <div>
                  <h2 className="font-heading text-lg font-bold text-text">{t('accountSecurity')}</h2>
                  <p className="mt-1 text-sm text-text-muted">{t('managePasswordSecurity')}</p>
                </div>
                {!session!.user.emailConfirmed && (
                  <button type="button" onClick={() => void resendVerificationEmail()} disabled={isResendingVerification} className="rounded-lg border border-primary/40 bg-primary/10 px-3 py-2 text-sm font-semibold text-primary-light disabled:opacity-60">
                    {isResendingVerification ? t('sending') : t('resendVerificationEmail')}
                  </button>
                )}
              </div>

              {!session!.user.emailConfirmed && <p className="mt-3 rounded-lg border border-[#e7b65b]/35 bg-[#e7b65b]/10 px-3 py-2 text-sm text-[#f4cf86]">{t('emailNotVerified')}</p>}
              {accountSecurityError && <p role="alert" className="mt-3 rounded-lg border border-[#e41e3f]/40 bg-[#e41e3f]/10 px-3 py-2 text-sm text-[#ff8a9b]">{accountSecurityError}</p>}
              {accountSecurityNotice && <p role="status" className="mt-3 rounded-lg border border-primary/35 bg-primary/10 px-3 py-2 text-sm text-primary-light">{accountSecurityNotice}</p>}

              <form onSubmit={(event) => void updatePassword(event)} className="mt-4 grid grid-cols-1 gap-3 sm:grid-cols-3">
                <label className="flex flex-col gap-1 text-sm text-text">{t('currentPassword')}
                  <input required autoComplete="current-password" type="password" value={currentPasswordDraft} onChange={(event) => setCurrentPasswordDraft(event.target.value)} className="rounded-lg border border-border bg-surface px-3 py-2 text-text outline-none" />
                </label>
                <label className="flex flex-col gap-1 text-sm text-text">{t('newPassword')}
                  <input required minLength={8} autoComplete="new-password" type="password" value={newPasswordDraft} onChange={(event) => setNewPasswordDraft(event.target.value)} className="rounded-lg border border-border bg-surface px-3 py-2 text-text outline-none" />
                </label>
                <label className="flex flex-col gap-1 text-sm text-text">{t('confirmNewPassword')}
                  <input required minLength={8} autoComplete="new-password" type="password" value={confirmPasswordDraft} onChange={(event) => setConfirmPasswordDraft(event.target.value)} className="rounded-lg border border-border bg-surface px-3 py-2 text-text outline-none" />
                </label>
                <div className="sm:col-span-3 flex justify-end">
                  <button disabled={isChangingPassword} className="rounded-lg bg-primary px-3 py-2 text-sm font-semibold text-white disabled:opacity-60">{isChangingPassword ? t('saving') : t('changePassword')}</button>
                </div>
              </form>
            </section>
          )}

          {/* 4. Tabs below: Posts | About | Friends | Photos */}
          <div className="border-t border-border mt-2" />
          <div className="flex items-center gap-1 overflow-x-auto scroll-smooth pt-1">
            {tabs.map((item) => {
              const isActive = tab === item.id
              return (
                <button
                  key={item.id}
                  type="button"
                  onClick={() => setTab(item.id)}
                  className={[
                    'px-4 py-3.5 text-[15px] font-semibold transition-all duration-150 cursor-pointer relative border-none bg-transparent whitespace-nowrap',
                    isActive
                      ? 'text-primary'
                      : 'text-text-muted hover:text-text hover:bg-surface-2/60 rounded-lg',
                  ].join(' ')}
                >
                  {item.label}
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
      <div className="mx-auto max-w-[1120px] px-4 py-5 sm:px-8">
        {tab === 'posts' && (
          <div className="grid grid-cols-1 lg:grid-cols-12 gap-4 items-start">
            {/* ── Left column (posts) ── */}
            <div className="order-2 lg:order-2 lg:col-span-7 xl:col-span-7 2xl:col-span-8 flex flex-col gap-4">
              <NewPostBox onPost={createProfilePost} />

              {/* Manage posts header */}
              <div className="bg-surface rounded-xl card-shadow border border-border p-3.5 px-4 flex items-center justify-between">
                <h2 className="font-heading font-bold text-[17px] text-text">{t('posts')}</h2>
                <div className="flex items-center gap-2">
                  <button
                    type="button"
                    className="px-3 py-1.5 bg-surface-2 hover:bg-surface-hover text-text text-xs font-semibold rounded-lg transition-colors cursor-pointer border-none flex items-center gap-1.5"
                  >
                    <span>⚙️</span>
                    <span>{t('managePosts')}</span>
                  </button>
                </div>
              </div>

              <div className="flex flex-col gap-3">
                {isProfilePostsLoading && <p className="text-sm text-text-muted">{t('loadingPosts')}</p>}
                {!isProfilePostsLoading && profilePosts.length === 0 && <p className="text-sm text-text-muted">{t('noPostsYet')}</p>}
                {profile && profilePosts.map((post) => (
                  <LivePostCard
                    key={post.id}
                    post={post}
                    author={profile}
                    currentUserId={session!.user.id}
                    allowProfilePin
                    onPostUpdated={(updatedPost) => setProfilePosts((currentPosts) => currentPosts
                      .map((item) => item.id === updatedPost.id
                        ? updatedPost
                        : updatedPost.isPinned && item.authorUserId === updatedPost.authorUserId
                          ? { ...item, isPinned: false }
                          : item)
                      .sort((left, right) => Number(right.isPinned) - Number(left.isPinned)))}
                    onPostDeleted={(postId) => {
                      setProfilePosts((currentPosts) => currentPosts.filter((item) => item.id !== postId))
                      setProfilePostsTotal((currentTotal) => Math.max(0, currentTotal - 1))
                    }}
                  />
                ))}
                {profile && <PaginationControls hasMore={profilePosts.length < profilePostsTotal} isLoading={isLoadingMoreProfilePosts} error={profilePostsPageError} label={t('loadMorePosts')} onLoadMore={() => void loadProfilePosts(profile.userId, profilePosts.length, true)} />}
              </div>
            </div>

            {/* ── Right column (Intro card with bio, location, join date) ── */}
            <div className="order-1 lg:order-1 lg:col-span-5 xl:col-span-5 2xl:col-span-4 flex flex-col gap-4">
              {/* 6. Intro Card */}
              <div className="bg-surface rounded-xl card-shadow border border-border p-4 flex flex-col gap-3.5">
                <h2 className="font-heading font-bold text-[18px] text-text">{t('intro')}</h2>

                {/* Bio text */}
                <p className="text-[14px] text-text text-center py-0.5 leading-relaxed whitespace-pre-wrap">
                  {bio || t('noBioYet')}
                </p>
                <button
                  type="button"
                  onClick={openProfileEditor}
                  className="w-full py-2 px-3 bg-surface-2 hover:bg-surface-hover text-text text-sm font-semibold rounded-lg transition-colors cursor-pointer border-none"
                >
                  {t('editBio')}
                </button>

                <div className="border-t border-border my-0.5" />

                {/* Details */}
                <div className="flex flex-col gap-3 text-sm">
                  <div className="flex items-center gap-3 text-text">
                    <span className="text-text-muted text-base shrink-0">📍</span>
                    <span>{t('livesIn')} <strong className="font-semibold text-text">{location}</strong></span>
                  </div>
                  <div className="flex items-center gap-3 text-text">
                    <span className="text-text-muted text-base shrink-0">📅</span>
                    <span>{t('joined')} <strong className="font-semibold text-text">{joinedDate}</strong></span>
                  </div>
                  <div className="flex items-center gap-3 text-text">
                    <span className="text-text-muted text-base shrink-0">👥</span>
                    <span><strong className="font-semibold text-text">{formatNumber(friends.total)}</strong> {t('friendsCount')}</span>
                  </div>
                </div>

                <button
                  type="button"
                  onClick={openProfileEditor}
                  className="w-full py-2 px-3 bg-surface-2 hover:bg-surface-hover text-text text-sm font-semibold rounded-lg transition-colors cursor-pointer border-none mt-1"
                >
                  {t('editDetails')}
                </button>
              </div>

              {/* Photos Preview Card */}
              <div className="bg-surface rounded-xl card-shadow border border-border p-4 flex flex-col gap-3">
                <div className="flex items-center justify-between">
                  <h2 className="font-heading font-bold text-[18px] text-text">{t('photos')}</h2>
                  <button
                    type="button"
                    onClick={() => setTab('photos')}
                    className="text-primary hover:underline text-sm font-medium cursor-pointer border-none bg-transparent"
                  >
                    {t('seeAllPhotos')}
                  </button>
                </div>
                {isPhotosLoading ? <p className="text-sm text-text-muted">{t('loadingPhotos')}</p> : photos.length === 0 ? <p className="text-sm text-text-muted">{t('noPhotosPosted')}</p> : (
                  <div className="grid grid-cols-3 gap-2">
                    {photos.slice(0, 6).map((photo) => <img key={photo.mediaId} src={photo.url} alt="" className="aspect-square w-full rounded-lg object-cover" />)}
                  </div>
                )}
              </div>

              {/* Friends Preview Card */}
              <div className="bg-surface rounded-xl card-shadow border border-border p-4 flex flex-col gap-3">
                <div className="flex items-center justify-between">
                  <div>
                    <h2 className="font-heading font-bold text-[18px] text-text">{t('friends')}</h2>
                    <p className="text-xs text-text-muted">{formatNumber(friends.total)} {t('friendsCount')}</p>
                  </div>
                  <button
                    type="button"
                    onClick={() => setTab('friends')}
                    className="text-primary hover:underline text-sm font-medium cursor-pointer border-none bg-transparent"
                  >
                    {t('seeAllFriends')}
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
                              src={resolveProfileImageUrl(profile.avatarUrl)}
                              alt=""
                              className="w-full h-full object-cover"
                            />
                          ) : getInitials(profile)}
                        </div>
                        <Link to={`/profile/${friend.userId}`} className="text-[12px] font-medium text-text truncate w-full group-hover:underline no-underline">
                          {getProfileName(profile, friend.userId)}
                        </Link>
                      </div>
                    )
                  })}
                  {!isRelationshipsLoading && friends.items.length === 0 && !relationshipError && (
                    <p className="col-span-3 text-xs text-text-muted">{t('noFriendsYet')}</p>
                  )}
                </div>
              </div>
            </div>
          </div>
        )}

        {/* ── About Tab ── */}
        {tab === 'about' && (
          <div className="bg-surface rounded-xl card-shadow border border-border p-6 flex flex-col gap-6">
            <h2 className="font-heading font-bold text-xl text-text border-b border-border pb-3">{t('about')}</h2>
            <div className="grid grid-cols-1 md:grid-cols-2 gap-6">
              <div className="flex flex-col gap-4">
                <h3 className="font-semibold text-text-muted text-xs uppercase tracking-wider">{t('overview')}</h3>
                <div className="flex items-start gap-3">
                  <span className="text-xl">📍</span>
                  <div>
                    <p className="text-sm font-semibold text-text">{t('livesIn')} {location}</p>
                    <p className="text-xs text-text-muted">{t('currentCity')}</p>
                  </div>
                </div>
                {profile?.hometown && <p className="text-sm text-text">🏠 Đến từ {profile.hometown}</p>}
                {profile?.workplace && <p className="text-sm text-text">💼 Làm việc tại {profile.workplace}</p>}
                {profile?.education && <p className="text-sm text-text">🎓 Học tại {profile.education}</p>}
                {profile?.website && <a href={profile.website} target="_blank" rel="noopener noreferrer" className="text-sm text-primary">🔗 {profile.website}</a>}
                {profile?.birthday && <p className="text-sm text-text">🎂 {profile.birthday.day}/{profile.birthday.month}</p>}
                <div className="flex items-start gap-3">
                  <span className="text-xl">📅</span>
                  <div>
                    <p className="text-sm font-semibold text-text">{t('joined')} {joinedDate}</p>
                    <p className="text-xs text-text-muted">{t('memberSince')}</p>
                  </div>
                </div>
              </div>
              <div className="flex flex-col gap-4">
                <h3 className="font-semibold text-text-muted text-xs uppercase tracking-wider">{t('bio')}</h3>
                <p className="text-sm text-text leading-relaxed whitespace-pre-line bg-surface-2 p-3.5 rounded-lg border border-border">
                  {bio || t('noBioYet')}
                </p>
              </div>
            </div>
          </div>
        )}

        {/* ── Friends Tab ── */}
        {tab === 'friends' && (
          <div className="bg-surface rounded-xl card-shadow border border-border p-6 flex flex-col gap-5">
            <div className="flex items-center justify-between border-b border-border pb-3">
              <div>
                <h2 className="font-heading font-bold text-xl text-text">{t('friends')}</h2>
                <p className="text-sm text-text-muted">{formatNumber(friends.total)} {t('friendsCount')}</p>
              </div>
              <button
                type="button"
                onClick={() => {
                  void loadRelationships()
                  void loadFriendSuggestions()
                }}
                disabled={isRelationshipsLoading || isSuggestionsLoading}
                className="px-3 py-1.5 bg-surface-2 hover:bg-surface-hover disabled:opacity-60 text-text rounded-lg text-xs font-semibold border border-border cursor-pointer transition-colors"
              >
                {isRelationshipsLoading || isSuggestionsLoading ? t('loading') : t('refresh')}
              </button>
            </div>

            {relationshipError && (
              <div className="rounded-lg border border-[#e41e3f]/40 bg-[#e41e3f]/10 px-3 py-2 text-sm text-[#ff8a9b]">
                {relationshipError}
              </div>
            )}

            <section className="flex flex-col gap-3 border-b border-border pb-5">
              <div>
                <h3 className="font-heading font-bold text-lg text-text">{t('peopleYouMayKnow')}</h3>
                <p className="text-sm text-text-muted">{t('peopleYouMayKnowDescription')}</p>
              </div>
              {suggestionsError ? (
                <div className="flex items-center justify-between gap-3 rounded-lg border border-[#e41e3f]/40 bg-[#e41e3f]/10 px-3 py-2 text-sm text-[#ff8a9b]">
                  <span>{suggestionsError}</span>
                  <button type="button" onClick={() => void loadFriendSuggestions()} disabled={isSuggestionsLoading} className="shrink-0 border-0 bg-transparent text-xs font-semibold text-primary hover:underline disabled:opacity-60">{t('refresh')}</button>
                </div>
              ) : isSuggestionsLoading ? <p className="text-sm text-text-muted">{t('loading')}</p> : friendSuggestions.length === 0 ? <p className="text-sm text-text-muted">{t('noSuggestionsYet')}</p> : (
                <div className="grid grid-cols-1 gap-3 sm:grid-cols-2">
                  {friendSuggestions.map((suggestion) => {
                    const { profile: suggestionProfile } = suggestion
                    const isAdding = actionId === `suggestion-friend-${suggestionProfile.userId}`
                    const isFollowing = actionId === `suggestion-follow-${suggestionProfile.userId}`

                    return <article key={suggestionProfile.userId} className="flex gap-3 rounded-xl border border-border bg-surface-2/60 p-3">
                      <Link to={`/profile/${suggestionProfile.userId}`} className="h-12 w-12 shrink-0 overflow-hidden rounded-full bg-primary text-center leading-[3rem] text-sm font-bold text-white no-underline">
                        {suggestionProfile.avatarUrl ? <img src={resolveProfileImageUrl(suggestionProfile.avatarUrl)} alt="" className="h-full w-full object-cover" /> : suggestionProfile.displayName.slice(0, 2).toUpperCase()}
                      </Link>
                      <div className="min-w-0 flex-1">
                        <Link to={`/profile/${suggestionProfile.userId}`} className="block truncate text-sm font-semibold text-text no-underline hover:underline">{suggestionProfile.displayName}</Link>
                        <p className="truncate text-xs text-text-muted">@{suggestionProfile.username}</p>
                        <p className="mt-1 text-xs text-text-muted">{suggestion.mutualFriendCount} {t('mutualFriends')}</p>
                        <div className="mt-2 flex flex-wrap gap-2">
                          {suggestion.relationshipStatus === 'none' && <button type="button" onClick={() => void addSuggestedFriend(suggestion)} disabled={actionId !== null} className="rounded-md bg-primary px-2.5 py-1 text-xs font-semibold text-white disabled:opacity-60">{isAdding ? t('sending') : t('addFriend')}</button>}
                          <button type="button" onClick={() => void toggleSuggestedFollow(suggestion)} disabled={actionId !== null} className="rounded-md border border-border bg-surface px-2.5 py-1 text-xs font-semibold text-text disabled:opacity-60">{isFollowing ? t('updating') : suggestion.isFollowing ? t('following') : t('follow')}</button>
                          <button type="button" onClick={() => removeSuggestedFriend(suggestionProfile.userId)} disabled={actionId !== null} className="rounded-md border border-border bg-transparent px-2.5 py-1 text-xs font-semibold text-text-muted disabled:opacity-60">{t('remove')}</button>
                        </div>
                      </div>
                    </article>
                  })}
                </div>
              )}
            </section>

            {incomingRequests.length > 0 && (
              <section className="flex flex-col gap-3">
                <h3 className="font-heading font-bold text-lg text-text">{t('friendRequests')}</h3>
                <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
                  {incomingRequests.map((request) => {
                    const profile = friendProfiles[request.senderUserId]
                    const isPending = actionId === request.id

                    return (
                      <div key={request.id} className="flex items-center justify-between gap-3 p-3 rounded-lg bg-surface-2/60 border border-border">
                        <div className="min-w-0">
                          <Link to={`/profile/${request.senderUserId}`} className="block font-semibold text-text text-sm truncate hover:underline no-underline">{getProfileName(profile, request.senderUserId)}</Link>
                          <p className="text-xs text-text-muted truncate">@{profile?.username ?? request.senderUserId.slice(0, 8)}</p>
                        </div>
                        <div className="flex gap-2 shrink-0">
                          <button
                            type="button"
                            onClick={() => void runRelationshipAction(request.id, () => friendsApi.acceptRequest(request.id))}
                            disabled={isPending}
                            className="px-3 py-1.5 bg-primary hover:bg-primary-dark disabled:opacity-60 text-white rounded-lg text-xs font-semibold border-none cursor-pointer transition-colors"
                          >
                            {t('accept')}
                          </button>
                          <button
                            type="button"
                            onClick={() => void runRelationshipAction(request.id, () => friendsApi.declineRequest(request.id))}
                            disabled={isPending}
                            className="px-3 py-1.5 bg-surface-2 hover:bg-surface-hover disabled:opacity-60 text-text rounded-lg text-xs font-semibold border border-border cursor-pointer transition-colors"
                          >
                            {t('decline')}
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
                <h3 className="font-heading font-bold text-lg text-text">{t('sentRequests')}</h3>
                <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
                  {outgoingRequests.map((request) => {
                    const profile = friendProfiles[request.receiverUserId]
                    const isPending = actionId === request.id

                    return (
                      <div key={request.id} className="flex items-center justify-between gap-3 p-3 rounded-lg bg-surface-2/60 border border-border">
                        <div className="min-w-0">
                          <Link to={`/profile/${request.receiverUserId}`} className="block font-semibold text-text text-sm truncate hover:underline no-underline">{getProfileName(profile, request.receiverUserId)}</Link>
                          <p className="text-xs text-text-muted truncate">@{profile?.username ?? request.receiverUserId.slice(0, 8)}</p>
                        </div>
                        <button
                          type="button"
                          onClick={() => void runRelationshipAction(request.id, () => friendsApi.cancelRequest(request.id))}
                          disabled={isPending}
                          className="px-3 py-1.5 bg-surface-2 hover:bg-surface-hover disabled:opacity-60 text-text rounded-lg text-xs font-semibold border border-border cursor-pointer transition-colors shrink-0"
                        >
                          {t('cancel')}
                        </button>
                      </div>
                    )
                  })}
                </div>
              </section>
            )}

            {blockedUsers.length > 0 && (
              <section className="flex flex-col gap-3">
                <h3 className="font-heading font-bold text-lg text-text">{t('blockedUsers')}</h3>
                <div className="grid grid-cols-1 sm:grid-cols-2 gap-3">
                  {blockedUsers.map((blockedUser) => {
                    const profile = friendProfiles[blockedUser.userId]
                    const isPending = actionId === blockedUser.userId

                    return (
                      <div key={blockedUser.userId} className="flex items-center justify-between gap-3 rounded-lg bg-surface-2/60 border border-border p-3">
                        <div className="min-w-0">
                          <Link to={`/profile/${blockedUser.userId}`} className="block font-semibold text-sm text-text truncate hover:underline no-underline">{getProfileName(profile, blockedUser.userId)}</Link>
                          <p className="text-xs text-text-muted truncate">@{profile?.username ?? blockedUser.userId.slice(0, 8)}</p>
                        </div>
                        <button type="button" onClick={() => void runRelationshipAction(blockedUser.userId, () => friendsApi.unblock(blockedUser.userId))} disabled={isPending} className="px-3 py-1.5 rounded-lg bg-surface border border-border text-text text-xs cursor-pointer disabled:opacity-60">{t('unblock')}</button>
                      </div>
                    )
                  })}
                </div>
              </section>
            )}

            {!isRelationshipsLoading && friends.items.length === 0 && !relationshipError && (
              <p className="text-sm text-text-muted">{t('noFriendsYet')}</p>
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
                        <img src={resolveProfileImageUrl(profile.avatarUrl)} alt="" className="w-full h-full object-cover" />
                      ) : getInitials(profile)}
                    </div>
                    <div>
                      <Link to={`/profile/${friend.userId}`} className="block font-semibold text-text text-sm hover:underline no-underline">
                        {getProfileName(profile, friend.userId)}
                      </Link>
                      <p className="text-xs text-text-muted">@{profile?.username ?? friend.userId.slice(0, 8)}</p>
                      <p className="text-xs text-text-light mt-0.5">
                        {t('friendsSince')} {new Date(friend.friendsSinceUtc).toLocaleDateString(locale)}
                      </p>
                    </div>
                  </div>
                  <button
                    type="button"
                    onClick={() => void runRelationshipAction(friend.userId, () => friendsApi.unfriend(friend.userId))}
                    disabled={isPending}
                    className="px-3 py-1.5 bg-surface-2 hover:bg-surface-hover disabled:opacity-60 text-text rounded-lg text-xs font-semibold border border-border cursor-pointer transition-colors"
                  >
                    {isPending ? t('updating') : t('unfriend')}
                  </button>
                </div>
                )
              })}
            </div>
            <PaginationControls hasMore={friends.items.length < friends.total} isLoading={isLoadingMoreFriends} error={loadMoreFriendsError} label={t('loadMoreFriends')} onLoadMore={() => void loadMoreFriends()} />
          </div>
        )}

        {/* ── Photos Tab ── */}
        {tab === 'photos' && (
          <div className="bg-surface rounded-xl card-shadow border border-border p-6 flex flex-col gap-5">
            <div className="border-b border-border pb-3">
              <h2 className="font-heading font-bold text-xl text-text">{t('photos')}</h2>
              <p className="text-sm text-text-muted">{t('photosFromPosts')}</p>
              <Link to="/photos" className="mt-2 inline-block text-sm font-semibold text-primary">Albums</Link>
            </div>
            {isPhotosLoading ? <p className="text-sm text-text-muted">{t('loadingPhotos')}</p> : photos.length === 0 ? <p className="text-sm text-text-muted">{t('noPhotosPosted')}</p> : (
              <div className="grid grid-cols-2 gap-3 sm:grid-cols-3 lg:grid-cols-4">
                {photos.map((photo) => <img key={photo.mediaId} src={photo.url} alt="" className="aspect-square w-full rounded-xl object-cover" />)}
              </div>
            )}
          </div>
        )}
      </div>
    </div>
  )
}
