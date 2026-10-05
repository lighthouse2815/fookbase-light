import assert from 'node:assert/strict'
import { postFixtures, postId, viewerId } from './postInteractionsFixture.mjs'

const { chromium } = await import(process.env.PLAYWRIGHT_MODULE ?? 'playwright')
const browser = await chromium.launch({ headless: true, executablePath: process.env.CHROMIUM_EXECUTABLE, args: ['--no-sandbox'] })
const baseUrl = process.env.POST_BASE_URL ?? 'http://127.0.0.1:5183'
const errors = []
const until = async (condition) => {
  for (let attempt = 0; attempt < 200; attempt++) {
    if (await condition()) return
    await new Promise(resolve => setTimeout(resolve, 25))
  }
  assert.fail('Deletion did not settle')
}

async function setup(options = {}) {
  const context = await browser.newContext({ viewport: { width: 1280, height: 900 }, ...options })
  const state = await postFixtures(context)
  Object.assign(state, { deleteDelay: 900, failDelete: false, deleteWrites: [], deleted: false, deleteResponses: 0 })
  await context.route('**/api/**', async route => {
    const path = new URL(route.request().url()).pathname
    if (state.deleted && path === '/api/feed') return route.fulfill({ json: { items: [], nextCursor: null } })
    if (route.request().method() !== 'DELETE' || !path.startsWith('/api/posts/')) return route.fallback()
    state.deleteWrites.push(path)
    const fail = state.failDelete
    await new Promise(resolve => setTimeout(resolve, state.deleteDelay))
    if (fail) return route.fulfill({ status: 503, json: { detail: 'Không thể xóa nội dung thử nghiệm.' } })
    if (path === `/api/posts/${postId}`) state.deleted = true
    else {
      state.comments = state.comments.filter(comment => comment.id !== path.split('/').at(-1))
      state.post.commentCount = state.comments.length
    }
    await route.fulfill({ status: 204 })
    state.deleteResponses++
  })
  const page = await context.newPage()
  page.setDefaultTimeout(5000)
  page.on('pageerror', error => errors.push(error.message))
  return { context, page, state }
}

async function openPostDelete(page) {
  await page.goto(`${baseUrl}/posts/${postId}`)
  await page.getByRole('button', { name: 'Tùy chọn khác', exact: true }).click()
  await page.getByRole('menuitem', { name: 'Xóa', exact: true }).click()
  const dialog = page.getByRole('dialog', { name: 'Xóa bài viết?', exact: true })
  await dialog.waitFor()
  await dialog.evaluate(element => Promise.all(element.getAnimations().map(animation => animation.finished)))
  return dialog
}

