import assert from 'node:assert/strict'
import test from 'node:test'
import { createPostInteractionState } from '../src/pages/feed/components/postInteractionState.ts'
import { dismissToast, showToast, toastState } from '../src/shared/toastState.ts'

const post = { id: 'post-1', viewerReaction: null, reactionCounts: { like: 3, love: 2 }, commentCount: 4 }
const tick = () => new Promise((resolve) => setImmediate(resolve))

function setup(initial = post) {
  const requests = []
  const errors = []
  const request = (postId, type) => new Promise((resolve, reject) => requests.push({ postId, type, resolve, reject }))
  const state = createPostInteractionState(initial, {
    setReaction: request,
    removeReaction: (postId) => request(postId, null),
  }, (error) => errors.push(error))
  return { state, requests, errors }
}

test('reaction and counts change immediately, then use authoritative server counts', async () => {
  const { state, requests } = setup()
  state.selectReaction('love')
  assert.equal(state.getSnapshot().viewerReaction, 'love')
  assert.deepEqual(state.getSnapshot().reactionCounts, { like: 3, love: 3 })
  assert.equal(state.getSnapshot().reactionPending, true)
  assert.equal(requests[0].type, 'love')
  requests[0].resolve({ ...post, viewerReaction: 'love', reactionCounts: { like: 4, love: 3 } })
  await tick()
  assert.deepEqual(state.getSnapshot().reactionCounts, { like: 4, love: 3 })
  assert.equal(state.getSnapshot().reactionPending, false)
})

test('changing and removing a vote move just one count and preserve unknown server categories', async () => {
  const initial = { ...post, viewerReaction: 'like', reactionCounts: { like: 3, love: 2, futureType: 9 } }
  const { state, requests } = setup(initial)
  state.selectReaction('love')
  assert.deepEqual(state.getSnapshot().reactionCounts, { like: 2, love: 3, futureType: 9 })
  requests[0].resolve({ ...initial, viewerReaction: 'love', reactionCounts: { like: 2, love: 3, futureType: 9 } })
  await tick()
  state.selectReaction('love')
  assert.equal(requests[1].type, null)
  assert.deepEqual(state.getSnapshot().reactionCounts, { like: 2, love: 2, futureType: 9 })
  requests[1].resolve({ ...initial, viewerReaction: null, reactionCounts: { like: 2, love: 2, futureType: 9 } })
  await tick()
})

test('a failed final request restores the exact confirmed reaction and counts', async () => {
  const initial = { ...post, viewerReaction: 'like' }
  const { state, requests, errors } = setup(initial)
  state.selectReaction('angry')
  requests[0].reject(new Error('offline'))
  await tick()
  assert.equal(state.getSnapshot().viewerReaction, 'like')
  assert.deepEqual(state.getSnapshot().reactionCounts, initial.reactionCounts)
  assert.equal(state.getSnapshot().reactionPending, false)
  assert.equal(errors.length, 1)
})

test('a successful authoritative response settles without repeatedly rewriting a different server result', async () => {
  const { state, requests } = setup()
  state.selectReaction('like')
  requests[0].resolve(post)
  await tick()
  assert.equal(requests.length, 1)
  assert.equal(state.getSnapshot().viewerReaction, null)
  assert.deepEqual(state.getSnapshot().reactionCounts, post.reactionCounts)
  assert.equal(state.getSnapshot().reactionPending, false)
})

test('rapid choices stay optimistic and serialize/coalesce requests to the latest choice', async () => {
  const { state, requests } = setup()
  state.selectReaction('like')
  state.selectReaction('love')
  state.selectReaction('wow')
  assert.equal(requests.length, 1)
  assert.equal(state.getSnapshot().viewerReaction, 'wow')
  requests[0].resolve({ ...post, viewerReaction: 'like', reactionCounts: { like: 4, love: 2 } })
  await tick()
  assert.equal(requests.length, 2)
  assert.equal(requests[1].type, 'wow')
  assert.deepEqual(state.getSnapshot().reactionCounts, { like: 3, love: 2, wow: 1 })
  requests[1].resolve({ ...post, viewerReaction: 'wow', reactionCounts: { like: 3, love: 2, wow: 1 } })
  await tick()
  assert.equal(state.getSnapshot().reactionPending, false)
})

