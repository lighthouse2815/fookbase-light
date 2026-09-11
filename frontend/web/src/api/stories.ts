import { apiRequest } from './client'
import type { MediaReadUrl } from './media'

export type StoryPrivacy = 'public' | 'friends' | 'onlyMe'
export type StoryReactionType = 'like' | 'love' | 'haha' | 'wow' | 'sad' | 'angry'

export interface StoryAuthor {
  userId: string
  username: string
  displayName: string
  avatarUrl: string | null
}

export interface StoryMedia {
  mediaId: string
  mediaType: 'image' | 'video'
  contentType: string
  durationMs: number | null
  width: number | null
  height: number | null
  accessPath: string
  posterAccessPath: string | null
}

export interface Story {
  id: string
  author: StoryAuthor
  caption: string | null
  privacy: StoryPrivacy
  createdAtUtc: string
  expiresAtUtc: string
  media: StoryMedia
  isViewed: boolean
  canManage: boolean
  viewerCount: number | null
  reactionCount: number
  viewerReaction: StoryReactionType | null
}

export interface StoryTrayAuthor {
  author: StoryAuthor
  hasUnseenStories: boolean
  stories: Story[]
}

export interface StoryViewer {
  userId: string
  username: string
  displayName: string
  avatarUrl: string | null
  viewedAtUtc: string
  reactionType: StoryReactionType | null
}

export interface StoryViewersPage {
  items: StoryViewer[]
  nextCursor: string | null
}

export interface StoryArchivePage {
  items: Story[]
  nextCursor: string | null
}

export const storiesApi = {
  tray: () => apiRequest<{ items: StoryTrayAuthor[] }>('/api/stories'),
  get: (storyId: string) => apiRequest<Story>(`/api/stories/${storyId}`),
  create: (mediaId: string, caption: string, privacy: StoryPrivacy) =>
    apiRequest<Story>('/api/stories', {
      method: 'POST',
      body: JSON.stringify({ mediaId, caption: caption || null, privacy }),
    }),
  remove: (storyId: string) => apiRequest<void>(`/api/stories/${storyId}`, { method: 'DELETE' }),
  markViewed: (storyId: string) => apiRequest<void>(`/api/stories/${storyId}/view`, { method: 'POST' }),
  viewers: (storyId: string, cursor?: string) => {
    const query = new URLSearchParams({ limit: '30' })
    if (cursor) query.set('cursor', cursor)
    return apiRequest<StoryViewersPage>(`/api/stories/${storyId}/viewers?${query.toString()}`)
  },
  setReaction: (storyId: string, type: StoryReactionType) =>
    apiRequest<Story>(`/api/stories/${storyId}/reaction`, {
      method: 'POST', body: JSON.stringify({ type }),
    }),
  removeReaction: (storyId: string) =>
    apiRequest<void>(`/api/stories/${storyId}/reaction`, { method: 'DELETE' }),
  reply: (storyId: string, content: string) =>
    apiRequest<{ id: string; conversationId: string }>(`/api/stories/${storyId}/reply`, {
      method: 'POST', body: JSON.stringify({ content }),
    }),
  mediaAccess: (story: Story) => apiRequest<MediaReadUrl>(story.media.accessPath),
  posterAccess: (story: Story) => story.media.posterAccessPath
    ? apiRequest<MediaReadUrl>(story.media.posterAccessPath)
    : Promise.resolve(null),
  archive: (cursor?: string) => {
    const query = new URLSearchParams({ limit: '30' })
    if (cursor) query.set('cursor', cursor)
    return apiRequest<StoryArchivePage>(`/api/stories/archive?${query.toString()}`)
  },
}
