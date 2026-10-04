import type { Comment, CommentAuthor, Post, postsApi } from '../../../api/posts'

type DiscussionApi = Pick<typeof postsApi, 'getById' | 'getComments' | 'createComment' | 'updateComment' | 'deleteComment' | 'setCommentReaction' | 'removeCommentReaction'>

export interface DiscussionComment extends Comment {
  clientId?: string
  pending?: boolean
}

export interface PostDiscussionSnapshot {
  comments: readonly DiscussionComment[]
  commentCount: number
  commentsLoaded: boolean
  commentsLoading: boolean
  commentsLoadingMore: boolean
  commentsError: string | null
  commentsHasMore: boolean
  commentSubmitting: boolean
  busyCommentIds: readonly string[]
}

export function createPostDiscussionState(post: Post, api: DiscussionApi, onError: (error: unknown, fallback: string) => void) {
  const listeners = new Set<() => void>()
  const pagedIds = new Set<string>()
  const localCreatedIds = new Set<string>()
  const deletedIds = new Set<string>()
  const modifiedVersions = new Map<string, number>()
  const busyIds = new Set<string>()
  let comments: DiscussionComment[] = []
  let confirmedCount = post.commentCount
  let knownServerTotal = post.commentCount
  let lastPropCount = post.commentCount
  let offset = 0
  let loaded = false
  let reading = false
  let loadingMore = false
  let readError: string | null = null
  let submitting = false
  let version = 0
  let deletionVersion = 0
  let needsCountRefresh = false
  let refreshingCount = false
  const hasMore = () => offset < Math.max(0, confirmedCount - localCreatedIds.size)
  const makeSnapshot = (): PostDiscussionSnapshot => ({
    comments, commentCount: Math.max(confirmedCount + (submitting ? 1 : 0), knownServerTotal, comments.length),
    commentsLoaded: loaded, commentsLoading: reading && !loadingMore,
    commentsLoadingMore: reading && loadingMore, commentsError: readError,
    commentsHasMore: hasMore(), commentSubmitting: submitting, busyCommentIds: [...busyIds],
  })
  let snapshot = makeSnapshot()
  const publish = () => { snapshot = makeSnapshot(); listeners.forEach((listener) => listener()) }
  const ordered = (items: Iterable<DiscussionComment>) => [...items].sort((a, b) =>
    a.createdAtUtc.localeCompare(b.createdAtUtc) || a.id.localeCompare(b.id))

  const refreshCommentCount = async () => {
    if (!needsCountRefresh || refreshingCount || submitting || reading || busyIds.size > 0) return
    needsCountRefresh = false
    refreshingCount = true
    const readVersion = version
    try {
      // A comments page read during a write may include that write or precede it.
      // The existing Post GET, started after the write settles, resolves this ambiguity.
      const response = await api.getById(post.id)
      if (version === readVersion && !submitting && busyIds.size === 0) {
        confirmedCount = response.commentCount
        knownServerTotal = response.commentCount
      }
      else needsCountRefresh = true
    } catch {
      // The write already succeeded/rolled back. Keep the local count until the next read.
    } finally {
      refreshingCount = false
      publish()
    }
    if (needsCountRefresh) void refreshCommentCount()
  }

  const loadComments = async (more = false) => {
    if (reading || (!more && loaded) || (more && !hasMore())) return
    reading = true
    loadingMore = more
    readError = null
    const readVersion = version
    const readDeletionVersion = deletionVersion
    publish()
    let retryPage = false
    try {
      const page = await api.getComments(post.id, more ? offset : 0)
      if (deletionVersion !== readDeletionVersion) {
        // Deleting an earlier row shifts offset pagination. Read at the corrected offset.
        retryPage = true
      } else {
        const merged = new Map(comments.map((comment) => [comment.id, comment]))
        for (const comment of page.items) {
          if (deletedIds.has(comment.id)) continue
          const current = merged.get(comment.id)
          if ((modifiedVersions.get(comment.id) ?? 0) <= readVersion) {
            merged.set(comment.id, { ...comment, clientId: current?.clientId })
          }
          pagedIds.add(comment.id)
          localCreatedIds.delete(comment.id)
        }
        comments = ordered(merged.values())
        knownServerTotal = page.total
        offset = page.offset + page.items.length
        // A response read before a local write cannot replace that write's count.
        if (version === readVersion && !submitting) confirmedCount = page.total
        else needsCountRefresh = true
        loaded = true
      }
    } catch (error) {
      readError = error instanceof Error ? error.message : 'Không thể tải bình luận. Vui lòng thử lại.'
    } finally {
      reading = false
      publish()
    }
    if (retryPage) await loadComments(more)
    else void refreshCommentCount()
  }

  const createComment = async (content: string, author: CommentAuthor, parentCommentId?: string) => {
    if (submitting || !content.trim()) return null
    const clientId = `pending:${crypto.randomUUID()}`
    const optimistic: DiscussionComment = {
      id: clientId, clientId, pending: true, postId: post.id, authorUserId: author.userId,
      parentCommentId: parentCommentId ?? null, content: content.trim(),
      createdAtUtc: new Date().toISOString(), updatedAtUtc: null, reactionCounts: {}, viewerReaction: null, author,
    }
    submitting = true
    version += 1
    comments = [...comments, optimistic]
    publish()
    try {
      const response = await api.createComment(post.id, content.trim(), parentCommentId)
      // A page may already contain the server ID. Keep exactly one row and its stable client key.
      comments = ordered(comments
        .filter((comment) => comment.id !== response.id || comment.clientId === clientId)
        .map((comment) => comment.clientId === clientId ? { ...response, clientId, pending: false } : comment))
      confirmedCount += 1
      if (!pagedIds.has(response.id)) localCreatedIds.add(response.id)
      modifiedVersions.set(response.id, ++version)
      return response
    } catch (error) {
      comments = comments.filter((comment) => comment.clientId !== clientId)
      version += 1
      onError(error, 'Không thể gửi bình luận. Vui lòng thử lại.')
      return null
    } finally {
      submitting = false
      confirmedCount = Math.max(confirmedCount, knownServerTotal, comments.length)
      publish()
      void refreshCommentCount()
    }
  }

  const mutateComment = async (id: string, request: () => Promise<Comment>, fallback: string) => {
    if (busyIds.has(id) || !comments.some((comment) => comment.id === id && !comment.pending)) return null
    busyIds.add(id)
    publish()
    try {
      const response = await request()
      comments = comments.map((comment) => comment.id === id ? { ...response, clientId: comment.clientId } : comment)
      modifiedVersions.set(id, ++version)
      return response
    } catch (error) {
      onError(error, fallback)
      return null
    } finally {
      busyIds.delete(id)
      publish()
      void refreshCommentCount()
    }
  }

  const deleteComment = async (id: string) => {
    if (busyIds.has(id) || !comments.some((comment) => comment.id === id && !comment.pending)) return false
    busyIds.add(id)
    publish()
    try {
      await api.deleteComment(id)
      // The existing backend soft-deletes only this comment, retaining its replies.
      comments = comments.filter((comment) => comment.id !== id)
      confirmedCount = Math.max(0, confirmedCount - 1)
      knownServerTotal = Math.max(0, knownServerTotal - 1)
      if (pagedIds.delete(id)) offset = Math.max(0, offset - 1)
      localCreatedIds.delete(id)
      deletedIds.add(id)
      version += 1
      deletionVersion += 1
      return true
    } catch (error) {
      onError(error, 'Không thể xóa bình luận. Vui lòng thử lại.')
      return false
    } finally {
      busyIds.delete(id)
      publish()
      void refreshCommentCount()
    }
  }

  return {
    getSnapshot: () => snapshot,
    subscribe: (listener: () => void) => { listeners.add(listener); return () => { listeners.delete(listener) } },
    get canEvict() { return listeners.size === 0 && !reading && !submitting && !refreshingCount && busyIds.size === 0 },
    syncPost: (next: Post) => {
      if (next.commentCount === lastPropCount) return
      lastPropCount = next.commentCount
      if (reading || submitting || busyIds.size > 0) return
      confirmedCount = next.commentCount
      knownServerTotal = next.commentCount
      version += 1
      publish()
    },
    loadComments,
    createComment,
    deleteComment,
    updateComment: (id: string, content: string) => mutateComment(id, () => api.updateComment(id, content.trim()), 'Không thể sửa bình luận. Vui lòng thử lại.'),
    reactToComment: (id: string, type?: string) => mutateComment(id,
      () => type ? api.setCommentReaction(id, type) : api.removeCommentReaction(id), 'Không thể cập nhật cảm xúc bình luận. Vui lòng thử lại.'),
  }
}

export type PostDiscussionState = ReturnType<typeof createPostDiscussionState>
