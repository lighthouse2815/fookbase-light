import assert from 'node:assert/strict'
import { mkdir } from 'node:fs/promises'
import { photoIds, photoVideoFixture, postId, postPhotoFixtures } from './postPhotoLightboxFixture.mjs'

// PLAYWRIGHT_MODULE=/path/to/playwright/index.mjs PHOTO_BASE_URL=http://127.0.0.1:5184 node tests/browser/postPhotoLightbox.mjs
// PHOTO_CHECK_PHASE=navigation runs the first implementation's navigation/loading checks.
const { chromium } = await import(process.env.PLAYWRIGHT_MODULE ?? 'playwright')
const browser = await chromium.launch({ headless: true, executablePath: process.env.CHROMIUM_EXECUTABLE, args: ['--no-sandbox'] })
const baseUrl = process.env.PHOTO_BASE_URL ?? 'http://127.0.0.1:5184'
const artifacts = process.env.PHOTO_ARTIFACTS ?? '/tmp/post-photo-lightbox-artifacts'
const variant = process.env.PHOTO_VARIANT ?? 'dev'
const navigationOnly = process.env.PHOTO_CHECK_PHASE === 'navigation'
const videoBody = await photoVideoFixture()
await mkdir(artifacts, { recursive: true })
const errors = []
const passed = []
const check = async (name, run) => { await run(); passed.push(name); console.log(`PASS ${name}`) }
const until = async (condition, message = 'Photo state did not settle') => {
  for (let attempt = 0; attempt < 250; attempt++) {
    if (await condition()) return
    await new Promise((resolve) => setTimeout(resolve, 30))
  }
  assert.fail(message)
}
const dialog = (page) => page.getByRole('dialog', { name: 'Xem ảnh', exact: true })
const stage = (page) => dialog(page).locator('[data-photo-stage]')
const image = (page) => stage(page).locator('img[data-photo-media-id]')
const sidebar = (page) => dialog(page).locator('[data-photo-sidebar]')
const sidebarScroll = (page) => dialog(page).locator('[data-photo-sidebar-scroll]')
const next = (page) => dialog(page).getByRole('button', { name: 'Ảnh tiếp theo', exact: true })
const previous = (page) => dialog(page).getByRole('button', { name: 'Ảnh trước', exact: true })
const zoomIn = (page) => dialog(page).getByRole('button', { name: 'Phóng to', exact: true })
const zoomOut = (page) => dialog(page).getByRole('button', { name: 'Thu nhỏ', exact: true })
const reset = (page) => dialog(page).getByRole('button', { name: 'Đặt lại', exact: true })
const scale = async (page) => Number(await stage(page).getAttribute('data-photo-scale'))
const index = async (page) => Number(await stage(page).getAttribute('data-photo-index'))
const current = async (page, expected, loaded = true) => {
  await until(async () => await index(page) === expected)
  if (loaded) await until(async () => await image(page).count() === 1 && await image(page).evaluate((element) => element.complete && element.naturalWidth > 0))
  assert.equal(await stage(page).getAttribute('data-lightbox-media-id'), photoIds[expected])
}
const close = async (page) => { await page.keyboard.press('Escape'); await dialog(page).waitFor({ state: 'hidden' }) }
const open = async (page, state, photoIndex = 1) => {
  state.phase = 'gallery'
  const grid = page.locator('article').first()
  const imageIndex = state.mixed && photoIndex > 2 ? photoIndex - 1 : photoIndex
  await grid.getByRole('button', { name: /^Xem ảnh(?: \d.*)?$/ }).nth(imageIndex).click()
  await dialog(page).waitFor()
}

