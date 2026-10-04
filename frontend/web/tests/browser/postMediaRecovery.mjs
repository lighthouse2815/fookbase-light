import assert from 'node:assert/strict'
import { postPhotoFixtures } from './postPhotoLightboxFixture.mjs'
const { chromium } = await import(process.env.PLAYWRIGHT_MODULE ?? 'playwright')
const browser = await chromium.launch({ headless: true, args: ['--no-sandbox'] })
const baseUrl = process.env.FEED_BASE_URL ?? 'http://127.0.0.1:5194'
try {
  const context = await browser.newContext()
  const fixture = await postPhotoFixtures(context, baseUrl, { count: 3 })
  fixture.failAccess.add(fixture.ids[1])
  const page = await context.newPage()
  const errors = []
  page.on('pageerror', error => errors.push(error.message))
  await page.goto(`${baseUrl}/feed`)
  const card = page.locator('article').first()
  await card.locator(`[data-media-error="${fixture.ids[1]}"]`).waitFor()
  assert.equal(await card.getByRole('button', { name: 'Xem ảnh', exact: true }).count(), 2)
  console.log('PASS an access failure leaves other attachments visible')
  fixture.failAccess.delete(fixture.ids[1])
  await card.locator(`[data-media-error="${fixture.ids[1]}"]`).getByRole('button', { name: 'Thử lại', exact: true }).click()
  await page.waitForFunction(() => document.querySelector('article').querySelectorAll('button[aria-label="Xem ảnh"]').length === 3)
  assert.equal(await card.locator('[data-media-error]').count(), 0)
  console.log('PASS retry restores only the failed attachment')
  await card.getByRole('button', { name: 'Xem ảnh', exact: true }).first().locator('img').evaluate(image => image.dispatchEvent(new Event('error')))
  await card.locator(`[data-media-error="${fixture.ids[0]}"]`).waitFor()
  await card.locator(`[data-media-error="${fixture.ids[0]}"]`).getByRole('button', { name: 'Thử lại', exact: true }).click()
  await page.waitForFunction(() => document.querySelector('article').querySelectorAll('button[aria-label="Xem ảnh"]').length === 3)
  assert.deepEqual(errors, [])
  console.log('PASS broken image delivery offers a fresh access URL and retry')
  await context.close()
} finally { await browser.close() }
