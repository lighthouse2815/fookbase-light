import { useEffect, useMemo, useSyncExternalStore } from 'react'
import { ApiError } from '../../../api/client'
import { postsApi, type Post } from '../../../api/posts'
import { showToast } from '../../../shared/toastState'
import { createPostInteractionState, type PostInteractionState } from './postInteractionState'

const states = new Map<string, PostInteractionState>()

function getState(post: Post, viewerId: string) {
  const key = `${viewerId}:${post.id}`
  const existing = states.get(key)
  if (existing) return existing

  if (states.size >= 200) {
    for (const [oldKey, state] of states) {
      if (state.canEvict) states.delete(oldKey)
      if (states.size < 200) break
    }
  }
  const state = createPostInteractionState(post, postsApi, (error) => {
    showToast(error instanceof ApiError ? error.message : 'Không thể cập nhật cảm xúc. Vui lòng thử lại.', 'error', `reaction:${post.id}`)
  })
  states.set(key, state)
  return state
}

export function usePostInteractions(post: Post, viewerId: string) {
  const state = useMemo(() => getState(post, viewerId), [post, viewerId])
  const snapshot = useSyncExternalStore(state.subscribe, state.getSnapshot)
  useEffect(() => { state.syncPost(post) }, [post, state])
  return { state, ...snapshot }
}
