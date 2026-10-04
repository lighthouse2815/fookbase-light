import assert from 'node:assert/strict'
import test from 'node:test'
import { createPostDiscussionState } from '../src/pages/feed/components/postDiscussionState.ts'

const author = { userId: 'viewer', username: 'viewer', displayName: 'Người dùng', avatarUrl: null }
const post = { id: 'post-1', commentCount: 0 }
const comment = (id, content = id, parentCommentId = null) => ({ id, content, parentCommentId, postId: post.id, authorUserId: author.userId, author, createdAtUtc: `2026-01-01T00:00:${id.slice(-2).padStart(2, '0')}Z`, updatedAtUtc: null, reactionCounts: {}, viewerReaction: null })
const tick = () => new Promise((resolve) => setImmediate(resolve))
const page = (items, total = items.length, offset = 0) => ({ items, total, offset, limit: 20 })

function setup(total = 0) {
  const requests = []
  const errors = []
  const request = (action, ...args) => new Promise((resolve, reject) => requests.push({ action, args, resolve, reject }))
  const state = createPostDiscussionState({ ...post, commentCount: total }, {
    getComments: (...args) => request('get', ...args),
    createComment: (...args) => request('create', ...args),
    updateComment: (...args) => request('update', ...args),
    deleteComment: (...args) => request('delete', ...args),
    setCommentReaction: (...args) => request('react', ...args),
    removeCommentReaction: (...args) => request('unreact', ...args),
  }, (...args) => errors.push(args))
  return { state, requests, errors }
}

test('new comment and count appear before the API finishes; server response preserves the row key', async () => {
  const { state, requests } = setup()
  const result = state.createComment('  Xin chào  ', author)
  const optimistic = state.getSnapshot().comments[0]
  assert.equal(optimistic.content, 'Xin chào')
  assert.equal(optimistic.pending, true)
  assert.equal(state.getSnapshot().commentCount, 1)
  assert.equal(state.getSnapshot().commentSubmitting, true)
  const response = comment('real-01', 'Xin chào')
  requests[0].resolve(response)
  assert.deepEqual(await result, response)
  assert.equal(state.getSnapshot().comments.length, 1)
  assert.equal(state.getSnapshot().comments[0].id, response.id)
  assert.equal(state.getSnapshot().comments[0].clientId, optimistic.clientId)
  assert.equal(state.getSnapshot().comments[0].pending, false)
  assert.equal(state.getSnapshot().commentCount, 1)
  assert.equal(state.getSnapshot().commentSubmitting, false)
})

test('double submit across cards produces one POST and empty submissions produce none', async () => {
  const { state, requests } = setup()
  assert.equal(await state.createComment(' ', author), null)
  const first = state.createComment('Một lần', author)
  assert.equal(await state.createComment('Một lần', author), null)
  assert.equal(requests.length, 1)
  requests[0].resolve(comment('real-01'))
  await first
})

test('failure removes only the optimistic comment and restores the original count', async () => {
  const { state, requests, errors } = setup(2)
  const loaded = state.loadComments()
  requests[0].resolve(page([comment('real-01'), comment('real-02')]))
  await loaded
  const submission = state.createComment('Không gửi được', author)
  assert.equal(state.getSnapshot().commentCount, 3)
  requests[1].reject(new Error('offline'))
  assert.equal(await submission, null)
  assert.equal(state.getSnapshot().commentCount, 2)
  assert.deepEqual(state.getSnapshot().comments.map(({ id }) => id), ['real-01', 'real-02'])
  assert.equal(errors.length, 1)
  assert.equal(state.getSnapshot().commentSubmitting, false)
})

test('first-page reads cannot erase a pending comment or a locally confirmed count', async () => {
  const { state, requests } = setup(1)
  const loading = state.loadComments()
  const submission = state.createComment('Mới', author)
  requests[1].resolve(comment('real-02', 'Mới'))
  await submission
  requests[0].resolve(page([comment('real-01')], 1))
  await loading
  assert.equal(state.getSnapshot().comments.length, 2)
  assert.equal(state.getSnapshot().commentCount, 2)
  assert.equal(state.getSnapshot().commentsHasMore, false)
})

test('a read containing the server-created ID reconciles without duplicates or counting twice', async () => {
  const { state, requests } = setup()
  const loading = state.loadComments()
  const submission = state.createComment('Mới', author)
  const key = state.getSnapshot().comments[0].clientId
  const response = comment('real-01', 'Mới')
  requests[0].resolve(page([response]))
  await loading
  requests[1].resolve(response)
  await submission
  assert.equal(state.getSnapshot().comments.length, 1)
  assert.equal(state.getSnapshot().comments[0].clientId, key)
  assert.equal(state.getSnapshot().commentCount, 1)
})

