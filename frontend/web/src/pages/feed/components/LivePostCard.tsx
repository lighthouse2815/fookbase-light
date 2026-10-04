import { useCallback, useEffect, useRef, useState } from 'react'
import { createPortal } from 'react-dom'
import { Link } from 'react-router-dom'
import { ApiError } from '../../../api/client'
import { friendsApi } from '../../../api/friends'
import { postsApi } from '../../../api/posts'
import type { Comment, CommentAuthor, MediaAccess, Post, PostReaction } from '../../../api/posts'
import { resolveProfileImageUrl } from '../../../api/users'
import type { UserProfile } from '../../../api/users'
import ReportButton from '../../../shared/components/ReportButton'
import AppDialog from '../../../shared/components/AppDialog'
import { formatPostTimestamp } from '../../../shared/formatPostTimestamp'
import TextWithReferences from '../../../shared/components/TextWithReferences'
import { usePreferences } from '../../../preferences'
import ShareDialog from './ShareDialog'
import { getPostBackgroundClass } from './postBackgrounds'
import { CommentComposer, DiscussionList } from './PostDiscussion'
import { reactionChoices } from './reactionChoices'
import type { ReactionType } from './reactionChoices'
import { usePostInteractions } from './usePostInteractions'
import { showToast } from '../../../shared/toastState'
import ReactionPicker from './PostReactionPicker'
import PostActionsMenu from './PostActionsMenu'

interface LivePostCardProps {
  post: Post
  author?: UserProfile
  group?: { id: string; name: string }
  currentUserId: string
  onPostUpdated: (post: Post) => void
  onPostDeleted: (postId: string) => void
  initialCommentId?: string
  allowProfilePin?: boolean
}

function MoreIcon() {
  return <svg viewBox="0 0 24 24" aria-hidden="true" className="h-5 w-5 fill-current"><circle cx="5" cy="12" r="1.7" /><circle cx="12" cy="12" r="1.7" /><circle cx="19" cy="12" r="1.7" /></svg>
}

function GlobeIcon() {
  return <svg viewBox="0 0 24 24" aria-hidden="true" className="h-3.5 w-3.5 fill-none stroke-current" strokeWidth="1.8"><circle cx="12" cy="12" r="8.25" /><path d="M3.9 12h16.2M12 3.75c2.1 2.2 3.1 5 3.1 8.25S14.1 18.05 12 20.25C9.9 18.05 8.9 15.25 8.9 12S9.9 5.95 12 3.75Z" /></svg>
}

function FriendsIcon() {
  return <svg viewBox="0 0 24 24" aria-hidden="true" className="h-3.5 w-3.5 fill-none stroke-current" strokeWidth="1.8" strokeLinecap="round" strokeLinejoin="round"><circle cx="9" cy="8" r="3" /><path d="M3.75 20.25c.5-3.25 2.33-5 5.25-5s4.75 1.75 5.25 5M16.25 5.5a2.75 2.75 0 0 1 0 5.5M17 15.4c1.8.38 2.95 1.9 3.25 4.1" /></svg>
}

function LockIcon() {
  return <svg viewBox="0 0 24 24" aria-hidden="true" className="h-3.5 w-3.5 fill-none stroke-current" strokeWidth="1.8" strokeLinecap="round" strokeLinejoin="round"><rect x="5.25" y="10" width="13.5" height="10" rx="2" /><path d="M8.25 10V7.5a3.75 3.75 0 0 1 7.5 0V10" /></svg>
}

function PrivacyIcon({ privacy }: { privacy: string }) {
  const normalizedPrivacy = privacy.toLowerCase()
  if (normalizedPrivacy === 'friends') {
    return <span role="img" aria-label="Bạn bè" title="Bạn bè"><FriendsIcon /></span>
  }
  if (normalizedPrivacy === 'onlyme' || normalizedPrivacy === 'only me') {
    return <span role="img" aria-label="Chỉ mình tôi" title="Chỉ mình tôi"><LockIcon /></span>
  }
  return <span role="img" aria-label="Công khai" title="Công khai"><GlobeIcon /></span>
}

function ReactionSummary({ reactionCounts, onClick }: { reactionCounts: Record<string, number>; onClick: () => void }) {
  const reactions = reactionChoices.filter(({ type }) => (reactionCounts[type] ?? 0) > 0)
  const total = Object.values(reactionCounts).reduce((sum, count) => sum + count, 0)

  if (total === 0) return null

  return <button type="button" onClick={onClick} className="flex items-center gap-1.5 rounded border-0 bg-transparent p-0 text-[13px] text-text-muted hover:underline" aria-label={`Xem ${total} cảm xúc`}>
    <span className="flex -space-x-1.5 text-base leading-none" aria-hidden="true">
      {reactions.slice(0, 3).map(({ type, icon }) => <span key={type}>{icon}</span>)}
    </span>
    <span key={total} className="post-count">{total}</span>
  </button>
}

interface ReactionDialogProps {
  postId: string
  reactionCounts: Record<string, number>
  onClose: () => void
}

