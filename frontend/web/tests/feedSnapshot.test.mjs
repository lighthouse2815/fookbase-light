import assert from 'node:assert/strict'
import test from 'node:test'
import { clearFeedSnapshot, readFeedSnapshot, saveFeedSnapshot } from '../src/pages/feed/feedSnapshot.ts'

test('feed snapshots keep the reading context and never cross accounts', () => {
  const snapshot = { mode: 'following', posts: [{ id: 'p1' }], nextCursor: 'older', asOfUtc: '2026-10-05T00:00:00Z', scrollY: 1540, savedAt: 1000 }
  saveFeedSnapshot('u1', snapshot)
  assert.deepEqual(readFeedSnapshot('u1', 2000), snapshot)
  assert.equal(readFeedSnapshot('u2', 2000), null)
  assert.equal(readFeedSnapshot('u1', 301001), null)
  clearFeedSnapshot()
  assert.equal(readFeedSnapshot('u1', 2000), null)
})
