import assert from 'node:assert/strict'
import { execFileSync } from 'node:child_process'
import { storyIds, storyViewerFixture } from './storyViewerFixture.mjs'

const { chromium } = await import(process.env.PLAYWRIGHT_MODULE ?? 'playwright')
const browser = await chromium.launch({ headless: true, executablePath: process.env.CHROMIUM_EXECUTABLE, args: ['--no-sandbox'] })
const baseUrl = process.env.STORY_BASE_URL ?? 'http://127.0.0.1:5184'
const scope = process.env.STORY_CHECK_SCOPE ?? 'full'
const pause = ms => new Promise(resolve => setTimeout(resolve, ms))
let passed = 0
const errors = []
const videoBody = ['playback', 'media', 'full'].includes(scope) ? execFileSync('ffmpeg', ['-hide_banner', '-loglevel', 'error', '-f', 'lavfi', '-i', 'color=c=blue:s=90x160:r=10', '-t', '12', '-c:v', 'libvpx', '-f', 'webm', 'pipe:1']) : undefined
const progress = (viewer, index = 0) => viewer.locator('[aria-label="Tiến trình Story"] > span > span').nth(index).evaluate(e => parseFloat(e.style.width))
async function check(name, run, options = {}) {
  const context = await browser.newContext({ viewport: options.viewport ?? { width: 1366, height: 900 }, hasTouch: options.touch ?? false, isMobile: options.touch ?? false })
  const state = await storyViewerFixture(context, options)
  await options.setup?.(state)
  const page = await context.newPage(); page.setDefaultTimeout(10000)
  page.on('pageerror', error => errors.push(error.stack))
  await options.preparePage?.(page)
  try {
    await page.goto(`${baseUrl}/feed`, { waitUntil: 'domcontentloaded' })
    await page.getByRole('button', { name: options.owner ? /Story của bạn/ : /Minh$/ }).click()
    const viewer = page.getByRole('dialog', { name: 'Trình xem Story', exact: true })
    await viewer.waitFor()
    if (options.waitForMedia !== false) await viewer.locator(options.video ? 'video' : 'img[alt="Story 1"]').waitFor()
    const reply = viewer.getByPlaceholder('Trả lời qua Zola Light…')
    const next = () => viewer.getByRole('button', { name: 'Story tiếp theo', exact: true }).click()
    const previous = () => viewer.getByRole('button', { name: 'Story trước', exact: true }).click()
    await run({ page, context, state, viewer, reply, next, previous })
    passed++; console.log(`PASS ${name}`)
  } finally { await context.close() }
}

