import assert from 'node:assert/strict'
import { mkdir } from 'node:fs/promises'
import { storyId, storyNotificationFixture } from './storyNotificationFixture.mjs'

const { chromium } = await import(process.env.PLAYWRIGHT_MODULE ?? 'playwright')
const browser = await chromium.launch({ headless: true, executablePath: process.env.CHROMIUM_EXECUTABLE, args: ['--no-sandbox'] })
const baseUrl = process.env.NOTIFICATIONS_BASE_URL ?? 'http://127.0.0.1:5184'
const artifacts = process.env.NOTIFICATIONS_ARTIFACTS ?? '/tmp/notification-browser-artifacts'
await mkdir(artifacts, { recursive: true })
const errors = []
const passed = []
const check = async (name, run) => { await run(); passed.push(name); console.log(`PASS ${name}`) }
const viewer = (page) => page.getByRole('dialog', { name: 'Trình xem Story', exact: true })
const bell = (page) => page.getByRole('button', { name: /^Thông báo, \d+ chưa đọc$/ })

async function create(options = {}, viewport = { width: 1366, height: 900 }) {
  const context = await browser.newContext({ viewport })
  const state = await storyNotificationFixture(context, options)
  const page = await context.newPage()
  page.setDefaultTimeout(10000)
  page.on('pageerror', (error) => errors.push(error.stack))
  page.on('console', (message) => {
    if (!['error', 'warning'].includes(message.type())) return
    if (/connection was stopped during negotiation|HttpConnection before stop\(\)/.test(message.text())) return
    if (/403|404|410|503/.test(message.text()) && message.location().url.includes(`/api/stories/${storyId}`)) return
    errors.push(`${message.type()}: ${message.text()}`)
  })
  return { context, state, page }
}