function ReactionDialog({ postId, reactionCounts, onClose }: ReactionDialogProps) {
  const pageSize = 20
  const [filter, setFilter] = useState<ReactionType | 'all'>('all')
  const [reactions, setReactions] = useState<PostReaction[]>([])
  const [isLoading, setIsLoading] = useState(true)
  const [isLoadingMore, setIsLoadingMore] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const [loadMoreError, setLoadMoreError] = useState<string | null>(null)
  const [reactionTotal, setReactionTotal] = useState(0)
  const [requestingUserId, setRequestingUserId] = useState<string | null>(null)
  const total = Object.values(reactionCounts).reduce((sum, count) => sum + count, 0)

  const chooseFilter = (nextFilter: ReactionType | 'all') => {
    if (nextFilter === filter) return
    setIsLoading(true)
    setError(null)
    setLoadMoreError(null)
    setReactions([])
    setReactionTotal(0)
    setFilter(nextFilter)
  }

  useEffect(() => {
    let isActive = true
    void postsApi.getReactions(postId, filter === 'all' ? undefined : filter, 0, pageSize)
      .then((page) => {
        if (!isActive) return
        setReactions(page.items)
        setReactionTotal(page.total)
      })
      .catch((requestError: unknown) => {
        if (isActive) setError(requestError instanceof ApiError ? requestError.message : 'Không thể tải danh sách cảm xúc.')
      })
      .finally(() => {
        if (isActive) setIsLoading(false)
      })
    return () => { isActive = false }
  }, [filter, postId])

  const loadMore = async () => {
    if (isLoadingMore || reactions.length >= reactionTotal) return

    setIsLoadingMore(true)
    setLoadMoreError(null)
    try {
      const page = await postsApi.getReactions(postId, filter === 'all' ? undefined : filter, reactions.length, pageSize)
      setReactions((current) => {
        const existingUserIds = new Set(current.map((reaction) => reaction.userId))
        return [...current, ...page.items.filter((reaction) => !existingUserIds.has(reaction.userId))]
      })
      setReactionTotal(page.total)
    } catch (requestError) {
      setLoadMoreError(requestError instanceof ApiError ? requestError.message : 'Không thể tải thêm cảm xúc.')
    } finally {
      setIsLoadingMore(false)
    }
  }

  const sendFriendRequest = async (userId: string) => {
    setRequestingUserId(userId)
    try {
      const request = await friendsApi.sendRequest(userId)
      setReactions((current) => current.map((reaction) => reaction.userId === userId
        ? { ...reaction, relationshipStatus: 'request_sent', relationshipRequestId: request.id }
        : reaction))
    } catch (requestError) {
      setLoadMoreError(requestError instanceof ApiError ? requestError.message : 'Không thể gửi lời mời kết bạn.')
    } finally {
      setRequestingUserId(null)
    }
  }

  return createPortal(
    <div className="fixed inset-0 z-[70] flex items-center justify-center bg-black/70 p-3 backdrop-blur-[2px]" role="presentation" onMouseDown={onClose}>
      <section role="dialog" aria-modal="true" aria-label="Người đã bày tỏ cảm xúc" onMouseDown={(event) => event.stopPropagation()} className="flex max-h-[min(80vh,620px)] w-full max-w-[540px] flex-col overflow-hidden rounded-2xl border border-border bg-surface shadow-2xl">
        <header className="relative border-b border-border px-4 pb-3 pt-4">
          <h2 className="text-center text-[17px] font-bold text-text">Cảm xúc</h2>
          <button type="button" onClick={onClose} aria-label="Đóng" className="absolute right-3 top-3 grid h-9 w-9 place-items-center rounded-full border-0 bg-surface-2 text-2xl leading-none text-text-muted hover:bg-surface-hover hover:text-text">×</button>
          <nav className="mt-3 flex gap-1 overflow-x-auto" aria-label="Lọc cảm xúc">
            <button type="button" onClick={() => chooseFilter('all')} className={`shrink-0 border-b-2 px-3 py-2 text-sm font-semibold ${filter === 'all' ? 'border-primary text-primary' : 'border-transparent text-text-muted hover:text-text'}`}>Tất cả <span className="text-xs">{total}</span></button>
            {reactionChoices.filter(({ type }) => (reactionCounts[type] ?? 0) > 0).map(({ type, icon, label }) => <button key={type} type="button" onClick={() => chooseFilter(type)} aria-label={label} className={`shrink-0 border-b-2 px-3 py-2 text-sm font-semibold ${filter === type ? 'border-primary text-primary' : 'border-transparent text-text-muted hover:text-text'}`}><span className="text-base leading-none">{icon}</span> <span className="text-xs">{reactionCounts[type]}</span></button>)}
          </nav>
        </header>
        <div className="min-h-0 flex-1 overflow-y-auto px-3 py-2">
          {isLoading && <p className="p-4 text-center text-sm text-text-muted">Đang tải cảm xúc…</p>}
          {error && <p className="p-4 text-center text-sm text-[#ff8a9b]">{error}</p>}
          {!isLoading && !error && reactions.length === 0 && <p className="p-4 text-center text-sm text-text-muted">Chưa có cảm xúc nào.</p>}
          {!isLoading && reactions.map((reaction) => {
            const choice = reactionChoices.find(({ type }) => type === reaction.type)
            return <div key={reaction.userId} className="flex items-center gap-3 rounded-xl px-2 py-2.5 hover:bg-surface-2">
              <Link to={`/profile/${reaction.userId}`} onClick={onClose} className="flex min-w-0 flex-1 items-center gap-3 no-underline">
              <div className="flex h-11 w-11 shrink-0 items-center justify-center overflow-hidden rounded-full bg-primary text-sm font-bold text-white">
                {reaction.avatarUrl ? <img src={resolveProfileImageUrl(reaction.avatarUrl)} alt="" className="h-full w-full object-cover" /> : reaction.displayName.slice(0, 2).toUpperCase()}
              </div>
              <span className="min-w-0 flex-1 truncate font-semibold text-text">{reaction.displayName}</span>
              </Link>
              {reaction.relationshipStatus === 'none' && <button type="button" onClick={() => void sendFriendRequest(reaction.userId)} disabled={requestingUserId === reaction.userId} className="shrink-0 rounded-lg border-0 bg-surface-2 px-3 py-1.5 text-xs font-bold text-text hover:bg-surface-hover disabled:cursor-wait disabled:opacity-70">{requestingUserId === reaction.userId ? 'Đang gửi…' : 'Thêm bạn bè'}</button>}
              {choice && <span className="shrink-0 text-xl" aria-label={choice.label}>{choice.icon}</span>}
            </div>
          })}
          {!isLoading && reactions.length > 0 && reactions.length < reactionTotal && <div className="p-2 text-center">
            {loadMoreError && <p className="mb-2 text-sm text-[#ff8a9b]">{loadMoreError}</p>}
            <button type="button" onClick={() => void loadMore()} disabled={isLoadingMore} className="rounded-lg border border-border bg-surface-2 px-4 py-2 text-sm font-semibold text-text hover:bg-surface-hover disabled:cursor-wait disabled:opacity-70">{isLoadingMore ? 'Đang tải…' : 'Xem thêm'}</button>
          </div>}
        </div>
      </section>
    </div>,
    document.body,
  )
}

function CommentIcon() {
  return <svg viewBox="0 0 24 24" aria-hidden="true" className="h-5 w-5 fill-none stroke-current" strokeWidth="1.9" strokeLinecap="round" strokeLinejoin="round"><path d="M20.25 11.5a7.75 7.75 0 0 1-8.1 7.74 8.55 8.55 0 0 1-3.1-.62L4 20l1.38-4.08A7.7 7.7 0 1 1 20.25 11.5Z" /></svg>
}

function ShareIcon() {
  return <svg viewBox="0 0 24 24" aria-hidden="true" className="h-5 w-5 fill-none stroke-current" strokeWidth="1.9" strokeLinecap="round" strokeLinejoin="round"><path d="m12 15 4-4-4-4M4 18.5v-1.1c0-3.55 2.85-6.4 6.4-6.4H16" /></svg>
}

