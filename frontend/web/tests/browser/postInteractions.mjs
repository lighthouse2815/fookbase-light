import assert from 'node:assert/strict'
import { postFixtures, postId } from './postInteractionsFixture.mjs'

const { chromium } = await import(process.env.PLAYWRIGHT_MODULE ?? 'playwright')
const browser = await chromium.launch({ headless: true, executablePath: process.env.CHROMIUM_EXECUTABLE, args: ['--no-sandbox'] })
const baseUrl = process.env.GAME_BASE_URL ?? 'http://127.0.0.1:5183'
const failures = []
const passed = []
const trackErrors = (page) => {
  page.on('pageerror', (error) => failures.push(error.stack))
  page.on('console', (message) => {
    if (message.type() !== 'error' && message.type() !== 'warning') return
    if (/connection was stopped during negotiation|HttpConnection before stop\(\)/.test(message.text())) return
    // These two API failures are deliberately injected to verify rollback.
    if (/503/.test(message.text()) && message.location().url.includes(`/api/posts/${postId}/`)) return
    failures.push(`${message.type()}: ${message.text()}`)
  })
}
const check = async (name, run) => { await run(); passed.push(name); console.log(`PASS ${name}`) }
const until = async (checkValue) => {
  for (let attempt = 0; attempt < 100; attempt++) {
    if (await checkValue()) return
    await new Promise((resolve) => setTimeout(resolve, 30))
  }
  assert.fail('Condition did not settle')
}

