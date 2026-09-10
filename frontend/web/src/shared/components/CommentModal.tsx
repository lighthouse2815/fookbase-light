import { useState, useEffect, useRef, type KeyboardEvent } from 'react'
import { createPortal } from 'react-dom'
import type { Post, User, Comment } from '../../data/mockData'
import {
  CURRENT_USER,
  getUserById,
  formatNumber,
  formatTimestamp,
  getCommentsByPostId,
} from '../../data/mockData'
import LikeButton from './LikeButton'

interface CommentModalProps {
  isOpen: boolean
  onClose: () => void
  post: Post
  author: User
  liked: boolean
  likeCount: number
  reposted: boolean
  repostCount: number
  onLike: () => void
  onRepost: () => void
  onCommentAdded?: (newComment: Comment) => void
}

const badgeClass: Record<string, string> = {
  root: 'badge-root',
  anon: 'badge-anon',
  cyborg: 'badge-cyborg',
  neural: 'badge-neural',
  ghost: 'badge-ghost',
}

let nextCommentId = 500

export default function CommentModal({
  isOpen,
  onClose,
  post,
  author,
  liked,
  likeCount,
  reposted,
  repostCount,
  onLike,
  onRepost,
  onCommentAdded,
}: CommentModalProps) {
  const [newComments, setNewComments] = useState<Comment[]>([])
  const [inputContent, setInputContent] = useState('')
  const [commentLikesOverride, setCommentLikesOverride] = useState<
    Record<string, { count: number; isLiked: boolean }>
  >({})
  const commentInputRef = useRef<HTMLTextAreaElement>(null)
  const commentsEndRef = useRef<HTMLDivElement>(null)

  const comments = [...getCommentsByPostId(post.id), ...newComments]

  // ESC key and body overflow lock
  useEffect(() => {
    if (!isOpen) return

    const handleKeyDown = (e: globalThis.KeyboardEvent) => {
      if (e.key === 'Escape') {
        onClose()
      }
    }

    const prevOverflow = document.body.style.overflow
    document.body.style.overflow = 'hidden'
    window.addEventListener('keydown', handleKeyDown)

    return () => {
      window.removeEventListener('keydown', handleKeyDown)
      document.body.style.overflow = prevOverflow
    }
  }, [isOpen, onClose])

  if (!isOpen) return null

  const handleToggleCommentLike = (comment: Comment) => {
    setCommentLikesOverride((prev) => {
      const current = prev[comment.id] ?? { count: comment.likes, isLiked: !!comment.isLiked }
      const newIsLiked = !current.isLiked
      return {
        ...prev,
        [comment.id]: {
          count: newIsLiked ? current.count + 1 : Math.max(0, current.count - 1),
          isLiked: newIsLiked,
        },
      }
    })
  }

  const handleAddComment = () => {
    const trimmed = inputContent.trim()
    if (!trimmed) return

    const newComment: Comment = {
      id: `cm-${nextCommentId++}`,
      postId: post.id,
      authorId: CURRENT_USER.id,
      content: trimmed,
      timestamp: new Date(),
      likes: 0,
      isLiked: false,
    }

    setNewComments((prev) => [...prev, newComment])
    setInputContent('')
    onCommentAdded?.(newComment)

    // Scroll to bottom of comments after state update
    setTimeout(() => {
      commentsEndRef.current?.scrollIntoView({ behavior: 'smooth' })
    }, 50)
  }

  const handleInputKeyDown = (e: KeyboardEvent<HTMLTextAreaElement>) => {
    if (e.key === 'Enter' && !e.shiftKey) {
      e.preventDefault()
      handleAddComment()
    }
  }

  const handleReplyClick = (replyAuthorName: string) => {
    setInputContent(`@${replyAuthorName} `)
    commentInputRef.current?.focus()
  }

  const modalContent = (
    <div
      className="fixed inset-0 z-50 flex items-center justify-center p-2 sm:p-4 bg-black/80 backdrop-blur-xs animate-in fade-in duration-200"
      onClick={onClose}
      role="dialog"
      aria-modal="true"
      aria-label={`${author.displayName}'s Post`}
    >
      <div
        className="bg-surface rounded-2xl border border-border w-full max-w-2xl max-h-[92vh] flex flex-col shadow-2xl overflow-hidden"
        style={{ animation: 'scale-in 0.2s ease both' }}
        onClick={(e) => e.stopPropagation()}
      >
        {/* ── Modal Header ───────────────────────────────── */}
        <div className="relative px-5 py-3.5 border-b border-border flex items-center justify-between">
          <div className="w-9" /> {/* Spacer for symmetry */}
          <h2 className="text-base sm:text-[17px] font-bold text-text text-center truncate">
            Bài viết của {author.displayName}
          </h2>
          <button
            type="button"
            onClick={onClose}
            className="w-9 h-9 rounded-full bg-surface-2 hover:bg-surface-3 text-text-muted hover:text-text flex items-center justify-center transition-colors cursor-pointer border-none text-base"
            title="Đóng (Esc)"
          >
            ✕
          </button>
        </div>

        {/* ── Modal Scrollable Body ─────────────────────────── */}
        <div className="flex-1 overflow-y-auto scroll-smooth p-4 sm:p-5 space-y-4">
          {/* Post Author Info */}
          <div className="flex items-center gap-3">
            <div
              className={`w-10 h-10 rounded-full flex items-center justify-center text-[12px] font-bold text-white shrink-0 ${author.isOnline ? 'avatar-online' : ''
                } ${author.avatarColor}`}
            >
              {author.avatar}
            </div>

            <div className="flex-1 min-w-0">
              <div className="flex flex-wrap items-center gap-1.5">
                <span className="font-semibold text-[14px] text-text hover:underline cursor-pointer">
                  {author.displayName}
                </span>
                {author.badges.map((b) => (
                  <span key={b} className={`badge-pill ${badgeClass[b] ?? ''}`}>
                    {b}
                  </span>
                ))}
              </div>
              <div className="flex items-center gap-2 text-[12px] text-text-muted">
                <span>@{author.handle}</span>
                <span>•</span>
                <span>{formatTimestamp(post.timestamp)}</span>
              </div>
            </div>
          </div>

          {/* Post Text Content */}
          <p className="text-[15px] text-text leading-relaxed whitespace-pre-wrap break-words">
            {post.content}
          </p>

          {/* Post Code snippet (if any) */}
          {post.codeSnippet && (
            <div className="relative rounded-xl overflow-hidden border border-border bg-surface-2">
              <div className="flex items-center gap-2 px-3 py-1.5 border-b border-border bg-surface-3">
                <div className="flex gap-1.5">
                  <span className="w-2.5 h-2.5 rounded-full bg-[#ff5f57]" />
                  <span className="w-2.5 h-2.5 rounded-full bg-[#febc2e]" />
                  <span className="w-2.5 h-2.5 rounded-full bg-[#28c840]" />
                </div>
                <span className="text-[11px] text-text-muted ml-auto font-mono font-medium">
                  {post.codeSnippet.lang}
                </span>
              </div>
              <pre className="p-3.5 text-[12px] text-text font-mono leading-relaxed overflow-x-auto whitespace-pre">
                {post.codeSnippet.code}
              </pre>
            </div>
          )}

          {/* Post Tags */}
          {post.tags.length > 0 && (
            <div className="flex flex-wrap gap-1.5">
              {post.tags.map((tag) => (
                <span
                  key={tag}
                  className="tag-pill text-[12px] px-2.5 py-0.5 rounded-full cursor-pointer transition-all duration-200"
                >
                  {tag}
                </span>
              ))}
            </div>
          )}

          {/* Post Reactions Stats Row */}
          {(likeCount > 0 || repostCount > 0 || comments.length > 0) && (
            <div className="flex items-center justify-between text-[13px] text-text-muted pt-1 px-1">
              <div className="flex items-center gap-1.5">
                {likeCount > 0 && (
                  <span className="flex items-center gap-1">
                    <span className="w-4 h-4 rounded-full bg-primary flex items-center justify-center text-[9px] text-white">
                      👍
                    </span>
                    <span>{formatNumber(likeCount)}</span>
                  </span>
                )}
              </div>
              <div className="flex items-center gap-3 text-[12px]">
                {comments.length > 0 && <span>{formatNumber(comments.length)} bình luận</span>}
                {repostCount > 0 && <span>{formatNumber(repostCount)} chia sẻ</span>}
              </div>
            </div>
          )}

          {/* Post Action Buttons */}
          <div className="grid grid-cols-3 border-y border-border py-1 divide-x divide-border">
            <LikeButton liked={liked} onToggle={onLike} />

            <button
              type="button"
              onClick={() => commentInputRef.current?.focus()}
              className="flex items-center justify-center gap-2 py-2 rounded-lg text-[13px] sm:text-[14px] font-medium text-text-muted hover:text-text hover:bg-surface-2 transition-colors cursor-pointer bg-transparent border-none"
            >
              <span className="text-base">💬</span>
              <span>Bình luận</span>
            </button>

            <button
              type="button"
              onClick={onRepost}
              className={`flex items-center justify-center gap-2 py-2 rounded-lg text-[13px] sm:text-[14px] font-medium transition-colors cursor-pointer bg-transparent border-none hover:bg-surface-2 ${reposted ? 'text-secondary font-semibold' : 'text-text-muted hover:text-text'
                }`}
            >
              <span className="text-base">↗️</span>
              <span>{reposted ? 'Đã chia sẻ' : 'Chia sẻ'}</span>
            </button>
          </div>

          {/* ── Comments Section ───────────────────────────── */}
          <div className="pt-2 space-y-3">
            <h3 className="text-[14px] font-semibold text-text">
              Tất cả bình luận ({comments.length})
            </h3>

            {comments.length === 0 ? (
              <div className="py-8 text-center text-text-muted text-[14px]">
                Chưa có bình luận nào. Hãy là người đầu tiên bình luận!
              </div>
            ) : (
              <div className="space-y-3.5">
                {comments.map((comment) => {
                  const commentAuthor = getUserById(comment.authorId) ?? {
                    id: comment.authorId,
                    displayName: 'Người dùng',
                    handle: 'user',
                    avatar: 'U',
                    avatarColor: 'bg-surface-2',
                    badges: [],
                    isOnline: false,
                  }
                  const likeInfo = commentLikesOverride[comment.id] ?? {
                    count: comment.likes,
                    isLiked: !!comment.isLiked,
                  }

                  return (
                    <div key={comment.id} className="flex items-start gap-2.5 group">
                      {/* Avatar */}
                      <div
                        className={`w-8 h-8 rounded-full flex items-center justify-center text-[10px] font-bold text-white shrink-0 mt-0.5 ${commentAuthor.isOnline ? 'avatar-online' : ''
                          } ${commentAuthor.avatarColor}`}
                      >
                        {commentAuthor.avatar}
                      </div>

                      {/* Bubble + Actions */}
                      <div className="flex-1 min-w-0">
                        <div className="bg-surface-2 rounded-2xl px-3.5 py-2.5 inline-block max-w-[95%]">
                          <div className="flex items-center gap-1.5 flex-wrap mb-0.5">
                            <span className="font-semibold text-[13px] text-text hover:underline cursor-pointer">
                              {commentAuthor.displayName}
                            </span>
                            {commentAuthor.badges?.slice(0, 1).map((b) => (
                              <span key={b} className={`badge-pill text-[9px] ${badgeClass[b] ?? ''}`}>
                                {b}
                              </span>
                            ))}
                          </div>
                          <p className="text-[13px] text-text leading-relaxed whitespace-pre-wrap break-words">
                            {comment.content}
                          </p>
                        </div>

                        {/* Actions below comment bubble */}
                        <div className="flex items-center gap-3.5 px-3 pt-1 text-[11px] text-text-muted font-medium">
                          <span>{formatTimestamp(comment.timestamp)}</span>

                          <button
                            type="button"
                            onClick={() => handleToggleCommentLike(comment)}
                            className={`hover:underline cursor-pointer bg-transparent border-none p-0 ${likeInfo.isLiked ? 'text-primary font-bold' : 'text-text-muted hover:text-text'
                              }`}
                          >
                            Thích {likeInfo.count > 0 && `(${formatNumber(likeInfo.count)})`}
                          </button>

                          <button
                            type="button"
                            onClick={() => handleReplyClick(commentAuthor.displayName)}
                            className="hover:underline cursor-pointer bg-transparent border-none p-0 text-text-muted hover:text-text"
                          >
                            Phản hồi
                          </button>
                        </div>
                      </div>
                    </div>
                  )
                })}
              </div>
            )}
            <div ref={commentsEndRef} />
          </div>
        </div>

        {/* ── Fixed Bottom Comment Input Bar ──────────────── */}
        <div className="p-3 sm:p-4 border-t border-border bg-surface flex items-start gap-3">
          <div
            className={`w-9 h-9 rounded-full flex items-center justify-center text-[11px] font-bold text-white shrink-0 mt-0.5 ${CURRENT_USER.avatarColor}`}
          >
            {CURRENT_USER.avatar}
          </div>

          <div className="flex-1 bg-surface-2 rounded-2xl border border-border focus-within:border-border-focus transition-colors flex items-center px-3 py-1.5 gap-2">
            <textarea
              ref={commentInputRef}
              rows={1}
              value={inputContent}
              onChange={(e) => setInputContent(e.target.value)}
              onKeyDown={handleInputKeyDown}
              placeholder={`Viết bình luận dưới tên ${CURRENT_USER.displayName}...`}
              className="flex-1 bg-transparent border-none text-[13px] sm:text-[14px] text-text outline-none resize-none placeholder:text-text-light py-1 leading-normal"
              style={{ maxHeight: 96 }}
            />

            <button
              type="button"
              onClick={handleAddComment}
              disabled={!inputContent.trim()}
              className="w-8 h-8 rounded-full bg-primary disabled:opacity-40 disabled:cursor-not-allowed hover:enabled:bg-primary-dark text-white flex items-center justify-center transition-colors cursor-pointer border-none shrink-0"
              title="Gửi bình luận (Enter)"
            >
              ➤
            </button>
          </div>
        </div>
      </div>
    </div>
  )

  return createPortal(modalContent, document.body)
}