function BookmarkIcon() {
  return <svg viewBox="0 0 24 24" aria-hidden="true" className="h-4.5 w-4.5 fill-none stroke-current" strokeWidth="1.9" strokeLinecap="round" strokeLinejoin="round"><path d="M6.5 3.75h11a1.75 1.75 0 0 1 1.75 1.75v14.75L12 16.4l-7.25 3.85V5.5A1.75 1.75 0 0 1 6.5 3.75Z" /></svg>
}

function PinIcon() {
  return <svg viewBox="0 0 24 24" aria-hidden="true" className="h-4.5 w-4.5 fill-none stroke-current" strokeWidth="1.9" strokeLinecap="round" strokeLinejoin="round"><path d="m14.2 3.8 6 6-2.1 2.1-1.55-.5-4.2 4.2.55 4.1-1.15 1.15-3.7-5.1-5.1-3.7 1.15-1.15 4.1.55 4.2-4.2-.5-2.1Z" /><path d="m8.1 15.75-4.75 4.75" /></svg>
}

function EditIcon() {
  return <svg viewBox="0 0 24 24" aria-hidden="true" className="h-4.5 w-4.5 fill-none stroke-current" strokeWidth="1.9" strokeLinecap="round" strokeLinejoin="round"><path d="m4 20 4.1-1 10.4-10.4a2.1 2.1 0 0 0-3-3L5.1 16 4 20Z" /><path d="m13.9 7.1 3 3" /></svg>
}

function TrashIcon() {
  return <svg viewBox="0 0 24 24" aria-hidden="true" className="h-4.5 w-4.5 fill-none stroke-current" strokeWidth="1.9" strokeLinecap="round" strokeLinejoin="round"><path d="M4.5 7.25h15M9.25 3.75h5.5l.75 3.5h-7l.75-3.5ZM6.5 7.25l.8 12h9.4l.8-12M10 11v4.5M14 11v4.5" /></svg>
}