test('rapidly undoing a pending vote sends DELETE after PUT without count drift', async () => {
  const { state, requests } = setup()
  state.toggleReaction()
  state.toggleReaction()
  assert.equal(state.getSnapshot().viewerReaction, null)
  assert.deepEqual(state.getSnapshot().reactionCounts, post.reactionCounts)
  requests[0].resolve({ ...post, viewerReaction: 'like', reactionCounts: { like: 4, love: 2 } })
  await tick()
  assert.equal(requests[1].type, null)
  requests[1].resolve(post)
  await tick()
  assert.equal(state.getSnapshot().viewerReaction, null)
  assert.deepEqual(state.getSnapshot().reactionCounts, post.reactionCounts)
})

test('an earlier failure cannot discard a newer intent, including the same reaction', async () => {
  const { state, requests, errors } = setup()
  state.selectReaction('love')
  state.selectReaction('wow')
  state.selectReaction('love')
  requests[0].reject(new Error('first request failed'))
  await tick()
  assert.equal(state.getSnapshot().viewerReaction, 'love')
  assert.equal(requests[1].type, 'love')
  requests[1].resolve({ ...post, viewerReaction: 'love', reactionCounts: { like: 3, love: 3 } })
  await tick()
  assert.equal(errors.length, 1)
  assert.equal(state.getSnapshot().reactionPending, false)
})

test('rollback after a successful earlier write uses that last confirmed state', async () => {
  const { state, requests } = setup()
  state.selectReaction('love')
  state.selectReaction('wow')
  requests[0].resolve({ ...post, viewerReaction: 'love', reactionCounts: { like: 3, love: 3 } })
  await tick()
  requests[1].reject(new Error('last request failed'))
  await tick()
  assert.equal(state.getSnapshot().viewerReaction, 'love')
  assert.deepEqual(state.getSnapshot().reactionCounts, { like: 3, love: 3 })
})

test('two subscribers share changes and unsubscribing while pending is safe', async () => {
  const { state, requests } = setup()
  let first = 0
  let second = 0
  const unsubscribe = state.subscribe(() => first++)
  const other = state.subscribe(() => second++)
  state.selectReaction('love')
  unsubscribe()
  const before = first
  state.syncPost({ ...post })
  assert.equal(state.getSnapshot().viewerReaction, 'love')
  requests[0].resolve({ ...post, viewerReaction: 'love', reactionCounts: { like: 3, love: 3 } })
  await tick()
  assert.equal(first, before)
  assert.ok(second > first)
  other()
  assert.equal(state.canEvict, true)
})

test('sync uses genuinely changed server data but never overwrites a pending choice', async () => {
  const { state, requests } = setup()
  state.syncPost({ ...post, reactionCounts: { like: 8, love: 2 } })
  assert.equal(state.getSnapshot().reactionCounts.like, 8)
  state.selectReaction('love')
  state.syncPost({ ...post, reactionCounts: { like: 10, love: 2 } })
  assert.equal(state.getSnapshot().viewerReaction, 'love')
  requests[0].resolve({ ...post, viewerReaction: 'love', reactionCounts: { like: 10, love: 3 } })
  await tick()
})

test('equivalent stale props with reordered keys or omitted zero counts cannot erase a confirmed local vote', async () => {
  const initial = { ...post, reactionCounts: { like: 3, love: 2, wow: 0 } }
  const { state, requests } = setup(initial)
  state.selectReaction('like')
  requests[0].resolve({ ...post, viewerReaction: 'like', reactionCounts: { like: 4, love: 2 } })
  await tick()
  const confirmed = state.getSnapshot()
  state.syncPost({ ...post, reactionCounts: { love: 2, like: 3 } })
  assert.equal(state.getSnapshot(), confirmed)
  assert.equal(state.getSnapshot().viewerReaction, 'like')
  assert.deepEqual(state.getSnapshot().reactionCounts, { like: 4, love: 2 })
})

test('toasts deduplicate, bound the stack, dismiss and expire automatically', (t) => {
  t.mock.timers.enable({ apis: ['setTimeout'] })
  showToast('Không thể cập nhật', 'error', 'reaction:1')
  showToast('Không thể cập nhật', 'error', 'reaction:1')
  assert.equal(toastState.getSnapshot().length, 1)
  showToast('Hai', 'error', '2')
  showToast('Ba', 'error', '3')
  showToast('Bốn', 'error', '4')
  assert.equal(toastState.getSnapshot().length, 3)
  dismissToast('3')
  assert.equal(toastState.getSnapshot().length, 2)
  t.mock.timers.tick(5_000)
  assert.equal(toastState.getSnapshot().length, 0)
})
