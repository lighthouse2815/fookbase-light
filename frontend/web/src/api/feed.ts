import { apiRequest } from './client'
import type { Post, PostDisplayIdentity } from './posts'
import type { ReelVideo } from './reels'

export type FeedMode = 'home' | 'following'

export interface FeedAuthor {
  userId: string | null
  username: string
  displayName: string
  avatarUrl: string | null
}

export interface FeedMedia {
  mediaId: string
  mediaType: 'image' | 'video'
  contentType: string
}

export interface FeedContainer {
  id: string
  name: string
  username?: string | null
  privacy?: string | null
}

export interface FeedShare {
  id: string
  originalPostId: string
  caption: string | null
  createdAtUtc: string
  actor: FeedAuthor
  originalAuthor: PostDisplayIdentity
  originalPost: Post
}

export interface FeedItem extends Omit<Post, 'authorUserId' | 'contentType'> {
  contentType: 'standardPost' | 'reel' | 'share'
  containerType: 'profile' | 'group' | 'page'
  container: FeedContainer
  displayAuthor: PostDisplayIdentity
  author: FeedAuthor
  media: FeedMedia[]
  video: ReelVideo | null
  reactionCount: number
  isSuggested: boolean
  share?: FeedShare | null
}

export interface FeedPage {
  items: FeedItem[]
  nextCursor: string | null
  asOfUtc: string
}

export const feedApi = {
  getHome: (cursor?: string, limit = 20, init?: RequestInit) => {
    const query = new URLSearchParams({ limit: String(limit) })
    if (cursor) query.set('cursor', cursor)
    return apiRequest<FeedPage>('/api/feed?' + query.toString(), init)
  },
  getFollowing: (cursor?: string, limit = 20, init?: RequestInit) => {
    const query = new URLSearchParams({ limit: String(limit) })
    if (cursor) query.set('cursor', cursor)
    return apiRequest<FeedPage>('/api/feed/following?' + query.toString(), init)
  },
}
