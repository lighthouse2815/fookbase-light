import assert from 'node:assert/strict'
import { execFileSync } from 'node:child_process'
import { storyIds, storyViewerFixture } from './storyViewerFixture.mjs'

const { chromium } = await import(process.env.PLAYWRIGHT_MODULE ?? 'playwright')
const browser = await chromium.launch({ headless: true, executablePath: process.env.CHROMIUM_EXECUTABLE, args: ['--no-sandbox'] })
const baseUrl = process.env.STORY_BASE_URL ?? 'http://127.0.0.1:5184'
const scope = process.env.STORY_INTERACTION_CHECK_SCOPE ?? 'full'
const pause = ms => new Promise(resolve => setTimeout(resolve, ms))
const selected = name => scope === 'full' || scope === name
const viewportCases = [
  { name: 'desktop', viewport: { width: 1366, height: 900 }, touch: false },
  { name: '375px', viewport: { width: 375, height: 812 }, touch: true },
  { name: '768px', viewport: { width: 768, height: 1024 }, touch: true },
]
const errors = []
let passed = 0
const progress = viewer => viewer.locator('[aria-label="Tiến trình Story"] > span > span').first().evaluate(element => parseFloat(element.style.width))
const activeCaption = viewer => viewer.locator('img[alt^="Story "]').getAttribute('alt')
const imageReady = async (viewer, caption = 'Story 1') => {
  const image = viewer.getByRole('img', { name: caption, exact: true })
  await image.waitFor()
  await image.evaluate(image => image.complete && image.naturalWidth > 0 || new Promise((resolve, reject) => {
    image.addEventListener('load', resolve, { once: true })
    image.addEventListener('error', () => reject(new Error('Story fixture image failed to load')), { once: true })
  }))
  return image
}
async function create(options = {}) {
  const context = await browser.newContext({ viewport: options.viewport ?? viewportCases[0].viewport, hasTouch: !!options.touch, isMobile: !!options.touch, reducedMotion: options.reducedMotion ?? 'no-preference' })
  const state = await storyViewerFixture(context, options)
  await options.setup?.(state)
  const page = await context.newPage()
  page.setDefaultTimeout(10000)
  page.on('pageerror', error => errors.push(error.stack))
  await page.goto(`${baseUrl}/feed`, { waitUntil: 'domcontentloaded' })
  const trigger = page.getByRole('button', { name: options.owner ? /Story của bạn/ : /Minh$/ })
  await trigger.waitFor()
  return { context, state, page, trigger }
}
async function open(test, video = false) {
  await test.trigger.focus()
  await test.trigger.evaluate(element => { window.storyOpeningTrigger = element })
  await test.trigger.click()
  const viewer = test.page.getByRole('dialog', { name: 'Trình xem Story', exact: true })
  await viewer.waitFor()
  if (video) {
    await viewer.locator('video').waitFor()
    await test.page.waitForFunction(() => {
      const video = document.querySelector('[aria-label="Trình xem Story"] video')
      return video && Number.isFinite(video.duration) && video.duration > 0 && !video.paused
    })
  } else await imageReady(viewer)
  return viewer
}
async function check(name, run, options = {}) {
  const test = await create(options)
  try {
    await run(test)
    passed++
    console.log(`PASS ${name}`)
  } finally { await test.context.close() }
}
async function assertWithinViewport(page, viewer) {
  const bounds = await viewer.boundingBox()
  const width = await page.evaluate(() => innerWidth)
  assert.ok(bounds.x >= -1 && bounds.x + bounds.width <= width + 1, 'viewer must fit the viewport')
  assert.equal(await page.evaluate(() => document.documentElement.scrollWidth > innerWidth), false, 'page must not overflow horizontally')
  const controls = viewer.locator('input, button').filter({ visible: true })
  for (const bounds of await controls.evaluateAll(elements => elements.map(element => {
    const rect = element.getBoundingClientRect()
    return { x: rect.x, right: rect.right, viewport: innerWidth }
  }))) assert.ok(bounds.x >= -1 && bounds.right <= bounds.viewport + 1, 'viewer control must fit the viewport')
}
async function mediaPoint(viewer, fraction = 0.5) {
  const bounds = await viewer.locator('img[alt^="Story "]').boundingBox()
  assert.ok(bounds)
  return { x: bounds.x + bounds.width * fraction, y: bounds.y + bounds.height * 0.4, bounds }
}
async function touchGesture(context, page, from, to) {
  const cdp = await context.newCDPSession(page)
  const point = (x, y) => [{ x, y, radiusX: 2, radiusY: 2, force: 1, id: 0 }]
  try {
    await cdp.send('Input.dispatchTouchEvent', { type: 'touchStart', touchPoints: point(from.x, from.y) })
    for (let step = 1; step <= 5; step++) {
      await cdp.send('Input.dispatchTouchEvent', { type: 'touchMove', touchPoints: point(from.x + (to.x - from.x) * step / 5, from.y + (to.y - from.y) * step / 5) })
      await pause(20)
    }
    await cdp.send('Input.dispatchTouchEvent', { type: 'touchEnd', touchPoints: [] })
    await pause(180)
  } finally { await cdp.detach() }
}

