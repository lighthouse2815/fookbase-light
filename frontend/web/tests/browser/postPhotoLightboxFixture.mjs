import { execFileSync } from 'node:child_process'
import { mkdtemp, readFile, rm } from 'node:fs/promises'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import { postFixtures, postId, viewerId } from './postInteractionsFixture.mjs'

export { postId }
export const photoIds = Array.from({ length: 6 }, (_, index) => `00000000-0000-0000-0000-${String(200 + index).padStart(12, '0')}`)
const sizes = [[480, 1440], [1440, 960], [960, 960], [1600, 600], [600, 1600], [1280, 720]]
const colors = ['#39597a', '#665873', '#476a62', '#8d6747', '#576584', '#70575c']
const pause = (duration) => new Promise((resolve) => setTimeout(resolve, duration))

// Match the existing reelsVolume test: real native video playback, with FFmpeg kept outside app dependencies.
export async function photoVideoFixture() {
  const directory = await mkdtemp(join(tmpdir(), 'post-photo-lightbox-'))
  const path = join(directory, 'video.webm')
  try {
    execFileSync('ffmpeg', ['-hide_banner', '-loglevel', 'error', '-f', 'lavfi', '-i', 'color=c=0x365f79:s=128x72:r=1:d=30', '-c:v', 'libvpx', '-deadline', 'realtime', '-b:v', '5k', '-an', path])
    return await readFile(path)
  } finally {
    await rm(directory, { recursive: true, force: true })
  }
}

export async function postPhotoFixtures(context, baseUrl, options = {}) {
  // Observe Image preloads even when the browser reuses its decoded-image cache.
  // The native constructor and src setter still perform every real image load.
  await context.addInitScript(() => {
    const source = Object.getOwnPropertyDescriptor(HTMLImageElement.prototype, 'src')
    window.__photoPreloadUrls = []
    window.Image = new Proxy(window.Image, {
      construct(target, args) {
        const image = Reflect.construct(target, args)
        Object.defineProperty(image, 'src', {
          get() { return source.get.call(this) },
          set(url) { window.__photoPreloadUrls.push(url); source.set.call(this, url) },
        })
        return image
      },
    })
  })
  const ids = photoIds.slice(0, options.count ?? 5)
  const interaction = await postFixtures(context, { mediaIds: ids, commentCount: 40 })
  interaction.comments = Array.from({ length: 40 }, (_, index) => ({
    id: `00000000-0000-0000-0000-${String(300 + index).padStart(12, '0')}`, postId,
    authorUserId: viewerId, parentCommentId: null, content: `Bình luận kiểm tra thanh bên ${index + 1}`,
    createdAtUtc: '2026-10-01T08:00:00Z', updatedAtUtc: null, reactionCounts: {}, viewerReaction: null,
    author: { userId: viewerId, username: 'explorer', displayName: 'Người kiểm tra', avatarUrl: null },
  }))
  const state = {
    interaction, ids, phase: 'grid', mixed: options.mixed ?? false, videoBody: options.videoBody,
    expiresInMs: options.expiresInMs ?? 600000, accessReads: [], assetReads: [], postReads: 0, commentReads: 0,
    accessDelay: new Map(), assetDelay: new Map(), failAccess: new Set(), failAssets: new Set(), corruptAssets: new Set(),
    accessVersions: new Map(),
  }
  await context.route('**/api/**', async (route) => {
    const request = route.request()
    const path = new URL(request.url()).pathname
    const access = new RegExp(`^/api/posts/${postId}/media/([^/]+)/access$`).exec(path)
    if (access) {
      const id = access[1]
      const version = (state.accessVersions.get(id) ?? 0) + 1
      state.accessVersions.set(id, version)
      state.accessReads.push({ id, version, phase: state.phase })
      const failure = state.failAccess.has(id)
      const isVideo = state.mixed && id === ids[2]
      const expiresAtUtc = new Date(Date.now() + state.expiresInMs).toISOString()
      await pause(state.accessDelay.get(id) ?? 0)
      return route.fulfill(failure ? { status: 503, json: { detail: 'Không thể tải ảnh thử nghiệm.' } } : { json: {
        mediaId: id, url: `${baseUrl}/photo-lightbox-fixture/${id}.${isVideo ? 'webm' : 'svg'}?version=${version}`,
        expiresAtUtc, mediaType: isVideo ? 'video' : 'image', contentType: isVideo ? 'video/webm' : 'image/svg+xml',
      } })
    }
    if (path === `/api/posts/${postId}` && request.method() === 'GET') state.postReads++
    if (path === `/api/posts/${postId}/comments` && request.method() === 'GET') state.commentReads++
    return route.fallback()
  })
  await context.route('**/photo-lightbox-fixture/**', async (route) => {
    const request = route.request()
    const url = new URL(request.url())
    const id = url.pathname.split('/').at(-1).split('.')[0]
    const index = photoIds.indexOf(id)
    const failure = state.failAssets.has(id)
    const corrupt = state.corruptAssets.has(id)
    state.assetReads.push({ id, phase: state.phase, type: request.resourceType(), version: url.searchParams.get('version') })
    await pause(state.assetDelay.get(id) ?? 0)
    if (failure) return route.fulfill({ status: 403, headers: { 'cache-control': 'no-store' }, body: '' })
    if (corrupt) return route.fulfill({ contentType: 'image/svg+xml', headers: { 'cache-control': 'no-store' }, body: 'invalid-image' })
    if (url.pathname.endsWith('.webm')) return route.fulfill({ contentType: 'video/webm', headers: { 'cache-control': 'no-store' }, body: state.videoBody })
    const [width, height] = sizes[index]
    const body = `<svg xmlns="http://www.w3.org/2000/svg" width="${width}" height="${height}" viewBox="0 0 ${width} ${height}"><rect width="100%" height="100%" fill="${colors[index]}"/><path d="M0 0L${width} ${height}M${width} 0L0 ${height}" stroke="white" opacity=".15" stroke-width="20"/><circle cx="${width / 2}" cy="${height / 2}" r="${Math.min(width, height) / 5}" fill="white" opacity=".15"/><text x="50%" y="50%" text-anchor="middle" dominant-baseline="middle" fill="white" font-size="${Math.min(width, height) / 9}">Ảnh ${index + 1}</text><text x="50%" y="60%" text-anchor="middle" fill="white" font-size="${Math.min(width, height) / 20}">${width} × ${height}</text></svg>`
    return route.fulfill({ contentType: 'image/svg+xml', headers: { 'cache-control': 'no-store' }, body })
  })
  return state
}
