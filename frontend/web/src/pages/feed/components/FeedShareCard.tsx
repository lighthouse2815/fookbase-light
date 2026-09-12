import { Link } from 'react-router-dom'
import type { FeedItem } from '../../../api/feed'
import type { Post } from '../../../api/posts'
import { resolveProfileImageUrl } from '../../../api/users'
import TextWithReferences from '../../../shared/components/TextWithReferences'
import LivePostCard from './LivePostCard'

export default function FeedShareCard({ item, currentUserId, onOriginalUpdated, onOriginalDeleted }: {
  item: FeedItem
  currentUserId: string
  onOriginalUpdated: (post: Post) => void
  onOriginalDeleted: (postId: string) => void
}) {
  const share = item.share
  if (!share) return null
  const author = item.displayAuthor
  const destination = author.type === 'page' ? `/pages/${author.username}` : `/profile/${author.id}`

  return (
    <article className="rounded-xl border border-border bg-surface p-4">
      <div className="mb-3 flex items-center gap-3">
        <div className="flex h-10 w-10 shrink-0 items-center justify-center overflow-hidden rounded-full bg-primary text-sm font-bold text-white">
          {author.avatarUrl ? <img src={resolveProfileImageUrl(author.avatarUrl)} alt="" className="h-full w-full object-cover" /> : author.name.slice(0, 2).toUpperCase()}
        </div>
        <div className="min-w-0">
          <Link to={destination} className="block truncate text-sm font-semibold text-text no-underline hover:underline">{author.name}</Link>
          <p className="text-xs text-text-muted">đã chia sẻ một bài viết</p>
        </div>
      </div>
      {share.caption && <TextWithReferences content={share.caption} className="mb-3 block whitespace-pre-wrap text-sm leading-relaxed text-text" />}
      <div className="rounded-lg border border-border bg-surface-2 p-1">
        <LivePostCard
          post={{ ...share.originalPost, displayAuthor: share.originalAuthor }}
          currentUserId={currentUserId}
          onPostUpdated={onOriginalUpdated}
          onPostDeleted={onOriginalDeleted}
        />
      </div>
    </article>
  )
}
