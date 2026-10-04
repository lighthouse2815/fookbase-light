import assert from 'node:assert/strict'
import { mkdir } from 'node:fs/promises'
import { commentId, eventId, notification, notificationFixtures, postId } from './notificationsFixture.mjs'

// Reuse an external Playwright installation; the application needs no browser-test dependency.
const { chromium } = await import(process.env.PLAYWRIGHT_MODULE ?? 'playwright')
const browser = await chromium.launch({ headless: true, executablePath: process.env.CHROMIUM_EXECUTABLE, args: ['--no-sandbox'] })
const baseUrl = process.env.NOTIFICATIONS_BASE_URL ?? 'http://127.0.0.1:5183'
const artifacts = process.env.NOTIFICATIONS_ARTIFACTS ?? '/tmp/notification-browser-artifacts'
await mkdir(artifacts, { recursive: true })
const errors = []
const passed = []
const check = async (name, run) => { await run(); passed.push(name); console.log(`PASS ${name}`) }
const until = async (condition, message = 'Condition did not settle') => {
  for (let attempt = 0; attempt < 250; attempt++) {
    if (await condition()) return
    await new Promise((resolve) => setTimeout(resolve, 30))
  }
  assert.fail(message)
}
const bell = (page) => page.getByRole('button', { name: /^Thông báo(?:, \d+ chưa đọc)?$/ })
const panel = (page) => page.getByRole('dialog', { name: 'Thông báo', exact: true })
const rows = (page) => panel(page).locator('[data-notification-id]')
const row = (page, id) => panel(page).locator(`[data-notification-id="${id}"]`)
const count = async (page, expected) => until(async () => (await bell(page).getAttribute('aria-label')) === `Thông báo, ${expected} chưa đọc`)
const open = async (page) => { if (!await panel(page).count()) await bell(page).click(); await panel(page).waitFor() }
const options = (page) => panel(page).getByRole('button', { name: 'Tùy chọn thông báo', exact: true })
const markAll = async (page) => { await options(page).click(); await panel(page).getByRole('button', { name: 'Đánh dấu tất cả là đã đọc', exact: true }).click() }
const closeToasts = async (page) => {
  const close = page.getByRole('status').getByRole('button', { name: 'Đóng thông báo', exact: true })
  while (await close.count()) await close.first().click()
}
const unread = async (target) => target.evaluate((element) => element.dataset.unread === 'true' || Boolean(element.querySelector('[aria-label="Chưa đọc"]')) || /Chưa đọc/.test(element.textContent))
const create = async (fixtureOptions = {}, contextOptions = {}) => {
  const context = await browser.newContext({ viewport: { width: 1366, height: 900 }, ...contextOptions })
  const state = await notificationFixtures(context, fixtureOptions)
  const page = await context.newPage()
  page.on('pageerror', (error) => errors.push(error.stack))
  page.on('console', (message) => {
    if (!['error', 'warning'].includes(message.type())) return
    if (/connection was stopped during negotiation|HttpConnection before stop\(\)|Browser fixture reconnect|Connection disconnected with error/.test(message.text())) return
    // The API failures below are deliberate; their UI recovery is checked explicitly.
    if (/503/.test(message.text()) && message.location().url.includes('/api/notifications')) return
    errors.push(`${message.type()}: ${message.text()}`)
  })
  await page.goto(`${baseUrl}/feed`)
  await bell(page).waitFor()
  return { context, state, page }
}

