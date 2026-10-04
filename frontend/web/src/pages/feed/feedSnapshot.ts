import type { FeedItem, FeedMode } from '../../api/feed'

export interface FeedSnapshot {
  mode: FeedMode
  posts: FeedItem[]
  nextCursor: string | null
  asOfUtc: string
  scrollY: number
  savedAt: number
}

let owner: string | null = null
let snapshot: FeedSnapshot | null = null

export function readFeedSnapshot(userId: string, now = Date.now()) {
  if (owner !== userId || !snapshot || now - snapshot.savedAt > 300_000) return null
  return snapshot
}

export function saveFeedSnapshot(userId: string, value: FeedSnapshot) {
  owner = userId
  snapshot = value
}

export function clearFeedSnapshot() {
  owner = null
  snapshot = null
}
