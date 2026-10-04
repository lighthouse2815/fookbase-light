import { useEffect, useMemo, useSyncExternalStore } from 'react'
import { ApiError } from '../../../api/client'
import { postsApi, type Post } from '../../../api/posts'
import { getAuthSession } from '../../../auth/session'
import { showToast } from '../../../shared/toastState'
import { createPostInteractionState, type PostInteractionState } from './postInteractionState'
import { createPostDiscussionState, type PostDiscussionState } from './postDiscussionState'

const states = new Map<string, { reactions: PostInteractionState; discussion: PostDiscussionState }>()

function getState(post: Post, viewerId: string) {
  const key = `${viewerId}:${post.id}`
  const existing = states.get(key)
  if (existing) return existing

  if (states.size >= 200) {
    for (const [oldKey, state] of states) {
      if (state.reactions.canEvict && state.discussion.canEvict) states.delete(oldKey)
      if (states.size < 200) break
    }
  }
  const isCurrentViewer = () => getAuthSession()?.user.id === viewerId
  const writeReaction = async (postId: string, type: string | null) => {
    // The queue may outlive its card; never send a previous viewer's intent with a new session.
    if (!isCurrentViewer()) throw new Error('Phiên đăng nhập đã thay đổi.')
    return type ? postsApi.setReaction(postId, type) : postsApi.removeReaction(postId)
  }
  const reactions = createPostInteractionState(post, {
    setReaction: writeReaction,
    removeReaction: (postId) => writeReaction(postId, null),
  }, (error) => {
    if (!isCurrentViewer()) return
    showToast(error instanceof ApiError ? error.message : 'Không thể cập nhật cảm xúc. Vui lòng thử lại.', 'error', `reaction:${post.id}`)
  })
  const discussion = createPostDiscussionState(post, postsApi, (error, fallback) => {
    showToast(error instanceof ApiError ? error.message : fallback, 'error', `comment:${post.id}:${fallback}`)
  })
  const state = { reactions, discussion }
  states.set(key, state)
  return state
}

export function usePostInteractions(post: Post, viewerId: string) {
  const state = useMemo(() => getState(post, viewerId), [post, viewerId])
  const reactions = useSyncExternalStore(state.reactions.subscribe, state.reactions.getSnapshot)
  const discussion = useSyncExternalStore(state.discussion.subscribe, state.discussion.getSnapshot)
  useEffect(() => { state.reactions.syncPost(post); state.discussion.syncPost(post) }, [post, state])
  return { state: state.reactions, discussion: state.discussion, ...reactions, ...discussion }
}
