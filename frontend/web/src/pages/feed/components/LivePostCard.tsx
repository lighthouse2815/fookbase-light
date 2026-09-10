import { useEffect, useState } from 'react'
import { Link } from 'react-router-dom'
import { ApiError } from '../../../api/client'
import { postsApi } from '../../../api/posts'
import type { Comment, MediaAccess, Post } from '../../../api/posts'
import type { UserProfile } from '../../../api/users'

interface LivePostCardProps {
  post: Post
  author?: UserProfile
  currentUserId: string
  onPostUpdated: (post: Post) => void
  onPostDeleted: (postId: string) => void
}

function relativeDate(value: string) {
  return new Intl.RelativeTimeFormat(undefined, { numeric: 'auto' }).format(
    Math.round((new Date(value).getTime() - Date.now()) / 60_000),
    'minute',
  )
}

export default function LivePostCard({
  post,
  author,
  currentUserId,
  onPostUpdated,
  onPostDeleted,
}: LivePostCardProps) {
  const [comments, setComments] = useState<Comment[]>([])
  const [showComments, setShowComments] = useState(false)
  const [commentText, setCommentText] = useState('')
  const [error, setError] = useState<string | null>(null)
  const [media, setMedia] = useState<MediaAccess[]>([])
  const isAuthor = post.authorUserId === currentUserId
  const likeCount = post.reactionCounts.like ?? 0
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
    try {
      const page = await postsApi.getComments(post.id)
      setComments(page.items)
      setShowComments(true)
    } catch (requestError) {
      setError(requestError instanceof ApiError ? requestError.message : 'Không thể tải bình luận.')
    }
  }

  const toggleLike = async () => {
    try {
      const updatedPost = isLiked
        ? await postsApi.removeReaction(post.id)
        : await postsApi.setReaction(post.id, 'like')
      onPostUpdated(updatedPost)
    } catch (requestError) {
      setError(requestError instanceof ApiError ? requestError.message : 'Không thể cập nhật cảm xúc.')
    }
  }

  const createComment = async (event: React.FormEvent<HTMLFormElement>) => {
    event.preventDefault()
    if (!commentText.trim()) return

    try {
      const comment = await postsApi.createComment(post.id, commentText.trim())
      setComments((currentComments) => [...currentComments, comment])
      setCommentText('')
      onPostUpdated({ ...post, commentCount: post.commentCount + 1 })
    } catch (requestError) {
      setError(requestError instanceof ApiError ? requestError.message : 'Không thể thêm bình luận.')
    }
  }

  const editComment = async (comment: Comment) => {
    const content = window.prompt('Edit comment', comment.content)
    if (content === null || !content.trim()) return

    try {
      const updatedComment = await postsApi.updateComment(comment.id, content.trim())
      setComments((currentComments) => currentComments.map((item) =>
        item.id === updatedComment.id ? updatedComment : item,
      ))
    } catch (requestError) {
      setError(requestError instanceof ApiError ? requestError.message : 'Không thể sửa bình luận.')
    }
  }

  const deleteComment = async (comment: Comment) => {
    if (!window.confirm('Delete this comment?')) return

    try {
      await postsApi.deleteComment(comment.id)
      setComments((currentComments) => currentComments.filter((item) => item.id !== comment.id))
      onPostUpdated({ ...post, commentCount: Math.max(0, post.commentCount - 1) })
    } catch (requestError) {
      setError(requestError instanceof ApiError ? requestError.message : 'Không thể xóa bình luận.')
    }
  }

  const editPost = async () => {
    const content = window.prompt('Edit post', post.content)
    if (content === null || !content.trim()) return

    try {
      onPostUpdated(await postsApi.update(post.id, {
        content: content.trim(),
        privacy: post.privacy,
        mediaIds: post.mediaIds,
      }))
    } catch (requestError) {
      setError(requestError instanceof ApiError ? requestError.message : 'Không thể sửa bài viết.')
    }
  }

  const deletePost = async () => {
    if (!window.confirm('Delete this post?')) return

    try {
      await postsApi.delete(post.id)
      onPostDeleted(post.id)
    } catch (requestError) {
      setError(requestError instanceof ApiError ? requestError.message : 'Không thể xóa bài viết.')
    }
  }

  return (
    <article className="bg-surface rounded-xl border border-border p-4 flex flex-col gap-3">
      <div className="flex items-center gap-3">
        <div className="w-10 h-10 rounded-full bg-primary text-white flex items-center justify-center font-bold shrink-0 overflow-hidden">
          {author?.avatarUrl ? <img src={author.avatarUrl} alt="" className="w-full h-full object-cover" /> : author?.displayName.slice(0, 2).toUpperCase()}
        </div>
        <div className="flex-1 min-w-0">
          <Link to={`/profile/${post.authorUserId}`} className="block font-semibold text-sm text-text truncate hover:underline no-underline">{author?.displayName ?? 'User'}</Link>
          <p className="text-xs text-text-muted">@{author?.username ?? post.authorUserId.slice(0, 8)} · {relativeDate(post.createdAtUtc)}</p>
        </div>
        {isAuthor && (
          <div className="flex gap-1">
            <button type="button" onClick={() => void editPost()} className="text-xs text-text-muted hover:text-text bg-transparent border-none cursor-pointer">Edit</button>
            <button type="button" onClick={() => void deletePost()} className="text-xs text-[#ff8a9b] bg-transparent border-none cursor-pointer">Delete</button>
          </div>
        )}
      </div>

      <p className="text-[14px] text-text leading-relaxed whitespace-pre-wrap">{post.content}</p>
      {media.length > 0 && (
        <div className={`grid gap-2 ${media.length > 1 ? 'sm:grid-cols-2' : 'grid-cols-1'}`}>
          {media.map((item) => item.mediaType === 'video' ? (
            <video key={item.mediaId} controls preload="metadata" className="max-h-[520px] w-full rounded-lg bg-surface-2">
              <source src={item.url} type={item.contentType} />
              Your browser does not support video playback.
            </video>
          ) : (
            <img key={item.mediaId} src={item.url} alt="Post attachment" className="max-h-[520px] w-full rounded-lg object-cover bg-surface-2" />
          ))}
        </div>
      )}
      {error && <p className="text-xs text-[#ff8a9b]">{error}</p>}

      <div className="flex items-center justify-between text-xs text-text-muted border-t border-border pt-2">
        <span>{likeCount > 0 ? `${likeCount} likes` : ''}</span>
        <span>{post.commentCount} comments</span>
      </div>
      <div className="grid grid-cols-2 gap-1 border-t border-border pt-1">
        <button type="button" onClick={() => void toggleLike()} className={`py-2 rounded-lg border-none cursor-pointer ${isLiked ? 'text-primary bg-primary/10' : 'text-text-muted bg-transparent hover:bg-surface-2'}`}>
          👍 {isLiked ? 'Liked' : 'Like'}
        </button>
        <button type="button" onClick={() => void (showComments ? setShowComments(false) : loadComments())} className="py-2 rounded-lg text-text-muted hover:bg-surface-2 bg-transparent border-none cursor-pointer">
          💬 Comment
        </button>
      </div>
      {showComments && (
        <div className="flex flex-col gap-2 border-t border-border pt-3">
          {comments.map((comment) => (
            <div key={comment.id} className="flex items-start gap-2 bg-surface-2 rounded-lg px-3 py-2">
              <p className="flex-1 text-sm text-text whitespace-pre-wrap">{comment.content}</p>
              {comment.authorUserId === currentUserId && (
                <div className="flex gap-1">
                  <button type="button" onClick={() => void editComment(comment)} className="bg-transparent border-none text-xs text-text-muted hover:text-text cursor-pointer">Edit</button>
                  <button type="button" onClick={() => void deleteComment(comment)} className="bg-transparent border-none text-xs text-[#ff8a9b] cursor-pointer">Delete</button>
                </div>
              )}
            </div>
          ))}
          <form onSubmit={(event) => void createComment(event)} className="flex gap-2">
            <input value={commentText} onChange={(event) => setCommentText(event.target.value)} placeholder="Write a comment" className="min-w-0 flex-1 bg-surface-2 border border-border rounded-lg px-3 py-2 text-sm text-text outline-none" />
            <button className="px-3 rounded-lg bg-primary text-white border-none cursor-pointer text-sm">Send</button>
          </form>
        </div>
      )}
    </article>
  )
}
