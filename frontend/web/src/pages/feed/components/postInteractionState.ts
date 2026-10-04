import type { Post } from '../../../api/posts'
import type { ReactionType } from './reactionChoices'

type ReactionApi = {
  setReaction: (postId: string, type: string) => Promise<Post>
  removeReaction: (postId: string) => Promise<Post>
}

export interface PostInteractionSnapshot {
  viewerReaction: string | null
  reactionCounts: Record<string, number>
  reactionPending: boolean
  reactionVersion: number
}

const reactionFingerprint = (post: Post) => JSON.stringify([post.viewerReaction, post.reactionCounts])

// Move only the viewer's vote; all other counts (including future server types) stay intact.
function withViewerReaction(counts: Record<string, number>, previous: string | null, next: string | null) {
  const result = { ...counts }
  if (previous === next) return result
  if (previous) result[previous] = Math.max(0, (result[previous] ?? 0) - 1)
  if (next) result[next] = (result[next] ?? 0) + 1
  return result
}

export function createPostInteractionState(post: Post, api: ReactionApi, onReactionError: (error: unknown) => void) {
  let confirmedReaction = post.viewerReaction
  let confirmedCounts = { ...post.reactionCounts }
  let desiredReaction = confirmedReaction
  let version = 0
  let running = false
  let lastPropFingerprint = reactionFingerprint(post)
  const listeners = new Set<() => void>()
  let snapshot: PostInteractionSnapshot = {
    viewerReaction: confirmedReaction,
    reactionCounts: confirmedCounts,
    reactionPending: false,
    reactionVersion: 0,
  }

  const publish = () => {
    snapshot = {
      viewerReaction: desiredReaction,
      reactionCounts: withViewerReaction(confirmedCounts, confirmedReaction, desiredReaction),
      reactionPending: running || desiredReaction !== confirmedReaction,
      reactionVersion: version,
    }
    listeners.forEach((listener) => listener())
  }

  // At most one write per post. Intermediate choices collapse into the latest intent.
  const drainReactions = async () => {
    if (running) return
    running = true
    while (desiredReaction !== confirmedReaction) {
      const requestedReaction = desiredReaction
      const requestedVersion = version
      try {
        const response = requestedReaction
          ? await api.setReaction(post.id, requestedReaction)
          : await api.removeReaction(post.id)
        confirmedReaction = response.viewerReaction
        confirmedCounts = { ...response.reactionCounts }
      } catch (error) {
        // A newer choice must survive an earlier failure. Otherwise restore the confirmed vote.
        if (version === requestedVersion) desiredReaction = confirmedReaction
        onReactionError(error)
      }
      publish()
    }
    running = false
    publish()
  }

  const choose = (next: string | null) => {
    desiredReaction = next
    version += 1
    publish()
    void drainReactions()
  }

  return {
    getSnapshot: () => snapshot,
    subscribe: (listener: () => void) => {
      listeners.add(listener)
      return () => { listeners.delete(listener) }
    },
    get canEvict() { return listeners.size === 0 && !running },
    syncPost: (nextPost: Post) => {
      const fingerprint = reactionFingerprint(nextPost)
      // A second card can still receive the original feed object after a local mutation.
      if (fingerprint === lastPropFingerprint) return
      lastPropFingerprint = fingerprint
      if (running) return
      confirmedReaction = nextPost.viewerReaction
      confirmedCounts = { ...nextPost.reactionCounts }
      desiredReaction = confirmedReaction
      publish()
    },
    selectReaction: (type: ReactionType) => choose(desiredReaction === type ? null : type),
    toggleReaction: () => choose(desiredReaction ? null : 'like'),
  }
}

export type PostInteractionState = ReturnType<typeof createPostInteractionState>
