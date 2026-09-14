import type { ChangeEvent, FormEvent } from 'react'
import type { Comment } from '../../../api/posts'
import { resolveProfileImageUrl } from '../../../api/users'
import type { UserProfile } from '../../../api/users'
import TextWithReferences from '../../../shared/components/TextWithReferences'
import { formatPostTimestamp } from '../../../shared/formatPostTimestamp'
import PaginationControls from '../../../shared/components/PaginationControls'

interface DiscussionListProps {
  comments: readonly Comment[]
  commentAuthors: Readonly<Record<string, UserProfile>>
  currentUserId: string
  isLoading: boolean
  isLoadingMore: boolean
  error: string | null
  paginationError: string | null
  hasMore: boolean
  loadingLabel: string
  loadMoreLabel: string
  editLabel: string
  deleteLabel: string
  onLoadMore: () => void
  onEdit: (comment: Comment) => void
  onDelete: (comment: Comment) => void
}

export function DiscussionList({
  comments,
  commentAuthors,
  currentUserId,
  isLoading,
  isLoadingMore,
  error,
  paginationError,
  hasMore,
  loadingLabel,
  loadMoreLabel,
  editLabel,
  deleteLabel,
  onLoadMore,
  onEdit,
  onDelete,
}: DiscussionListProps) {
  return <>
    {isLoading && <p className="text-center text-sm text-text-muted">{loadingLabel}</p>}
    {error && <p className="text-center text-xs text-[#ff8a9b]">{error}</p>}
    {comments.map((comment) => {
      const commentAuthor = commentAuthors[comment.authorUserId]
      const commentAuthorName = commentAuthor?.displayName ?? 'Người dùng'
      return (
        <div key={comment.id} className="flex items-start gap-2.5">
          <div className="flex h-9 w-9 shrink-0 items-center justify-center overflow-hidden rounded-full bg-primary text-xs font-bold text-white">
            {commentAuthor?.avatarUrl ? <img src={resolveProfileImageUrl(commentAuthor.avatarUrl)} alt="" className="h-full w-full object-cover" /> : commentAuthorName.slice(0, 2).toUpperCase()}
          </div>
          <div className="min-w-0 flex-1">
            <div className="inline-block max-w-full rounded-2xl bg-surface-2 px-3 py-2">
              <p className="text-[13px] font-bold text-text">{commentAuthorName}</p>
              <TextWithReferences content={comment.content} mentions={comment.mentions} className="mt-0.5 text-sm leading-5 text-text whitespace-pre-wrap" />
            </div>
            <div className="flex items-center gap-3 px-2 pt-1 text-xs font-semibold text-text-muted">
              <time dateTime={comment.createdAtUtc} title={formatPostTimestamp(comment.createdAtUtc).absolute}>{formatPostTimestamp(comment.createdAtUtc).compact}</time>
              {comment.authorUserId === currentUserId && <><button type="button" onClick={() => onEdit(comment)} className="border-0 bg-transparent p-0 text-xs font-semibold text-text-muted hover:text-text">{editLabel}</button><button type="button" onClick={() => onDelete(comment)} className="border-0 bg-transparent p-0 text-xs font-semibold text-[#ff8a9b]">{deleteLabel}</button></>}
            </div>
          </div>
        </div>
      )
    })}
    {!isLoading && comments.length === 0 && <p className="py-5 text-center text-sm text-text-muted">Chưa có bình luận nào.</p>}
    <PaginationControls hasMore={hasMore} isLoading={isLoadingMore} error={paginationError} label={loadMoreLabel} onLoadMore={onLoadMore} />
  </>
}

interface CommentComposerProps {
  currentUserProfile?: UserProfile
  currentUserName: string
  value: string
  placeholder: string
  sendLabel: string
  onChange: (event: ChangeEvent<HTMLInputElement>) => void
  onSubmit: (event: FormEvent<HTMLFormElement>) => void
}

export function CommentComposer({
  currentUserProfile,
  currentUserName,
  value,
  placeholder,
  sendLabel,
  onChange,
  onSubmit,
}: CommentComposerProps) {
  return <form onSubmit={onSubmit} className="flex shrink-0 items-center gap-2 border-t border-border bg-surface px-3 py-3">
    <div className="flex h-9 w-9 shrink-0 items-center justify-center overflow-hidden rounded-full bg-primary text-xs font-bold text-white">
      {currentUserProfile?.avatarUrl ? <img src={resolveProfileImageUrl(currentUserProfile.avatarUrl)} alt="" className="h-full w-full object-cover" /> : currentUserName.slice(0, 2).toUpperCase()}
    </div>
    <input value={value} onChange={onChange} placeholder={placeholder} className="min-w-0 flex-1 rounded-full border-0 bg-surface-2 px-4 py-2.5 text-sm text-text outline-none ring-1 ring-transparent focus:ring-primary" />
    <button type="submit" disabled={!value.trim()} className="rounded-full border-0 bg-transparent px-2 text-sm font-bold text-primary disabled:cursor-not-allowed disabled:opacity-40">{sendLabel}</button>
  </form>
}