try {
  if (selected('focus')) {
    for (const options of viewportCases) {
      await check(`${options.name}: Home viewer traps Tab/ShiftTab, locks scrolling and restores exact trigger on Escape`, async test => {
        const overflowBefore = await test.page.evaluate(() => document.body.style.overflow)
        const viewer = await open(test)
        assert.equal(await viewer.evaluate(element => element.contains(document.activeElement)), true)
        assert.equal(await test.page.evaluate(() => document.body.style.overflow), 'hidden')
        const focusable = 'button:not([disabled]), [href], input:not([disabled]), select:not([disabled]), textarea:not([disabled]), [tabindex]:not([tabindex="-1"])'
        const controls = viewer.locator(focusable).filter({ visible: true })
        const first = controls.first(), last = controls.last()
        await last.focus(); await test.page.keyboard.press('Tab')
        assert.equal(await first.evaluate(element => document.activeElement === element), true, 'Tab from last should wrap to first')
        await first.focus(); await test.page.keyboard.press('Shift+Tab')
        assert.equal(await last.evaluate(element => document.activeElement === element), true, 'Shift+Tab from first should wrap to last')
        for (let index = 0; index < await controls.count() + 2; index++) {
          await test.page.keyboard.press('Tab')
          assert.equal(await viewer.evaluate(element => element.contains(document.activeElement)), true)
        }
        await assertWithinViewport(test.page, viewer)
        await test.page.keyboard.press('Escape'); await viewer.waitFor({ state: 'hidden' })
        assert.equal(await test.page.evaluate(() => document.activeElement === window.storyOpeningTrigger), true)
        assert.equal(await test.page.evaluate(() => document.body.style.overflow), overflowBefore)
      }, options)
    }
    await check('own Story card opens existing viewer; its separate plus opens only the existing composer', async test => {
      const viewer = await open(test)
      assert.equal(await activeCaption(viewer), 'Story 1')
      await viewer.getByRole('button', { name: 'Đóng Story', exact: true }).click()
      await viewer.waitFor({ state: 'hidden' })
      const create = test.page.getByRole('button', { name: 'Tạo Story mới', exact: true })
      await create.click()
      await test.page.getByRole('dialog', { name: 'Tạo Story', exact: true }).waitFor()
      assert.equal(await test.page.getByRole('dialog', { name: 'Trình xem Story', exact: true }).count(), 0)
      await test.page.getByRole('button', { name: 'Hủy', exact: true }).click()
      assert.equal(await test.page.getByRole('dialog', { name: 'Tạo Story', exact: true }).count(), 0)
    }, { owner: true })
    await check('keyboard navigation and Space pause work outside editable input; reply preserves text editing', async test => {
      const viewer = await open(test)
      await test.page.keyboard.press('ArrowRight'); await imageReady(viewer, 'Story 2')
      await test.page.keyboard.press('ArrowLeft'); await imageReady(viewer, 'Story 1')
      await test.page.keyboard.press(' '); await viewer.getByRole('button', { name: 'Tiếp tục', exact: true }).waitFor()
      const before = await progress(viewer); await pause(350); assert.equal(await progress(viewer), before)
      await test.page.keyboard.press(' '); await viewer.getByRole('button', { name: 'Tạm dừng', exact: true }).waitFor()
      const reply = viewer.getByRole('textbox', { name: 'Trả lời Story', exact: true })
      await reply.focus(); await reply.pressSequentially('Xin chào Minh'); await reply.press('ArrowLeft'); await reply.press('ArrowRight')
      assert.equal(await reply.inputValue(), 'Xin chào Minh')
      assert.equal(await activeCaption(viewer), 'Story 1')
    })
  }
  if (selected('hold')) {
    await check('pointer hold pauses image; release resumes without restarting elapsed', async test => {
      const viewer = await open(test)
      await pause(550)
      const point = await mediaPoint(viewer)
      await test.page.mouse.move(point.x, point.y); await test.page.mouse.down(); await pause(120)
      const held = await progress(viewer); await pause(750); assert.equal(await progress(viewer), held)
      await test.page.mouse.up(); await pause(300)
      const resumed = await progress(viewer)
      assert.ok(resumed > held && resumed < held + 12, 'release should continue the same image clock')
      assert.equal(await activeCaption(viewer), 'Story 1')
    })
    await check('long hold on navigation overlay does not navigate on release', async test => {
      const viewer = await open(test)
      const point = await mediaPoint(viewer, 0.85)
      await test.page.mouse.move(point.x, point.y); await test.page.mouse.down(); await pause(700)
      assert.equal(await activeCaption(viewer), 'Story 1')
      await test.page.mouse.up(); await pause(180)
      assert.equal(await activeCaption(viewer), 'Story 1')
      const before = await progress(viewer); await pause(250); assert.ok(await progress(viewer) > before)
      await viewer.getByRole('button', { name: 'Story tiếp theo', exact: true }).click(); await imageReady(viewer, 'Story 2')
      await viewer.getByRole('button', { name: 'Story tiếp theo', exact: true }).click(); await imageReady(viewer, 'Story 3')
      const finalPoint = await mediaPoint(viewer, 0.85)
      await test.page.mouse.move(finalPoint.x, finalPoint.y); await test.page.mouse.down(); await pause(700); await test.page.mouse.up(); await pause(180)
      assert.equal(await viewer.count(), 1, 'long hold on the final Story should not close the viewer on release')
      assert.equal(await activeCaption(viewer), 'Story 3')
    })
    await check('pointer cancellation releases hold without overriding manual pause', async test => {
      const viewer = await open(test)
      const point = await mediaPoint(viewer)
      await test.page.mouse.move(point.x, point.y); await test.page.mouse.down(); await pause(150)
      await viewer.getByRole('img', { name: 'Story 1', exact: true }).dispatchEvent('pointercancel', { pointerId: 1, pointerType: 'mouse', isPrimary: true, bubbles: true })
      await test.page.mouse.up()
      const before = await progress(viewer); await pause(300); assert.ok(await progress(viewer) > before)
      await viewer.getByRole('button', { name: 'Tạm dừng', exact: true }).click()
      await test.page.mouse.move(point.x, point.y); await test.page.mouse.down(); await pause(150)
      await viewer.getByRole('img', { name: 'Story 1', exact: true }).dispatchEvent('pointercancel', { pointerId: 1, pointerType: 'mouse', isPrimary: true, bubbles: true })
      await test.page.mouse.up()
      const manuallyPaused = await progress(viewer); await pause(300); assert.equal(await progress(viewer), manuallyPaused)
    })
  }
  if (selected('mobile')) {
    for (const options of viewportCases.filter(item => item.touch)) {
      await check(`${options.name}: real touch swipe across both navigation overlays navigates once; vertical gestures do not`, async test => {
        const viewer = await open(test)
        const left = await mediaPoint(viewer, 0.3)
        const distance = Math.max(55, left.bounds.width * 0.2)
        await touchGesture(test.context, test.page, left, { x: left.x - distance, y: left.y })
        await imageReady(viewer, 'Story 2'); assert.equal(await activeCaption(viewer), 'Story 2')
        const right = await mediaPoint(viewer, 0.7)
        await touchGesture(test.context, test.page, right, { x: right.x + distance, y: right.y })
        await imageReady(viewer, 'Story 1'); assert.equal(await activeCaption(viewer), 'Story 1')
        const vertical = await mediaPoint(viewer, 0.85)
        await touchGesture(test.context, test.page, vertical, { x: vertical.x, y: vertical.y - 75 })
        assert.equal(await activeCaption(viewer), 'Story 1')
        await assertWithinViewport(test.page, viewer)
      }, options)
      await check(`${options.name}: mobile taps navigate and reply text/controls remain usable`, async test => {
        const viewer = await open(test)
        let point = await mediaPoint(viewer, 0.85)
        await test.page.touchscreen.tap(point.x, point.y); await imageReady(viewer, 'Story 2')
        point = await mediaPoint(viewer, 0.15)
        await test.page.touchscreen.tap(point.x, point.y); await imageReady(viewer, 'Story 1')
        const reply = viewer.getByRole('textbox', { name: 'Trả lời Story', exact: true })
        await reply.tap(); await reply.pressSequentially('Tin nhắn có dấu cách'); await reply.press('ArrowLeft'); await reply.press('ArrowRight')
        assert.equal(await reply.inputValue(), 'Tin nhắn có dấu cách')
        const before = await progress(viewer); await pause(350); assert.equal(await progress(viewer), before)
        await viewer.getByRole('button', { name: 'like', exact: true }).tap()
        assert.equal(await activeCaption(viewer), 'Story 1')
        await assertWithinViewport(test.page, viewer)
      }, options)
    }
  }
  if (selected('mobile')) {
    await check('375px: first Retry tap works after a vertical gesture on failed media', async test => {
      await test.trigger.click()
      const viewer = test.page.getByRole('dialog', { name: 'Trình xem Story', exact: true })
      await viewer.getByText('Không thể tải tin', { exact: true }).waitFor()
      const bounds = await viewer.getByRole('button', { name: 'Story tiếp theo', exact: true }).boundingBox()
      const start = { x: bounds.x + bounds.width * 0.5, y: bounds.y + bounds.height * 0.4 }
      await touchGesture(test.context, test.page, start, { x: start.x, y: start.y - 75 })
      test.state.broken.delete(storyIds[0])
      await viewer.getByRole('button', { name: 'Thử lại', exact: true }).tap()
      await imageReady(viewer)
      assert.equal(await activeCaption(viewer), 'Story 1')
      assert.equal(await viewer.getByRole('button', { name: 'Thử lại', exact: true }).count(), 0)
    }, { ...viewportCases[1], setup: state => state.broken.add(storyIds[0]) })
  }
  if (selected('video')) {
    const videoBody = execFileSync('ffmpeg', ['-hide_banner', '-loglevel', 'error', '-f', 'lavfi', '-i', 'color=c=blue:s=90x160:r=10', '-t', '12', '-c:v', 'libvpx', '-f', 'webm', 'pipe:1'])
    for (const options of [viewportCases[0], viewportCases[1]]) {
      await check(`${options.name}: video starts muted, toggle controls real audio state and persists within viewer`, async test => {
        const viewer = await open(test, true)
        let video = viewer.locator('video')
        assert.equal(await video.evaluate(element => element.muted), true)
        const toggle = viewer.getByRole('button', { name: /âm thanh|tiếng/ })
        assert.equal(await toggle.count(), 1)
        if (options.touch) await toggle.tap(); else await toggle.click()
        assert.equal(await video.evaluate(element => element.muted), false)
        await pause(150); assert.equal(await video.evaluate(element => element.paused), false, 'audio control should not activate hold pause')
        await viewer.getByRole('button', { name: 'Story tiếp theo', exact: true }).click(); await imageReady(viewer, 'Story 2')
        await viewer.getByRole('button', { name: 'Story trước', exact: true }).click()
        await test.page.waitForFunction(() => {
          const video = document.querySelector('[aria-label="Trình xem Story"] video')
          return video && Number.isFinite(video.duration) && !video.paused
        })
        video = viewer.locator('video'); assert.equal(await video.evaluate(element => element.muted), false)
        await toggle.click(); assert.equal(await video.evaluate(element => element.muted), true)
        await assertWithinViewport(test.page, viewer)
      }, { ...options, video: true, videoBody })
    }
  }
  if (selected('motion')) {
    await check('reduced motion removes nonessential progress transition while progress still advances', async test => {
      const viewer = await open(test)
      const style = await viewer.locator('[aria-label="Tiến trình Story"] > span > span').first().evaluate(element => {
        const style = getComputedStyle(element)
        return { property: style.transitionProperty, durations: style.transitionDuration.split(',').map(value => parseFloat(value)) }
      })
      assert.ok(style.property === 'none' || style.durations.every(duration => duration === 0))
      const before = await progress(viewer); await pause(300); assert.ok(await progress(viewer) > before)
    }, { reducedMotion: 'reduce' })
  }
  assert.deepEqual(errors, [], 'interaction checks must not create unhandled browser errors')
  console.log(`${passed} Story interaction browser checks passed (${scope}).`)
} finally { await browser.close() }