try {
  const context = await browser.newContext({ viewport: { width: 1366, height: 900 } })
  const state = await postFixtures(context)
  const page = await context.newPage()
  trackErrors(page)
  await page.goto(`${baseUrl}/posts/${postId}`)
  const reaction = page.locator('[data-post-reaction]').first()
  await reaction.waitFor()
  await check('desktop picker appears above the button without moving the card', async () => {
    const before = await page.locator('article').last().boundingBox()
    const button = await reaction.boundingBox()
    await reaction.hover()
    const menu = page.getByRole('menu', { name: 'Chọn cảm xúc', exact: true })
    await menu.waitFor()
    await page.waitForTimeout(200)
    const popup = await menu.boundingBox()
    assert.ok(popup.y + popup.height <= button.y)
    assert.deepEqual(await page.locator('article').last().boundingBox(), before)
    assert.equal(await menu.getByRole('menuitemradio').count(), 6)
  })
  await check('optimistic reaction, change and removal use existing API', async () => {
    await page.getByRole('menuitemradio', { name: 'Yêu thích', exact: true }).click()
    assert.equal(await reaction.getAttribute('aria-pressed'), 'true')
    assert.match(await reaction.innerText(), /Yêu thích/)
    await until(() => state.post.viewerReaction === 'love')
    await reaction.focus()
    await reaction.press('ArrowDown')
    await page.getByRole('menuitemradio', { name: 'Wow', exact: true }).click()
    assert.match(await reaction.innerText(), /Wow/)
    await until(() => state.post.viewerReaction === 'wow')
    await reaction.click()
    assert.equal(await reaction.getAttribute('aria-pressed'), 'false')
    await until(() => state.post.viewerReaction === null)
    assert.deepEqual(state.post.reactionCounts, { like: 2, love: 1, wow: 0 })
  })
  await check('reaction picker supports arrows, Escape and focus return', async () => {
    await reaction.focus()
    await reaction.press('ArrowDown')
    assert.equal(await page.locator(':focus').getAttribute('aria-label'), 'Thích')
    await page.keyboard.press('ArrowRight')
    assert.equal(await page.locator(':focus').getAttribute('aria-label'), 'Yêu thích')
    await page.keyboard.press('Escape')
    assert.equal(await reaction.getAttribute('aria-expanded'), 'false')
    assert.equal(await reaction.evaluate((button) => button === document.activeElement), true)
  })
  await check('post dropdown supports outside click, keyboard and Escape', async () => {
    const trigger = page.getByRole('button', { name: 'Tùy chọn khác', exact: true })
    await trigger.click()
    const menu = page.getByRole('menu', { name: 'Tùy chọn khác', exact: true })
    await menu.waitFor()
    assert.equal(await page.locator(':focus').getAttribute('role'), 'menuitem')
    await page.keyboard.press('End')
    assert.equal(await page.locator(':focus').innerText(), 'Xóa')
    await page.keyboard.press('Escape')
    assert.equal(await trigger.getAttribute('aria-expanded'), 'false')
    assert.equal(await trigger.evaluate((button) => button === document.activeElement), true)
    await trigger.press('ArrowDown')
    await menu.waitFor()
    await page.mouse.click(10, 400)
    assert.equal(await trigger.getAttribute('aria-expanded'), 'false')
  })
  await check('reduced motion disables picker and vote animations', async () => {
    await page.emulateMedia({ reducedMotion: 'reduce' })
    await reaction.focus()
    await reaction.press('ArrowDown')
    const animation = await page.locator('.post-reaction-picker').evaluate((element) => getComputedStyle(element).animationName)
    assert.equal(animation, 'none')
    await page.keyboard.press('Escape')
  })
  await check('comments appear immediately and double submit creates one real comment', async () => {
    state.commentDelay = 700
    await page.getByRole('button', { name: 'Bình luận', exact: true }).first().click()
    const dialog = page.getByRole('dialog', { name: 'Bài viết của Người kiểm tra', exact: true })
    await dialog.waitFor()
    const input = dialog.getByRole('textbox', { name: 'Viết bình luận', exact: true })
    await input.fill('Bình luận tức thì')
    await input.press('Enter')
    assert.equal(await input.inputValue(), '')
    assert.equal(await dialog.locator('[data-comment-id][aria-busy="true"]').count(), 1)
    await dialog.getByRole('form', { name: 'Gửi bình luận' }).evaluate((form) => {
      form.dispatchEvent(new Event('submit', { bubbles: true, cancelable: true }))
      form.dispatchEvent(new Event('submit', { bubbles: true, cancelable: true }))
    })
    await until(() => state.comments.length === 1)
    await until(async () => await dialog.locator('[data-comment-id][aria-busy="true"]').count() === 0)
    assert.equal(await dialog.locator('[data-comment-id]').count(), 1)
    assert.equal(state.writes.filter((write) => write.action === 'comment').length, 1)
    assert.match(await page.locator('article').last().innerText(), /1 bình luận/)
    await page.getByRole('button', { name: 'Đóng bình luận', exact: true }).click()
  })
  await check('failed comment rolls back row/count and restores unchanged input', async () => {
    state.failComment = true
    state.commentDelay = 500
    await page.getByRole('button', { name: 'Bình luận', exact: true }).first().click()
    const dialog = page.getByRole('dialog', { name: 'Bài viết của Người kiểm tra', exact: true })
    const input = dialog.getByRole('textbox', { name: 'Viết bình luận', exact: true })
    await input.fill('Bản nháp cần giữ')
    await input.press('Enter')
    assert.equal(await dialog.locator('[data-comment-id]').count(), 2)
    await until(async () => await input.inputValue() === 'Bản nháp cần giữ')
    assert.equal(await dialog.locator('[data-comment-id]').count(), 1)
    assert.match(await page.locator('article').last().innerText(), /1 bình luận/)
    assert.equal(await page.getByRole('status').filter({ hasText: 'Không thể gửi bình luận thử nghiệm.' }).count(), 1)
    await input.fill('Bản nháp tiếp theo')
    await input.press('Enter')
    await input.fill('Đang viết nội dung khác')
    await until(async () => await dialog.locator('[data-comment-id][aria-busy="true"]').count() === 0)
    assert.equal(await input.inputValue(), 'Đang viết nội dung khác')
    await page.getByRole('button', { name: 'Đóng bình luận', exact: true }).click()
    state.failComment = false
  })
  await context.close()

  const mobile = await browser.newContext({ viewport: { width: 390, height: 844 }, isMobile: true, hasTouch: true })
  const mobileState = await postFixtures(mobile)
  const phone = await mobile.newPage()
  trackErrors(phone)
  await phone.goto(`${baseUrl}/posts/${postId}`)
  const mobileReaction = phone.locator('[data-post-reaction]').first()
  await mobileReaction.waitFor()
  await check('mobile long press opens six reactions and preserves scroll gestures', async () => {
    await mobileReaction.dispatchEvent('pointerdown', { pointerType: 'touch', clientX: 80, clientY: 230 })
    await mobileReaction.dispatchEvent('pointermove', { pointerType: 'touch', clientX: 80, clientY: 260 })
    await phone.waitForTimeout(500)
    assert.equal(await mobileReaction.getAttribute('aria-expanded'), 'false')
    await mobileReaction.dispatchEvent('pointercancel', { pointerType: 'touch' })
    await mobileReaction.dispatchEvent('pointerdown', { pointerType: 'touch', clientX: 80, clientY: 230 })
    await phone.waitForTimeout(470)
    await mobileReaction.dispatchEvent('pointerup', { pointerType: 'touch' })
    await mobileReaction.dispatchEvent('click')
    const menu = phone.getByRole('menu', { name: 'Chọn cảm xúc', exact: true })
    await menu.waitFor()
    const popup = await menu.boundingBox()
    assert.ok(popup.x >= 0 && popup.x + popup.width <= 390)
    await phone.getByRole('menuitemradio', { name: 'Yêu thích', exact: true }).tap()
    await until(() => mobileState.post.viewerReaction === 'love')
    assert.equal(await phone.evaluate(() => document.documentElement.scrollWidth > innerWidth), false)
  })
  await mobile.close()
  assert.deepEqual(failures, [])
  console.log(`${passed.length} browser checks passed; no unexpected console/page errors.`)
} finally {
  await browser.close()
}