try {
  const direct = await create({ delay: 1500 })
  await check('direct Story URL loads the requested story with a skeleton and existing viewer', async () => {
    await direct.page.goto(`${baseUrl}/stories/${storyId}`, { waitUntil: 'domcontentloaded' })
    await direct.page.getByRole('status', { name: 'Đang tải Story', exact: true }).waitFor()
    await viewer(direct.page).waitFor()
    await viewer(direct.page).getByRole('img', { name: 'Story cần mở từ thông báo', exact: true }).waitFor()
    assert.deepEqual(direct.state.reads.map((read) => read.id), [storyId])
    assert.equal(direct.state.views[0], storyId)
    assert.equal(await viewer(direct.page).evaluate((element) => element.contains(document.activeElement)), true)
    assert.equal(await direct.page.evaluate(() => document.body.style.overflow), 'hidden')
    await direct.page.screenshot({ path: `${artifacts}/story-direct.png` })
  })
  await check('Story ESC returns to feed once and restores body scrolling', async () => {
    await direct.page.keyboard.press('Escape')
    await direct.page.waitForURL(`${baseUrl}/feed`)
    await viewer(direct.page).waitFor({ state: 'hidden' })
    assert.equal(await viewer(direct.page).count(), 0)
    assert.notEqual(await direct.page.evaluate(() => document.body.style.overflow), 'hidden')
    // This tab started at the deep-link; going back would leave the app for about:blank.
    assert.equal(new URL(direct.page.url()).pathname, '/feed')
  })
  await direct.context.close()

  const fromNotification = await create({ canManage: true })
  await check('StoryReaction notification opens its exact story; close replaces the route safely', async () => {
    await fromNotification.page.goto(`${baseUrl}/feed`, { waitUntil: 'domcontentloaded' })
    await bell(fromNotification.page).click()
    const panel = fromNotification.page.getByRole('dialog', { name: 'Thông báo', exact: true })
    const notificationRow = panel.locator('[data-notification-id]').first()
    await notificationRow.waitFor()
    assert.equal(await notificationRow.getAttribute('href'), `/stories/${storyId}`)
    await notificationRow.click()
    await viewer(fromNotification.page).waitFor()
    assert.equal(new URL(fromNotification.page.url()).pathname, `/stories/${storyId}`)
    assert.equal(await panel.count(), 0)
    await viewer(fromNotification.page).getByRole('button', { name: 'Đóng Story', exact: true }).click()
    await fromNotification.page.waitForURL(`${baseUrl}/feed`)
    await viewer(fromNotification.page).waitFor({ state: 'hidden' })
    await fromNotification.page.goBack()
    assert.notEqual(new URL(fromNotification.page.url()).pathname, `/stories/${storyId}`)
  })
  await fromNotification.context.close()

  const retry = await create({ status: 503 })
  await check('temporary Story failure retries only the requested story', async () => {
    await retry.page.goto(`${baseUrl}/stories/${storyId}`, { waitUntil: 'domcontentloaded' })
    await retry.page.getByRole('heading', { name: 'Không thể tải Story', exact: true }).waitFor()
    assert.equal(await viewer(retry.page).count(), 0)
    const reads = retry.state.reads.length
    retry.state.status = 200
    await retry.page.getByRole('button', { name: 'Thử lại', exact: true }).click()
    await viewer(retry.page).waitFor()
    assert.equal(retry.state.reads.length, reads + 1)
    assert.equal(retry.state.archiveReads, 0)
    await retry.page.keyboard.press('Escape')
  })
  await retry.context.close()

  for (const status of [403, 404, 410]) {
    const unavailable = await create({ status })
    await check(`unavailable/private/deleted/expired Story (${status}) gives a graceful safe fallback`, async () => {
      await unavailable.page.goto(`${baseUrl}/stories/${storyId}`, { waitUntil: 'domcontentloaded' })
      await unavailable.page.getByRole('heading', { name: 'Story không còn khả dụng', exact: true }).waitFor()
      assert.equal(await viewer(unavailable.page).count(), 0)
      assert.equal(unavailable.state.mediaReads, 0)
      assert.equal(unavailable.state.views.length, 0)
      assert.equal(unavailable.state.archiveReads, 0)
      await unavailable.page.getByRole('link', { name: 'Về bảng tin', exact: true }).click()
      await unavailable.page.waitForURL(`${baseUrl}/feed`)
    })
    await unavailable.context.close()
  }
  const mobile = await create({}, { width: 375, height: 812 })
  await check('375px Story route stays inside viewport and keyboard focus stays in the viewer', async () => {
    await mobile.page.goto(`${baseUrl}/stories/${storyId}`, { waitUntil: 'domcontentloaded' })
    await viewer(mobile.page).waitFor()
    const bounds = await viewer(mobile.page).boundingBox()
    assert.ok(bounds.x >= 0 && bounds.x + bounds.width <= 375)
    assert.equal(await mobile.page.evaluate(() => document.documentElement.scrollWidth > innerWidth), false)
    const buttons = viewer(mobile.page).getByRole('button')
    await buttons.last().focus()
    await mobile.page.keyboard.press('Tab')
    assert.equal(await viewer(mobile.page).evaluate((element) => element.contains(document.activeElement)), true)
    await mobile.page.screenshot({ path: `${artifacts}/story-mobile.png` })
    await mobile.page.keyboard.press('Escape')
    await mobile.page.waitForURL(`${baseUrl}/feed`)
  })
  await mobile.context.close()

  const archive = await create()
  await check('existing archive URL still uses the archive page', async () => {
    await archive.page.goto(`${baseUrl}/stories/archive`, { waitUntil: 'domcontentloaded' })
    await archive.page.getByRole('heading', { name: 'Kho lưu trữ Story', exact: true }).waitFor()
    assert.equal(archive.state.reads.length, 0)
    assert.equal(archive.state.archiveReads, 1)
  })
  await archive.context.close()
  assert.deepEqual(errors, [])
  console.log(`${passed.length} Story notification browser checks passed; no unexpected console/page errors.`)
} finally {
  await browser.close()
}
