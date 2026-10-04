import { actorId, notification, notificationFixtures, viewerId } from './notificationsFixture.mjs'

export const storyId = '00000000-0000-0000-0000-000000000701'
const image = 'data:image/svg+xml,' + encodeURIComponent('<svg xmlns="http://www.w3.org/2000/svg" width="360" height="640"><rect width="360" height="640" fill="#284658"/><text x="180" y="320" text-anchor="middle" fill="white">Story cần mở</text></svg>')

export async function storyNotificationFixture(context, options = {}) {
  const notifications = await notificationFixtures(context, { items: [notification(1, { type: 'StoryReaction', entityType: 'Story', entityId: storyId })] })
  const story = {
    id: storyId, author: { userId: options.canManage ? viewerId : actorId, username: options.canManage ? 'explorer' : 'minh', displayName: options.canManage ? 'Người kiểm tra' : 'Minh', avatarUrl: null },
    caption: 'Story cần mở từ thông báo', privacy: 'friends',
    createdAtUtc: new Date().toISOString(), expiresAtUtc: new Date(Date.now() + 86400000).toISOString(),
    media: { mediaId: '00000000-0000-0000-0000-000000000702', mediaType: 'image', contentType: 'image/svg+xml', durationMs: null, width: 360, height: 640, accessPath: `/api/stories/${storyId}/media/access`, posterAccessPath: null },
    isViewed: false, canManage: options.canManage ?? false, viewerCount: null, reactionCount: 1, viewerReaction: null,
  }
  const state = { notifications, story, status: options.status ?? 200, delay: options.delay ?? 0, reads: [], mediaReads: 0, views: [], archiveReads: 0 }
  await context.route('**/api/stories**', async (route) => {
    const request = route.request()
    const url = new URL(request.url())
    if (url.pathname === `/api/stories/${storyId}`) {
      const status = state.status
      const response = structuredClone(state.story)
      state.reads.push({ id: storyId, status })
      await new Promise((resolve) => setTimeout(resolve, state.delay))
      return route.fulfill(status === 200 ? { json: response } : { status, json: { detail: status === 503 ? 'Story tạm thời không tải được.' : 'The story was not found.' } })
    }
    if (url.pathname === `/api/stories/${storyId}/media/access`) {
      state.mediaReads++
      return route.fulfill({ json: { url: image, expiresAtUtc: new Date(Date.now() + 600000).toISOString() } })
    }
    if (url.pathname === `/api/stories/${storyId}/view`) {
      state.views.push(storyId)
      state.story.isViewed = true
      return route.fulfill({ status: 204 })
    }
    if (url.pathname === '/api/stories/archive') {
      state.archiveReads++
      return route.fulfill({ json: { items: [], nextCursor: null } })
    }
    if (url.pathname === '/api/stories') return route.fulfill({ json: { items: [] } })
    return route.fallback()
  })
  return state
}
