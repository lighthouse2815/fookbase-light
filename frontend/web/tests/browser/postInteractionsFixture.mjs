import { fixtures } from './lastSignalFixture.mjs'

export const viewerId = '00000000-0000-0000-0000-000000000007'
export const postId = '00000000-0000-0000-0000-000000000101'
export const basePost = {
  id: postId, authorUserId: viewerId, content: 'Bài viết kiểm tra tương tác', privacy: 'public',
  createdAtUtc: '2026-10-01T08:00:00Z', updatedAtUtc: null, mediaIds: [],
  commentCount: 0, shareCount: 0, reactionCounts: { like: 2, love: 1 }, viewerReaction: null,
  displayAuthor: { type: 'user', id: viewerId, username: 'explorer', name: 'Người kiểm tra', avatarUrl: null },
  containerType: 'profile', contentType: 'standardPost', isPinned: false, viewerHasSaved: false,
}

export async function postFixtures(context, initial = {}) {
  await fixtures(context)
  await context.addInitScript(() => localStorage.setItem('fookbase.preferences', JSON.stringify({ language: 'vi', theme: 'dark' })))
  const state = { post: { ...basePost, ...initial }, comments: [], writes: [], reactionDelay: 350, failReaction: false, commentDelay: 350, failComment: false, actionDelay: 400, failAction: false, feedReads: 0 }
  await context.route('**/api/**', async (route) => {
    const request = route.request()
    const url = new URL(request.url())
    const path = url.pathname
    if (!path.startsWith('/api/')) return route.fallback()
    if (path === `/api/posts/${postId}`) {
      if (request.method() === 'PUT') {
        state.writes.push({ action: 'edit', ...request.postDataJSON() })
        const fail = state.failAction
        await new Promise((resolve) => setTimeout(resolve, state.actionDelay))
        if (fail) return route.fulfill({ status: 503, json: { detail: 'Không thể lưu bài viết thử nghiệm.' } })
        state.post = { ...state.post, ...request.postDataJSON(), updatedAtUtc: new Date().toISOString() }
      }
      return route.fulfill({ json: state.post })
    }
    if (path === `/api/posts/${postId}/save`) {
      state.writes.push({ action: 'save', method: request.method() })
      await new Promise((resolve) => setTimeout(resolve, state.actionDelay))
      state.post = { ...state.post, viewerHasSaved: request.method() === 'POST' }
      return route.fulfill({ status: 204 })
    }
    if (path.startsWith('/api/posts/comments/')) {
      const id = path.split('/')[4]
      const current = state.comments.find((comment) => comment.id === id)
      if (!current) return route.fulfill({ status: 404, json: { detail: 'Bình luận không tồn tại.' } })
      state.writes.push({ action: path.endsWith('/reaction') ? 'comment-reaction' : 'comment-edit', id })
      await new Promise((resolve) => setTimeout(resolve, state.actionDelay))
      if (path.endsWith('/reaction')) {
        const type = request.method() === 'DELETE' ? null : request.postDataJSON().type
        current.reactionCounts = type ? { [type]: 1 } : {}
        current.viewerReaction = type
      } else if (request.method() === 'PUT') {
        current.content = request.postDataJSON().content
        current.updatedAtUtc = new Date().toISOString()
      }
      return route.fulfill({ json: current })
    }
    if (path === `/api/posts/${postId}/reaction`) {
      const type = request.method() === 'DELETE' ? null : request.postDataJSON().type
      state.writes.push({ action: 'reaction', type })
      const fail = state.failReaction
      await new Promise((resolve) => setTimeout(resolve, state.reactionDelay))
      if (fail) return route.fulfill({ status: 503, json: { detail: 'Không thể lưu cảm xúc thử nghiệm.' } })
      const counts = { ...state.post.reactionCounts }
      if (state.post.viewerReaction) counts[state.post.viewerReaction]--
      if (type) counts[type] = (counts[type] ?? 0) + 1
      state.post = { ...state.post, viewerReaction: type, reactionCounts: counts }
      return route.fulfill({ json: state.post })
    }
    if (path === `/api/posts/${postId}/comments`) {
      if (request.method() === 'GET') {
        const offset = Number(url.searchParams.get('offset') ?? 0)
        const limit = Number(url.searchParams.get('limit') ?? 20)
        return route.fulfill({ json: { items: state.comments.slice(offset, offset + limit), total: state.comments.length, offset, limit } })
      }
      state.writes.push({ action: 'comment', ...request.postDataJSON() })
      const fail = state.failComment
      await new Promise((resolve) => setTimeout(resolve, state.commentDelay))
      if (fail) return route.fulfill({ status: 503, json: { detail: 'Không thể gửi bình luận thử nghiệm.' } })
      const body = request.postDataJSON()
      const comment = { id: `comment-${state.comments.length + 1}`, postId, authorUserId: viewerId, parentCommentId: body.parentCommentId ?? null, content: body.content, createdAtUtc: new Date().toISOString(), updatedAtUtc: null, reactionCounts: {}, viewerReaction: null, author: { userId: viewerId, username: 'explorer', displayName: 'Người kiểm tra', avatarUrl: null } }
      state.comments.push(comment)
      state.post = { ...state.post, commentCount: state.comments.length }
      return route.fulfill({ json: comment })
    }
    return route.fallback()
  })
  return state
}
