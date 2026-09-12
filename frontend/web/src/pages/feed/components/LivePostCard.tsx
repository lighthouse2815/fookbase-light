import { useEffect, useState } from 'react'
import { Link } from 'react-router-dom'
import { ApiError } from '../../../api/client'
import { postsApi } from '../../../api/posts'
import type { Comment, MediaAccess, Post } from '../../../api/posts'
import { resolveProfileImageUrl } from '../../../api/users'
import type { UserProfile } from '../../../api/users'
import ReportButton from '../../../shared/components/ReportButton'
import { usePreferences } from '../../../preferences'
import PaginationControls from '../../../shared/components/PaginationControls'

interface LivePostCardProps {
  post: Post
  author?: UserProfile
  group?: { id: string; name: string }
  currentUserId: string
  onPostUpdated: (post: Post) => void
  onPostDeleted: (postId: string) => void
}

function relativeDate(value: string, locale: string) {
  return new Intl.RelativeTimeFormat(locale, { numeric: 'auto' }).format(
    Math.round((new Date(value).getTime() - Date.now()) / 60_000),
    'minute',
  )
}

export default function LivePostCard({
  post,
  author,
  group,
  currentUserId,
  onPostUpdated,
  onPostDeleted,
}: LivePostCardProps) {
  const { language, t } = usePreferences()
  const [comments, setComments] = useState<Comment[]>([])
  const [commentsTotal, setCommentsTotal] = useState(0)
  const [commentsOffset, setCommentsOffset] = useState(0)
  const [showComments, setShowComments] = useState(false)
  const [isLoadingComments, setIsLoadingComments] = useState(false)
  const [isLoadingMoreComments, setIsLoadingMoreComments] = useState(false)
  const [commentsPageError, setCommentsPageError] = useState<string | null>(null)
  const [commentText, setCommentText] = useState('')
  const [error, setError] = useState<string | null>(null)
  const [media, setMedia] = useState<MediaAccess[]>([])
  const isAuthor = post.authorUserId === currentUserId
  const displayAuthor = post.displayAuthor
  const authorName = displayAuthor?.name ?? author?.displayName ?? t('user')
  const authorUsername = displayAuthor?.username ?? author?.username ?? t('user')
  const authorAvatarUrl = displayAuthor?.avatarUrl ?? author?.avatarUrl
  const authorDestination = displayAuthor?.type === 'page'
    ? `/pages/${displayAuthor.username}`
    : post.authorUserId ? `/profile/${post.authorUserId}` : '/'
  const reactionCount = Object.values(post.reactionCounts).reduce(
    (total, count) => total + count,
    0,
  )
  const isLiked = post.viewerReaction === 'like'

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

  const loadComments = async () => {
    setIsLoadingComments(true)
    setCommentsPageError(null)
    try {
      const page = await postsApi.getComments(post.id)
      setComments(page.items)
      setCommentsTotal(page.total)
      setCommentsOffset(page.offset + page.items.length)
      setShowComments(true)
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
    } catch (requestError) {
      setCommentsPageError(requestError instanceof ApiError ? requestError.message : t('unableLoadComments'))
    } finally {
      setIsLoadingMoreComments(false)
    }
  }

  const toggleLike = async () => {
    try {
      const updatedPost = isLiked
        ? await postsApi.removeReaction(post.id)
        : await postsApi.setReaction(post.id, 'like')
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

  return (
    <article className="bg-surface rounded-xl border border-border p-4 flex flex-col gap-3">
      <div className="flex items-center gap-3">
        <div className="w-10 h-10 rounded-full bg-primary text-white flex items-center justify-center font-bold shrink-0 overflow-hidden">
          {authorAvatarUrl ? <img src={resolveProfileImageUrl(authorAvatarUrl)} alt="" className="w-full h-full object-cover" /> : authorName.slice(0, 2).toUpperCase()}
        </div>
        <div className="flex-1 min-w-0">
          <Link to={authorDestination} className="block font-semibold text-sm text-text truncate hover:underline no-underline">{authorName}</Link>
          {group && <p className="text-xs text-text-muted">{t('inGroup')} <Link to={`/groups/${group.id}`} className="font-semibold text-primary no-underline hover:underline">{group.name}</Link></p>}
          <p className="text-xs text-text-muted">@{authorUsername} · {relativeDate(post.createdAtUtc, language === 'vi' ? 'vi-VN' : 'en-US')}</p>
        </div>
        {isAuthor ? (
          <div className="flex gap-1">
            <button type="button" onClick={() => void editPost()} className="text-xs text-text-muted hover:text-text bg-transparent border-none cursor-pointer">{t('edit')}</button>
            <button type="button" onClick={() => void deletePost()} className="text-xs text-[#ff8a9b] bg-transparent border-none cursor-pointer">{t('delete')}</button>
          </div>
        ) : (
          <ReportButton targetType="post" targetId={post.id} className="border-none bg-transparent text-xs text-text-muted hover:text-[#ff8a9b]" />
        )}
      </div>

      <p className="text-[14px] text-text leading-relaxed whitespace-pre-wrap">{post.content}</p>
      {media.length > 0 && (
        <div className={`grid gap-2 ${media.length > 1 ? 'sm:grid-cols-2' : 'grid-cols-1'}`}>
          {media.map((item) => item.mediaType === 'video' ? (
            <video key={item.mediaId} controls preload="metadata" className="max-h-[520px] w-full rounded-lg bg-surface-2">
              <source src={item.url} type={item.contentType} />
              {t('browserNoVideo')}
            </video>
          ) : (
            <img key={item.mediaId} src={item.url} alt={t('postAttachment')} className="max-h-[520px] w-full rounded-lg object-cover bg-surface-2" />
          ))}
        </div>
      )}
      {error && <p className="text-xs text-[#ff8a9b]">{error}</p>}

      <div className="flex items-center justify-between text-xs text-text-muted border-t border-border pt-2">
        <span>{reactionCount > 0 ? `${reactionCount} ${t('reactions')}` : ''}</span>
        <span>{post.commentCount} {t('comments')}</span>
      </div>
      <div className="grid grid-cols-2 gap-1 border-t border-border pt-1">
        <button type="button" onClick={() => void toggleLike()} className={`py-2 rounded-lg border-none cursor-pointer ${isLiked ? 'text-primary bg-primary/10' : 'text-text-muted bg-transparent hover:bg-surface-2'}`}>
          👍 {isLiked ? t('liked') : t('like')}
        </button>
        <button type="button" onClick={() => void (showComments ? setShowComments(false) : loadComments())} className="py-2 rounded-lg text-text-muted hover:bg-surface-2 bg-transparent border-none cursor-pointer">
          💬 {t('comment')}
        </button>
      </div>
      {showComments && (
        <div className="flex flex-col gap-2 border-t border-border pt-3">
          {isLoadingComments && <p className="text-sm text-text-muted">{t('loading')}</p>}
          {comments.map((comment) => (
            <div key={comment.id} className="flex items-start gap-2 bg-surface-2 rounded-lg px-3 py-2">
              <p className="flex-1 text-sm text-text whitespace-pre-wrap">{comment.content}</p>
              {comment.authorUserId === currentUserId && (
                <div className="flex gap-1">
                  <button type="button" onClick={() => void editComment(comment)} className="bg-transparent border-none text-xs text-text-muted hover:text-text cursor-pointer">{t('edit')}</button>
                  <button type="button" onClick={() => void deleteComment(comment)} className="bg-transparent border-none text-xs text-[#ff8a9b] cursor-pointer">{t('delete')}</button>
                </div>
              )}
            </div>
          ))}
          <PaginationControls hasMore={commentsOffset < commentsTotal} isLoading={isLoadingMoreComments} error={commentsPageError} label={t('loadMoreComments')} onLoadMore={() => void loadMoreComments()} />
          <form onSubmit={(event) => void createComment(event)} className="flex gap-2">
            <input value={commentText} onChange={(event) => setCommentText(event.target.value)} placeholder={t('writeComment')} className="min-w-0 flex-1 bg-surface-2 border border-border rounded-lg px-3 py-2 text-sm text-text outline-none" />
            <button className="px-3 rounded-lg bg-primary text-white border-none cursor-pointer text-sm">{t('send')}</button>
          </form>
        </div>
      )}
    </article>
  )
}
