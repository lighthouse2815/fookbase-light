import { useState } from 'react'
import type { ChangeEvent, FormEvent } from 'react'
import type { Comment } from '../../../api/posts'
import { resolveProfileImageUrl } from '../../../api/users'
import type { UserProfile } from '../../../api/users'
import TextWithReferences from '../../../shared/components/TextWithReferences'
import { formatPostTimestamp } from '../../../shared/formatPostTimestamp'
import PaginationControls from '../../../shared/components/PaginationControls'
import { reactionChoices } from './reactionChoices'
import type { ReactionType } from './reactionChoices'

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
  onReact: (comment: Comment, type: ReactionType) => void
  onRemoveReaction: (comment: Comment) => void
  reactingCommentId: string | null
}

function CommentReactionControl({
  comment,
  isReacting,
  onReact,
  onRemoveReaction,
}: {
  comment: Comment
  isReacting: boolean
  onReact: (type: ReactionType) => void
  onRemoveReaction: () => void
}) {
  const [isPickerOpen, setIsPickerOpen] = useState(false)
  const selectedReaction = reactionChoices.find(({ type }) => type === comment.viewerReaction)
  const reactionTotal = Object.values(comment.reactionCounts).reduce((total, count) => total + count, 0)

  return <div className="group relative flex items-center gap-2">
    <button type="button" disabled={isReacting} onClick={() => {
      if (selectedReaction) onRemoveReaction()
      else setIsPickerOpen((current) => !current)
    }} className={`border-0 bg-transparent p-0 text-xs font-semibold disabled:cursor-wait disabled:opacity-70 ${selectedReaction?.color ?? 'text-text-muted hover:text-text'}`} aria-label={selectedReaction ? `Bỏ cảm xúc ${selectedReaction.label}` : 'Thêm cảm xúc'}>
      {selectedReaction ? `${selectedReaction.icon} ${selectedReaction.label}` : 'Thích'}
    </button>
    {reactionTotal > 0 && <span className="text-xs font-medium text-text-muted">{reactionTotal}</span>}
    <div className={`absolute bottom-[calc(100%+6px)] left-0 z-30 items-center rounded-full border border-border bg-surface px-1.5 py-1 shadow-xl ${isPickerOpen ? 'flex' : 'hidden'} group-hover:flex group-focus-within:flex`} role="group" aria-label="Chọn cảm xúc cho bình luận">
      {reactionChoices.map(({ type, icon, label }) => <button key={type} type="button" disabled={isReacting} onClick={() => {
        setIsPickerOpen(false)
        onReact(type)
      }} className="grid h-8 w-8 place-items-center rounded-full border-0 bg-transparent p-0 text-[22px] leading-none transition-transform hover:-translate-y-1 hover:scale-125 disabled:opacity-70" aria-label={label} title={label}>{icon}</button>)}
    </div>
  </div>
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
  onReact,
  onRemoveReaction,
  reactingCommentId,
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
              <CommentReactionControl comment={comment} isReacting={reactingCommentId === comment.id} onReact={(type) => onReact(comment, type)} onRemoveReaction={() => onRemoveReaction(comment)} />
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