try {
  if (['reply', 'full'].includes(scope)) {
    await check('reply accepts Space and Arrow editing without navigating', async ({ viewer, reply }) => {
      await reply.focus(); await reply.pressSequentially('Xin chào Minh'); await reply.press('ArrowLeft'); await reply.press('ArrowRight')
      assert.equal(await reply.inputValue(), 'Xin chào Minh'); assert.equal(await viewer.locator('img[alt="Story 1"]').count(), 1)
    })
    await check('reply focus prevents auto-next beyond image duration', async ({ viewer, reply }) => {
      await reply.fill('Đang nhập trả lời'); await pause(5400)
      assert.equal(await viewer.locator('img[alt="Story 1"]').count(), 1)
    })
    await check('draft clears on navigation; later response cannot clear the new draft', async ({ viewer, reply, next, state }) => {
      state.replyDelay = 700; await reply.fill('Cho Story A'); await reply.press('Enter'); await next()
      await viewer.locator('img[alt="Story 2"]').waitFor(); assert.equal(await reply.inputValue(), '')
      await reply.fill('Cho Story B'); await pause(850)
      assert.equal(await reply.inputValue(), 'Cho Story B'); assert.equal(await viewer.getByText('Đã gửi vào Zola Light.', { exact: true }).count(), 0)
      assert.deepEqual(state.replies, [{ storyId: storyIds[0], content: 'Cho Story A' }])
    })
    await check('reply captures StoryId, prevents double-submit and displays success', async ({ viewer, reply, state }) => {
      state.replyDelay = 400; await reply.fill('Gửi một lần'); await reply.press('Enter'); await reply.press('Enter')
      await viewer.getByText('Đã gửi vào Zola Light.', { exact: true }).waitFor()
      assert.deepEqual(state.replies, [{ storyId: storyIds[0], content: 'Gửi một lần' }]); assert.equal(await reply.inputValue(), '')
    })
    await check('reply failure retains the draft and permits retry', async ({ viewer, reply, state }) => {
      state.failReply = true; await reply.fill('Giữ nội dung'); await reply.press('Enter')
      await viewer.getByText('Gửi thất bại thử nghiệm', { exact: true }).waitFor(); assert.equal(await reply.inputValue(), 'Giữ nội dung')
      state.failReply = false; await reply.press('Enter'); await viewer.getByText('Đã gửi vào Zola Light.', { exact: true }).waitFor()
      assert.equal(state.replies.length, 2)
    })
  }
  if (['playback', 'full'].includes(scope)) {
    await check('image pause at 40 percent preserves elapsed through a ten-second pause', async ({ viewer }) => {
      await pause(2000); await viewer.getByRole('button', { name: 'Tạm dừng', exact: true }).click()
      const before = await progress(viewer); assert.ok(before > 30 && before < 65)
      await pause(10000); assert.equal(await progress(viewer), before)
      await viewer.getByRole('button', { name: 'Tiếp tục', exact: true }).click(); await pause(250)
      const after = await progress(viewer); assert.ok(after >= before && after < before + 12)
    })
    await check('window blur pauses and focus respects manual pause', async ({ page, viewer }) => {
      await pause(300); await page.evaluate(() => window.dispatchEvent(new Event('blur')))
      const before = await progress(viewer); await pause(400); assert.equal(await progress(viewer), before)
      await viewer.getByRole('button', { name: 'Tạm dừng', exact: true }).click()
      await page.evaluate(() => window.dispatchEvent(new Event('focus'))); await pause(300)
      assert.equal(await progress(viewer), before)
      await viewer.getByRole('button', { name: 'Tiếp tục', exact: true }).click(); await pause(300)
      assert.ok(await progress(viewer) > before)
    })
    await check('document visibility pauses and returning respects another pause reason', async ({ page, viewer, reply }) => {
      await pause(300)
      await page.evaluate(() => { Object.defineProperty(document, 'hidden', { configurable: true, value: true }); document.dispatchEvent(new Event('visibilitychange')) })
      await pause(100); const before = await progress(viewer); await pause(350); assert.equal(await progress(viewer), before)
      await reply.focus()
      await page.evaluate(() => { delete document.hidden; document.dispatchEvent(new Event('visibilitychange')) })
      await pause(300); assert.equal(await progress(viewer), before)
      await viewer.getByRole('button', { name: 'Đóng Story', exact: true }).focus(); await pause(300)
      assert.ok(await progress(viewer) > before)
    })
    await check('video pause/resume controls native playback and reply focus also pauses', async ({ viewer, reply }) => {
      const video = viewer.locator('video'); await pause(600); assert.equal(await video.evaluate(v => v.paused), false)
      await viewer.getByRole('button', { name: 'Tạm dừng', exact: true }).click(); const time = await video.evaluate(v => v.currentTime)
      await pause(400); assert.equal(await video.evaluate(v => v.paused), true); assert.ok(Math.abs(await video.evaluate(v => v.currentTime) - time) < 0.08)
      const duration = await video.evaluate(v => v.duration); assert.ok(Math.abs(await progress(viewer) - time / duration * 100) < 2)
      await viewer.getByRole('button', { name: 'Tiếp tục', exact: true }).click(); await pause(300); assert.equal(await video.evaluate(v => v.paused), false)
      await reply.focus(); await pause(100); assert.equal(await video.evaluate(v => v.paused), true)
      await viewer.getByRole('button', { name: 'Đóng Story', exact: true }).focus(); await pause(200); assert.equal(await video.evaluate(v => v.paused), false)
    }, { video: true, videoBody })
    await check('video completion advances using native duration rather than the image timer', async ({ viewer }) => {
      await viewer.locator('img[alt="Story 2"]').waitFor(); assert.equal(await viewer.locator('video').count(), 0)
    }, { video: true, videoBody: execFileSync('ffmpeg', ['-hide_banner', '-loglevel', 'error', '-f', 'lavfi', '-i', 'color=c=blue:s=90x160:r=10', '-t', '0.9', '-c:v', 'libvpx', '-f', 'webm', 'pipe:1']) })
    await check('autoplay rejection is caught and explicit play respects manual pause', async ({ page, viewer }) => {
      const play = viewer.getByRole('button', { name: 'Phát video Story', exact: true }); await play.waitFor()
      assert.equal(await viewer.locator('video').evaluate(video => video.paused), true)
      await viewer.getByRole('button', { name: 'Tạm dừng', exact: true }).click(); assert.equal(await play.count(), 0)
      await viewer.getByRole('button', { name: 'Tiếp tục', exact: true }).click(); await play.waitFor()
      await page.evaluate(() => { window.allowStoryPlayback = true }); await play.click()
      await page.waitForFunction(() => !document.querySelector('[aria-label="Trình xem Story"] video').paused)
    }, { video: true, videoBody, preparePage: page => page.addInitScript(() => {
      const nativePlay = HTMLMediaElement.prototype.play
      HTMLMediaElement.prototype.play = function () {
        if (this.closest('[aria-label="Trình xem Story"]') && !window.allowStoryPlayback) return Promise.reject(new DOMException('Autoplay blocked by test policy', 'NotAllowedError'))
        return nativePlay.call(this)
      }
    }) })
    await check('navigation stops the previously playing video', async ({ page, viewer, next }) => {
      await pause(500); await viewer.locator('video').evaluate(v => { window.oldStoryVideo = v }); await next()
      await viewer.locator('img[alt="Story 2"]').waitFor(); await pause(200)
      const old = await page.evaluate(() => ({ paused: window.oldStoryVideo.paused, connected: window.oldStoryVideo.isConnected, time: window.oldStoryVideo.currentTime }))
      assert.equal(old.paused, true); assert.equal(old.connected, false); await pause(300)
      assert.equal(await page.evaluate(() => window.oldStoryVideo.currentTime), old.time)
    }, { video: true, videoBody })
  }
  if (['media', 'full'].includes(scope)) {
    await check('delayed image does not start progress or mark-view before onLoad', async ({ viewer, state }) => {
      await pause(5300); assert.equal(await progress(viewer), 0)
      assert.equal(await viewer.locator('img[alt="Story 1"]').count(), 1); assert.equal(state.views.length, 0)
      await viewer.locator('img[alt="Story 1"]').evaluate(img => img.decode()); await pause(300)
      assert.ok(await progress(viewer) > 0); assert.deepEqual(state.views, [storyIds[0]])
    }, { viewed: false, setup: state => { state.imageDelays[storyIds[0]] = 6000 } })
    await check('broken image pauses progress and explicit retry only reloads the active media', async ({ viewer, state, reply }) => {
      await viewer.getByText('Không thể tải tin', { exact: true }).waitFor(); await reply.fill('Giữ draft khi retry')
      await pause(5300); assert.equal(await progress(viewer), 0); assert.equal(state.views.length, 0)
      state.broken.delete(storyIds[0]); await viewer.getByRole('button', { name: 'Thử lại', exact: true }).click()
      await viewer.locator('img[alt="Story 1"]').evaluate(img => img.decode())
      assert.equal(await reply.inputValue(), 'Giữ draft khi retry'); assert.equal(state.accesses.filter(id => id === storyIds[0]).length, 2)
    }, { viewed: false, waitForMedia: false, setup: state => state.broken.add(storyIds[0]) })
    await check('video media load failure is local, paused, and retry recovers', async ({ viewer, state }) => {
      await viewer.getByText('Không thể tải tin', { exact: true }).waitFor(); assert.equal(await progress(viewer), 0)
      state.broken.delete(storyIds[0]); await viewer.getByRole('button', { name: 'Thử lại', exact: true }).click()
      await viewer.locator('video').waitFor(); await pause(700)
      assert.equal(await viewer.locator('video').evaluate(v => v.paused), false)
    }, { video: true, videoBody, waitForMedia: false, setup: state => state.broken.add(storyIds[0]) })
    for (const status of [403, 404, 410]) {
      await check(`media access ${status} shows unavailable without an endless retry`, async ({ viewer }) => {
        await viewer.getByText('Story không còn khả dụng', { exact: true }).waitFor()
        assert.equal(await viewer.getByRole('button', { name: 'Thử lại', exact: true }).count(), 0)
        assert.equal(await progress(viewer), 0)
      }, { waitForMedia: false, setup: state => { state.accessFailures[storyIds[0]] = status } })
    }
    await check('transient media access error supports retry', async ({ viewer, state }) => {
      await viewer.getByText('Không thể tải tin', { exact: true }).waitFor()
      delete state.accessFailures[storyIds[0]]; await viewer.getByRole('button', { name: 'Thử lại', exact: true }).click()
      await viewer.locator('img[alt="Story 1"]').evaluate(img => img.decode()); await pause(200)
      assert.ok(await progress(viewer) > 0)
    }, { waitForMedia: false, setup: state => { state.accessFailures[storyIds[0]] = 503 } })
    await check('late media response cannot overwrite the next story', async ({ viewer, next }) => {
      await next(); await viewer.locator('img[alt="Story 2"]').evaluate(img => img.decode()); await pause(1600)
      assert.equal(await viewer.locator('img[alt="Story 1"]').count(), 0); assert.equal(await viewer.locator('img[alt="Story 2"]').count(), 1)
      assert.ok(await progress(viewer, 1) > 10 && await progress(viewer, 1) < 60)
    }, { waitForMedia: false, setup: state => { state.accessDelays[storyIds[0]] = 1300 } })
    await check('mark-view confirmation keeps the image and elapsed clock intact', async ({ viewer, state }) => {
      const beforeUrl = await viewer.locator('img[alt="Story 1"]').getAttribute('src'); await pause(1300)
      assert.equal(await viewer.locator('img[alt="Story 1"]').getAttribute('src'), beforeUrl)
      assert.equal(state.accesses.filter(id => id === storyIds[0]).length, 1); assert.ok(await progress(viewer) > 20)
    }, { viewed: false, setup: state => { state.viewDelay = 900 } })
    for (const viewport of [{ width: 1366, height: 900 }, { width: 375, height: 812 }, { width: 768, height: 1024 }]) {
      for (const [shape, size] of [['portrait', [300, 1800]], ['landscape', [1800, 300]], ['square', [600, 600]]]) {
        await check(`${shape} image fits its media container at ${viewport.width}px`, async ({ viewer }) => {
          const img = viewer.locator('img[alt="Story 1"]'); await img.evaluate(img => img.decode())
          const layout = await img.evaluate(img => {
            const image = img.getBoundingClientRect(), container = img.parentElement.getBoundingClientRect()
            const scale = Math.min(image.width / img.naturalWidth, image.height / img.naturalHeight)
            return { image: { width: image.width, height: image.height, top: image.top, bottom: image.bottom }, container: { width: container.width, height: container.height, top: container.top, bottom: container.bottom }, fit: getComputedStyle(img).objectFit, visibleWidth: img.naturalWidth * scale, visibleHeight: img.naturalHeight * scale }
          })
          assert.equal(layout.fit, 'contain'); assert.ok(layout.image.height <= layout.container.height + 1)
          assert.ok(layout.image.top >= layout.container.top - 1 && layout.image.bottom <= layout.container.bottom + 1)
          assert.ok(layout.visibleHeight <= layout.container.height + 1 && layout.visibleWidth <= layout.container.width + 1)
        }, { viewport, touch: viewport.width === 375, setup: state => { state.sizes[storyIds[0]] = size } })
      }
    }
  }
  assert.deepEqual(errors, [])
  console.log(`${passed} Story viewer browser checks passed (${scope}).`)
} finally { await browser.close() }