export default function LivePostCard({
  post,
  author,
  group,
  currentUserId,
  onPostUpdated,
  onPostDeleted,
  initialCommentId,
  allowProfilePin = false,
}: LivePostCardProps) {
  const { t } = usePreferences()
  const { state: interactionState, viewerReaction, reactionCounts, reactionVersion } = usePostInteractions(post, currentUserId)
  const [comments, setComments] = useState<Comment[]>([])
  const [commentsTotal, setCommentsTotal] = useState(0)
  const [commentsOffset, setCommentsOffset] = useState(0)
  const [isCommentsDialogOpen, setIsCommentsDialogOpen] = useState(() => Boolean(initialCommentId))
  const [isReactionDialogOpen, setIsReactionDialogOpen] = useState(false)
  const [selectedPhoto, setSelectedPhoto] = useState<MediaAccess | null>(null)
  const [isLoadingComments, setIsLoadingComments] = useState(false)
  const [isLoadingMoreComments, setIsLoadingMoreComments] = useState(false)
  const [commentsPageError, setCommentsPageError] = useState<string | null>(null)
  const [reactingCommentId, setReactingCommentId] = useState<string | null>(null)
  const [commentAuthors, setCommentAuthors] = useState<Record<string, CommentAuthor>>({})
  const [commentText, setCommentText] = useState('')
  const [replyTarget, setReplyTarget] = useState<Comment | null>(null)
  const [error, setError] = useState<string | null>(null)
  const [media, setMedia] = useState<MediaAccess[]>([])
  const [isSaved, setIsSaved] = useState(post.viewerHasSaved)
  const [isSavingPost, setIsSavingPost] = useState(false)
  const [isShareOpen, setIsShareOpen] = useState(false)
  const [isMediaVisible, setIsMediaVisible] = useState(false)
  const [editingPostContent, setEditingPostContent] = useState<string | null>(null)
  const [editingPostPrivacy, setEditingPostPrivacy] = useState<string | null>(null)
  const [isUpdatingPostPin, setIsUpdatingPostPin] = useState(false)
  const [isTextBackgroundExpanded, setIsTextBackgroundExpanded] = useState(false)
  const [hasTextBackgroundOverflow, setHasTextBackgroundOverflow] = useState(false)
  const [editingComment, setEditingComment] = useState<Comment | null>(null)
  const [editingCommentContent, setEditingCommentContent] = useState('')
  const [commentPendingDeletion, setCommentPendingDeletion] = useState<Comment | null>(null)
  const [isPostPendingDeletion, setIsPostPendingDeletion] = useState(false)
  const postCardRef = useRef<HTMLElement>(null)
  const textBackgroundContentRef = useRef<HTMLDivElement>(null)
  const isAuthor = post.authorUserId === currentUserId
  const canPinPost = allowProfilePin && isAuthor && post.containerType === 'profile'
  const displayAuthor = post.displayAuthor
  const authorName = displayAuthor?.name ?? author?.displayName ?? t('user')
  const authorAvatarUrl = displayAuthor?.avatarUrl ?? author?.avatarUrl
  const authorDestination = displayAuthor?.type === 'page'
    ? `/pages/${displayAuthor.username}`
    : post.authorUserId ? `/profile/${post.authorUserId}` : '/'
  const postTimestamp = formatPostTimestamp(post.createdAtUtc)
  const currentUserProfile = commentAuthors[currentUserId] ?? (isAuthor ? author : undefined)
  const currentUserName = currentUserProfile?.displayName ?? 'Bạn'
  const replyTargetName = replyTarget ? commentAuthors[replyTarget.authorUserId]?.displayName ?? 'Người dùng' : undefined
  const profileMediaUpdateStatus = post.content === 'đã cập nhật ảnh đại diện.' ||
    post.content === 'đã cập nhật ảnh bìa.'
    ? post.content
    : null
  const canEditPost = isAuthor && !profileMediaUpdateStatus
  const canEditPostPrivacy = canEditPost || Boolean(isAuthor && profileMediaUpdateStatus)
  const textBackgroundClass = getPostBackgroundClass(post.textBackground)

  useEffect(() => {
    const content = textBackgroundContentRef.current
    const text = content?.firstElementChild as HTMLElement | null
    const measure = () => {
      setIsTextBackgroundExpanded(false)
      setHasTextBackgroundOverflow(Boolean(textBackgroundClass && text && text.scrollHeight > text.clientHeight + 1))
    }
    const frame = window.requestAnimationFrame(measure)
    return () => window.cancelAnimationFrame(frame)
  }, [post.content, textBackgroundClass])

  useEffect(() => {
    if (post.mediaIds.length === 0 || isMediaVisible) return
    const target = postCardRef.current
    if (!target || !('IntersectionObserver' in window)) {
      setIsMediaVisible(true)
      return
    }
    const observer = new IntersectionObserver((entries) => {
      if (!entries.some((entry) => entry.isIntersecting)) return
      setIsMediaVisible(true)
      observer.disconnect()
    }, { rootMargin: '400px 0px' })
    observer.observe(target)
    return () => observer.disconnect()
  }, [isMediaVisible, post.mediaIds.length])

  useEffect(() => {
    if (!isMediaVisible) return
    let isActive = true

    void Promise.all(post.mediaIds.map((mediaId) => postsApi.getMediaAccess(post.id, mediaId)))
      .then((media) => {
        if (isActive) setMedia(media)
      })
      .catch(() => {
        if (isActive) setMedia([])
      })

    return () => {
      isActive = false
    }
  }, [isMediaVisible, post.id, post.mediaIds])

  useEffect(() => {
    if (!isCommentsDialogOpen && !isReactionDialogOpen && !selectedPhoto) return

    const closeOnEscape = (event: KeyboardEvent) => {
      if (event.key === 'Escape') {
        setIsCommentsDialogOpen(false)
        setIsReactionDialogOpen(false)
        setSelectedPhoto(null)
      }
    }
    window.addEventListener('keydown', closeOnEscape)
    return () => window.removeEventListener('keydown', closeOnEscape)
  }, [isCommentsDialogOpen, isReactionDialogOpen, selectedPhoto])

  const cacheCommentAuthors = useCallback((items: readonly Comment[]) => {
    setCommentAuthors((current) => {
      const next = { ...current }
      items.forEach((comment) => { if (comment.author) next[comment.author.userId] = comment.author })
      return next
    })
  }, [])

  const loadComments = useCallback(async () => {
    setIsLoadingComments(true)
    setCommentsPageError(null)
    try {
      const page = await postsApi.getComments(post.id)
      setComments(page.items)
      setCommentsTotal(page.total)
      setCommentsOffset(page.offset + page.items.length)
      cacheCommentAuthors(page.items)
    } catch (requestError) {
      setError(requestError instanceof ApiError ? requestError.message : t('unableLoadComments'))
    } finally {
      setIsLoadingComments(false)
    }
  }, [cacheCommentAuthors, post.id, t])

  useEffect(() => {
    if (!initialCommentId || comments.length !== 0) return
    const timer = window.setTimeout(() => { void loadComments() }, 0)
    return () => window.clearTimeout(timer)
  }, [comments.length, initialCommentId, loadComments])

  const loadMoreComments = async () => {
    setIsLoadingMoreComments(true)
    setCommentsPageError(null)
    try {
      const page = await postsApi.getComments(post.id, commentsOffset)
      setComments((current) => [...current, ...page.items.filter((comment) => !current.some((item) => item.id === comment.id))])
      setCommentsTotal(page.total)
      setCommentsOffset(page.offset + page.items.length)
      cacheCommentAuthors(page.items)
    } catch (requestError) {
      setCommentsPageError(requestError instanceof ApiError ? requestError.message : t('unableLoadComments'))
    } finally {
      setIsLoadingMoreComments(false)
    }
  }

  const openCommentsDialog = () => {
    setSelectedPhoto(null)
    setIsCommentsDialogOpen(true)
    if (comments.length === 0) void loadComments()
  }

  const openPhotoViewer = (photo: MediaAccess) => {
    setSelectedPhoto(photo)
    if (comments.length === 0) void loadComments()
  }

  const closeDiscussion = () => {
    setIsCommentsDialogOpen(false)
    setSelectedPhoto(null)
  }

  const toggleDefaultReaction = interactionState.toggleReaction
  const setReaction = interactionState.selectReaction

  const createComment = async (event: React.FormEvent<HTMLFormElement>) => {
    event.preventDefault()
    if (!commentText.trim()) return

    try {
      const parentCommentId = replyTarget?.parentCommentId ?? replyTarget?.id
      const comment = await postsApi.createComment(post.id, commentText.trim(), parentCommentId)
      setComments((currentComments) => [...currentComments, comment])
      setCommentsTotal((currentTotal) => currentTotal + 1)
      setCommentsOffset((currentOffset) => currentOffset + 1)
      setCommentText('')
      setReplyTarget(null)
      cacheCommentAuthors([comment])
      onPostUpdated({ ...post, commentCount: post.commentCount + 1 })
    } catch (requestError) {
      setError(requestError instanceof ApiError ? requestError.message : t('unableCreateComment'))
    }
  }

  const saveCommentEdit = async () => {
    if (!editingComment || !editingCommentContent.trim()) return

    try {
      const updatedComment = await postsApi.updateComment(editingComment.id, editingCommentContent.trim())
      setComments((currentComments) => currentComments.map((item) =>
        item.id === updatedComment.id ? updatedComment : item,
      ))
      setEditingComment(null)
      setEditingCommentContent('')
    } catch (requestError) {
      setError(requestError instanceof ApiError ? requestError.message : t('unableEditComment'))
    }
  }

  const deleteComment = async () => {
    if (!commentPendingDeletion) return

    try {
      await postsApi.deleteComment(commentPendingDeletion.id)
      setComments((currentComments) => currentComments.filter((item) => item.id !== commentPendingDeletion.id))
      if (replyTarget?.id === commentPendingDeletion.id || replyTarget?.parentCommentId === commentPendingDeletion.id) setReplyTarget(null)
      setCommentsTotal((currentTotal) => Math.max(0, currentTotal - 1))
      setCommentsOffset((currentOffset) => Math.max(0, currentOffset - 1))
      onPostUpdated({ ...post, commentCount: Math.max(0, post.commentCount - 1) })
      setCommentPendingDeletion(null)
    } catch (requestError) {
      setError(requestError instanceof ApiError ? requestError.message : t('unableDeleteComment'))
    }
  }

  const updateCommentReaction = async (comment: Comment, type?: ReactionType) => {
    setReactingCommentId(comment.id)
    try {
      const updatedComment = type
        ? await postsApi.setCommentReaction(comment.id, type)
        : await postsApi.removeCommentReaction(comment.id)
      setComments((currentComments) => currentComments.map((item) =>
        item.id === updatedComment.id ? updatedComment : item,
      ))
    } catch (requestError) {
      setError(requestError instanceof ApiError ? requestError.message : t('unableUpdateReaction'))
    } finally {
      setReactingCommentId(null)
    }
  }

  const savePostEdit = async () => {
    if (!editingPostContent?.trim()) return

    try {
      onPostUpdated(await postsApi.update(post.id, {
        content: editingPostContent.trim(),
        privacy: post.privacy,
        mediaIds: post.mediaIds,
      }))
      setEditingPostContent(null)
    } catch (requestError) {
      setError(requestError instanceof ApiError ? requestError.message : t('unableEditPost'))
    }
  }

  const savePostPrivacy = async () => {
    if (!editingPostPrivacy) return

    try {
      onPostUpdated(await postsApi.update(post.id, {
        content: post.content,
        privacy: editingPostPrivacy,
        mediaIds: post.mediaIds,
      }))
      setEditingPostPrivacy(null)
      showToast('Đã cập nhật đối tượng xem bài viết.', 'success')
    } catch (requestError) {
      setError(requestError instanceof ApiError ? requestError.message : t('unableEditPost'))
    }
  }

  const deletePost = async () => {
    try {
      await postsApi.delete(post.id)
      onPostDeleted(post.id)
    } catch (requestError) {
      setError(requestError instanceof ApiError ? requestError.message : t('unableDeletePost'))
    }
  }

  const savePost = async () => {
    if (isSavingPost) return

    const wasSaved = isSaved
    setIsSavingPost(true)
    try {
      if (wasSaved) await postsApi.removeSaved(post.id)
      else await postsApi.save(post.id)
      setIsSaved(!wasSaved)
      onPostUpdated({ ...post, viewerHasSaved: !wasSaved })
      showToast(wasSaved ? 'Đã bỏ lưu bài viết.' : 'Đã lưu bài viết.', 'success')
    } catch (requestError) {
      setError(requestError instanceof ApiError ? requestError.message : 'Không thể cập nhật bài viết đã lưu.')
    } finally {
      setIsSavingPost(false)
    }
  }

  const togglePostPin = async () => {
    if (isUpdatingPostPin) return

    setIsUpdatingPostPin(true)
    try {
      const updatedPost = post.isPinned ? await postsApi.unpin(post.id) : await postsApi.pin(post.id)
      onPostUpdated(updatedPost)
      showToast(updatedPost.isPinned ? 'Đã ghim bài viết trên trang cá nhân.' : 'Đã bỏ ghim bài viết.', 'success')
    } catch (requestError) {
      setError(requestError instanceof ApiError ? requestError.message : 'Không thể cập nhật trạng thái ghim bài viết.')
    } finally {
      setIsUpdatingPostPin(false)
    }
  }

  return (
    <>
    <article ref={postCardRef} className="overflow-hidden rounded-2xl border border-border bg-surface shadow-sm">
      <header className="relative flex items-center gap-3 px-4 pb-2 pt-3">
        <div className="flex h-11 w-11 shrink-0 items-center justify-center overflow-hidden rounded-full bg-primary text-sm font-bold text-white">
          {authorAvatarUrl ? <img src={resolveProfileImageUrl(authorAvatarUrl)} alt="" className="w-full h-full object-cover" /> : authorName.slice(0, 2).toUpperCase()}
        </div>
        <div className="flex-1 min-w-0">
          <div className="flex min-w-0 items-baseline gap-1 text-[15px] leading-5">
            <Link to={authorDestination} className="max-w-[48%] shrink-0 truncate font-bold text-text no-underline hover:underline">{authorName}</Link>
            {profileMediaUpdateStatus && <span className="min-w-0 truncate text-text-muted">{profileMediaUpdateStatus}</span>}
          </div>
          {group && <p className="truncate text-xs leading-4 text-text-muted">{t('inGroup')} <Link to={`/groups/${group.id}`} className="font-semibold text-primary no-underline hover:underline">{group.name}</Link></p>}
          <p className="flex min-w-0 items-center gap-1 overflow-hidden whitespace-nowrap text-[12px] font-medium leading-4 text-text-muted">
            <time dateTime={post.createdAtUtc} title={postTimestamp.absolute} aria-label={`Đăng lúc ${postTimestamp.absolute}`} className="min-w-0 cursor-help truncate rounded-sm hover:text-text focus:outline-none focus:ring-1 focus:ring-primary" tabIndex={0}>{postTimestamp.compact}</time>
            <span aria-hidden="true" className="text-text-light">·</span>
            <span className="inline-flex shrink-0 items-center text-text-muted"><PrivacyIcon privacy={post.privacy} /></span>
            {post.isPinned && post.containerType === 'profile' && <><span aria-hidden="true" className="text-text-light">·</span><span className="inline-flex shrink-0 items-center gap-1 text-primary"><PinIcon />Đã ghim</span></>}
          </p>
        </div>
        <PostActionsMenu label={t('moreOptions')} icon={<MoreIcon />}>
            {canPinPost && <button role="menuitem" type="button" disabled={isUpdatingPostPin} onClick={() => { void togglePostPin() }} className="flex w-full items-center gap-2 rounded-lg px-3 py-2 text-left text-sm font-medium text-text hover:bg-surface-2 disabled:cursor-not-allowed disabled:opacity-60"><PinIcon />{post.isPinned ? 'Bỏ ghim bài viết' : 'Ghim bài viết'}</button>}
            <button role="menuitem" type="button" disabled={isSavingPost} onClick={() => { void savePost() }} className="flex w-full items-center gap-2 rounded-lg px-3 py-2 text-left text-sm font-medium text-text hover:bg-surface-2 disabled:cursor-not-allowed disabled:opacity-60"><BookmarkIcon />{isSaved ? 'Bỏ lưu bài viết' : 'Lưu bài viết'}</button>
            {isAuthor ? <>
              {canEditPost && <button role="menuitem" type="button" onClick={() => { setEditingPostContent(post.content) }} className="flex w-full items-center gap-2 rounded-lg px-3 py-2 text-left text-sm font-medium text-text hover:bg-surface-2"><EditIcon />Chỉnh sửa bài viết</button>}
              {canEditPostPrivacy && <><button role="menuitem" type="button" onClick={() => { setEditingPostPrivacy(post.privacy) }} className="flex w-full items-center gap-2 rounded-lg px-3 py-2 text-left text-sm font-medium text-text hover:bg-surface-2"><PrivacyIcon privacy={post.privacy} />Chỉnh sửa đối tượng</button><div className="my-1 border-t border-border" /></>}
              <button role="menuitem" type="button" onClick={() => { setIsPostPendingDeletion(true) }} className="flex w-full items-center gap-2 rounded-lg px-3 py-2 text-left text-sm font-medium text-[#ff8a9b] hover:bg-surface-2"><TrashIcon />{t('delete')}</button>
            </> : <ReportButton role="menuitem" targetType="post" targetId={post.id} className="w-full rounded-lg px-3 py-2 text-left text-sm font-medium text-text-muted hover:bg-surface-2 hover:text-[#ff8a9b]" />}
        </PostActionsMenu>
      </header>

      {post.content && !profileMediaUpdateStatus && (textBackgroundClass
        ? <div className={`relative mx-3 mb-3 aspect-square overflow-hidden rounded-xl text-center ${textBackgroundClass}`}>
          <div ref={textBackgroundContentRef} className={`grid h-full place-items-center px-7 py-12 ${isTextBackgroundExpanded ? 'overflow-y-auto pb-20' : 'overflow-hidden'}`}><TextWithReferences content={post.content} mentions={post.mentions} className={`whitespace-pre-wrap text-2xl font-bold leading-tight text-white sm:text-3xl ${!isTextBackgroundExpanded ? 'line-clamp-7' : ''}`} /></div>
          {hasTextBackgroundOverflow && <div className="absolute inset-x-0 bottom-0 bg-linear-to-t from-black/45 to-transparent px-4 pb-4 pt-10"><button type="button" onClick={() => setIsTextBackgroundExpanded((current) => !current)} className="rounded-full border border-white/35 bg-black/45 px-4 py-2 text-sm font-bold text-white shadow-lg backdrop-blur-sm hover:bg-black/60">{isTextBackgroundExpanded ? 'Thu gọn' : 'Xem thêm'}</button></div>}
        </div>
        : <div className="px-4 pb-3 pt-1"><TextWithReferences content={post.content} mentions={post.mentions} className="text-[15px] leading-[1.45] text-text whitespace-pre-wrap" /></div>)}
      {media.length > 0 && (
        <div className={`grid overflow-hidden bg-black ${media.length > 1 ? 'grid-cols-2 gap-0.5' : 'grid-cols-1'}`}>
          {media.map((item) => item.mediaType === 'video' ? (
            <video key={item.mediaId} controls preload="metadata" className={`w-full bg-black object-contain ${media.length > 1 ? 'max-h-80' : 'max-h-[760px]'}`}>
              <source src={item.url} type={item.contentType} />
              {t('browserNoVideo')}
            </video>
          ) : (
            <button key={item.mediaId} type="button" onClick={() => openPhotoViewer(item)} aria-label="Xem ảnh" className="border-0 bg-black p-0 text-left">
              <img src={item.url} alt={t('postAttachment')} loading="lazy" decoding="async" className={`w-full bg-black ${media.length > 1 ? 'h-52 object-cover sm:h-72' : 'max-h-[760px] object-contain'}`} />
            </button>
          ))}
        </div>
      )}
      {error && <p className="px-4 pt-3 text-xs text-[#ff8a9b]">{error}</p>}

      <div className="mx-4 flex min-h-11 items-center justify-between gap-3 border-b border-border text-[13px] text-text-muted">
        <ReactionSummary reactionCounts={reactionCounts} onClick={() => setIsReactionDialogOpen(true)} />
        <span className="flex gap-2"><button type="button" onClick={openCommentsDialog} className="border-0 bg-transparent p-0 text-[13px] text-text-muted hover:underline">{post.commentCount > 0 ? `${post.commentCount} ${t('comments')}` : ''}</button>{post.shareCount > 0 && <span>{post.shareCount} lượt chia sẻ</span>}</span>
      </div>
      <div className="mx-2 grid grid-cols-3 gap-1 py-1">
        <ReactionPicker animationVersion={reactionVersion} viewerReaction={viewerReaction} onToggleDefault={() => void toggleDefaultReaction()} onSelect={(type) => void setReaction(type)} />
        <button type="button" onClick={openCommentsDialog} className="flex items-center justify-center gap-2 rounded-lg border-0 bg-transparent py-2 text-sm font-semibold text-text-muted transition-colors hover:bg-surface-2">
          <CommentIcon />{t('comment')}
        </button>
        <button type="button" onClick={() => setIsShareOpen(true)} className="flex items-center justify-center gap-2 rounded-lg border-0 bg-transparent py-2 text-sm font-semibold text-text-muted transition-colors hover:bg-surface-2">
          <ShareIcon />{t('share')}
        </button>
      </div>
      {isShareOpen && <ShareDialog postId={post.id} onClose={() => setIsShareOpen(false)} onShared={() => onPostUpdated({ ...post, shareCount: post.shareCount + 1 })} />}
    </article>
    {isReactionDialogOpen && <ReactionDialog key={post.id} postId={post.id} reactionCounts={reactionCounts} onClose={() => setIsReactionDialogOpen(false)} />}
    {isCommentsDialogOpen && createPortal(
      <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/75 p-3 backdrop-blur-[2px]" role="presentation" onMouseDown={() => setIsCommentsDialogOpen(false)}>
        <section role="dialog" aria-modal="true" aria-labelledby={`comments-dialog-${post.id}`} onMouseDown={(event) => event.stopPropagation()} className="flex h-[min(92vh,900px)] w-full max-w-[620px] flex-col overflow-hidden rounded-2xl border border-border bg-surface shadow-2xl">
          <header className="relative flex h-13 shrink-0 items-center justify-center border-b border-border px-14">
            <h2 id={`comments-dialog-${post.id}`} className="truncate text-center text-[17px] font-bold text-text">Bài viết của {authorName}</h2>
            <button type="button" onClick={() => setIsCommentsDialogOpen(false)} aria-label="Đóng bình luận" className="absolute right-3 grid h-9 w-9 place-items-center rounded-full border-0 bg-surface-2 text-2xl leading-none text-text-muted hover:bg-surface-hover hover:text-text">×</button>
          </header>

          <div className="min-h-0 flex-1 overflow-y-auto">
            <div className="border-b border-border">
              {post.content && !profileMediaUpdateStatus && <div className="px-4 py-3"><TextWithReferences content={post.content} mentions={post.mentions} className="text-[15px] leading-[1.45] text-text whitespace-pre-wrap" /></div>}
              {media.length > 0 && (
                <div className={`grid overflow-hidden bg-black ${media.length > 1 ? 'grid-cols-2 gap-0.5' : 'grid-cols-1'}`}>
                  {media.map((item) => item.mediaType === 'video' ? (
                    <video key={item.mediaId} controls preload="metadata" className={`w-full bg-black object-contain ${media.length > 1 ? 'max-h-72' : 'max-h-[48vh]'}`}>
                      <source src={item.url} type={item.contentType} />
                      {t('browserNoVideo')}
                    </video>
                  ) : (
                    <img key={item.mediaId} src={item.url} alt={t('postAttachment')} className={`w-full bg-black ${media.length > 1 ? 'h-52 object-cover' : 'max-h-[48vh] object-contain'}`} />
                  ))}
                </div>
              )}
              <div className="flex items-center justify-between px-4 py-2 text-[13px] text-text-muted">
                <ReactionSummary reactionCounts={reactionCounts} onClick={() => setIsReactionDialogOpen(true)} />
                <span>{commentsTotal > 0 ? `${commentsTotal} ${t('comments')}` : ''}</span>
              </div>
              <div className="grid grid-cols-3 border-t border-border px-2 py-1">
                <ReactionPicker animationVersion={reactionVersion} viewerReaction={viewerReaction} onToggleDefault={() => void toggleDefaultReaction()} onSelect={(type) => void setReaction(type)} />
                <span className="flex items-center justify-center gap-2 py-2 text-sm font-semibold text-text-muted"><CommentIcon />{t('comment')}</span>
                <button type="button" onClick={() => { setIsCommentsDialogOpen(false); setIsShareOpen(true) }} className="flex items-center justify-center gap-2 rounded-lg py-2 text-sm font-semibold text-text-muted hover:bg-surface-2"><ShareIcon />{t('share')}</button>
              </div>
            </div>

            <div className="space-y-4 px-4 py-4">
              <DiscussionList initialCommentId={initialCommentId} comments={comments} commentAuthors={commentAuthors} currentUserId={currentUserId} isLoading={isLoadingComments} isLoadingMore={isLoadingMoreComments} error={error} paginationError={commentsPageError} hasMore={commentsOffset < commentsTotal} loadingLabel={t('loading')} loadMoreLabel={t('loadMoreComments')} editLabel={t('edit')} deleteLabel={t('delete')} onLoadMore={() => void loadMoreComments()} onReply={setReplyTarget} onEdit={(comment) => { setEditingComment(comment); setEditingCommentContent(comment.content) }} onDelete={setCommentPendingDeletion} onReact={(comment, type) => void updateCommentReaction(comment, type)} onRemoveReaction={(comment) => void updateCommentReaction(comment)} reactingCommentId={reactingCommentId} />
            </div>
          </div>

          <CommentComposer currentUserProfile={currentUserProfile} currentUserName={currentUserName} value={commentText} placeholder={replyTargetName ? `Trả lời ${replyTargetName}` : t('writeComment')} sendLabel={t('send')} replyingToName={replyTargetName} onCancelReply={() => setReplyTarget(null)} onChange={(event) => setCommentText(event.target.value)} onSubmit={(event) => void createComment(event)} />
        </section>
      </div>
    , document.body)}
    {selectedPhoto && createPortal(
      <div className="fixed inset-0 z-[60] flex flex-col bg-black text-text md:flex-row" role="dialog" aria-modal="true" aria-label="Xem ảnh">
        <div className="relative flex min-h-0 flex-1 items-center justify-center bg-black p-4 md:p-8">
          <img src={selectedPhoto.url} alt={t('postAttachment')} className="max-h-full max-w-full object-contain" />
          <button type="button" onClick={closeDiscussion} aria-label="Đóng ảnh" className="absolute left-4 top-4 grid h-10 w-10 place-items-center rounded-full border-0 bg-black/55 text-2xl leading-none text-white hover:bg-black/80">×</button>
        </div>

        <aside className="flex h-[48vh] w-full shrink-0 flex-col border-t border-border bg-surface md:h-full md:w-[390px] md:border-l md:border-t-0">
          <header className="flex items-start gap-3 border-b border-border px-4 py-3">
            <div className="flex h-10 w-10 shrink-0 items-center justify-center overflow-hidden rounded-full bg-primary text-sm font-bold text-white">
              {authorAvatarUrl ? <img src={resolveProfileImageUrl(authorAvatarUrl)} alt="" className="h-full w-full object-cover" /> : authorName.slice(0, 2).toUpperCase()}
            </div>
            <div className="min-w-0 flex-1">
              <div className="flex min-w-0 items-baseline gap-1 text-sm leading-5"><Link to={authorDestination} onClick={closeDiscussion} className="max-w-[48%] shrink-0 truncate font-bold text-text no-underline hover:underline">{authorName}</Link>{profileMediaUpdateStatus && <span className="min-w-0 truncate text-text-muted">{profileMediaUpdateStatus}</span>}</div>
              <time dateTime={post.createdAtUtc} title={postTimestamp.absolute} className="text-xs text-text-muted">{postTimestamp.compact}</time>
              {post.content && !profileMediaUpdateStatus && <TextWithReferences content={post.content} mentions={post.mentions} className="mt-1 text-sm leading-5 text-text whitespace-pre-wrap" />}
            </div>
            <button type="button" onClick={closeDiscussion} aria-label="Đóng ảnh" className="grid h-8 w-8 shrink-0 place-items-center rounded-full border-0 bg-surface-2 text-xl leading-none text-text-muted hover:bg-surface-hover hover:text-text">×</button>
          </header>

          <div className="flex items-center justify-between border-b border-border px-4 py-2 text-[13px] text-text-muted">
            <ReactionSummary reactionCounts={reactionCounts} onClick={() => setIsReactionDialogOpen(true)} />
            <span>{commentsTotal > 0 ? `${commentsTotal} ${t('comments')}` : ''}</span>
          </div>
          <div className="grid grid-cols-3 border-b border-border px-2 py-1">
            <ReactionPicker animationVersion={reactionVersion} viewerReaction={viewerReaction} onToggleDefault={() => void toggleDefaultReaction()} onSelect={(type) => void setReaction(type)} />
            <span className="flex items-center justify-center gap-2 py-2 text-sm font-semibold text-text-muted"><CommentIcon />{t('comment')}</span>
            <button type="button" onClick={() => { closeDiscussion(); setIsShareOpen(true) }} className="flex items-center justify-center gap-2 rounded-lg py-2 text-sm font-semibold text-text-muted hover:bg-surface-2"><ShareIcon />{t('share')}</button>
          </div>

          <div className="min-h-0 flex-1 overflow-y-auto px-4 py-4">
            <div className="space-y-4"><DiscussionList initialCommentId={initialCommentId} comments={comments} commentAuthors={commentAuthors} currentUserId={currentUserId} isLoading={isLoadingComments} isLoadingMore={isLoadingMoreComments} error={error} paginationError={commentsPageError} hasMore={commentsOffset < commentsTotal} loadingLabel={t('loading')} loadMoreLabel={t('loadMoreComments')} editLabel={t('edit')} deleteLabel={t('delete')} onLoadMore={() => void loadMoreComments()} onReply={setReplyTarget} onEdit={(comment) => { setEditingComment(comment); setEditingCommentContent(comment.content) }} onDelete={setCommentPendingDeletion} onReact={(comment, type) => void updateCommentReaction(comment, type)} onRemoveReaction={(comment) => void updateCommentReaction(comment)} reactingCommentId={reactingCommentId} /></div>
          </div>
          <CommentComposer currentUserProfile={currentUserProfile} currentUserName={currentUserName} value={commentText} placeholder={replyTargetName ? `Trả lời ${replyTargetName}` : t('writeComment')} sendLabel={t('send')} replyingToName={replyTargetName} onCancelReply={() => setReplyTarget(null)} onChange={(event) => setCommentText(event.target.value)} onSubmit={(event) => void createComment(event)} />
        </aside>
      </div>
    , document.body)}
    {editingPostContent !== null && <AppDialog title="Chỉnh sửa bài viết" onClose={() => setEditingPostContent(null)}>
      <form onSubmit={(event) => { event.preventDefault(); void savePostEdit() }}>
        <label className="mt-4 block text-sm font-semibold text-text">Nội dung bài viết
          <textarea data-dialog-initial-focus value={editingPostContent} onChange={(event) => setEditingPostContent(event.target.value)} maxLength={10_000} rows={6} className="mt-1.5 w-full resize-y rounded-lg border border-border bg-surface-2 p-3 text-sm text-text outline-none focus:border-primary" />
        </label>
        <div className="mt-5 flex justify-end gap-2"><button type="button" onClick={() => setEditingPostContent(null)} className="rounded-lg border-0 bg-surface-2 px-4 py-2 text-sm font-semibold text-text hover:bg-surface-3">Hủy</button><button type="submit" disabled={!editingPostContent.trim()} className="rounded-lg border-0 bg-primary px-4 py-2 text-sm font-semibold text-white disabled:cursor-not-allowed disabled:opacity-50">Lưu</button></div>
      </form>
    </AppDialog>}
    {editingPostPrivacy !== null && <AppDialog title="Chỉnh sửa đối tượng" onClose={() => setEditingPostPrivacy(null)}>
      <form onSubmit={(event) => { event.preventDefault(); void savePostPrivacy() }}>
        <p className="mt-3 text-sm text-text-muted">Chọn những ai có thể xem bài viết này.</p>
        <label className="mt-4 flex cursor-pointer items-center gap-3 rounded-lg border border-border p-3 text-sm text-text hover:bg-surface-2"><input data-dialog-initial-focus type="radio" name={`post-privacy-${post.id}`} value="public" checked={editingPostPrivacy === 'public'} onChange={(event) => setEditingPostPrivacy(event.target.value)} />Công khai</label>
        <label className="mt-2 flex cursor-pointer items-center gap-3 rounded-lg border border-border p-3 text-sm text-text hover:bg-surface-2"><input type="radio" name={`post-privacy-${post.id}`} value="friends" checked={editingPostPrivacy === 'friends'} onChange={(event) => setEditingPostPrivacy(event.target.value)} />Bạn bè</label>
        <label className="mt-2 flex cursor-pointer items-center gap-3 rounded-lg border border-border p-3 text-sm text-text hover:bg-surface-2"><input type="radio" name={`post-privacy-${post.id}`} value="onlyMe" checked={editingPostPrivacy === 'onlyMe'} onChange={(event) => setEditingPostPrivacy(event.target.value)} />Chỉ mình tôi</label>
        <div className="mt-5 flex justify-end gap-2"><button type="button" onClick={() => setEditingPostPrivacy(null)} className="rounded-lg border-0 bg-surface-2 px-4 py-2 text-sm font-semibold text-text hover:bg-surface-3">Hủy</button><button type="submit" className="rounded-lg border-0 bg-primary px-4 py-2 text-sm font-semibold text-white">Lưu</button></div>
      </form>
    </AppDialog>}
    {editingComment && <AppDialog title="Chỉnh sửa bình luận" onClose={() => { setEditingComment(null); setEditingCommentContent('') }}>
      <form onSubmit={(event) => { event.preventDefault(); void saveCommentEdit() }}>
        <label className="mt-4 block text-sm font-semibold text-text">Nội dung bình luận
          <textarea data-dialog-initial-focus value={editingCommentContent} onChange={(event) => setEditingCommentContent(event.target.value)} maxLength={5_000} rows={4} className="mt-1.5 w-full resize-y rounded-lg border border-border bg-surface-2 p-3 text-sm text-text outline-none focus:border-primary" />
        </label>
        <div className="mt-5 flex justify-end gap-2"><button type="button" onClick={() => { setEditingComment(null); setEditingCommentContent('') }} className="rounded-lg border-0 bg-surface-2 px-4 py-2 text-sm font-semibold text-text hover:bg-surface-3">Hủy</button><button type="submit" disabled={!editingCommentContent.trim()} className="rounded-lg border-0 bg-primary px-4 py-2 text-sm font-semibold text-white disabled:cursor-not-allowed disabled:opacity-50">Lưu</button></div>
      </form>
    </AppDialog>}
    {isPostPendingDeletion && <AppDialog title="Xóa bài viết?" onClose={() => setIsPostPendingDeletion(false)}>
      <p className="mt-3 text-sm text-text-muted">Bài viết này sẽ bị xóa khỏi Fookbase.</p>
      <div className="mt-5 flex justify-end gap-2"><button type="button" onClick={() => setIsPostPendingDeletion(false)} className="rounded-lg border-0 bg-surface-2 px-4 py-2 text-sm font-semibold text-text hover:bg-surface-3">Hủy</button><button type="button" onClick={() => { setIsPostPendingDeletion(false); void deletePost() }} className="rounded-lg border-0 bg-[#e41e3f] px-4 py-2 text-sm font-semibold text-white hover:brightness-110">Xóa</button></div>
    </AppDialog>}
    {commentPendingDeletion && <AppDialog title="Xóa bình luận?" onClose={() => setCommentPendingDeletion(null)}>
      <p className="mt-3 text-sm text-text-muted">Bình luận này sẽ bị xóa khỏi Fookbase.</p>
      <div className="mt-5 flex justify-end gap-2"><button type="button" onClick={() => setCommentPendingDeletion(null)} className="rounded-lg border-0 bg-surface-2 px-4 py-2 text-sm font-semibold text-text hover:bg-surface-3">Hủy</button><button type="button" onClick={() => void deleteComment()} className="rounded-lg border-0 bg-[#e41e3f] px-4 py-2 text-sm font-semibold text-white hover:brightness-110">Xóa</button></div>
    </AppDialog>}
    </>
  )
}
