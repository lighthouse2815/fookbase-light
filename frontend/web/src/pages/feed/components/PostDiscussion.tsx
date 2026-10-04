import { useLayoutEffect, useRef } from 'react'
import type { ChangeEvent, FormEvent, ReactNode } from 'react'
import type { Comment, CommentAuthor } from '../../../api/posts'
import { resolveProfileImageUrl } from '../../../api/users'
import TextWithReferences from '../../../shared/components/TextWithReferences'
import { formatPostTimestamp } from '../../../shared/formatPostTimestamp'
import PaginationControls from '../../../shared/components/PaginationControls'
import type { ReactionType } from './reactionChoices'
import type { DiscussionComment } from './postDiscussionState'
import PostReactionPicker from './PostReactionPicker'

interface DiscussionListProps {
  initialCommentId?: string
  comments: readonly DiscussionComment[]
  commentAuthors: Readonly<Record<string, CommentAuthor>>
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
  onReply: (comment: Comment) => void
  onEdit: (comment: Comment) => void
  onDelete: (comment: Comment) => void
  onReact: (comment: Comment, type: ReactionType) => void
  onRemoveReaction: (comment: Comment) => void
  reactingCommentId: string | null
  busyCommentIds?: readonly string[]
  onRetryLoad?: () => void
}

function CommentEntry({ comment, isReply, isTarget, children }: { comment: DiscussionComment; isReply: boolean; isTarget: boolean; children: ReactNode }) {
  const elementRef = useRef<HTMLDivElement>(null)
  useLayoutEffect(() => {
    if (comment.pending || isTarget) elementRef.current?.scrollIntoView({ block: 'nearest' })
  }, [comment.pending, isTarget])
  return <div ref={elementRef} id={`comment-${comment.id}`} data-comment-id={comment.id} aria-busy={comment.pending || undefined} className={`flex items-start gap-2.5 ${comment.clientId ? 'post-comment-enter' : ''} ${isReply ? 'ml-8 sm:ml-12' : ''}`}>{children}</div>
}

export function DiscussionList({
  initialCommentId,
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
  onReply,
  onEdit,
  onDelete,
  onReact,
  onRemoveReaction,
  reactingCommentId,
  busyCommentIds = [],
  onRetryLoad,
}: DiscussionListProps) {
  const commentIds = new Set(comments.map((comment) => comment.id))
  const rootComments = comments.filter((comment) => !comment.parentCommentId || !commentIds.has(comment.parentCommentId))
  const repliesByParent = comments.reduce<Record<string, DiscussionComment[]>>((replies, comment) => {
    if (comment.parentCommentId && commentIds.has(comment.parentCommentId)) {
      ;(replies[comment.parentCommentId] ??= []).push(comment)
    }
    return replies
  }, {})

  const renderComment = (comment: DiscussionComment, isReply = false) => {
    const commentAuthor = commentAuthors[comment.authorUserId]
    const commentAuthorName = commentAuthor?.displayName ?? 'Người dùng'
    const disabled = Boolean(comment.pending || busyCommentIds.includes(comment.id) || reactingCommentId === comment.id)
    const reactionTotal = Object.values(comment.reactionCounts).reduce((sum, count) => sum + count, 0)
    return (
      <CommentEntry key={comment.clientId ?? comment.id} comment={comment} isReply={isReply} isTarget={comment.id === initialCommentId}>
        <div className="flex h-9 w-9 shrink-0 items-center justify-center overflow-hidden rounded-full bg-primary text-xs font-bold text-white">
          {commentAuthor?.avatarUrl ? <img src={resolveProfileImageUrl(commentAuthor.avatarUrl)} alt="" loading="lazy" decoding="async" className="h-full w-full object-cover" /> : commentAuthorName.slice(0, 2).toUpperCase()}
        </div>
        <div className="min-w-0 flex-1">
          <div className="inline-block max-w-full rounded-2xl bg-surface-2 px-3 py-2">
            <p className="text-[13px] font-bold text-text">{commentAuthorName}</p>
            <TextWithReferences content={comment.content} mentions={comment.mentions} className="mt-0.5 text-sm leading-5 text-text whitespace-pre-wrap" />
          </div>
          <div className="flex items-center gap-3 px-2 pt-1 text-xs font-semibold text-text-muted">
            <time dateTime={comment.createdAtUtc} title={formatPostTimestamp(comment.createdAtUtc).absolute}>{formatPostTimestamp(comment.createdAtUtc).compact}</time>
            <PostReactionPicker compact disabled={disabled} viewerReaction={comment.viewerReaction} onToggleDefault={() => onRemoveReaction(comment)} onSelect={(type) => onReact(comment, type)} />
            {reactionTotal > 0 && <span key={reactionTotal} className="post-count text-xs font-medium text-text-muted">{reactionTotal}</span>}
            <button type="button" disabled={disabled} onClick={() => onReply(comment)} className="post-action-focus border-0 bg-transparent p-0 text-xs font-semibold text-text-muted hover:text-text disabled:opacity-50">Trả lời</button>
            {comment.authorUserId === currentUserId && <><button type="button" disabled={disabled} onClick={() => onEdit(comment)} className="post-action-focus border-0 bg-transparent p-0 text-xs font-semibold text-text-muted hover:text-text disabled:opacity-50">{editLabel}</button><button type="button" disabled={disabled} onClick={() => onDelete(comment)} className="post-action-focus border-0 bg-transparent p-0 text-xs font-semibold text-danger disabled:opacity-50">{deleteLabel}</button></>}
          </div>
        </div>
      </CommentEntry>
    )
  }

  return <>
    {isLoading && <p className="text-center text-sm text-text-muted">{loadingLabel}</p>}
    {error && <div role="status" className="text-center text-xs text-danger"><p>{error}</p>{onRetryLoad && <button type="button" onClick={onRetryLoad} className="post-action-focus mt-2 rounded-lg bg-surface-2 px-3 py-2 font-semibold text-text">Thử lại</button>}</div>}
    {rootComments.map((comment) => <div key={comment.clientId ?? comment.id} className="space-y-3">{renderComment(comment)}{repliesByParent[comment.id]?.map((reply) => renderComment(reply, true))}</div>)}
    {!isLoading && comments.length === 0 && <p className="py-5 text-center text-sm text-text-muted">Chưa có bình luận nào.</p>}
    <PaginationControls hasMore={hasMore} isLoading={isLoadingMore} error={paginationError} label={loadMoreLabel} onLoadMore={onLoadMore} />
  </>
}

