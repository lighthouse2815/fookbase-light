import { apiRequest } from './client'
import type { Post } from './posts'

export type SearchType = 'all' | 'people' | 'groups' | 'pages' | 'posts' | 'reels'

export interface SearchPerson {
  userId: string
  username: string
  displayName: string
  avatarUrl: string | null
  bio: string | null
}

export interface SearchGroup {
  groupId: string
  name: string
  description: string | null
  privacy: 'public' | 'private'
  coverUrl: string | null
  memberCount: number
  viewerMembershipState: 'member' | 'pending' | null
}

export interface SearchPage {
  pageId: string
  name: string
  username: string
  category: string
  bio: string | null
  avatarUrl: string | null
  followerCount: number
  viewerIsFollowing: boolean
}

export interface SearchPost extends Pick<Post, 'authorUserId' | 'mediaIds' | 'commentCount' | 'reactionCounts' | 'containerType' | 'displayAuthor'> {
  postId: string
  snippet: string
  containerId: string
  createdAtUtc: string
}

export interface SearchReel {
  reelId: string
  author: {
    userId: string
    username: string
    displayName: string
    avatarUrl: string | null
  }
  snippet: string
  media: {
    mediaId: string
    durationMs: number
    width: number
    height: number
    videoAccessPath: string
    posterAccessPath: string
  }
  commentCount: number
  reactionCount: number
  viewCount: number
  createdAtUtc: string
}

export interface SearchHashtag {
  tag: string
  displayName: string
}

export interface GlobalSearchResponse {
  people: SearchPerson[]
  groups: SearchGroup[]
  pages: SearchPage[]
  posts: SearchPost[]
  reels: SearchReel[]
  nextCursor: string | null
  hashtags?: SearchHashtag[]
}

export interface SearchSuggestions {
  people: SearchPerson[]
  groups: SearchGroup[]
  pages: SearchPage[]
}

export const searchApi = {
  search: (q: string, type: SearchType = 'all', cursor?: string, limit = 20, init?: RequestInit) => {
    const params = new URLSearchParams({ q: q.trim(), type, limit: String(limit) })
    if (cursor) params.set('cursor', cursor)
    return apiRequest<GlobalSearchResponse>('/api/search?' + params.toString(), init)
  },
  suggestions: (q: string, init?: RequestInit) =>
    apiRequest<SearchSuggestions>('/api/search/suggestions?' + new URLSearchParams({ q: q.trim(), limit: '5' }), init),
}
