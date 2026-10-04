import assert from 'node:assert/strict'
import { postFixtures } from './postInteractionsFixture.mjs'
const { chromium } = await import(process.env.PLAYWRIGHT_MODULE ?? 'playwright')
const browser = await chromium.launch({ headless: true, args: ['--no-sandbox'] })
const baseUrl = process.env.FEED_BASE_URL ?? 'http://127.0.0.1:5194'
try {
  const context = await browser.newContext({ viewport: { width: 1280, height: 720 } })
  const fixture = await postFixtures(context)
  let reads = 0
  let fail = false
  await context.route('**/api/feed**', async route => {
    const url = new URL(route.request().url())
    if (url.pathname !== '/api/feed' && url.pathname !== '/api/feed/following') return route.fallback()
    reads++
    if (fail) return route.fulfill({ status: 503, json: { detail: 'Lỗi làm mới thử nghiệm' } })
    const following = url.pathname.endsWith('/following')
    const start = url.searchParams.has('cursor') ? 16 : 0
    const count = following ? 2 : start ? 12 : 16
    const items = Array.from({ length: count }, (_, index) => ({ ...fixture.post, id: `feed-${following ? 'following' : 'home'}-${index + start}`, content: `Bài đọc ${index + start}`, author: { userId: fixture.post.authorUserId, username: 'explorer', displayName: 'Người kiểm tra', avatarUrl: null }, contentType: 'standardPost', containerType: 'profile', container: { id: fixture.post.authorUserId, name: 'Trang cá nhân' }, media: [], video: null, isSuggested: false, reactionCount: 0, mediaIds: [] }))
    return route.fulfill({ json: { items, nextCursor: following || start ? null : 'older', asOfUtc: '2026-10-05T00:00:00Z' } })
  })
  const page = await context.newPage()
  const errors = []
  page.on('pageerror', error => errors.push(error.message))
  await page.goto(`${baseUrl}/feed`)
  await page.getByRole('button', { name: 'Tải thêm bài viết', exact: true }).click()
  await page.getByText('Bài đọc 27', { exact: true }).waitFor()
  await page.evaluate(() => window.scrollTo(0, 1600))
  await page.waitForTimeout(100)
  const y = await page.evaluate(() => window.scrollY)
  const feedReads = reads
  // Programmatic DOM activation avoids scrolling the navbar into view before leaving.
  await page.getByRole('link', { name: 'Chơi game', exact: true }).first().evaluate(link => link.click())
  await page.locator('#games-page-title').waitFor()
  await page.goBack()
  await page.getByText('Bài đọc 27', { exact: true }).waitFor()
  await page.waitForTimeout(150)
  assert.equal(reads, feedReads)
  assert.ok(Math.abs(await page.evaluate(() => window.scrollY) - y) < 3)
  console.log('PASS back preserves loaded feed pages and reading position')
  fail = true
  await page.getByRole('button', { name: 'Làm mới', exact: true }).first().click()
  await page.getByRole('alert').filter({ hasText: 'Lỗi làm mới thử nghiệm' }).waitFor()
  assert.equal(await page.getByText('Bài đọc 27', { exact: true }).count(), 1)
  console.log('PASS failed refresh leaves the loaded feed available')
  fail = false
  await page.getByRole('button', { name: 'Đang theo dõi', exact: true }).click()
  await page.getByText('Bài đọc 1', { exact: true }).waitFor()
  await page.getByRole('link', { name: 'Chơi game', exact: true }).first().click()
  await page.locator('#games-page-title').waitFor()
  await page.goBack()
  await page.getByRole('button', { name: 'Đang theo dõi', exact: true }).waitFor()
  assert.equal(await page.getByRole('button', { name: 'Đang theo dõi', exact: true }).getAttribute('aria-pressed'), 'true')
  assert.deepEqual(errors, [])
  console.log('PASS back retains the selected feed mode')
  await context.close()
} finally { await browser.close() }