async function create(options = {}, contextOptions = {}) {
  const context = await browser.newContext({ viewport: { width: 1366, height: 900 }, ...contextOptions })
  const state = await postPhotoFixtures(context, baseUrl, { videoBody, ...options })
  const page = await context.newPage()
  page.setDefaultTimeout(10000)
  page.on('pageerror', (error) => errors.push(error.stack))
  page.on('console', (message) => {
    if (!['error', 'warning'].includes(message.type())) return
    if (/connection was stopped during negotiation|HttpConnection before stop\(\)/.test(message.text())) return
    if (/403|503/.test(message.text()) && /\/media\/|\/photo-lightbox-fixture\//.test(message.location().url)) return
    errors.push(`${message.type()}: ${message.text()}`)
  })
  await page.goto(`${baseUrl}/posts/${postId}`, { waitUntil: 'domcontentloaded' })
  await page.locator('article').first().getByRole('button', { name: /^Xem ảnh(?: \d.*)?$/ }).nth(Math.min(1, state.ids.length - 1)).waitFor()
  return { context, state, page }
}

async function drag(page, target, dx, dy = 0) {
  const bounds = await target.boundingBox()
  const x = bounds.x + bounds.width / 2
  const y = bounds.y + bounds.height / 2
  await page.mouse.move(x, y)
  await page.mouse.down()
  await page.mouse.move(x + dx, y + dy, { steps: 8 })
  await page.mouse.up()
}

// Dispatch browser PointerEvents (the same handlers as real touch input); no app state is modified.
async function touchGesture(target, dx, dy = 0, pointerId = 11) {
  const bounds = await target.boundingBox()
  const x = bounds.x + bounds.width / 2
  const y = bounds.y + bounds.height / 2
  await target.dispatchEvent('pointerdown', { pointerId, pointerType: 'touch', isPrimary: true, button: 0, buttons: 1, clientX: x, clientY: y })
  await target.dispatchEvent('pointermove', { pointerId, pointerType: 'touch', isPrimary: true, buttons: 1, clientX: x + dx, clientY: y + dy })
  await target.dispatchEvent('pointerup', { pointerId, pointerType: 'touch', isPrimary: true, button: 0, buttons: 0, clientX: x + dx, clientY: y + dy })
}

// Native Chromium touch also exercises touch-action, pointer capture/cancel and scroll.
async function nativeTouch(page, target, dx, dy = 0) {
  const bounds = await target.boundingBox()
  const x = bounds.x + bounds.width / 2
  const y = bounds.y + bounds.height / 2
  const session = await page.context().newCDPSession(page)
  try {
    await session.send('Input.dispatchTouchEvent', { type: 'touchStart', touchPoints: [{ x, y }] })
    for (let step = 1; step <= 8; step++) {
      await session.send('Input.dispatchTouchEvent', { type: 'touchMove', touchPoints: [{ x: x + dx * step / 8, y: y + dy * step / 8 }] })
      await page.waitForTimeout(20)
    }
    await session.send('Input.dispatchTouchEvent', { type: 'touchEnd', touchPoints: [] })
  } finally {
    await session.detach()
  }
}

async function assertPanBounds(page) {
  const geometry = await stage(page).evaluate((element) => {
    const active = element.querySelector('img[data-photo-media-id]')
    const viewport = element.getBoundingClientRect()
    const actual = active.getBoundingClientRect()
    return { viewport: { x: viewport.x, y: viewport.y, width: viewport.width, height: viewport.height }, actual: { x: actual.x, y: actual.y, width: actual.width, height: actual.height } }
  })
  const { viewport, actual } = geometry
  for (const [position, dimension] of [['x', 'width'], ['y', 'height']]) {
    if (actual[dimension] > viewport[dimension] + 2) {
      assert.ok(actual[position] <= viewport[position] + 2)
      assert.ok(actual[position] + actual[dimension] >= viewport[position] + viewport[dimension] - 2)
    } else {
      assert.ok(Math.abs(actual[position] + actual[dimension] / 2 - viewport[position] - viewport[dimension] / 2) < 3)
    }
  }
}

try {
  const main = await create()
  await check('clicking the second image opens the exact attachment, exposes bounded navigation and keeps the full dialog in the viewport', async () => {
    await open(main.page, main.state, 1)
    await current(main.page, 1)
    assert.equal(await previous(main.page).isEnabled(), true)
    assert.equal(await next(main.page).isEnabled(), true)
    assert.match(await dialog(main.page).getByRole('status').first().innerText(), /2.*5/)
    const bounds = await dialog(main.page).boundingBox()
    assert.ok(bounds.x >= 0 && bounds.y >= 0 && bounds.x + bounds.width <= 1366 && bounds.y + bounds.height <= 901)
    assert.equal(await dialog(main.page).evaluate((element) => element.contains(document.activeElement)), true)
    await main.page.screenshot({ path: `${artifacts}/${variant}-desktop.png` })
  })
  await check('buttons and arrow keys navigate without wrapping at the first or last attachment', async () => {
    await previous(main.page).click()
    await current(main.page, 0)
    assert.equal(await previous(main.page).isDisabled(), true)
    await dialog(main.page).focus()
    await main.page.keyboard.press('ArrowLeft')
    await current(main.page, 0)
    await main.page.keyboard.press('ArrowRight')
    await current(main.page, 1)
    for (let step = 0; step < 3; step++) await next(main.page).click()
    await current(main.page, 4)
    assert.equal(await next(main.page).isDisabled(), true)
    await dialog(main.page).focus()
    await main.page.keyboard.press('ArrowRight')
    await current(main.page, 4)
  })
  await check('photo navigation preserves sidebar nodes, scroll, composer draft, loaded comments and reaction state without extra post/comment reads', async () => {
    await until(async () => await sidebar(main.page).locator('[data-comment-id]').count() === 20)
    const composer = sidebar(main.page).getByRole('textbox', { name: 'Viết bình luận', exact: true })
    await composer.fill('Bản nháp giữ nguyên khi đổi ảnh')
    const retained = await sidebarScroll(main.page).elementHandle()
    await retained.evaluate((element) => { element.scrollTop = 180 })
    const scrollTop = await retained.evaluate((element) => element.scrollTop)
    const reads = { posts: main.state.postReads, comments: main.state.commentReads }
    const reaction = sidebar(main.page).locator('[data-post-reaction]').first()
    await reaction.click()
    await until(() => main.state.interaction.reactionResponses === 1)
    await previous(main.page).click()
    await current(main.page, 3)
    assert.equal(await retained.evaluate((element) => element.isConnected), true)
    assert.equal(await retained.evaluate((element) => element.scrollTop), scrollTop)
    assert.equal(await composer.inputValue(), 'Bản nháp giữ nguyên khi đổi ảnh')
    assert.equal(await reaction.getAttribute('aria-pressed'), 'true')
    assert.deepEqual({ posts: main.state.postReads, comments: main.state.commentReads }, reads)
    assert.equal(main.state.interaction.writes.filter((write) => write.action === 'reaction').length, 1)
  })
  await check('arrow keys inside the comment composer keep the selected media unchanged', async () => {
    const composer = sidebar(main.page).getByRole('textbox', { name: 'Viết bình luận', exact: true })
    await composer.focus()
    await main.page.keyboard.press('ArrowLeft')
    await main.page.keyboard.press('ArrowRight')
    await current(main.page, 3)
    await close(main.page)
    assert.notEqual(await main.page.evaluate(() => document.body.style.overflow), 'hidden')
  })

  if (!navigationOnly) {
    await check('zoom controls clamp scale, double click toggles zoom and reset returns image to its fit position', async () => {
      await open(main.page, main.state, 1)
      await current(main.page, 1)
      assert.equal(await scale(main.page), 1)
      assert.equal(await zoomOut(main.page).isDisabled(), true)
      await zoomIn(main.page).click()
      await until(async () => await scale(main.page) > 1)
      for (let step = 0; step < 8; step++) await zoomIn(main.page).click({ force: true })
      assert.ok(await scale(main.page) <= 3)
      assert.equal(await zoomIn(main.page).isDisabled(), true)
      await zoomOut(main.page).click()
      assert.ok(await scale(main.page) < 3)
      await reset(main.page).click()
      assert.equal(await scale(main.page), 1)
      await image(main.page).dblclick()
      assert.ok(await scale(main.page) > 1)
      await image(main.page).dblclick()
      assert.equal(await scale(main.page), 1)
    })
    await check('zoomed dragging pans within image bounds and recomputes bounds on viewport resize', async () => {
      await zoomIn(main.page).click()
      await zoomIn(main.page).click()
      await drag(main.page, image(main.page), 2000, 1500)
      await assertPanBounds(main.page)
      await drag(main.page, image(main.page), -2500, -1800)
      await assertPanBounds(main.page)
      assert.equal(await index(main.page), 1)
      await main.page.setViewportSize({ width: 1024, height: 720 })
      await main.page.waitForTimeout(150)
      await assertPanBounds(main.page)
      await main.page.setViewportSize({ width: 1366, height: 900 })
      await main.page.waitForTimeout(150)
      await reset(main.page).click()
    })
    await check('changing image and closing/reopening reset zoom and pan without clearing the sidebar draft', async () => {
      await zoomIn(main.page).click()
      await drag(main.page, image(main.page), 100, 50)
      await next(main.page).click()
      await current(main.page, 2)
      assert.equal(await scale(main.page), 1)
      await zoomIn(main.page).click()
      await close(main.page)
      await open(main.page, main.state, 1)
      await current(main.page, 1)
      assert.equal(await scale(main.page), 1)
      await assertPanBounds(main.page)
      assert.equal(await sidebar(main.page).getByRole('textbox', { name: 'Viết bình luận', exact: true }).inputValue(), 'Bản nháp giữ nguyên khi đổi ảnh')
      await close(main.page)
    })
  }
  await main.context.close()

  const single = await create({ count: 1 })
  await check('a single attachment has no active navigation/counter and closing restores focus to its original trigger', async () => {
    const trigger = single.page.locator('article').first().getByRole('button', { name: /^Xem ảnh(?: \d.*)?$/ })
    await open(single.page, single.state, 0)
    await current(single.page, 0)
    assert.equal(await previous(single.page).count() === 0 || await previous(single.page).isDisabled(), true)
    assert.equal(await next(single.page).count() === 0 || await next(single.page).isDisabled(), true)
    assert.equal(await dialog(single.page).getByRole('status').filter({ hasText: /1.*1/ }).count(), 0)
    await close(single.page)
    assert.equal(await trigger.evaluate((element) => element === document.activeElement), true)
  })
  await single.context.close()

  const loading = await create({ expiresInMs: 150 })
  await check('expired selected access displays a compact loading state and refreshes only the current media', async () => {
    await loading.page.waitForTimeout(300)
    loading.state.accessDelay.set(photoIds[1], 900)
    await open(loading.page, loading.state, 1)
    await current(loading.page, 1, false)
    assert.equal(await stage(loading.page).getAttribute('aria-busy'), 'true')
    // Compare layout dimensions; the existing modal entry animation briefly scales its bounding rect.
    const before = await stage(loading.page).evaluate((element) => ({ width: element.offsetWidth, height: element.offsetHeight }))
    await current(loading.page, 1)
    assert.deepEqual(await stage(loading.page).evaluate((element) => ({ width: element.offsetWidth, height: element.offsetHeight })), before)
    assert.deepEqual(loading.state.accessReads.filter((read) => read.phase === 'gallery').map((read) => read.id), [photoIds[1]])
  })
  await loading.context.close()

  const broken = await create({ expiresInMs: 150 })
  await check('metadata error stays in the lightbox; retry refreshes the selected URL without losing sidebar state', async () => {
    await broken.page.waitForTimeout(300)
    broken.state.failAccess.add(photoIds[1])
    await open(broken.page, broken.state, 1)
    await stage(broken.page).getByText(/Không thể tải ảnh/).waitFor()
    assert.equal(await sidebar(broken.page).count(), 1)
    const composer = sidebar(broken.page).getByRole('textbox', { name: 'Viết bình luận', exact: true })
    await composer.fill('Nháp trong lúc lỗi ảnh')
    const comments = broken.state.commentReads
    broken.state.failAccess.clear()
    broken.state.expiresInMs = 600000
    await stage(broken.page).getByRole('button', { name: 'Thử lại', exact: true }).click()
    await current(broken.page, 1)
    assert.equal(await composer.inputValue(), 'Nháp trong lúc lỗi ảnh')
    assert.equal(broken.state.commentReads, comments)
    assert.ok(broken.state.accessReads.filter((read) => read.phase === 'gallery').every((read) => read.id === photoIds[1]))
  })
  await broken.context.close()

  const assets = await create({ expiresInMs: 150 })
  await check('image decode/load errors are retryable with a fresh current access URL', async () => {
    await assets.page.waitForTimeout(300)
    assets.state.corruptAssets.add(photoIds[1])
    await open(assets.page, assets.state, 1)
    await stage(assets.page).getByText(/Không thể tải ảnh/).waitFor()
    assets.state.corruptAssets.clear()
    const previousReads = assets.state.accessVersions.get(photoIds[1])
    await stage(assets.page).getByRole('button', { name: 'Thử lại', exact: true }).click()
    await current(assets.page, 1)
    assert.ok(assets.state.accessVersions.get(photoIds[1]) > previousReads)
  })
  await assets.context.close()

  const preload = await create({ count: 6 })
  await check('lightbox preloads only adjacent images and does not refetch metadata for the attachment list', async () => {
    // Finish initial lazy grid image reads so they cannot be mistaken for gallery preloads.
    await preload.page.locator('article img').evaluateAll((elements) => elements.forEach((element) => { element.loading = 'eager' }))
    await until(async () => await preload.page.locator('article img').evaluateAll((elements) => elements.every((element) => element.complete && element.naturalWidth > 0)))
    await preload.page.evaluate(() => { window.__photoPreloadUrls.length = 0 })
    await open(preload.page, preload.state, 1)
    await current(preload.page, 1)
    await preload.page.waitForTimeout(150)
    const reads = preload.state.assetReads.filter((read) => read.phase === 'gallery').map((read) => read.id)
    assert.ok(reads.every((id) => [photoIds[0], photoIds[1], photoIds[2]].includes(id)), `Unrelated image loaded: ${reads}`)
    const preloads = await preload.page.evaluate(() => window.__photoPreloadUrls)
    assert.deepEqual([...new Set(preloads.map((url) => new URL(url).pathname.split('/').at(-1).split('.')[0]))].sort(), [photoIds[0], photoIds[2]].sort())
    assert.equal(preload.state.accessReads.filter((read) => read.phase === 'gallery').length, 0)
  })
  await preload.context.close()

  const racing = await create({ expiresInMs: 150 })
  await check('rapid navigation ignores stale access responses and keeps the final selected image', async () => {
    await racing.page.waitForTimeout(300)
    racing.state.expiresInMs = 600000
    racing.state.accessDelay.set(photoIds[1], 1200)
    racing.state.accessDelay.set(photoIds[2], 200)
    racing.state.accessDelay.set(photoIds[3], 100)
    await open(racing.page, racing.state, 1)
    await next(racing.page).click()
    await next(racing.page).click()
    await previous(racing.page).click()
    await current(racing.page, 2)
    await racing.page.waitForTimeout(1400)
    await current(racing.page, 2)
    assert.equal(await image(racing.page).getAttribute('data-photo-media-id'), photoIds[2])
    assert.deepEqual(racing.state.accessReads.filter((read) => read.phase === 'gallery').map((read) => read.id).sort(), [photoIds[1], photoIds[2], photoIds[3]].sort())
    assert.equal(racing.state.commentReads, 1)
  })
  await racing.context.close()

  const mixed = await create({ mixed: true })
  await check('mixed image/video sequence preserves exact order, disables image zoom on video and pauses video when changing media', async () => {
    await open(mixed.page, mixed.state, 1)
    await current(mixed.page, 1)
    await next(mixed.page).click()
    await current(mixed.page, 2, false)
    const video = stage(mixed.page).locator('video')
    await video.waitFor()
    const activeVideo = await video.elementHandle()
    await activeVideo.evaluate((element) => { element.muted = true; return element.play() })
    assert.equal(await activeVideo.evaluate((element) => element.paused), false)
    if (!navigationOnly) assert.equal(await zoomIn(mixed.page).count() === 0 || await zoomIn(mixed.page).isDisabled(), true)
    await next(mixed.page).click()
    await current(mixed.page, 3)
    assert.equal(await activeVideo.evaluate((element) => element.paused), true)
    assert.equal(await activeVideo.evaluate((element) => element.isConnected), false)
    assert.equal(mixed.state.commentReads, 1)
    await previous(mixed.page).click()
    await current(mixed.page, 2, false)
    const reopened = await stage(mixed.page).locator('video').elementHandle()
    await reopened.evaluate((element) => { element.muted = true; return element.play() })
    await close(mixed.page)
    assert.equal(await reopened.evaluate((element) => element.paused), true)
  })
  await mixed.context.close()

  for (const width of [375, 768]) {
    const responsive = await create({}, { viewport: { width, height: 844 }, isMobile: width === 375, hasTouch: true, reducedMotion: 'reduce' })
    await check(`${width}px portrait and landscape fit without horizontal overflow or shifted controls`, async () => {
      await open(responsive.page, responsive.state, 1)
      await current(responsive.page, 1)
      const dialogBounds = await dialog(responsive.page).boundingBox()
      const stageBounds = await stage(responsive.page).boundingBox()
      const imageBounds = await image(responsive.page).boundingBox()
      assert.ok(dialogBounds.x >= 0 && dialogBounds.x + dialogBounds.width <= width + 1)
      assert.ok(dialogBounds.y >= 0 && dialogBounds.y + dialogBounds.height <= 845)
      assert.ok(imageBounds.width <= stageBounds.width + 1 && imageBounds.height <= stageBounds.height + 1)
      assert.equal(await responsive.page.evaluate(() => document.documentElement.scrollWidth > innerWidth), false)
      assert.equal(await image(responsive.page).evaluate((element) => getComputedStyle(element).animationName), 'none')
      for (const control of [previous(responsive.page), next(responsive.page), zoomIn(responsive.page), zoomOut(responsive.page), reset(responsive.page)]) {
        if (await control.count() === 0) continue
        const bounds = await control.boundingBox()
        assert.ok(bounds.x >= 0 && bounds.x + bounds.width <= width + 1 && bounds.width >= 44 && bounds.height >= 44)
      }
      await previous(responsive.page).click()
      await current(responsive.page, 0)
      const portrait = await image(responsive.page).boundingBox()
      assert.ok(portrait.height > portrait.width)
      await responsive.page.screenshot({ path: `${artifacts}/${variant}-${width}px.png` })
    })
    if (!navigationOnly) {
      await check(`${width}px horizontal touch swipe uses the 60px threshold and ignores vertical/short/sidebar gestures`, async () => {
        await touchGesture(stage(responsive.page), -40)
        await current(responsive.page, 0)
        await touchGesture(stage(responsive.page), -60)
        await current(responsive.page, 1)
        await touchGesture(stage(responsive.page), 15, 110)
        await current(responsive.page, 1)
        await touchGesture(sidebarScroll(responsive.page), -120)
        await current(responsive.page, 1)
        await touchGesture(stage(responsive.page), 100)
        await current(responsive.page, 0)
      })
      await check(`${width}px zoomed touch gestures pan rather than switch images and reset on close`, async () => {
        await zoomIn(responsive.page).click()
        await zoomIn(responsive.page).click()
        const before = await image(responsive.page).boundingBox()
        await touchGesture(stage(responsive.page), -100, 100)
        await current(responsive.page, 0)
        assert.ok(await scale(responsive.page) > 1)
        const after = await image(responsive.page).boundingBox()
        assert.ok(Math.abs(before.y - after.y) > 1 || Math.abs(before.x - after.x) > 1)
        await assertPanBounds(responsive.page)
        await close(responsive.page)
        await open(responsive.page, responsive.state, 1)
        await current(responsive.page, 1)
        assert.equal(await scale(responsive.page), 1)
      })
      if (width === 375) await check('native mobile touch navigates/pans in the media area while comments keep native scrolling and text input', async () => {
        await nativeTouch(responsive.page, stage(responsive.page), -100)
        await current(responsive.page, 2)
        await nativeTouch(responsive.page, stage(responsive.page), 100)
        await current(responsive.page, 1)
        await zoomIn(responsive.page).click()
        await zoomIn(responsive.page).click()
        const before = await image(responsive.page).boundingBox()
        await nativeTouch(responsive.page, stage(responsive.page), -90, 70)
        await current(responsive.page, 1)
        const after = await image(responsive.page).boundingBox()
        assert.ok(Math.abs(before.x - after.x) > 1 || Math.abs(before.y - after.y) > 1)
        await assertPanBounds(responsive.page)
        await reset(responsive.page).click()
        await until(async () => await sidebar(responsive.page).locator('[data-comment-id]').count() === 20)
        await sidebarScroll(responsive.page).evaluate((element) => { element.scrollTop = 0 })
        await nativeTouch(responsive.page, sidebarScroll(responsive.page), 0, -100)
        await until(async () => await sidebarScroll(responsive.page).evaluate((element) => element.scrollTop) > 10)
        await current(responsive.page, 1)
        const composer = sidebar(responsive.page).getByRole('textbox', { name: 'Viết bình luận', exact: true })
        await composer.fill('Bình luận sau khi vuốt ảnh')
        await composer.press('ArrowLeft')
        await current(responsive.page, 1)
        assert.equal(await composer.inputValue(), 'Bình luận sau khi vuốt ảnh')
      })
    }
    await responsive.context.close()
  }
  assert.deepEqual(errors, [])
  console.log(`${passed.length} post photo lightbox ${navigationOnly ? 'navigation' : 'full'} checks passed (${variant}); no unexpected console/page errors.`)
} finally {
  await browser.close()
}
