import assert from 'node:assert/strict'
import { execFileSync } from 'node:child_process'
import { storyIds, storyViewerFixture } from './storyViewerFixture.mjs'

const { chromium } = await import(process.env.PLAYWRIGHT_MODULE ?? 'playwright')
const browser = await chromium.launch({ headless: true, executablePath: process.env.CHROMIUM_EXECUTABLE, args: ['--no-sandbox'] })
const baseUrl = process.env.STORY_BASE_URL ?? 'http://127.0.0.1:5184'
const checkPattern = new RegExp(process.env.STORY_RACE_PATTERN ?? '.')
const pause = ms => new Promise(resolve => setTimeout(resolve, ms))
const videoBody = execFileSync('ffmpeg', ['-hide_banner', '-loglevel', 'error', '-f', 'lavfi', '-i', 'color=c=blue:s=90x160:r=10', '-t', '12', '-c:v', 'libvpx', '-f', 'webm', 'pipe:1'])
const errors = []
let passed = 0

async function eventually(predicate, description) {
  const deadline = Date.now() + 10000
  while (!await predicate()) {
    assert.ok(Date.now() < deadline, description)
    await pause(30)
  }
}

async function check(name, run, options = {}) {
  if (!checkPattern.test(name)) return
  const context = await browser.newContext({ viewport: { width: 1366, height: 900 } })
  const state = await storyViewerFixture(context, { ...options, videoBody })
  await options.setup?.(state, context)
  const page = await context.newPage()
  page.setDefaultTimeout(10000)
  page.on('pageerror', error => errors.push(error.stack))
  try {
    await page.goto(`${baseUrl}/feed`, { waitUntil: 'domcontentloaded' })
    await page.getByRole('button', { name: options.owner ? /Story của bạn/ : /Minh$/ }).click()
    const viewer = page.getByRole('dialog', { name: 'Trình xem Story', exact: true })
    await viewer.locator(options.video ? 'video' : 'img[alt="Story 1"]').waitFor()
    if (!options.delayedImage) await eventually(() => viewer.locator(options.video ? 'video' : 'img[alt="Story 1"]').evaluate(media => media.tagName === 'VIDEO' ? media.readyState >= 2 : media.complete && media.naturalWidth > 0), 'Active media becomes ready')
    const next = () => viewer.getByRole('button', { name: 'Story tiếp theo', exact: true }).click()
    const previous = () => viewer.getByRole('button', { name: 'Story trước', exact: true }).click()
    const reaction = type => viewer.getByRole('button', { name: type, exact: true })
    const selected = type => eventually(async () => await reaction(type).getAttribute('aria-pressed') === 'true', `${type} is the selected reaction`)
    const accessCount = id => state.accesses.filter(storyId => storyId === id).length
    const assetCount = id => state.assets.filter(storyId => storyId === id).length
    await run({ page, viewer, state, next, previous, reaction, selected, accessCount, assetCount })
    passed++
    console.log(`PASS ${name}`)
  } finally { await context.close() }
}

