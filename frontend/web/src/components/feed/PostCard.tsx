import { useState } from 'react'
import type { Post, User } from '../../data/mockData'
import { formatNumber, formatTimestamp } from '../../data/mockData'

interface PostCardProps {
  post: Post
  author: User
  style?: React.CSSProperties
}

export default function PostCard({ post, author, style }: PostCardProps) {
  const [liked, setLiked]     = useState(post.isLiked)
  const [reposted, setReposted] = useState(post.isReposted)
  const [likeCount, setLikeCount]     = useState(post.likes)
  const [repostCount, setRepostCount] = useState(post.reposts)

  const handleLike = () => {
    setLiked((v) => !v)
    setLikeCount((n) => liked ? n - 1 : n + 1)
  }

  const handleRepost = () => {
    setReposted((v) => !v)
    setRepostCount((n) => reposted ? n - 1 : n + 1)
  }

  const badgeColors: Record<string, string> = {
    root:   'text-cyber-danger   border-[rgba(255,0,64,0.4)]   bg-[rgba(255,0,64,0.08)]',
    anon:   'text-cyber-green   border-[rgba(0,255,65,0.4)]   bg-[rgba(0,255,65,0.08)]',
    cyborg: 'text-cyber-purple  border-[rgba(189,0,255,0.4)]  bg-[rgba(189,0,255,0.08)]',
    neural: 'text-cyber-cyan    border-[rgba(0,255,255,0.4)]  bg-[rgba(0,255,255,0.08)]',
    ghost:  'text-cyber-yellow  border-[rgba(255,215,0,0.4)]  bg-[rgba(255,215,0,0.08)]',
  }

  return (
    <article
      style={style}
      className="group border border-[rgba(0,255,255,0.12)] bg-bg-card rounded-[4px]
                 p-4 flex gap-3 transition-all duration-200
                 hover:border-[rgba(0,255,255,0.28)] hover:shadow-[0_0_16px_rgba(0,255,255,0.06)]"
    >
      {/* Avatar */}
      <div className="shrink-0">
        <div
          className={`w-10 h-10 rounded-[2px] flex items-center justify-center
                      font-mono text-[11px] text-cyber-cyan border border-[rgba(0,255,255,0.3)]
                      ${author.isOnline ? 'avatar-online' : ''}`}
          style={{ background: author.avatarColor }}
        >
          {author.avatar}
        </div>
      </div>

      {/* Content */}
      <div className="flex-1 min-w-0">
        {/* Header */}
        <div className="flex flex-wrap items-center gap-x-2 gap-y-1 mb-2">
          <span className="font-mono text-[13px] text-cyber-cyan tracking-wide">
            {author.displayName}
          </span>
          <span className="font-mono text-[11px] text-text-dim">@{author.handle}</span>

          {/* Badges */}
          {author.badges.map((b) => (
            <span
              key={b}
              className={`font-mono text-[9px] tracking-widest uppercase px-1.5 py-px border rounded-[2px] ${badgeColors[b] ?? ''}`}
            >
              {b}
            </span>
          ))}

          <span className="font-mono text-[10px] text-text-dim ml-auto">
            {formatTimestamp(post.timestamp)}
          </span>
        </div>

        {/* Post text */}
        <p className="text-[14px] text-text-mid leading-relaxed mb-3 whitespace-pre-wrap font-cyber">
          {post.content}
        </p>

        {/* Code snippet */}
        {post.codeSnippet && (
          <div
            className="code-block mb-3 text-[12px]"
            data-lang={post.codeSnippet.lang}
          >
            {post.codeSnippet.code}
          </div>
        )}

        {/* Tags */}
        {post.tags.length > 0 && (
          <div className="flex flex-wrap gap-1.5 mb-3">
            {post.tags.map((tag) => (
              <span
                key={tag}
                className="font-mono text-[11px] text-cyber-cyan-dim px-2 py-px
                           border border-[rgba(0,188,212,0.3)] bg-[rgba(0,188,212,0.06)]
                           rounded-[2px] cursor-pointer transition-all duration-200
                           hover:text-cyber-cyan hover:border-[rgba(0,255,255,0.5)]
                           hover:shadow-[0_0_6px_rgba(0,255,255,0.1)]"
              >
                {tag}
              </span>
            ))}
          </div>
        )}

        {/* Actions */}
        <div className="flex items-center gap-5 pt-2 border-t border-[rgba(0,255,255,0.07)]">
          {/* Like */}
          <button
            type="button"
            onClick={handleLike}
            className={`flex items-center gap-1.5 font-mono text-[11px] transition-all duration-200 cursor-pointer bg-transparent border-none ${
              liked
                ? 'text-cyber-danger [text-shadow:0_0_8px_rgba(255,0,64,0.5)]'
                : 'text-text-dim hover:text-cyber-danger'
            }`}
          >
            {liked ? '♥' : '♡'} {formatNumber(likeCount)}
          </button>

          {/* Repost */}
          <button
            type="button"
            onClick={handleRepost}
            className={`flex items-center gap-1.5 font-mono text-[11px] transition-all duration-200 cursor-pointer bg-transparent border-none ${
              reposted
                ? 'text-cyber-green [text-shadow:0_0_8px_rgba(0,255,65,0.5)]'
                : 'text-text-dim hover:text-cyber-green'
            }`}
          >
            ⇄ {formatNumber(repostCount)}
          </button>

          {/* Comment */}
          <button
            type="button"
            className="flex items-center gap-1.5 font-mono text-[11px] text-text-dim
                       hover:text-cyber-cyan transition-colors duration-200 cursor-pointer bg-transparent border-none"
          >
            ◇ {formatNumber(post.comments)}
          </button>

          {/* Share */}
          <button
            type="button"
            className="ml-auto flex items-center gap-1.5 font-mono text-[11px] text-text-dim
                       hover:text-cyber-cyan transition-colors duration-200 cursor-pointer bg-transparent border-none"
          >
            ↗ SHARE
          </button>
        </div>
      </div>
    </article>
  )
}
