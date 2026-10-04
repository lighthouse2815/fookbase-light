import { useEffect, useMemo, useSyncExternalStore } from 'react'
import { ApiError } from '../../../api/client'
import { postsApi, type Post } from '../../../api/posts'
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
  const reactions = createPostInteractionState(post, postsApi, (error) => {
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