try {
  await check('rapid reaction changes keep the latest intent in UI and on the server without restarting the image', async ({ viewer, state, reaction, selected, accessCount, assetCount }) => {
    await pause(550)
    await viewer.getByRole('button', { name: 'Tạm dừng', exact: true }).click()
    const progress = viewer.locator('[aria-label="Tiến trình Story"] > span > span').first()
    const before = await progress.evaluate(element => parseFloat(element.style.width))
    assert.ok(before > 0)
    const accesses = accessCount(storyIds[0]), assets = assetCount(storyIds[0])
    await reaction('like').click(); await reaction('love').click(); await reaction('haha').click()
    await selected('haha')
    await eventually(() => state.stories[0].viewerReaction === 'haha', 'Server stores the latest reaction intent')
    await pause(900)
    assert.equal(state.stories[0].viewerReaction, 'haha')
    assert.equal(state.reactions.at(-1).type, 'haha')
    assert.equal(await reaction('like').getAttribute('aria-pressed'), 'false')
    assert.equal(await reaction('love').getAttribute('aria-pressed'), 'false')
    assert.equal(await progress.evaluate(element => parseFloat(element.style.width)), before)
    assert.equal(accessCount(storyIds[0]), accesses)
    assert.equal(assetCount(storyIds[0]), assets)
  }, { setup: state => { state.reactionDelays.like = 650; state.reactionDelays.love = 350; state.reactionDelays.haha = 50 } })

  await check('a second click while reaction is pending removes the latest intent', async ({ state, reaction }) => {
    await reaction('like').click(); await reaction('like').click()
    await eventually(() => state.reactions.some(request => request.type === null), 'Pending reaction is followed by removal')
    await eventually(async () => await reaction('like').getAttribute('aria-pressed') === 'false', 'Reaction is deselected')
    await pause(400)
    assert.equal(state.stories[0].viewerReaction, null)
    assert.equal(state.stories[0].reactionCount, 0)
  }, { setup: state => { state.reactionDelays.like = 450 } })

  await check('a reaction failure is handled locally and the next attempt succeeds', async ({ viewer, state, reaction, selected }) => {
    state.failReaction = true
    await reaction('love').click()
    await viewer.getByText('Không thể cập nhật phản ứng.', { exact: true }).waitFor()
    assert.equal(state.stories[0].viewerReaction, null)
    assert.equal(await reaction('love').getAttribute('aria-pressed'), 'false')
    state.failReaction = false
    await reaction('love').click(); await selected('love')
    assert.equal(state.stories[0].viewerReaction, 'love')
    assert.equal(state.reactions.length, 2)
  })

  await check('a queued reaction failure displays the previously confirmed server reaction', async ({ viewer, state, reaction, selected }) => {
    await reaction('like').click(); await reaction('love').click()
    await viewer.getByText('Không thể cập nhật phản ứng.', { exact: true }).waitFor()
    await selected('like')
    assert.equal(await reaction('love').getAttribute('aria-pressed'), 'false')
    assert.equal(state.stories[0].viewerReaction, 'like')
    assert.equal(state.stories[0].reactionCount, 1)
  }, { setup: async (state, context) => {
    state.reactionDelays.like = 450
    await context.route(`**/api/stories/${storyIds[0]}/reaction`, route => {
      if (route.request().postDataJSON()?.type !== 'love') return route.fallback()
      state.reactions.push({ storyId: storyIds[0], type: 'love' })
      return route.fulfill({ status: 503, json: { detail: 'Queued reaction failed' } })
    })
  } })

  await check('reaction updates preserve native video playback and media access', async ({ page, viewer, reaction, selected, accessCount, assetCount }) => {
    const video = viewer.locator('video')
    await eventually(() => video.evaluate(element => !element.paused && element.currentTime > 0.2), 'Video is playing')
    await video.evaluate(element => { window.activeRaceVideo = element })
    const before = await video.evaluate(element => element.currentTime)
    const accesses = accessCount(storyIds[0]), assets = assetCount(storyIds[0])
    await reaction('love').click(); await selected('love')
    assert.equal(await video.evaluate(element => element === window.activeRaceVideo), true)
    assert.ok(await video.evaluate(element => element.currentTime) > before)
    assert.equal(await video.evaluate(element => element.paused), false)
    assert.equal(accessCount(storyIds[0]), accesses)
    assert.equal(assetCount(storyIds[0]), assets)
    assert.equal(await page.evaluate(() => window.activeRaceVideo.isConnected), true)
  }, { video: true, setup: state => { state.reactionDelays.love = 450 } })

  await check('a delayed mark-view response preserves a newer reaction and active media', async ({ viewer, state, reaction, selected, accessCount, assetCount }) => {
    await eventually(() => state.views.includes(storyIds[0]), 'Mark-view request begins after media is ready')
    const accesses = accessCount(storyIds[0]), assets = assetCount(storyIds[0])
    await pause(300)
    const progress = viewer.locator('[aria-label="Tiến trình Story"] > span > span').first()
    const before = await progress.evaluate(element => parseFloat(element.style.width))
    await reaction('haha').click(); await selected('haha')
    await eventually(() => state.stories[0].isViewed, 'Delayed mark-view request succeeds')
    await pause(100)
    assert.equal(await reaction('haha').getAttribute('aria-pressed'), 'true')
    assert.ok(await progress.evaluate(element => parseFloat(element.style.width)) > before)
    assert.equal(accessCount(storyIds[0]), accesses)
    assert.equal(assetCount(storyIds[0]), assets)
  }, { viewed: false, setup: state => { state.viewDelay = 800 } })

  let concurrentView, concurrentReaction
  await check('concurrent mark-view and reaction responses preserve both seen and reaction state', async ({ page, viewer, state, reaction, selected, accessCount }) => {
    await eventually(() => Boolean(concurrentView), 'Mark-view is waiting for a response')
    await reaction('love').click()
    await eventually(() => Boolean(concurrentReaction), 'Reaction is waiting for a response')
    const staleReactionSnapshot = { ...structuredClone(state.stories[0]), viewerReaction: 'love', reactionCount: 1, isViewed: false }
    state.stories[0].isViewed = true
    state.stories[0].viewerReaction = 'love'
    state.stories[0].reactionCount = 1
    const accesses = accessCount(storyIds[0])
    await Promise.all([
      concurrentView.fulfill({ status: 204 }),
      concurrentReaction.fulfill({ json: staleReactionSnapshot }),
    ])
    await selected('love')
    await pause(150)
    assert.equal(accessCount(storyIds[0]), accesses)
    await viewer.getByRole('button', { name: 'Đóng Story', exact: true }).click()
    assert.equal(await page.getByRole('button', { name: /Minh$/ }).evaluate(card => card.style.borderColor), 'var(--color-border)')
  }, { viewed: false, setup: async (state, context) => {
    state.stories[1].isViewed = true
    await context.route(`**/api/stories/${storyIds[0]}/view`, route => { state.views.push(storyIds[0]); concurrentView = route })
    await context.route(`**/api/stories/${storyIds[0]}/reaction`, route => {
      state.reactions.push({ storyId: storyIds[0], type: route.request().postDataJSON().type })
      concurrentReaction = route
    })
  } })

  await check('viewers from an old story cannot open or populate the new story list', async ({ viewer, state, next }) => {
    await viewer.getByRole('button', { name: '2 lượt xem', exact: true }).click()
    await eventually(() => state.viewerReads.length === 1, 'First owner list request begins')
    await next(); await viewer.locator('img[alt="Story 2"]').waitFor()
    await pause(850)
    assert.equal(await viewer.getByText('Viewer A', { exact: true }).count(), 0)
    assert.equal(await viewer.getByText('Người đã xem', { exact: true }).count(), 0)
    await viewer.getByRole('button', { name: '2 lượt xem', exact: true }).click()
    await viewer.getByText('Viewer B', { exact: true }).waitFor()
    assert.equal(await viewer.getByText('Viewer A', { exact: true }).count(), 0)
    assert.equal(state.viewerReads.at(-1).storyId, storyIds[1])
    assert.equal(state.views.length, 0)
  }, { owner: true, setup: state => { state.viewerDelay = 650 } })

  await check('owner viewer-list failure is caught and local retry loads the same story', async ({ viewer, state }) => {
    state.failViewers = true
    await viewer.getByRole('button', { name: '2 lượt xem', exact: true }).click()
    const list = viewer.locator('aside')
    await list.getByText(/Không thể tải danh sách|Danh sách thất bại thử nghiệm/).waitFor()
    state.failViewers = false
    await list.getByRole('button', { name: 'Thử lại', exact: true }).click()
    await list.getByText('Viewer A', { exact: true }).waitFor()
    assert.deepEqual(state.viewerReads.map(request => request.storyId), [storyIds[0], storyIds[0]])
    await list.getByRole('button', { name: 'Đóng danh sách người xem', exact: true }).click()
    assert.equal(await viewer.locator('aside').count(), 0)
  }, { owner: true })

  await check('owner viewer-list pagination deduplicates users and clears the final cursor', async ({ viewer, state }) => {
    await viewer.getByRole('button', { name: '2 lượt xem', exact: true }).click()
    const list = viewer.locator('aside')
    await list.getByText('Viewer A', { exact: true }).waitFor()
    await list.getByRole('button', { name: 'Tải thêm', exact: true }).click()
    await list.getByText('Viewer 2', { exact: true }).waitFor()
    assert.equal(await list.getByText('Viewer A', { exact: true }).count(), 1)
    assert.equal(await list.getByText('Viewer 2', { exact: true }).count(), 1)
    assert.equal(await list.getByRole('button', { name: 'Tải thêm', exact: true }).count(), 0)
    assert.deepEqual(state.viewerReads, [{ storyId: storyIds[0], cursor: null }, { storyId: storyIds[0], cursor: 'page-2' }])
  }, { owner: true })

  await check('reopening a failed fresh viewer list retries page one instead of an old cursor', async ({ viewer, state }) => {
    const list = viewer.locator('aside')
    await viewer.getByRole('button', { name: '2 lượt xem', exact: true }).click()
    await list.getByText('Viewer A', { exact: true }).waitFor()
    await list.getByRole('button', { name: 'Đóng danh sách người xem', exact: true }).click()
    state.failViewers = true
    await viewer.getByRole('button', { name: '2 lượt xem', exact: true }).click()
    await list.getByText(/Không thể tải danh sách|Danh sách thất bại thử nghiệm/).waitFor()
    assert.equal(await list.getByText('Viewer A', { exact: true }).count(), 0)
    state.failViewers = false
    await list.getByRole('button', { name: 'Thử lại', exact: true }).click()
    await list.getByText('Viewer A', { exact: true }).waitFor()
    assert.deepEqual(state.viewerReads.map(request => request.cursor), [null, null, null])
  }, { owner: true })

  await check('failed viewer pagination preserves existing viewers and retries the same cursor', async ({ viewer, state }) => {
    await viewer.getByRole('button', { name: '2 lượt xem', exact: true }).click()
    const list = viewer.locator('aside')
    await list.getByText('Viewer A', { exact: true }).waitFor()
    state.failViewers = true
    await list.getByRole('button', { name: 'Tải thêm', exact: true }).click()
    await list.getByText(/Không thể tải danh sách|Danh sách thất bại thử nghiệm/).waitFor()
    assert.equal(await list.getByText('Viewer A', { exact: true }).count(), 1)
    state.failViewers = false
    await list.getByRole('button', { name: 'Thử lại', exact: true }).click()
    await list.getByText('Viewer 2', { exact: true }).waitFor()
    assert.equal(await list.getByText('Viewer A', { exact: true }).count(), 1)
    assert.deepEqual(state.viewerReads.map(request => request.cursor), [null, 'page-2', 'page-2'])
  }, { owner: true })

  await check('rapid owner viewer-list clicks deduplicate in-flight requests', async ({ viewer, state }) => {
    await viewer.getByRole('button', { name: '2 lượt xem', exact: true }).evaluate(button => { button.click(); button.click() })
    await viewer.getByText('Viewer A', { exact: true }).waitFor()
    assert.equal(state.viewerReads.length, 1)
  }, { owner: true, setup: state => { state.viewerDelay = 350 } })

  await check('mark-view waits until image load completes', async ({ viewer, state }) => {
    assert.equal(await viewer.locator('img[alt="Story 1"]').evaluate(image => image.complete && image.naturalWidth > 0), false)
    await pause(350)
    assert.equal(state.views.length, 0)
    await eventually(() => state.views.filter(id => id === storyIds[0]).length === 1, 'Image readiness triggers one mark-view')
  }, { viewed: false, delayedImage: true, setup: state => { state.imageDelays[storyIds[0]] = 900 } })

  await check('mark-view deduplicates repeated visits while pending and after success', async ({ viewer, state, next, previous }) => {
    await eventually(() => state.views.includes(storyIds[0]), 'First mark-view starts')
    await next(); await viewer.locator('img[alt="Story 2"]').waitFor()
    await previous(); await viewer.locator('img[alt="Story 1"]').waitFor()
    await pause(400)
    assert.equal(state.views.filter(id => id === storyIds[0]).length, 1)
    await eventually(() => state.stories[0].isViewed, 'Mark-view succeeds')
    await next(); await viewer.locator('img[alt="Story 2"]').waitFor()
    await previous(); await viewer.locator('img[alt="Story 1"]').waitFor()
    await pause(200)
    assert.equal(state.views.filter(id => id === storyIds[0]).length, 1)
  }, { viewed: false, setup: state => { state.viewDelay = 1100 } })

  await check('a failed mark-view can retry on a later visit without a false seen state', async ({ viewer, state, next, previous }) => {
    await eventually(() => state.views.includes(storyIds[0]), 'First mark-view starts')
    await pause(350)
    assert.equal(state.stories[0].isViewed, false)
    await next(); await viewer.locator('img[alt="Story 2"]').waitFor()
    await previous(); await viewer.locator('img[alt="Story 1"]').waitFor()
    await eventually(() => state.stories[0].isViewed, 'Second visit retries successfully')
    assert.equal(state.views.filter(id => id === storyIds[0]).length, 2)
  }, { viewed: false, setup: state => { state.viewDelay = 150; state.viewFailures = 1 } })

  await check('closing during owner viewer-list request causes no stale update or unhandled error', async ({ page, viewer, state }) => {
    await viewer.getByRole('button', { name: '2 lượt xem', exact: true }).click()
    await eventually(() => state.viewerReads.length === 1, 'Owner list request starts')
    await viewer.getByRole('button', { name: 'Đóng Story', exact: true }).click()
    await pause(650)
    assert.equal(await page.getByRole('dialog', { name: 'Trình xem Story', exact: true }).count(), 0)
    assert.equal(await page.getByText('Viewer A', { exact: true }).count(), 0)
  }, { owner: true, setup: state => { state.viewerDelay = 400; state.failViewers = true } })

  await check('closing during a failed reaction causes no stale update or unhandled error', async ({ page, viewer, state, reaction }) => {
    await reaction('like').click()
    await eventually(() => state.reactions.length === 1, 'Reaction request starts')
    await viewer.getByRole('button', { name: 'Đóng Story', exact: true }).click()
    await pause(650)
    assert.equal(await page.getByRole('dialog', { name: 'Trình xem Story', exact: true }).count(), 0)
    assert.equal(await page.getByText('Không thể cập nhật phản ứng.', { exact: true }).count(), 0)
  }, { setup: state => { state.reactionDelays.like = 400; state.failReaction = true } })

  assert.deepEqual(errors, [])
  console.log(`${passed} Story viewer race browser checks passed.`)
} finally { await browser.close() }
