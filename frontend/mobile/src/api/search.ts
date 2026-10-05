import { apiRequest } from './client'

export type SearchType = 'all' | 'people' | 'groups' | 'pages' | 'posts' | 'reels' | 'events'

export interface SearchPerson {
  userId: string
  username: string
  displayName: string
  avatarUrl: string | null
  bio: string | null
  followerCount: number
  followingCount: number
  isFollowing: boolean | null
  isFollowedBy: boolean | null
  friendshipState: string | null
}

export interface SearchGroup {
  groupId: string
  name: string
  description: string | null
  privacy: string
  coverUrl: string | null
  memberCount: number
  viewerMembershipState: string | null
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

export interface SearchPost {
  postId: string
  authorUserId: string | null
  displayAuthor: { type: 'user' | 'page'; id: string; username: string; name: string; avatarUrl: string | null } | null
  snippet: string
  mediaIds: string[]
  commentCount: number
  reactionCounts: Record<string, number>
  containerType: string
  containerId: string
  createdAtUtc: string
}

export interface SearchReel {
  reelId: string
  author: { userId: string; username: string; displayName: string; avatarUrl: string | null }
  snippet: string
  media: { mediaId: string; durationMs: number; width: number; height: number; videoAccessPath: string; posterAccessPath: string }
  commentCount: number
  reactionCount: number
  viewCount: number
  createdAtUtc: string
}

export interface SearchHashtag { tag: string; displayName: string }
export interface SearchEvent {
  eventId: string
  name: string
  hostType: string
  hostId: string
  hostName: string
  startsAtUtc: string
  locationType: string
  locationName: string | null
  coverUrl: string | null
  goingCount: number
  interestedCount: number
}

export interface GlobalSearchResult {
  people: SearchPerson[]
  groups: SearchGroup[]
  pages: SearchPage[]
  posts: SearchPost[]
  reels: SearchReel[]
  nextCursor: string | null
  hashtags?: SearchHashtag[]
  events?: SearchEvent[]
}

export interface SearchSuggestionsResult {
  people: SearchPerson[]
  groups: SearchGroup[]
  pages: SearchPage[]
}

export const searchApi = {
  search: (queryText: string, type: SearchType = 'all', cursor?: string, limit = 20, init?: RequestInit) => {
    const query = new URLSearchParams({ q: queryText, type, limit: String(limit) })
    if (cursor) query.set('cursor', cursor)
    return apiRequest<GlobalSearchResult>(`/api/search?${query.toString()}`, init)
  },
  suggestions: (queryText: string, limit = 5, init?: RequestInit) => {
    const query = new URLSearchParams({ q: queryText, limit: String(limit) })
    return apiRequest<SearchSuggestionsResult>(`/api/search/suggestions?${query.toString()}`, init)
  },
}
