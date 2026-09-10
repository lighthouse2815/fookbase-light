import { apiRequest } from './client'
import type { Post } from './posts'

export interface FeedAuthor {
  userId: string
  username: string
  displayName: string
  avatarUrl: string | null
}

export interface FeedMedia {
  mediaId: string
  mediaType: 'image' | 'video'
  contentType: string
}

export interface FeedItem extends Post {
  author: FeedAuthor
  media: FeedMedia[]
  reactionCount: number
}

export interface FeedPage {
  items: FeedItem[]
  nextCursor: string | null
}

export const feedApi = {
  getHome: (cursor?: string, limit = 20) => {
    const query = new URLSearchParams({ limit: String(limit) })
    if (cursor) query.set('cursor', cursor)
    return apiRequest<FeedPage>('/api/feed?' + query.toString())
  },
}