const tests = [
  ['post deletion waits for the API, blocks duplicate clicks and keeps focus inside', async ({ page, state }) => {
    state.deleteDelay = 1800
    const dialog = await openPostDelete(page)
    const button = dialog.getByRole('button', { name: 'Xóa', exact: true })
    const before = await button.boundingBox()
    assert.equal(await dialog.getByRole('button', { name: 'Hủy' }).evaluate(element => element === document.activeElement), true)
    await button.evaluate(element => { element.click(); element.click(); element.click() })
    await until(async () => await button.getAttribute('data-state') === 'pending')
    assert.equal(state.deleteWrites.length, 1)
    assert.equal(state.deleteResponses, 0)
    assert.equal(await button.isDisabled(), true)
    assert.equal(await button.getAttribute('aria-busy'), 'true')
    assert.deepEqual(await button.boundingBox(), before)
    await page.keyboard.press('Escape')
    await page.mouse.click(4, 4)
    await page.keyboard.press('Tab')
    assert.equal(await dialog.evaluate(element => element === document.activeElement), true)
    await page.keyboard.press('Shift+Tab')
    assert.equal(await dialog.evaluate(element => element === document.activeElement), true)
    assert.equal(await dialog.count(), 1)
    await until(async () => state.deleted && await dialog.count() === 0)
    assert.equal(state.deleteResponses, 1)
    assert.equal(await page.evaluate(() => document.body.style.overflow), '')
  }],
  ['failed post deletion restores letters, preserves the post and allows explicit retry', async ({ page, state }) => {
    state.failDelete = true
    const dialog = await openPostDelete(page)
    const button = dialog.getByRole('button', { name: 'Xóa', exact: true })
    await button.click()
    await until(async () => await button.isEnabled())
    assert.equal(state.deleted, false)
    assert.equal(state.deleteWrites.length, 1)
    assert.equal(await dialog.count(), 1)
    assert.match(await page.locator('article').last().innerText(), /Bài viết kiểm tra tương tác/)
    assert.equal(await button.locator('.animated-delete__label').textContent(), 'Xóa')
    assert.equal(await button.locator('.animated-delete__label > span').evaluateAll(letters => letters.every(letter => getComputedStyle(letter).opacity === '1' && letter.getAnimations().length === 0)), true)
    assert.equal(await page.getByRole('status').filter({ hasText: 'Không thể xóa nội dung thử nghiệm.' }).count(), 1)
    state.failDelete = false
    await button.click()
    await until(async () => await dialog.count() === 0)
    assert.equal(state.deleteWrites.length, 2)
    assert.equal(state.deleted, true)
  }],
  ['cancel and Escape leave the post intact and restore focus to its menu trigger', async ({ page, state }) => {
    const dialog = await openPostDelete(page)
    await page.keyboard.press('Escape')
    assert.equal(await dialog.count(), 0)
    assert.equal(await page.getByRole('button', { name: 'Tùy chọn khác' }).evaluate(element => element === document.activeElement), true)
    assert.equal(state.deleteWrites.length, 0)
    await page.getByRole('button', { name: 'Tùy chọn khác', exact: true }).click()
    await page.getByRole('menuitem', { name: 'Xóa', exact: true }).click()
    await dialog.getByRole('button', { name: 'Hủy', exact: true }).click()
    assert.equal(await dialog.count(), 0)
    assert.equal(state.deleteWrites.length, 0)
  }],
  ['comment deletion recovers from failure, retains replies and closes only its confirmation', async ({ page, state }) => {
    const comment = (id, content, parentCommentId = null) => ({ id, postId, authorUserId: viewerId, parentCommentId, content, createdAtUtc: '2026-10-01T08:00:00Z', updatedAtUtc: null, reactionCounts: {}, viewerReaction: null, author: { userId: viewerId, displayName: 'Người kiểm tra', username: 'explorer', avatarUrl: null } })
    state.comments.push(comment('parent', 'Bình luận cần xóa'), comment('reply', 'Trả lời cần giữ', 'parent'))
    state.post.commentCount = 2
    await page.goto(`${baseUrl}/posts/${postId}`)
    await page.getByRole('button', { name: 'Bình luận', exact: true }).first().click()
    const discussion = page.getByRole('dialog', { name: 'Bài viết của Người kiểm tra', exact: true })
    await discussion.locator('[data-comment-id="parent"]').getByRole('button', { name: 'Xóa', exact: true }).click()
    const dialog = page.getByRole('dialog', { name: 'Xóa bình luận?', exact: true })
    const button = dialog.getByRole('button', { name: 'Xóa', exact: true })
    state.failDelete = true
    await button.click()
    await page.keyboard.press('Escape')
    assert.equal(await dialog.count(), 1)
    assert.equal(await discussion.count(), 1)
    await until(async () => await button.isEnabled())
    assert.equal(await discussion.locator('[data-comment-id]').count(), 2)
    state.failDelete = false
    await button.evaluate(element => { element.click(); element.click() })
    await until(async () => await dialog.count() === 0)
    assert.equal(state.deleteWrites.length, 2)
    assert.equal(await discussion.locator('[data-comment-id="parent"]').count(), 0)
    assert.equal(await discussion.locator('[data-comment-id="reply"]').innerText().then(text => text.includes('Trả lời cần giữ')), true)
    assert.equal(await discussion.count(), 1)
    await page.keyboard.press('Escape')
    assert.equal(await page.evaluate(() => document.body.style.overflow), '')
  }],
  ['unmounting during deletion cancels animations without stale navigation', async ({ page, state }) => {
    state.deleteDelay = 1200
    const dialog = await openPostDelete(page)
    await dialog.getByRole('button', { name: 'Xóa', exact: true }).click()
    await until(() => state.deleteWrites.length === 1)
    await page.locator('a[href="/feed"]').first().evaluate(element => element.click())
    await until(async () => await dialog.count() === 0)
    const destination = page.url()
    await until(() => state.deleteResponses === 1)
    assert.equal(page.url(), destination)
    assert.equal(await page.evaluate(() => document.body.style.overflow), '')
  }],
]

try {
  for (const [name, run] of tests) {
    const setupResult = await setup()
    try { await run(setupResult); console.log(`PASS ${name}`) }
    finally { await setupResult.context.close() }
  }
  for (const theme of ['dark', 'light']) {
    const { context, page, state } = await setup({ viewport: { width: 360, height: 740 }, reducedMotion: 'reduce' })
    try {
      await context.addInitScript(theme => localStorage.setItem('fookbase.preferences', JSON.stringify({ language: 'vi', theme })), theme)
      const dialog = await openPostDelete(page)
      const button = dialog.getByRole('button', { name: 'Xóa', exact: true })
      const bounds = await dialog.boundingBox()
      assert.ok(bounds.x >= 0 && bounds.x + bounds.width <= 360)
      assert.equal(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth), true)
      assert.equal(await button.evaluate(element => getComputedStyle(element).backgroundColor), 'rgb(228, 30, 63)')
      assert.equal(await button.evaluate(element => getComputedStyle(element).borderRadius), '8px')
      await dialog.screenshot({ path: `/tmp/fookbase-delete-${theme}.png` })
      state.failDelete = true
      await button.click()
      await until(async () => await button.getAttribute('data-state') === 'pending')
      assert.equal(await button.evaluate(element => element.getAnimations({ subtree: true }).length), 0)
      await until(async () => await button.isEnabled())
      assert.equal(state.deleted, false)
      state.failDelete = false
      await button.focus()
      await button.press('Enter')
      await until(async () => await dialog.count() === 0)
      assert.equal(state.deleted, true)
      assert.equal(state.deleteWrites.length, 2)
      console.log(`PASS mobile ${theme} theme, keyboard and reduced motion`)
    } finally { await context.close() }
  }
  assert.deepEqual(errors, [])
} finally { await browser.close() }
