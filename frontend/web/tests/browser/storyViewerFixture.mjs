import { notificationFixtures, viewerId, actorId } from './notificationsFixture.mjs'

export const storyIds = [701, 703, 704].map(n => `00000000-0000-0000-0000-${String(n).padStart(12, '0')}`)
const pause = ms => new Promise(resolve => setTimeout(resolve, ms))

export async function storyViewerFixture(context, options = {}) {
  await notificationFixtures(context, { items: [] })
  const author = { userId: options.owner ? viewerId : actorId, username: 'minh', displayName: 'Minh', avatarUrl: null }
  const stories = storyIds.map((id, index) => ({
    id, author: index < 2 ? author : { userId: '00000000-0000-0000-0000-000000000009', username: 'lan', displayName: 'Lan', avatarUrl: null },
    caption: `Story ${index + 1}`, privacy: 'friends', createdAtUtc: new Date().toISOString(), expiresAtUtc: new Date(Date.now() + 86400000).toISOString(),
    media: { mediaId: `media-${index}`, mediaType: options.video && index === 0 ? 'video' : 'image', contentType: options.video && index === 0 ? 'video/webm' : 'image/svg+xml', durationMs: options.video && index === 0 ? 12000 : null, width: 360, height: 640, accessPath: `/api/stories/${id}/media/access`, posterAccessPath: null },
    isViewed: options.viewed ?? true, canManage: options.owner && index < 2 || false, viewerCount: options.owner ? 2 : null, reactionCount: 0, viewerReaction: null,
  }))
  const state = { stories, accesses: [], assets: [], views: [], reactions: [], replies: [], viewerReads: [], accessDelays: {}, accessFailures: {}, imageDelays: {}, broken: new Set(), replyDelay: 0, failReply: false, reactionDelays: {}, failReaction: false, viewDelay: 0, viewFailures: 0, viewerDelay: 0, failViewers: false, sizes: {}, videoBody: options.videoBody }
  await context.route('**/story-viewer-fixture/**', async route => {
    const id = new URL(route.request().url()).pathname.split('/').at(-1).split('.')[0]
    state.assets.push(id)
    await pause(state.imageDelays[id] ?? 0)
    if (state.broken.has(id)) return route.fulfill({ status: 404, body: '' })
    if (state.stories.find(s => s.id === id)?.media.mediaType === 'video') return route.fulfill({ contentType: 'video/webm', body: state.videoBody })
    const [width, height] = state.sizes[id] ?? [360, 640]
    return route.fulfill({ contentType: 'image/svg+xml', body: `<svg xmlns="http://www.w3.org/2000/svg" width="${width}" height="${height}"><rect width="100%" height="100%" fill="#284658"/><text x="50%" y="50%" text-anchor="middle" fill="white">Story</text></svg>` })
  })
  await context.route('**/api/stories**', async route => {
    const request = route.request(), url = new URL(request.url()), path = url.pathname
    if (path === '/api/stories') return route.fulfill({ json: { items: [
      { author, hasUnseenStories: stories.slice(0, 2).some(s => !s.isViewed && !s.canManage), stories: stories.slice(0, 2) },
      { author: stories[2].author, hasUnseenStories: !stories[2].isViewed, stories: [stories[2]] },
    ] } })
    const story = stories.find(s => path.includes(s.id))
    if (!story) return route.fallback()
    if (path.endsWith('/media/access')) {
      state.accesses.push(story.id)
      await pause(state.accessDelays[story.id] ?? 0)
      if (state.accessFailures[story.id]) return route.fulfill({ status: state.accessFailures[story.id], json: { detail: 'Story was not found.' } })
      return route.fulfill({ json: { url: `${url.origin}/story-viewer-fixture/${story.id}.${story.media.mediaType === 'video' ? 'webm' : 'svg'}`, expiresAtUtc: new Date(Date.now() + 600000).toISOString() } })
    }
    if (path.endsWith('/view')) {
      state.views.push(story.id); await pause(state.viewDelay)
      if (state.viewFailures > 0) { state.viewFailures--; return route.fulfill({ status: 503, json: { detail: 'View failed' } }) }
      story.isViewed = true; return route.fulfill({ status: 204 })
    }
    if (path.endsWith('/reaction')) {
      const type = request.method() === 'DELETE' ? null : request.postDataJSON().type
      state.reactions.push({ storyId: story.id, type })
      const fail = state.failReaction
      if (!fail) { story.viewerReaction = type; story.reactionCount = type ? 1 : 0 }
      const response = structuredClone(story); await pause(state.reactionDelays[type] ?? 0)
      return route.fulfill(fail ? { status: 503, json: { detail: 'Reaction failed' } } : type === null ? { status: 204 } : { json: response })
    }
    if (path.endsWith('/reply')) {
      state.replies.push({ storyId: story.id, ...request.postDataJSON() }); const fail = state.failReply; await pause(state.replyDelay)
      return route.fulfill(fail ? { status: 503, json: { detail: 'Gửi thất bại thử nghiệm' } } : { json: { id: 'reply-1', conversationId: 'conversation-1' } })
    }
    if (path.endsWith('/viewers')) {
      const cursor = url.searchParams.get('cursor'); state.viewerReads.push({ storyId: story.id, cursor })
      const page = { items: [{ userId: 'viewer-1', displayName: `Viewer ${story.id === storyIds[0] ? 'A' : 'B'}`, username: 'viewer', avatarUrl: null, viewedAtUtc: new Date().toISOString(), reactionType: null }, ...(cursor ? [{ userId: 'viewer-2', displayName: 'Viewer 2', username: 'viewer2', avatarUrl: null, viewedAtUtc: new Date().toISOString(), reactionType: null }] : [])], nextCursor: cursor ? null : 'page-2' }
      const fail = state.failViewers; await pause(state.viewerDelay)
      return route.fulfill(fail ? { status: 503, json: { detail: 'Danh sách thất bại thử nghiệm' } } : { json: page })
    }
    if (path === `/api/stories/${story.id}`) return route.fulfill({ json: story })
    return route.fallback()
  })
  return state
}
