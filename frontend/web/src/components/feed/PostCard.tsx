import { useState } from 'react'
import type { Post, User } from '../../data/mockData'
import { formatNumber, formatTimestamp } from '../../data/mockData'

interface PostCardProps {
  post: Post
  author: User
  style?: React.CSSProperties
}

const badgeClass: Record<string, string> = {
  root:   'badge-root',
  anon:   'badge-anon',
  cyborg: 'badge-cyborg',
  neural: 'badge-neural',
  ghost:  'badge-ghost',
}

export default function PostCard({ post, author, style }: PostCardProps) {
  const [liked,    setLiked]    = useState(post.isLiked)
  const [reposted, setReposted] = useState(post.isReposted)
  const [likeCount,   setLikeCount]   = useState(post.likes)
  const [repostCount, setRepostCount] = useState(post.reposts)

  const handleLike = () => {
    setLiked((v) => !v)
    setLikeCount((n) => liked ? n - 1 : n + 1)
  }
  const handleRepost = () => {
    setReposted((v) => !v)
    setRepostCount((n) => reposted ? n - 1 : n + 1)
  }

  return (
    <article
      style={style}
      className="bg-surface rounded-2xl card-shadow border border-border
                 p-4 flex gap-3 transition-all duration-200
                 hover:card-shadow-hover hover:border-[rgba(108,99,255,0.2)]"
    >
      {/* ── Avatar ─────────────────────────────────────── */}
      <div className="shrink-0">
        <div className={`w-10 h-10 rounded-full flex items-center justify-center
                         text-[12px] font-bold text-white
                         ${author.isOnline ? 'avatar-online' : ''}
                         ${author.avatarColor}`}>
          {author.avatar}
        </div>
      </div>

      {/* ── Content ────────────────────────────────────── */}
      <div className="flex-1 min-w-0">

        {/* Header */}
        <div className="flex flex-wrap items-center gap-x-1.5 gap-y-1 mb-1.5">
          <span className="font-semibold text-[14px] text-text">{author.displayName}</span>
          <span className="text-[13px] text-text-muted">@{author.handle}</span>
          {author.badges.map((b) => (
            <span key={b} className={`badge-pill ${badgeClass[b] ?? ''}`}>{b}</span>
          ))}
          <span className="text-[12px] text-text-light ml-auto">{formatTimestamp(post.timestamp)}</span>
        </div>

        {/* Post text */}
        <p className="text-[14px] text-text leading-relaxed mb-3 whitespace-pre-wrap">
          {post.content}
        </p>

        {/* Code snippet */}
        {post.codeSnippet && (
          <div className="relative mb-3 rounded-xl overflow-hidden border border-border bg-surface-2">
            <div className="flex items-center gap-2 px-3 py-2 border-b border-border bg-surface-3">
              <div className="flex gap-1.5">
                <span className="w-3 h-3 rounded-full bg-[#ff5f57]" />
                <span className="w-3 h-3 rounded-full bg-[#febc2e]" />
                <span className="w-3 h-3 rounded-full bg-[#28c840]" />
              </div>
              <span className="text-[11px] text-text-muted ml-auto font-medium">{post.codeSnippet.lang}</span>
            </div>
            <pre className="px-4 py-3 text-[12px] text-text font-mono leading-relaxed overflow-x-auto whitespace-pre">
              {post.codeSnippet.code}
            </pre>
          </div>
        )}

        {/* Tags */}
        {post.tags.length > 0 && (
          <div className="flex flex-wrap gap-1.5 mb-3">
            {post.tags.map((tag) => (
              <span key={tag} className="tag-pill text-[12px] px-2.5 py-0.5 rounded-full cursor-pointer transition-all duration-200">
                {tag}
              </span>
            ))}
          </div>
        )}

        {/* Actions */}
        <div className="flex items-center gap-1 pt-2.5 border-t border-border -mx-1">
          {/* Like */}
          <button
            type="button"
            onClick={handleLike}
            className={`flex items-center gap-1.5 px-3 py-1.5 rounded-full text-[13px]
                        font-medium transition-all duration-200 cursor-pointer bg-transparent border-none
                        ${liked
                          ? 'text-like bg-red-50'
                          : 'text-text-muted hover:text-like hover:bg-red-50'}`}
          >
            {liked ? '❤️' : '🤍'} {formatNumber(likeCount)}
          </button>

          {/* Repost */}
          <button
            type="button"
            onClick={handleRepost}
            className={`flex items-center gap-1.5 px-3 py-1.5 rounded-full text-[13px]
                        font-medium transition-all duration-200 cursor-pointer bg-transparent border-none
                        ${reposted
                          ? 'text-repost bg-green-50'
                          : 'text-text-muted hover:text-repost hover:bg-green-50'}`}
          >
            🔁 {formatNumber(repostCount)}
          </button>

          {/* Comment */}
          <button
            type="button"
            className="flex items-center gap-1.5 px-3 py-1.5 rounded-full text-[13px]
                       font-medium text-text-muted hover:text-info hover:bg-blue-50
                       transition-all duration-200 cursor-pointer bg-transparent border-none"
          >
            💬 {formatNumber(post.comments)}
          </button>

          {/* Share */}
          <button
            type="button"
            className="flex items-center gap-1.5 px-3 py-1.5 rounded-full text-[13px]
                       font-medium text-text-muted hover:text-primary hover:bg-purple-50
                       transition-all duration-200 cursor-pointer bg-transparent border-none ml-auto"
          >
            ↗ Share
          </button>
        </div>
      </div>
    </article>
  )
}
