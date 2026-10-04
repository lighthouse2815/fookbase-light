import assert from 'node:assert/strict'
import { execFileSync } from 'node:child_process'
import { mkdtemp, readFile, rm } from 'node:fs/promises'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import { fixtures, trackErrors } from './lastSignalFixture.mjs'

// Requires FFmpeg and Playwright, matching the existing browser-test setup.
// PLAYWRIGHT_MODULE=/path/to/playwright/index.mjs REELS_BASE_URL=http://127.0.0.1:5184 node tests/browser/reelsVolume.mjs
const { chromium } = await import(process.env.PLAYWRIGHT_MODULE ?? 'playwright')
const baseUrl = process.env.REELS_BASE_URL ?? 'http://127.0.0.1:5183'
const directory = await mkdtemp(join(tmpdir(), 'reels-volume-'))
const videoPath = join(directory, 'reel.webm')
execFileSync('ffmpeg', ['-hide_banner', '-loglevel', 'error', '-f', 'lavfi', '-i', 'color=c=black:s=90x160:r=1', '-f', 'lavfi', '-i', 'sine=frequency=440:sample_rate=8000', '-t', '60', '-c:v', 'libvpx', '-c:a', 'libvorbis', videoPath])
const videoBody = await readFile(videoPath)
const browser = await chromium.launch({ headless: true, executablePath: process.env.CHROMIUM_EXECUTABLE, args: ['--no-sandbox'] })
const errors = []
const reels = [1, 2, 3].map((index) => ({
  id: `reel-${index}`, privacy: 'public', createdAtUtc: '2026-10-01T08:00:00Z', updatedAtUtc: null,
  author: { userId: 'author-1', username: 'creator', displayName: 'Người đăng Reel', avatarUrl: null },
  caption: 'Kiểm tra âm lượng', commentCount: 0, shareCount: 0, reactionCounts: {}, viewerReaction: null,
  isPinned: false, viewerHasSaved: false, viewerFollowsAuthor: false, reactionCount: 0, viewCount: 0, completionCount: 0,
  video: { mediaId: `video-${index}`, durationMs: 60_000, width: 90, height: 160, contentType: 'video/webm', videoAccessPath: `/api/reels/reel-${index}/video/access`, posterAccessPath: `/api/reels/reel-${index}/poster/access` },
}))
const until = async (condition, message) => {
  for (let attempt = 0; attempt < 150; attempt++) {
    if (await condition()) return
    await new Promise((resolve) => setTimeout(resolve, 30))
  }
  assert.fail(message)
}
const check = async (name, run) => { await run(); console.log(`PASS ${name}`) }

