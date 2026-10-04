import { basePost, postFixtures, viewerId } from './postInteractionsFixture.mjs'

export const searchPostIds = Array.from({ length: 4 }, (_, index) => `00000000-0000-0000-0000-${String(510 + index).padStart(12, '0')}`)
export const personId = '00000000-0000-0000-0000-000000000520'
export const groupId = '00000000-0000-0000-0000-000000000530'
export const pageId = '00000000-0000-0000-0000-000000000540'
export const eventId = '00000000-0000-0000-0000-000000000550'
export const emptySearch = () => ({ people: [], groups: [], pages: [], posts: [], reels: [], events: [], hashtags: [], nextCursor: null })
export const person = { userId: personId, username: 'dangkhoa', displayName: 'Trần Đăng Khoa', avatarUrl: null, bio: 'Cộng đồng lập trình', followerCount: 10, followingCount: 5, isFollowing: false, isFollowedBy: false, friendshipState: null }
export const group = { groupId, name: 'Nhóm Đăng chia sẻ', description: 'Cộng đồng Đăng', privacy: 'public', coverUrl: null, memberCount: 15, viewerMembershipState: null }
export const searchPage = { pageId, name: 'Trang Đăng sáng tạo', username: 'dang.page', category: 'community', bio: 'Chia sẻ kiến thức', avatarUrl: null, followerCount: 12, viewerIsFollowing: false }
export const event = { eventId, name: 'Sự kiện Đăng học React', hostType: 'user', hostId: viewerId, hostName: 'Người kiểm tra', startsAtUtc: '2026-11-01T08:00:00Z', locationType: 'physical', locationName: 'Hà Nội', coverUrl: null, goingCount: 8, interestedCount: 12 }
export const searchPosts = ['profile', 'group', 'page', 'event'].map((containerType, index) => ({
  postId: searchPostIds[index], authorUserId: viewerId, displayAuthor: { ...basePost.displayAuthor, ...(containerType === 'page' ? { type: 'page', id: pageId, username: searchPage.username, name: searchPage.name } : {}) },
  snippet: `Post từ ${containerType}: Đăng chia sẻ`, mediaIds: [], commentCount: 0, reactionCounts: {}, containerType, containerId: groupId, createdAtUtc: '2026-10-01T08:00:00Z',
}))

export async function searchFixtures(context) {
  await postFixtures(context)
  const state = {
    requests: [], delays: new Map(), failures: new Set(),
    results: new Map(), suggestions: new Map(),
  }
  const defaultResults = { ...emptySearch(), people: [person], groups: [group], pages: [searchPage], posts: searchPosts, events: [event], hashtags: [{ tag: 'dang', displayName: 'Đăng' }] }
  await context.route('**/api/**', async (route) => {
    const url = new URL(route.request().url())
    const path = url.pathname
    if (path === '/api/search' || path === '/api/search/suggestions') {
      const query = url.searchParams.get('q')
      const type = url.searchParams.get('type') ?? 'all'
      const cursor = url.searchParams.get('cursor') ?? ''
      const kind = path.endsWith('/suggestions') ? 'suggestions' : 'results'
      const key = `${kind}:${query}:${type}:${cursor}`
      state.requests.push({ kind, query, type, cursor, limit: url.searchParams.get('limit') })
      await new Promise((resolve) => setTimeout(resolve, state.delays.get(key) ?? state.delays.get(kind) ?? 0))
      if (state.failures.has(key) || state.failures.has(kind)) return route.fulfill({ status: 503, json: { detail: 'Search fixture failure' } })
      if (kind === 'suggestions') return route.fulfill({ json: state.suggestions.get(query) ?? { people: [person, person], groups: [group], pages: [searchPage] } })
      const result = state.results.get(`${query}:${type}:${cursor}`) ?? (type === 'all' ? defaultResults : { ...emptySearch(), [type]: defaultResults[type] ?? [] })
      return route.fulfill({ json: result })
    }
    const postIndex = searchPostIds.indexOf(path.split('/').at(-1))
    if (path.startsWith('/api/posts/') && postIndex >= 0) {
      const item = searchPosts[postIndex]
      return route.fulfill({ json: { ...basePost, id: item.postId, content: item.snippet, displayAuthor: item.displayAuthor, containerType: item.containerType } })
    }
    return route.fallback()
  })
  return state
}