try {
  const { context, state, page } = await create()
  await count(page, 1)
  await check('bell, grouped filters, popover focus, ESC and outside click', async () => {
    const before = await page.evaluate(() => ({ width: document.body.clientWidth, scrollY }))
    await open(page)
    await until(async () => await rows(page).count() === 2)
    assert.equal(await panel(page).getByRole('heading', { name: 'Mới', exact: true }).count(), 1)
    assert.equal(await panel(page).getByRole('heading', { name: 'Trước đó', exact: true }).count(), 1)
    assert.equal(await panel(page).evaluate((element) => element.contains(document.activeElement)), true)
    await panel(page).getByRole('button', { name: 'Chưa đọc', exact: true }).click()
    assert.equal(await rows(page).count(), 1)
    await panel(page).getByRole('button', { name: 'Tất cả', exact: true }).click()
    assert.equal(await rows(page).count(), 2)
    await page.keyboard.press('Escape')
    assert.equal(await panel(page).count(), 0)
    assert.equal(await bell(page).evaluate((element) => element === document.activeElement), true)
    await open(page)
    await page.mouse.click(10, 500)
    assert.equal(await panel(page).count(), 0)
    assert.deepEqual(await page.evaluate(() => ({ width: document.body.clientWidth, scrollY })), before)
    await open(page)
    const last = panel(page).getByRole('link').last()
    await last.focus()
    await page.keyboard.press('Tab')
    assert.equal(await panel(page).count(), 0)
    await page.keyboard.press('Escape')
  })
  await check('single read is immediate, closes and navigates; failed API rolls unread back with a toast', async () => {
    state.failRead = true
    await open(page)
    await row(page, state.items[0].id).click()
    await count(page, 0)
    assert.equal(state.readResponses, 0)
    assert.equal(await panel(page).count(), 0)
    assert.equal(new URL(page.url()).pathname, `/posts/${postId}`)
    await until(() => state.readResponses === 1)
    await count(page, 1)
    await page.getByRole('status').filter({ hasText: /Không thể|thất bại/ }).waitFor()
    await closeToasts(page)
    state.failRead = false
    await open(page)
    assert.equal(await unread(row(page, state.items[0].id)), true)
    await row(page, state.items[0].id).click()
    await count(page, 0)
    await until(() => state.readResponses === 2)
    await open(page)
    await row(page, state.items[0].id).click()
    await count(page, 0)
    await page.waitForTimeout(100)
    assert.equal(state.writes.filter((write) => write.action === 'read').length, 2)
  })
  await check('real SignalR increments once, inserts once and suppresses toast while panel is open', async () => {
    await open(page)
    const next = notification(70, { createdAtUtc: new Date().toISOString() })
    await state.push(next)
    await count(page, 1)
    await row(page, next.id).waitFor()
    assert.equal(await rows(page).first().getAttribute('data-notification-id'), next.id)
    assert.equal(await page.getByRole('status').count(), 0)
    await state.push(next)
    await page.waitForTimeout(180)
    await count(page, 1)
    assert.equal(await row(page, next.id).count(), 1)
    const readsBefore = state.reads.length
    await state.reconnect()
    await until(() => state.reads.length > readsBefore)
    await until(async () => await rows(page).count() === 3)
    await count(page, 1)
    assert.equal(await row(page, next.id).count(), 1)
    await page.keyboard.press('Escape')
  })
  await check('compact realtime toast dedupes, links comment to parent post and reads on click', async () => {
    await page.locator('header a[href="/feed"]').first().click()
    const next = notification(71, { type: 'PostComment', entityType: 'Comment', entityId: commentId, parentEntityId: postId, createdAtUtc: new Date().toISOString() })
    await state.push(next)
    await count(page, 2)
    const toast = page.getByRole('status').filter({ hasText: /Minh.*bình luận/ })
    await toast.waitFor()
    assert.equal(await toast.getByRole('link').count(), 1)
    await state.push(next)
    await page.waitForTimeout(180)
    assert.equal(await toast.count(), 1)
    await count(page, 2)
    await toast.getByRole('link').click()
    await count(page, 1)
    assert.equal(new URL(page.url()).pathname, `/posts/${postId}`)
    assert.notEqual(new URL(page.url()).pathname, `/posts/${commentId}`)
    await until(() => state.readResponses === 3)
    await closeToasts(page)
    // The comment deep-link opens the app's existing discussion dialog.
    await page.keyboard.press('Escape')
  })
  await check('system icon has no fake actor; EventInvite targets the parent event', async () => {
    await open(page)
    const system = notification(72, { type: 'AccountWarning', actorUserId: null, actorUsername: null, actorDisplayName: null, entityType: null, entityId: null, createdAtUtc: new Date().toISOString() })
    await state.push(system)
    const target = row(page, system.id)
    await target.waitFor()
    assert.equal(await target.getAttribute('href'), '/settings/security')
    assert.doesNotMatch(await target.innerText(), /Người dùng|00000000|MINH/i)
    assert.equal(await target.locator('img').count(), 0)
    assert.ok(await target.locator('svg').count() > 0)
    const event = notification(73, { type: 'EventInvite', entityType: 'Event', entityId: '00000000-0000-0000-0000-000000000999', parentEntityId: eventId, createdAtUtc: new Date().toISOString() })
    await state.push(event)
    await row(page, event.id).waitFor()
    assert.equal(await row(page, event.id).getAttribute('href'), `/events/${eventId}`)
    await row(page, event.id).click()
    assert.equal(new URL(page.url()).pathname, `/events/${eventId}`)
    await page.getByRole('heading', { name: 'Sự kiện kiểm tra thông báo', exact: true }).waitFor()
    assert.ok(state.profileReads.every((path) => path === '/api/users/me'), 'Notifications must not request each actor profile')
  })
  await context.close()

  for (const failed of [false, true]) {
    const pending = await create({ items: [notification(1), notification(2)] })
    await count(pending.page, 2)
    pending.state.failAllRead = failed
    pending.state.allReadDelay = 900
    await check(`mark all ${failed ? 'rolls back' : 'succeeds'} and retains read rows plus a notification received in flight`, async () => {
      await open(pending.page)
      await markAll(pending.page)
      await count(pending.page, 0)
      assert.equal(await rows(pending.page).count(), 2)
      assert.equal(await unread(rows(pending.page).first()), false)
      await options(pending.page).click()
      assert.equal(await panel(pending.page).getByRole('button', { name: 'Đánh dấu tất cả là đã đọc', exact: true }).isDisabled(), true)
      await options(pending.page).click()
      const next = notification(80, { createdAtUtc: new Date().toISOString() })
      await pending.state.push(next)
      await count(pending.page, 1)
      await until(() => pending.state.allReadResponses === 1)
      await count(pending.page, failed ? 3 : 1)
      assert.equal(await rows(pending.page).count(), 3)
      assert.equal(await unread(row(pending.page, next.id)), true)
      assert.equal(await unread(row(pending.page, notification(1).id)), failed)
      assert.equal(pending.state.writes.filter((write) => write.action === 'all-read').length, 1)
      if (failed) await pending.page.getByRole('status').waitFor()
      await pending.page.screenshot({ path: `${artifacts}/mark-all-${failed ? 'rollback' : 'success'}.png` })
    })
    await pending.context.close()
  }

  const paged = await create({ items: Array.from({ length: 25 }, (_, index) => notification(index + 1)) })
  await count(paged.page, 25)
  await check('pagination errors preserve loaded rows; retry dedupes; realtime preserves the reading position', async () => {
    await open(paged.page)
    await until(async () => await rows(paged.page).count() === 20)
    paged.state.failMore = true
    paged.state.pageDelay = 500
    const more = panel(paged.page).getByRole('button', { name: /Xem thông báo trước đó|Tải thêm/ })
    await more.click()
    await more.click({ force: true }).catch(() => undefined)
    await until(() => paged.state.reads.filter((read) => read.before).length === 1)
    await panel(paged.page).getByRole('button', { name: /Thử lại/ }).waitFor()
    assert.equal(await rows(paged.page).count(), 20)
    paged.state.failMore = false
    await panel(paged.page).getByRole('button', { name: /Thử lại/ }).click()
    await until(async () => await rows(paged.page).count() === 25)
    const scroll = panel(paged.page).locator('.notification-scroll')
    await scroll.evaluate((element) => { element.scrollTop = 450 })
    const anchor = row(paged.page, notification(10).id)
    const before = await anchor.boundingBox()
    const scrollBefore = await scroll.evaluate((element) => element.scrollTop)
    const next = notification(90, { createdAtUtc: new Date().toISOString() })
    await paged.state.push(next)
    await row(paged.page, next.id).waitFor({ state: 'attached' })
    await count(paged.page, 26)
    const after = await anchor.boundingBox()
    assert.ok(await scroll.evaluate((element) => element.scrollTop) >= scrollBefore)
    assert.ok(Math.abs(after.y - before.y) < 3, `Reading anchor moved by ${after.y - before.y}px`)
    const beforeReload = paged.state.reads.length
    await paged.state.reconnect()
    await until(() => paged.state.reads.length > beforeReload)
    await paged.page.waitForTimeout(650)
    const ids = await rows(paged.page).evaluateAll((elements) => elements.map((element) => element.dataset.notificationId))
    assert.equal(ids.length, 26)
    assert.equal(new Set(ids).size, ids.length)
    assert.equal(await rows(paged.page).first().getAttribute('data-notification-id'), next.id)
  })
  await paged.context.close()

  const covered = await create({ items: Array.from({ length: 25 }, (_, index) => notification(index + 1)), readAllIncludesArrivals: true })
  await count(covered.page, 25)
  await check('read-all locks pagination and reconciles a realtime arrival also read by the server', async () => {
    await open(covered.page)
    await until(async () => await rows(covered.page).count() === 20)
    covered.state.allReadDelay = 900
    await markAll(covered.page)
    const more = panel(covered.page).getByRole('button', { name: 'Xem thông báo trước đó', exact: true })
    assert.equal(await more.isDisabled(), true)
    const next = notification(91, { createdAtUtc: new Date().toISOString() })
    await covered.state.push(next)
    await count(covered.page, 1)
    await until(() => covered.state.allReadResponses === 1)
    await count(covered.page, 0)
    await until(async () => !await unread(row(covered.page, next.id)))
    assert.equal(covered.state.reads.filter((read) => read.before).length, 0)
    assert.equal(await rows(covered.page).count(), 21)
  })
  await covered.context.close()

  const olderUnread = await create({ items: Array.from({ length: 25 }, (_, index) => notification(index + 1, { isRead: index < 20 })) })
  await count(olderUnread.page, 5)
  await check('unread filter does not claim caught up while unread records are beyond the loaded page', async () => {
    await open(olderUnread.page)
    await until(async () => await rows(olderUnread.page).count() === 20)
    await panel(olderUnread.page).getByRole('button', { name: 'Chưa đọc', exact: true }).click()
    await panel(olderUnread.page).getByText('Còn 5 thông báo chưa đọc', { exact: true }).waitFor()
    assert.equal(await panel(olderUnread.page).getByText('Bạn đã xem hết thông báo', { exact: true }).count(), 0)
    await panel(olderUnread.page).getByRole('button', { name: 'Xem thông báo trước đó', exact: true }).click()
    await until(async () => await rows(olderUnread.page).count() === 5)
  })
  await olderUnread.context.close()

  const racing = await create({ pageDelay: 500, countDelay: 500 })
  await check('late first-page and count responses preserve a concurrent realtime notification', async () => {
    await open(racing.page)
    const next = notification(92, { createdAtUtc: new Date().toISOString() })
    await racing.state.push(next)
    await count(racing.page, 2)
    await until(async () => await rows(racing.page).count() === 3)
    await racing.page.waitForTimeout(550)
    await count(racing.page, 2)
    assert.equal(await row(racing.page, next.id).count(), 1)
  })
  await racing.context.close()

  const loading = await create({ pageDelay: 1400 })
  await check('initial loading uses row skeletons and does not report an empty inbox prematurely', async () => {
    await open(loading.page)
    assert.ok(await panel(loading.page).locator('[aria-hidden="true"]').count() >= 4)
    assert.equal(await panel(loading.page).getByText('Chưa có thông báo', { exact: true }).count(), 0)
    await until(async () => await rows(loading.page).count() === 2)
    await loading.page.screenshot({ path: `${artifacts}/desktop.png` })
  })
  await loading.context.close()
  const empty = await create({ items: [] })
  await check('empty and unread-empty messages are distinct; zero-count mark-all is disabled', async () => {
    await open(empty.page)
    await panel(empty.page).getByText('Chưa có thông báo', { exact: true }).waitFor()
    await panel(empty.page).getByRole('button', { name: 'Chưa đọc', exact: true }).click()
    await panel(empty.page).getByText('Bạn đã xem hết thông báo', { exact: true }).waitFor()
    await options(empty.page).click()
    assert.equal(await panel(empty.page).getByRole('button', { name: 'Đánh dấu tất cả là đã đọc', exact: true }).isDisabled(), true)
  })
  await empty.context.close()
  const broken = await create({ failInitial: true })
  await check('initial fetch error retries notifications and recovers', async () => {
    await open(broken.page)
    await panel(broken.page).getByText('Không thể tải thông báo', { exact: true }).waitFor()
    // Wait for the feed's independent profile request before measuring retry scope.
    await broken.page.getByRole('button', { name: /Bạn đang nghĩ gì.*Game Tester/ }).waitFor()
    const reads = broken.state.reads.length
    const profileReads = broken.state.profileReads.length
    broken.state.failInitial = false
    await panel(broken.page).getByRole('button', { name: 'Thử lại', exact: true }).click()
    await until(async () => await rows(broken.page).count() === 2)
    assert.equal(broken.state.reads.length, reads + 1)
    assert.equal(broken.state.profileReads.length, profileReads)
  })
  await broken.context.close()

  for (const width of [375, 768, 1280, 1920]) {
    const responsive = await create({ unreadCount: width === 375 ? 120 : 99 }, { viewport: { width, height: 850 }, reducedMotion: width === 375 ? 'reduce' : 'no-preference' })
    await check(`responsive ${width}px panel, capped badge, keyboard and ${width === 375 ? 'reduced' : 'normal'} motion`, async () => {
      await count(responsive.page, width === 375 ? 120 : 99)
      assert.match(await bell(responsive.page).innerText(), width === 375 ? /99\+/ : /99/)
      await open(responsive.page)
      await until(async () => await rows(responsive.page).count() === 2)
      const bounds = await panel(responsive.page).boundingBox()
      assert.ok(bounds.x >= 0 && bounds.x + bounds.width <= width + 1)
      assert.ok(bounds.y >= 0 && bounds.y + bounds.height <= 851)
      assert.equal(await responsive.page.evaluate(() => document.documentElement.scrollWidth > innerWidth), false)
      await rows(responsive.page).first().focus()
      assert.equal(await rows(responsive.page).first().evaluate((element) => element === document.activeElement), true)
      if (width === 375) {
        const next = notification(95, { createdAtUtc: new Date().toISOString() })
        await responsive.state.push(next)
        await row(responsive.page, next.id).waitFor()
        assert.equal(await row(responsive.page, next.id).evaluate((element) => getComputedStyle(element).animationName), 'none')
        assert.equal(await bell(responsive.page).evaluate((element) => getComputedStyle(element).animationName), 'none')
      }
      if (width === 768) {
        await responsive.page.evaluate(() => { document.documentElement.dataset.theme = 'light' })
        assert.equal(await panel(responsive.page).evaluate((element) => getComputedStyle(element).backgroundColor), 'rgb(255, 255, 255)')
      }
      await responsive.page.screenshot({ path: `${artifacts}/${width}px.png` })
      await responsive.page.keyboard.press('Escape')
      assert.equal(await panel(responsive.page).count(), 0)
      assert.equal(await bell(responsive.page).evaluate((element) => element === document.activeElement), true)
    })
    await responsive.context.close()
  }
  assert.deepEqual(errors, [])
  console.log(`${passed.length} notification browser checks passed; no unexpected console/page errors.`)
} finally {
  await browser.close()
}