test('pagination keeps its server offset after submitting a comment on a partially loaded thread', async () => {
  const { state, requests } = setup(4)
  const loading = state.loadComments()
  requests[0].resolve(page([comment('real-01'), comment('real-02')], 4))
  await loading
  const submission = state.createComment('Mới', author)
  requests[1].resolve(comment('real-05'))
  await submission
  const more = state.loadComments(true)
  assert.deepEqual(requests[2].args, ['post-1', 2])
  requests[2].resolve(page([comment('real-03'), comment('real-04'), comment('real-05')], 5, 2))
  await more
  assert.equal(state.getSnapshot().comments.length, 5)
  assert.equal(state.getSnapshot().commentCount, 5)
  assert.equal(state.getSnapshot().commentsHasMore, false)
})

test('concurrent loads deduplicate, failures stay retryable and empty results are cached', async () => {
  const { state, requests } = setup()
  const loading = state.loadComments()
  await state.loadComments()
  assert.equal(requests.length, 1)
  requests[0].reject(new Error('Không thể tải'))
  await loading
  assert.equal(state.getSnapshot().commentsLoaded, false)
  assert.match(state.getSnapshot().commentsError, /Không thể tải/)
  const retry = state.loadComments()
  requests[1].resolve(page([]))
  await retry
  await state.loadComments()
  assert.equal(requests.length, 2)
})

test('parent deletion keeps replies according to the existing backend behavior', async () => {
  const { state, requests } = setup(2)
  const loading = state.loadComments()
  requests[0].resolve(page([comment('real-01'), comment('real-02', 'Trả lời', 'real-01')]))
  await loading
  const deleting = state.deleteComment('real-01')
  requests[1].resolve()
  assert.equal(await deleting, true)
  assert.deepEqual(state.getSnapshot().comments.map(({ id }) => id), ['real-02'])
  assert.equal(state.getSnapshot().commentCount, 1)
})

test('deleting an earlier row during pagination rereads the corrected offset without skipping a comment', async () => {
  const { state, requests } = setup(4)
  const loading = state.loadComments()
  requests[0].resolve(page([comment('real-01'), comment('real-02')], 4))
  await loading
  const more = state.loadComments(true)
  const deleting = state.deleteComment('real-01')
  requests[2].resolve()
  await deleting
  requests[1].resolve(page([comment('real-03'), comment('real-04')], 4, 2))
  await tick()
  assert.deepEqual(requests[3].args, ['post-1', 1])
  requests[3].resolve(page([comment('real-03'), comment('real-04')], 3, 1))
  await more
  assert.deepEqual(state.getSnapshot().comments.map(({ id }) => id), ['real-02', 'real-03', 'real-04'])
  assert.equal(state.getSnapshot().commentCount, 3)
})

test('comment edits/reactions are shared, guarded per comment and cannot act on temporary IDs', async () => {
  const { state, requests } = setup(1)
  const loading = state.loadComments()
  requests[0].resolve(page([comment('real-01')]))
  await loading
  const edit = state.updateComment('real-01', 'Đã sửa')
  assert.equal(await state.reactToComment('real-01', 'love'), null)
  requests[1].resolve(comment('real-01', 'Đã sửa'))
  await edit
  assert.equal(state.getSnapshot().comments[0].content, 'Đã sửa')
  const submission = state.createComment('Mới', author)
  const tempId = state.getSnapshot().comments.find((item) => item.pending).id
  assert.equal(await state.deleteComment(tempId), false)
  requests[2].resolve(comment('real-02'))
  await submission
})

test('newer comment edits survive an older page response', async () => {
  const { state, requests } = setup(2)
  const loading = state.loadComments()
  requests[0].resolve(page([comment('real-01')], 2))
  await loading
  const more = state.loadComments(true)
  const edit = state.updateComment('real-01', 'Đã sửa')
  requests[2].resolve(comment('real-01', 'Đã sửa'))
  await edit
  requests[1].resolve(page([comment('real-01'), comment('real-02')], 2, 1))
  await more
  assert.equal(state.getSnapshot().comments.find(({ id }) => id === 'real-01').content, 'Đã sửa')
})

test('unmounting subscribers does not interrupt reconciliation or notify removed listeners', async () => {
  const { state, requests } = setup()
  let calls = 0
  const unsubscribe = state.subscribe(() => calls++)
  const submission = state.createComment('Mới', author)
  unsubscribe()
  const before = calls
  state.syncPost({ ...post })
  assert.equal(state.canEvict, false)
  requests[0].resolve(comment('real-01'))
  await submission
  assert.equal(calls, before)
  assert.equal(state.getSnapshot().commentCount, 1)
  assert.equal(state.canEvict, true)
})
