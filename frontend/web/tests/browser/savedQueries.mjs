import assert from 'node:assert/strict'
import { basePost, postFixtures } from './postInteractionsFixture.mjs'

const { chromium } = await import(process.env.PLAYWRIGHT_MODULE ?? 'playwright')
const baseUrl = process.env.SAVED_BASE_URL ?? 'http://127.0.0.1:5198'
const browser = await chromium.launch({ headless: true, args: ['--no-sandbox'] })
const context = await browser.newContext()
await postFixtures(context)
const posts = [1, 2, 3].map(index => ({
  ...basePost, id: `00000000-0000-0000-0000-${String(800 + index).padStart(12, '0')}`,
  content: `Saved query post ${index}`, viewerHasSaved: true,
}))
const state = { reads: [], failPage: true, otherUser: false, removed: false }
await context.route('**/api/feed?**', route => route.fulfill({ json: {
  items: [{ ...posts[0], viewerHasSaved: !state.removed,
    author: { userId: basePost.authorUserId, username: 'explorer', displayName: 'Người kiểm tra', avatarUrl: null },
    media: [], container: null, video: null, isSuggested: false, reactionCount: 3 }],
  nextCursor: null,
} }))
await context.route(`**/api/posts/${posts[0].id}/save`, route => {
  state.removed = route.request().method() === 'DELETE'
  return route.fulfill({ status: 204 })
})
await context.route('**/api/posts/saved?**', async route => {
  const cursor = new URL(route.request().url()).searchParams.get('cursor')
  state.reads.push(cursor)
  if (state.otherUser) return route.fulfill({ json: { items: [{ ...posts[0], content: 'Saved other account' }], nextCursor: null } })
  if (cursor && state.failPage) return route.fulfill({ status: 503, json: { detail: 'Saved next page failed' } })
  return route.fulfill({ json: {
    items: cursor ? [posts[1], posts[2], posts[2]] : posts.slice(0, 2),
    nextCursor: cursor ? null : 'saved-page-2',
  } })
})
const page = await context.newPage()
page.setDefaultTimeout(10000)
const errors = []
page.on('pageerror', error => errors.push(error.message))
try {
  await page.goto(`${baseUrl}/saved`)
  await page.getByText(posts[1].content, { exact: true }).waitFor()
  await page.getByRole('button', { name: 'Tải thêm bài viết đã lưu', exact: true }).click()
  await page.getByRole('alert').filter({ hasText: 'Saved next page failed' }).waitFor()
  assert.equal(await page.getByText(posts[0].content, { exact: true }).count(), 1)
  assert.equal(await page.getByText(posts[1].content, { exact: true }).count(), 1)
  state.failPage = false
  await page.getByRole('alert').getByRole('button', { name: 'Thử lại', exact: true }).click()
  await page.getByText(posts[2].content, { exact: true }).waitFor()
  assert.equal(await page.getByText(posts[1].content, { exact: true }).count(), 1)
  assert.equal(await page.getByText(posts[2].content, { exact: true }).count(), 1)
  assert.equal(state.reads.filter(cursor => cursor === null).length, 1)
  assert.equal(state.reads.filter(cursor => cursor === 'saved-page-2').length, 2)
  console.log('PASS saved pagination deduplicates rows and retries only the failed page')

  const reads = state.reads.length
  await page.locator('a[href="/feed"]').first().click()
  await page.getByRole('button', { name: 'Làm mới', exact: true }).waitFor()
  await page.locator('a[href="/saved"]').first().click()
  await page.getByText(posts[2].content, { exact: true }).waitFor()
  assert.equal(state.reads.length, reads)
  console.log('PASS returning to Saved preserves cached pages without another request')

  await page.locator('a[href="/feed"]').first().click()
  const card = page.locator('article').filter({ hasText: posts[0].content })
  await card.getByRole('button', { name: 'Tùy chọn khác', exact: true }).click()
  await page.getByRole('menuitem', { name: 'Bỏ lưu bài viết', exact: true }).click()
  await page.getByText('Đã bỏ lưu bài viết.', { exact: true }).waitFor()
  await page.locator('a[href="/saved"]').first().click()
  await page.getByText(posts[2].content, { exact: true }).waitFor()
  assert.equal(await page.getByText(posts[0].content, { exact: true }).count(), 0)
  assert.equal(state.reads.length, reads)
  console.log('PASS removing a bookmark on Feed updates the cached Saved list')

  state.otherUser = true
  await page.evaluate(() => {
    const session = JSON.parse(localStorage.getItem('fookbase.session'))
    session.user.id = '00000000-0000-0000-0000-000000000099'
    session.accessToken = 'other-account-token'
    localStorage.setItem('fookbase.session', JSON.stringify(session))
    localStorage.setItem('fookbase.accessToken', session.accessToken)
    window.dispatchEvent(new Event('fookbase.auth-session-changed'))
  })
  await page.getByText('Saved other account', { exact: true }).waitFor()
  assert.equal(await page.getByText(posts[0].content, { exact: true }).count(), 0)
  assert.equal(await page.getByText(posts[2].content, { exact: true }).count(), 0)
  assert.ok(state.reads.length > reads)
  console.log('PASS changing account clears Saved data from the previous session')
  assert.deepEqual(errors, [])
} finally {
  await browser.close()
}
