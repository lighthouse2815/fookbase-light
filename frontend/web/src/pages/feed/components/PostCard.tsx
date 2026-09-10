import { useState, type CSSProperties } from 'react'
import type { Post, User } from '../../../data/mockData'
import { formatNumber, formatTimestamp } from '../../../data/mockData'
import LikeButton from '../../../shared/components/LikeButton'
import CommentModal from '../../../shared/components/CommentModal'

interface PostCardProps {
  post: Post
  author: User
  style?: CSSProperties
}

const badgeClass: Record<string, string> = {
  root: 'badge-root',
  anon: 'badge-anon',
  cyborg: 'badge-cyborg',
  neural: 'badge-neural',
  ghost: 'badge-ghost',
}

export default function PostCard({ post, author, style }: PostCardProps) {
  const { t } = usePreferences()
  const [liked, setLiked] = useState(post.isLiked)
  const [reposted, setReposted] = useState(post.isReposted)
  const [likeCount, setLikeCount] = useState(post.likes)
  const [repostCount, setRepostCount] = useState(post.reposts)
  const [commentCount, setCommentCount] = useState(post.comments)
  const [isCommentModalOpen, setIsCommentModalOpen] = useState(false)

  const handleLike = () => {
    setLiked((v) => !v)
    setLikeCount((n) => (liked ? n - 1 : n + 1))
  }

  const handleRepost = () => {
    setReposted((v) => !v)
    setRepostCount((n) => (reposted ? n - 1 : n + 1))
  }

  return (
    <article
      style={style}
      className="bg-surface rounded-xl border border-border p-4 flex flex-col gap-3 transition-colors"
    >
      {/* ── Header: Avatar, Display Name, Badges, Timestamp ── */}
      <div className="flex items-center gap-3">
        <div className="shrink-0">
          <div
            className={`w-10 h-10 rounded-full flex items-center justify-center
                       text-[12px] font-bold text-white
                       ${author.isOnline ? 'avatar-online' : ''}
                       ${author.avatarColor}`}
          >
            {author.avatar}
          </div>
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

        <button
          type="button"
          className="w-8 h-8 rounded-full flex items-center justify-center text-text-muted
                     hover:bg-surface-2 hover:text-text transition-colors cursor-pointer border-none"
          title={t('moreOptions')}
        >
          •••
        </button>
      </div>

      {/* ── Post Content ── */}
      <p className="text-[14px] text-text leading-relaxed whitespace-pre-wrap">{post.content}</p>

      {/* ── Code snippet (if any) ── */}
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
          <pre className="p-3 text-[12px] text-text font-mono leading-relaxed overflow-x-auto whitespace-pre">
            {post.codeSnippet.code}
          </pre>
        </div>
      )}

      {/* ── Tags ── */}
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

      {/* ── Reactions / Stats Row ── */}
      {(likeCount > 0 || repostCount > 0 || commentCount > 0) && (
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
            {commentCount > 0 && (
              <button
                type="button"
                onClick={() => setIsCommentModalOpen(true)}
                className="hover:underline cursor-pointer bg-transparent border-none text-text-muted p-0"
              >
                {formatNumber(commentCount)} bình luận
              </button>
            )}
            {repostCount > 0 && <span>{formatNumber(repostCount)} chia sẻ</span>}
          </div>
        </div>
      )}

      {/* ── Actions Row: 👍 Like, 💬 Comment, ↗️ Share ── */}
      <div className="grid grid-cols-3 border-t border-border pt-1 mt-1 divide-x divide-border">
        {/* Like */}
        <LikeButton liked={liked} onToggle={handleLike} />

        {/* Comment */}
        <button
          type="button"
          onClick={() => setIsCommentModalOpen(true)}
          className="flex items-center justify-center gap-2 py-2 rounded-lg text-[13px] sm:text-[14px]
                     font-medium text-text-muted hover:text-text hover:bg-surface-2
                     transition-colors cursor-pointer bg-transparent border-none"
        >
          <span className="text-base">💬</span>
          <span>Bình luận</span>
        </button>

        {/* Share / Repost */}
        <button
          type="button"
          onClick={handleRepost}
          className={`flex items-center justify-center gap-2 py-2 rounded-lg text-[13px] sm:text-[14px]
                     font-medium transition-colors cursor-pointer bg-transparent border-none
                     hover:bg-surface-2
                     ${reposted ? 'text-secondary font-semibold' : 'text-text-muted hover:text-text'}`}
        >
          <span className="text-base">↗️</span>
          <span>{reposted ? 'Đã chia sẻ' : 'Chia sẻ'}</span>
        </button>
      </div>

      {/* ── Comment Modal ── */}
      <CommentModal
        isOpen={isCommentModalOpen}
        onClose={() => setIsCommentModalOpen(false)}
        post={post}
        author={author}
        liked={liked}
        likeCount={likeCount}
        reposted={reposted}
        repostCount={repostCount}
        onLike={handleLike}
        onRepost={handleRepost}
        onCommentAdded={() => setCommentCount((c) => c + 1)}
      />
    </article>
  )
}
