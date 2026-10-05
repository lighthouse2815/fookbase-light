import assert from 'node:assert/strict'
import { fixtures } from './lastSignalFixture.mjs'
import { postFixtures, viewerId } from './postInteractionsFixture.mjs'

const { chromium } = await import(process.env.PLAYWRIGHT_MODULE ?? 'playwright')
const browser = await chromium.launch({ headless: true, executablePath: process.env.CHROMIUM_EXECUTABLE, args: ['--no-sandbox'] })
const baseUrl = process.env.FEED_BASE_URL ?? 'http://127.0.0.1:5194'

try {
  const context = await browser.newContext({ viewport: { width: 1280, height: 900 } })
  await postFixtures(context)
  let failChunk = true
  let chunkRequests = 0
  await context.route('**/src/pages/games/GamesPage.tsx*', (route) => {
    chunkRequests++
    return failChunk ? route.abort('failed') : route.continue()
  })
  const page = await context.newPage()
  page.setDefaultTimeout(10000)
  await page.goto(`${baseUrl}/feed`)
  await page.getByRole('button', { name: /Bạn đang nghĩ gì/ }).click()
  await page.getByRole('textbox', { name: 'Nội dung bài viết', exact: true }).fill('Bản nháp còn lại sau khi tải trang lỗi')
  await page.getByRole('link', { name: 'Chơi game', exact: true }).first().click()
  const recovery = page.getByRole('alert').filter({ hasText: 'Không thể mở trang này' })
  await recovery.waitFor()
  assert.equal(await page.getByRole('navigation', { name: 'Điều hướng chính' }).count(), 1)
  assert.equal(await page.locator(':focus').textContent(), 'Không thể mở trang này')
  assert.doesNotMatch(await page.locator('body').innerText(), /Unexpected Application Error|Failed to fetch dynamically imported module/)
  assert.equal(chunkRequests, 1)
  const draftKey = `fookbase.post-draft.${encodeURIComponent(viewerId)}.${encodeURIComponent('/feed')}`
  const draft = await page.evaluate((key) => sessionStorage.getItem(key), draftKey)
  assert.match(draft, /Bản nháp còn lại/)
  console.log('PASS failed child chunk shows a focused localized recovery screen and keeps navigation and drafts')

  await page.evaluate(() => history.replaceState(null, '', '/games?source=recovery#playroom'))
  failChunk = false
  await recovery.getByRole('button', { name: 'Thử lại', exact: true }).click()
  await page.locator('#games-page-title').waitFor()
  assert.equal(page.url(), `${baseUrl}/games?source=recovery#playroom`)
  assert.equal(chunkRequests, 2)
  assert.equal(await page.evaluate((key) => sessionStorage.getItem(key), draftKey), draft)
  console.log('PASS retry reloads the rejected lazy module at the same URL and preserves stored drafts')
  await context.close()

  const renderContext = await browser.newContext()
  await postFixtures(renderContext)
  await renderContext.route('**/src/pages/games/GamesPage.tsx*', (route) => route.fulfill({ contentType: 'application/javascript', body: 'export default function GamesPage() { throw new Error("private-fixture-render-error"); }' }))
  const renderPage = await renderContext.newPage()
  renderPage.setDefaultTimeout(10000)
  await renderPage.goto(`${baseUrl}/games`)
  await renderPage.getByRole('alert').filter({ hasText: 'Không thể mở trang này' }).waitFor()
  assert.doesNotMatch(await renderPage.locator('body').innerText(), /private-fixture-render-error|Unexpected Application Error/)
  assert.equal(await renderPage.getByRole('navigation', { name: 'Điều hướng chính' }).count(), 1)
  await renderPage.getByRole('link', { name: 'Về bảng tin', exact: true }).click()
  await renderPage.waitForURL('**/feed')
  await renderPage.getByRole('button', { name: /Bạn đang nghĩ gì/ }).waitFor()
  console.log('PASS render failures hide internal errors and provide a working way back to the feed')
  await renderContext.close()

  const loginContext = await browser.newContext()
  await fixtures(loginContext, false)
  await loginContext.addInitScript(() => localStorage.setItem('fookbase.preferences', JSON.stringify({ language: 'en', theme: 'light' })))
  let failLogin = true
  await loginContext.route('**/src/pages/auth/LoginPage.tsx*', (route) => failLogin ? route.abort('failed') : route.continue())
  const loginPage = await loginContext.newPage()
  loginPage.setDefaultTimeout(10000)
  await loginPage.goto(`${baseUrl}/login`)
  const loginRecovery = loginPage.getByRole('alert').filter({ hasText: 'Unable to open this page' })
  await loginRecovery.waitFor()
  assert.equal(await loginRecovery.getByRole('link', { name: 'Back to sign in', exact: true }).getAttribute('href'), '/login')
  failLogin = false
  await loginRecovery.getByRole('button', { name: 'Retry', exact: true }).click()
  await loginPage.getByRole('button', { name: 'Sign in', exact: true }).waitFor()
  assert.equal(await loginPage.locator('html').getAttribute('data-theme'), 'light')
  console.log('PASS the sign-in route has localized recovery and retry restores the actual page')
  await loginContext.close()
} finally {
  await browser.close()
}
