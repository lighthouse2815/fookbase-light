import { apiRequest } from './client'
import type { Comment, Post } from './posts'
import type { MediaReadUrl } from './media'

export interface ReelAuthor {
  userId: string
  username: string
  displayName: string
  avatarUrl: string | null
}

export interface ReelVideo {
  mediaId: string
  durationMs: number
  width: number
  height: number
  contentType: string
  videoAccessPath: string
  posterAccessPath: string
}

export interface Reel extends Omit<Post, 'authorUserId' | 'content' | 'mediaIds'> {
  author: ReelAuthor
  caption: string
  video: ReelVideo
  reactionCount: number
  viewCount: number
  completionCount: number
  viewerHasSaved: boolean
  viewerFollowsAuthor: boolean
}

export type ReelFeedMode = 'forYou' | 'following'

export interface ReelPage {
  items: Reel[]
  nextCursor: string | null
}

export interface CreateReelDetails {
  caption: string
  privacy: 'public' | 'friends' | 'onlyMe'
  videoMediaId: string
}

export const reelsApi = {
  create: (details: CreateReelDetails) =>
    apiRequest<Reel>('/api/reels', { method: 'POST', body: JSON.stringify(details) }),
  get: (reelId: string) => apiRequest<Reel>(`/api/reels/${reelId}`),
  getFeed: (mode: ReelFeedMode = 'forYou', cursor?: string, limit = 20) => {
    const query = new URLSearchParams({ limit: String(limit) })
    query.set('mode', mode)
    if (cursor) query.set('cursor', cursor)
    return apiRequest<ReelPage>('/api/reels?' + query.toString())
  },
  getVideoAccess: (reelId: string) =>
    apiRequest<MediaReadUrl>(`/api/reels/${reelId}/video/access`),
  getPosterAccess: (reelId: string) =>
    apiRequest<MediaReadUrl>(`/api/reels/${reelId}/poster/access`),
  recordView: (reelId: string, watchDurationMs: number, completed: boolean, replayed: boolean) =>
    apiRequest<void>(`/api/reels/${reelId}/views`, {
      method: 'POST',
      body: JSON.stringify({ watchDurationMs, completed, replayed }),
    }),
  getComments: (reelId: string) =>
    apiRequest<{ items: Comment[] }>(`/api/posts/${reelId}/comments?offset=0&limit=100`),
}