interface CommentComposerProps {
  currentUserProfile?: Pick<CommentAuthor, 'avatarUrl'>
  currentUserName: string
  value: string
  placeholder: string
  sendLabel: string
  isSubmitting?: boolean
  replyingToName?: string
  onCancelReply?: () => void
  onChange: (event: ChangeEvent<HTMLInputElement>) => void
  onSubmit: (event: FormEvent<HTMLFormElement>) => void
}

export function CommentComposer({
  currentUserProfile,
  currentUserName,
  value,
  placeholder,
  sendLabel,
  isSubmitting = false,
  replyingToName,
  onCancelReply,
  onChange,
  onSubmit,
}: CommentComposerProps) {
  return <form onSubmit={onSubmit} aria-label="Gửi bình luận" className="shrink-0 border-t border-border bg-surface px-3 py-3">
    {replyingToName && <div className="mb-2 flex items-center justify-between pl-11 text-xs text-text-muted"><span>Đang trả lời <strong className="text-text">{replyingToName}</strong></span><button type="button" onClick={onCancelReply} className="border-0 bg-transparent p-0 font-semibold text-primary hover:underline">Hủy</button></div>}
    <div className="flex items-center gap-2">
    <div className="flex h-9 w-9 shrink-0 items-center justify-center overflow-hidden rounded-full bg-primary text-xs font-bold text-white">
      {currentUserProfile?.avatarUrl ? <img src={resolveProfileImageUrl(currentUserProfile.avatarUrl)} alt="" className="h-full w-full object-cover" /> : currentUserName.slice(0, 2).toUpperCase()}
    </div>
    <input value={value} onChange={onChange} placeholder={placeholder} aria-label={placeholder} maxLength={5_000} autoComplete="off" className="min-w-0 flex-1 rounded-full border-0 bg-surface-2 px-4 py-2.5 text-sm text-text outline-none ring-1 ring-transparent focus:ring-primary" />
    <button type="submit" aria-busy={isSubmitting} disabled={!value.trim() || isSubmitting} className="post-action-focus rounded-full border-0 bg-transparent px-2 text-sm font-bold text-primary disabled:cursor-not-allowed disabled:opacity-40">{sendLabel}</button>
    </div>
  </form>
}