try {
  for (const viewport of [{ width: 1366, height: 900 }, { width: 390, height: 844 }]) {
    const context = await browser.newContext({ viewport })
    await fixtures(context)
    await context.route('**/api/reels**', async (route) => {
      const path = new URL(route.request().url()).pathname
      if (path === '/api/reels') return route.fulfill({ json: { items: reels, nextCursor: null } })
      if (path.endsWith('/views')) return route.fulfill({ status: 204 })
      if (path.endsWith('/video/access')) return route.fulfill({ json: { url: `${baseUrl}/reel-volume-fixture.webm`, expiresAtUtc: '2099-01-01T00:00:00Z' } })
      if (path.endsWith('/poster/access')) return route.fulfill({ json: { url: 'data:image/gif;base64,R0lGODlhAQABAIAAAAAAAP///yH5BAEAAAAALAAAAAABAAEAAAIBRAA7', expiresAtUtc: '2099-01-01T00:00:00Z' } })
      return route.fallback()
    })
    await context.route('**/reel-volume-fixture.webm', (route) => route.fulfill({ contentType: 'video/webm', body: videoBody }))
    const page = await context.newPage()
    trackErrors(page, errors)
    await page.goto(`${baseUrl}/reels`)
    const card = page.locator('[data-reel-index="0"]')
    const video = card.locator('video')
    const slider = card.getByRole('slider', { name: 'Âm lượng Reel', exact: true })
    await video.waitFor()

    await check(`${viewport.width}px: volume control is accessible and starts muted`, async () => {
      assert.equal(await video.evaluate((element) => element.muted), true)
      assert.equal(await slider.count(), 1, 'Reels must expose a volume slider, rather than only a mute toggle')
      assert.equal(await slider.inputValue(), '0')
      await slider.scrollIntoViewIfNeeded()
      const bounds = await slider.boundingBox()
      assert.ok(bounds.x >= 0 && bounds.x + bounds.width <= viewport.width)
    })
    await check(`${viewport.width}px: keyboard volume changes affect playback without pausing`, async () => {
      await until(() => video.evaluate((element) => !element.paused), 'The initial Reel should autoplay muted')
      await slider.focus()
      await slider.press('Home')
      for (let index = 0; index < 35; index++) await slider.press('ArrowRight')
      await until(() => video.evaluate((element) => element.volume === 0.35 && !element.muted), 'The slider should apply 35% volume and unmute')
      assert.equal(await video.evaluate((element) => element.paused), false)
    })
    await check(`${viewport.width}px: muting and setting zero restore the last audible volume`, async () => {
      await card.getByRole('button', { name: 'Tắt âm thanh', exact: true }).click()
      assert.equal(await video.evaluate((element) => element.muted), true)
      assert.equal(await slider.inputValue(), '0')
      await card.getByRole('button', { name: 'Bật âm thanh', exact: true }).click()
      assert.equal(await slider.inputValue(), '35')
      assert.equal(await video.evaluate((element) => element.volume), 0.35)
      await slider.focus()
      await slider.press('Home')
      assert.equal(await video.evaluate((element) => element.muted), true)
      await card.getByRole('button', { name: 'Bật âm thanh', exact: true }).click()
      assert.equal(await slider.inputValue(), '35')
      assert.equal(await video.evaluate((element) => element.muted), false)
    })
    await check(`${viewport.width}px: pointer volume changes do not toggle pause`, async () => {
      const bounds = await slider.boundingBox()
      await slider.click({ position: { x: bounds.width / 2, y: bounds.height / 2 } })
      const actualVolume = await video.evaluate((element) => element.volume)
      assert.ok(actualVolume >= 0.45 && actualVolume <= 0.55)
      assert.equal(await video.evaluate((element) => element.paused), false)
      await card.getByRole('button', { name: 'Tạm dừng Reel', exact: true }).click()
      await slider.press('End')
      assert.equal(await video.evaluate((element) => element.volume), 1)
      assert.equal(await video.evaluate((element) => element.paused), true)
      await slider.press('ArrowLeft')
      assert.equal(await video.evaluate((element) => element.volume), 0.99)
    })
    await check(`${viewport.width}px: preloaded and newly loaded Reels retain volume and mute`, async () => {
      await page.getByRole('button', { name: 'Reel tiếp theo', exact: true }).click()
      const second = page.locator('[data-reel-index="1"]')
      await until(() => second.locator('video').evaluate((element) => !element.paused && element.volume === 0.99 && !element.muted), 'The preloaded Reel should retain 99% volume')
      assert.equal(await video.evaluate((element) => element.paused && element.muted), true)
      await second.getByRole('button', { name: 'Tắt âm thanh', exact: true }).click()
      await page.getByRole('button', { name: 'Reel tiếp theo', exact: true }).click()
      const third = page.locator('[data-reel-index="2"]')
      await third.locator('video').waitFor()
      await until(() => third.locator('video').evaluate((element) => !element.paused && element.volume === 0.99 && element.muted), 'The newly loaded Reel should retain volume and mute')
      await third.getByRole('button', { name: 'Bật âm thanh', exact: true }).click()
      assert.equal(await third.getByRole('slider', { name: 'Âm lượng Reel', exact: true }).inputValue(), '99')
    })
    await context.close()
  }
  assert.deepEqual(errors, [])
} finally {
  await browser.close()
  await rm(directory, { recursive: true, force: true })
}
