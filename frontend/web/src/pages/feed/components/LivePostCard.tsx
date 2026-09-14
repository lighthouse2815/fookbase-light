import { useEffect, useRef, useState } from 'react'
import { createPortal } from 'react-dom'
import { Link } from 'react-router-dom'
import { ApiError } from '../../../api/client'
import { postsApi } from '../../../api/posts'
import type { Comment, MediaAccess, Post } from '../../../api/posts'
import { resolveProfileImageUrl, usersApi } from '../../../api/users'
import type { UserProfile } from '../../../api/users'
import ReportButton from '../../../shared/components/ReportButton'
import { formatPostTimestamp } from '../../../shared/formatPostTimestamp'
import TextWithReferences from '../../../shared/components/TextWithReferences'
import { usePreferences } from '../../../preferences'
import ShareDialog from './ShareDialog'
import { CommentComposer, DiscussionList } from './PostDiscussion'

interface LivePostCardProps {
  post: Post
  author?: UserProfile
  group?: { id: string; name: string }
  currentUserId: string
  onPostUpdated: (post: Post) => void
  onPostDeleted: (postId: string) => void
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

function LikeIcon() {
  return <svg viewBox="0 0 24 24" aria-hidden="true" className="h-5 w-5 fill-none stroke-current" strokeWidth="1.9" strokeLinecap="round" strokeLinejoin="round"><path d="M7.1 21H4.4a1.4 1.4 0 0 1-1.4-1.4v-7.2A1.4 1.4 0 0 1 4.4 11H7l2.2-6.2a2.05 2.05 0 0 1 4 .7l-.3 4.5h4.3a2.8 2.8 0 0 1 2.7 3.5l-1.2 5a3.2 3.2 0 0 1-3.1 2.5H7.1V11" /></svg>
}

type ReactionType = 'like' | 'love' | 'haha' | 'wow' | 'sad' | 'angry'

const reactionChoices: ReadonlyArray<{ type: ReactionType; icon: string; label: string; color: string }> = [
  { type: 'like', icon: '👍', label: 'Thích', color: 'text-[#1877f2]' },
  { type: 'love', icon: '❤️', label: 'Yêu thích', color: 'text-[#f33e58]' },
  { type: 'haha', icon: '😆', label: 'Haha', color: 'text-[#f7b125]' },
  { type: 'wow', icon: '😮', label: 'Wow', color: 'text-[#f7b125]' },
  { type: 'sad', icon: '😢', label: 'Buồn', color: 'text-[#f7b125]' },
  { type: 'angry', icon: '😡', label: 'Phẫn nộ', color: 'text-[#e9710f]' },
]

function ReactionSummary({ reactionCounts }: { reactionCounts: Record<string, number> }) {
  const reactions = reactionChoices.filter(({ type }) => (reactionCounts[type] ?? 0) > 0)
  const total = Object.values(reactionCounts).reduce((sum, count) => sum + count, 0)

  if (total === 0) return null

  return <span className="flex items-center gap-1.5" aria-label={`${total} cảm xúc`}>
    <span className="flex -space-x-1.5 text-base leading-none" aria-hidden="true">
      {reactions.slice(0, 3).map(({ type, icon }) => <span key={type}>{icon}</span>)}
    </span>
    <span>{total}</span>
  </span>
}

interface ReactionPickerProps {
  viewerReaction: string | null
  onToggleDefault: () => void
  onSelect: (type: ReactionType) => void
  className?: string
}

function ReactionPicker({ viewerReaction, onToggleDefault, onSelect, className = '' }: ReactionPickerProps) {
  const [isPickerOpen, setIsPickerOpen] = useState(false)
  const pressTimerRef = useRef<number | null>(null)
  const skipClickRef = useRef(false)
  const selectedReaction = reactionChoices.find(({ type }) => type === viewerReaction)

  const clearPressTimer = () => {
    if (pressTimerRef.current === null) return
    window.clearTimeout(pressTimerRef.current)
    pressTimerRef.current = null
  }

  const handlePointerDown = (event: React.PointerEvent<HTMLButtonElement>) => {
    if (event.pointerType !== 'touch') return
    pressTimerRef.current = window.setTimeout(() => {
      skipClickRef.current = true
      setIsPickerOpen(true)
    }, 450)
  }

  const chooseReaction = (type: ReactionType) => {
    setIsPickerOpen(false)
    onSelect(type)
  }

  return <div className={`group relative ${className}`} onMouseLeave={() => setIsPickerOpen(false)}>
    <button
      type="button"
      onPointerDown={handlePointerDown}
      onPointerUp={clearPressTimer}
      onPointerCancel={clearPressTimer}
      onClick={() => {
        if (skipClickRef.current) {
          skipClickRef.current = false
          return
        }
        setIsPickerOpen(false)
        onToggleDefault()
      }}
      className={`flex w-full items-center justify-center gap-2 rounded-lg py-2 text-sm font-semibold transition-colors ${selectedReaction ? `${selectedReaction.color} bg-primary/10` : 'text-text-muted hover:bg-surface-2'}`}
      aria-label={selectedReaction ? `Bỏ cảm xúc ${selectedReaction.label}` : 'Thích'}
      aria-expanded={isPickerOpen}
    >
      {selectedReaction ? <span className="text-[19px] leading-none" aria-hidden="true">{selectedReaction.icon}</span> : <LikeIcon />}
      {selectedReaction?.label ?? 'Thích'}
    </button>
    <div className={`absolute bottom-[calc(100%+4px)] left-1/2 z-30 -translate-x-1/2 items-center rounded-full border border-border bg-surface px-1.5 py-1 shadow-xl ${isPickerOpen ? 'flex' : 'hidden'} group-hover:flex group-focus-within:flex`} role="group" aria-label="Chọn cảm xúc">
      {reactionChoices.map(({ type, icon, label }) => <button
        key={type}
        type="button"
        onClick={(event) => {
          event.currentTarget.blur()
          chooseReaction(type)
        }}
        className="grid h-9 w-9 place-items-center rounded-full border-0 bg-transparent p-0 text-[26px] leading-none transition-transform hover:-translate-y-1 hover:scale-125 focus-visible:-translate-y-1 focus-visible:scale-125 focus-visible:outline-none"
        aria-label={label}
        title={label}
      >{icon}</button>)}
    </div>
  </div>
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

export default function LivePostCard({
  post,
  author,
  group,
  currentUserId,
  onPostUpdated,
  onPostDeleted,
}: LivePostCardProps) {
  const { t } = usePreferences()
  const [comments, setComments] = useState<Comment[]>([])
  const [commentsTotal, setCommentsTotal] = useState(0)
  const [commentsOffset, setCommentsOffset] = useState(0)
  const [isCommentsDialogOpen, setIsCommentsDialogOpen] = useState(false)
  const [selectedPhoto, setSelectedPhoto] = useState<MediaAccess | null>(null)
  const [isLoadingComments, setIsLoadingComments] = useState(false)
  const [isLoadingMoreComments, setIsLoadingMoreComments] = useState(false)
  const [commentsPageError, setCommentsPageError] = useState<string | null>(null)
  const [commentAuthors, setCommentAuthors] = useState<Record<string, UserProfile>>({})
  const [commentText, setCommentText] = useState('')
  const [error, setError] = useState<string | null>(null)
  const [media, setMedia] = useState<MediaAccess[]>([])
  const [isSaved, setIsSaved] = useState(false)
  const [isShareOpen, setIsShareOpen] = useState(false)
  const [isPostMenuOpen, setIsPostMenuOpen] = useState(false)
  const isAuthor = post.authorUserId === currentUserId
  const displayAuthor = post.displayAuthor
  const authorName = displayAuthor?.name ?? author?.displayName ?? t('user')
  const authorAvatarUrl = displayAuthor?.avatarUrl ?? author?.avatarUrl
  const authorDestination = displayAuthor?.type === 'page'
    ? `/pages/${displayAuthor.username}`
    : post.authorUserId ? `/profile/${post.authorUserId}` : '/'
  const postTimestamp = formatPostTimestamp(post.createdAtUtc)
  const currentUserProfile = commentAuthors[currentUserId] ?? (isAuthor ? author : undefined)
  const currentUserName = currentUserProfile?.displayName ?? 'Bạn'
  const profileMediaUpdateStatus = post.content === 'đã cập nhật ảnh đại diện.' ||
    post.content === 'đã cập nhật ảnh bìa.'
    ? post.content
    : null

  useEffect(() => {
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
  }, [post.id, post.mediaIds])

  useEffect(() => {
    if (!isCommentsDialogOpen && !selectedPhoto) return

    const closeOnEscape = (event: KeyboardEvent) => {
      if (event.key === 'Escape') {
        setIsCommentsDialogOpen(false)
        setSelectedPhoto(null)
      }
    }
    window.addEventListener('keydown', closeOnEscape)
    return () => window.removeEventListener('keydown', closeOnEscape)
  }, [isCommentsDialogOpen, selectedPhoto])

  const loadCommentAuthors = async (items: readonly Comment[]) => {
    const authorIds = [...new Set(items.map((comment) => comment.authorUserId))]
      .filter((userId) => !commentAuthors[userId])
    if (authorIds.length === 0) return

    const results = await Promise.allSettled(authorIds.map((userId) => usersApi.getById(userId)))
    setCommentAuthors((current) => {
      const next = { ...current }
      results.forEach((result, index) => {
        if (result.status === 'fulfilled') next[authorIds[index]] = result.value
      })
      return next
    })
  }

  const loadComments = async () => {
    setIsLoadingComments(true)
    setCommentsPageError(null)
    try {
      const page = await postsApi.getComments(post.id)
      setComments(page.items)
      setCommentsTotal(page.total)
      setCommentsOffset(page.offset + page.items.length)
      await loadCommentAuthors(page.items)
    } catch (requestError) {
      setError(requestError instanceof ApiError ? requestError.message : t('unableLoadComments'))
    } finally {
      setIsLoadingComments(false)
    }
  }

  const loadMoreComments = async () => {
    setIsLoadingMoreComments(true)
    setCommentsPageError(null)
    try {
      const page = await postsApi.getComments(post.id, commentsOffset)
      setComments((current) => [...current, ...page.items.filter((comment) => !current.some((item) => item.id === comment.id))])
      setCommentsTotal(page.total)
      setCommentsOffset(page.offset + page.items.length)
      await loadCommentAuthors(page.items)
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

  const toggleDefaultReaction = async () => {
    try {
      const updatedPost = post.viewerReaction
        ? await postsApi.removeReaction(post.id)
        : await postsApi.setReaction(post.id, 'like')
      onPostUpdated(updatedPost)
    } catch (requestError) {
      setError(requestError instanceof ApiError ? requestError.message : t('unableUpdateReaction'))
    }
  }

  const setReaction = async (type: ReactionType) => {
    try {
      const updatedPost = post.viewerReaction === type
        ? await postsApi.removeReaction(post.id)
        : await postsApi.setReaction(post.id, type)
      onPostUpdated(updatedPost)
    } catch (requestError) {
      setError(requestError instanceof ApiError ? requestError.message : t('unableUpdateReaction'))
    }
  }

  const createComment = async (event: React.FormEvent<HTMLFormElement>) => {
    event.preventDefault()
    if (!commentText.trim()) return

    try {
      const comment = await postsApi.createComment(post.id, commentText.trim())
      setComments((currentComments) => [...currentComments, comment])
      setCommentsTotal((currentTotal) => currentTotal + 1)
      setCommentsOffset((currentOffset) => currentOffset + 1)
      setCommentText('')
      await loadCommentAuthors([comment])
      onPostUpdated({ ...post, commentCount: post.commentCount + 1 })
    } catch (requestError) {
      setError(requestError instanceof ApiError ? requestError.message : t('unableCreateComment'))
    }
  }

  const editComment = async (comment: Comment) => {
    const content = window.prompt(t('editCommentPrompt'), comment.content)
    if (content === null || !content.trim()) return

    try {
      const updatedComment = await postsApi.updateComment(comment.id, content.trim())
      setComments((currentComments) => currentComments.map((item) =>
        item.id === updatedComment.id ? updatedComment : item,
      ))
    } catch (requestError) {
      setError(requestError instanceof ApiError ? requestError.message : t('unableEditComment'))
    }
  }

  const deleteComment = async (comment: Comment) => {
    if (!window.confirm(t('deleteCommentConfirm'))) return

    try {
      await postsApi.deleteComment(comment.id)
      setComments((currentComments) => currentComments.filter((item) => item.id !== comment.id))
      setCommentsTotal((currentTotal) => Math.max(0, currentTotal - 1))
      setCommentsOffset((currentOffset) => Math.max(0, currentOffset - 1))
      onPostUpdated({ ...post, commentCount: Math.max(0, post.commentCount - 1) })
    } catch (requestError) {
      setError(requestError instanceof ApiError ? requestError.message : t('unableDeleteComment'))
    }
  }

  const editPost = async () => {
    const content = window.prompt(t('editPostPrompt'), post.content)
    if (content === null || !content.trim()) return

    try {
      onPostUpdated(await postsApi.update(post.id, {
        content: content.trim(),
        privacy: post.privacy,
        mediaIds: post.mediaIds,
      }))
    } catch (requestError) {
      setError(requestError instanceof ApiError ? requestError.message : t('unableEditPost'))
    }
  }

  const deletePost = async () => {
    if (!window.confirm(t('deletePostConfirm'))) return

    try {
      await postsApi.delete(post.id)
      onPostDeleted(post.id)
    } catch (requestError) {
      setError(requestError instanceof ApiError ? requestError.message : t('unableDeletePost'))
    }
  }

  const savePost = async () => {
    try {
      if (isSaved) await postsApi.removeSaved(post.id)
      else await postsApi.save(post.id)
      setIsSaved((current) => !current)
    } catch (requestError) {
      setError(requestError instanceof ApiError ? requestError.message : 'Không thể cập nhật bài viết đã lưu.')
    }
  }

  return (
    <>
    <article className="overflow-hidden rounded-2xl border border-border bg-surface shadow-sm">
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
          </p>
        </div>
        <button type="button" onClick={() => setIsPostMenuOpen((current) => !current)} className="grid h-9 w-9 shrink-0 place-items-center rounded-full border-0 bg-transparent text-text-muted transition-colors hover:bg-surface-2 hover:text-text" aria-label={t('moreOptions')} aria-expanded={isPostMenuOpen}><MoreIcon /></button>
        {isPostMenuOpen && <div className="absolute right-3 top-12 z-20 min-w-44 rounded-xl border border-border bg-surface p-1.5 shadow-2xl">
          <button type="button" onClick={() => { void savePost(); setIsPostMenuOpen(false) }} className="flex w-full items-center gap-2 rounded-lg px-3 py-2 text-left text-sm font-medium text-text hover:bg-surface-2"><BookmarkIcon />{isSaved ? 'Bỏ lưu bài viết' : 'Lưu bài viết'}</button>
          {isAuthor ? <>
            <button type="button" onClick={() => { void editPost(); setIsPostMenuOpen(false) }} className="w-full rounded-lg px-3 py-2 text-left text-sm font-medium text-text hover:bg-surface-2">{t('edit')}</button>
            <button type="button" onClick={() => { void deletePost(); setIsPostMenuOpen(false) }} className="w-full rounded-lg px-3 py-2 text-left text-sm font-medium text-[#ff8a9b] hover:bg-surface-2">{t('delete')}</button>
          </> : <ReportButton targetType="post" targetId={post.id} className="w-full rounded-lg px-3 py-2 text-left text-sm font-medium text-text-muted hover:bg-surface-2 hover:text-[#ff8a9b]" />}
        </div>}
      </header>

      {post.content && !profileMediaUpdateStatus && <div className="px-4 pb-3 pt-1"><TextWithReferences content={post.content} mentions={post.mentions} className="text-[15px] leading-[1.45] text-text whitespace-pre-wrap" /></div>}
      {media.length > 0 && (
        <div className={`grid overflow-hidden bg-black ${media.length > 1 ? 'grid-cols-2 gap-0.5' : 'grid-cols-1'}`}>
          {media.map((item) => item.mediaType === 'video' ? (
            <video key={item.mediaId} controls preload="metadata" className={`w-full bg-black object-contain ${media.length > 1 ? 'max-h-80' : 'max-h-[760px]'}`}>
              <source src={item.url} type={item.contentType} />
              {t('browserNoVideo')}
            </video>
          ) : (
            <button key={item.mediaId} type="button" onClick={() => openPhotoViewer(item)} aria-label="Xem ảnh" className="border-0 bg-black p-0 text-left">
              <img src={item.url} alt={t('postAttachment')} className={`w-full bg-black ${media.length > 1 ? 'h-52 object-cover sm:h-72' : 'max-h-[760px] object-contain'}`} />
            </button>
          ))}
        </div>
      )}
      {error && <p className="px-4 pt-3 text-xs text-[#ff8a9b]">{error}</p>}

      <div className="mx-4 flex min-h-11 items-center justify-between gap-3 border-b border-border text-[13px] text-text-muted">
        <ReactionSummary reactionCounts={post.reactionCounts} />
        <button type="button" onClick={openCommentsDialog} className="border-0 bg-transparent p-0 text-[13px] text-text-muted hover:underline">{post.commentCount > 0 ? `${post.commentCount} ${t('comments')}` : ''}</button>
      </div>
      <div className="mx-2 grid grid-cols-3 gap-1 py-1">
        <ReactionPicker viewerReaction={post.viewerReaction} onToggleDefault={() => void toggleDefaultReaction()} onSelect={(type) => void setReaction(type)} />
        <button type="button" onClick={openCommentsDialog} className="flex items-center justify-center gap-2 rounded-lg border-0 bg-transparent py-2 text-sm font-semibold text-text-muted transition-colors hover:bg-surface-2">
          <CommentIcon />{t('comment')}
        </button>
        <button type="button" onClick={() => setIsShareOpen(true)} className="flex items-center justify-center gap-2 rounded-lg border-0 bg-transparent py-2 text-sm font-semibold text-text-muted transition-colors hover:bg-surface-2">
          <ShareIcon />{t('share')}
        </button>
      </div>
      {isShareOpen && <ShareDialog postId={post.id} onClose={() => setIsShareOpen(false)} />}
    </article>
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
                <ReactionSummary reactionCounts={post.reactionCounts} />
                <span>{commentsTotal > 0 ? `${commentsTotal} ${t('comments')}` : ''}</span>
              </div>
              <div className="grid grid-cols-3 border-t border-border px-2 py-1">
                <ReactionPicker viewerReaction={post.viewerReaction} onToggleDefault={() => void toggleDefaultReaction()} onSelect={(type) => void setReaction(type)} />
                <span className="flex items-center justify-center gap-2 py-2 text-sm font-semibold text-text-muted"><CommentIcon />{t('comment')}</span>
                <button type="button" onClick={() => { setIsCommentsDialogOpen(false); setIsShareOpen(true) }} className="flex items-center justify-center gap-2 rounded-lg py-2 text-sm font-semibold text-text-muted hover:bg-surface-2"><ShareIcon />{t('share')}</button>
              </div>
            </div>

            <div className="space-y-4 px-4 py-4">
              <DiscussionList comments={comments} commentAuthors={commentAuthors} currentUserId={currentUserId} isLoading={isLoadingComments} isLoadingMore={isLoadingMoreComments} error={error} paginationError={commentsPageError} hasMore={commentsOffset < commentsTotal} loadingLabel={t('loading')} loadMoreLabel={t('loadMoreComments')} editLabel={t('edit')} deleteLabel={t('delete')} onLoadMore={() => void loadMoreComments()} onEdit={(comment) => void editComment(comment)} onDelete={(comment) => void deleteComment(comment)} />
            </div>
          </div>

          <CommentComposer currentUserProfile={currentUserProfile} currentUserName={currentUserName} value={commentText} placeholder={t('writeComment')} sendLabel={t('send')} onChange={(event) => setCommentText(event.target.value)} onSubmit={(event) => void createComment(event)} />
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
            <ReactionSummary reactionCounts={post.reactionCounts} />
            <span>{commentsTotal > 0 ? `${commentsTotal} ${t('comments')}` : ''}</span>
          </div>
          <div className="grid grid-cols-3 border-b border-border px-2 py-1">
            <ReactionPicker viewerReaction={post.viewerReaction} onToggleDefault={() => void toggleDefaultReaction()} onSelect={(type) => void setReaction(type)} />
            <span className="flex items-center justify-center gap-2 py-2 text-sm font-semibold text-text-muted"><CommentIcon />{t('comment')}</span>
            <button type="button" onClick={() => { closeDiscussion(); setIsShareOpen(true) }} className="flex items-center justify-center gap-2 rounded-lg py-2 text-sm font-semibold text-text-muted hover:bg-surface-2"><ShareIcon />{t('share')}</button>
          </div>

          <div className="min-h-0 flex-1 overflow-y-auto px-4 py-4">
            <div className="space-y-4"><DiscussionList comments={comments} commentAuthors={commentAuthors} currentUserId={currentUserId} isLoading={isLoadingComments} isLoadingMore={isLoadingMoreComments} error={error} paginationError={commentsPageError} hasMore={commentsOffset < commentsTotal} loadingLabel={t('loading')} loadMoreLabel={t('loadMoreComments')} editLabel={t('edit')} deleteLabel={t('delete')} onLoadMore={() => void loadMoreComments()} onEdit={(comment) => void editComment(comment)} onDelete={(comment) => void deleteComment(comment)} /></div>
          </div>
          <CommentComposer currentUserProfile={currentUserProfile} currentUserName={currentUserName} value={commentText} placeholder={t('writeComment')} sendLabel={t('send')} onChange={(event) => setCommentText(event.target.value)} onSubmit={(event) => void createComment(event)} />
        </aside>
      </div>
    , document.body)}
    </>
  )
}
