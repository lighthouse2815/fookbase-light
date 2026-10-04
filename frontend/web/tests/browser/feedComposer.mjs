import assert from 'node:assert/strict'
import { postFixtures, viewerId } from './postInteractionsFixture.mjs'
import { trackErrors } from './lastSignalFixture.mjs'

const { chromium } = await import(process.env.PLAYWRIGHT_MODULE ?? 'playwright')
const browser = await chromium.launch({ headless: true, executablePath: process.env.CHROMIUM_EXECUTABLE, args: ['--no-sandbox'] })
const baseUrl = process.env.FEED_BASE_URL ?? 'http://127.0.0.1:5194'
const errors = []
const until = async (check) => {
  for (let i = 0; i < 200; i++) {
    if (await check()) return
    await new Promise((resolve) => setTimeout(resolve, 25))
  }
  assert.fail('Condition did not settle')
}

try {
  const context = await browser.newContext({ viewport: { width: 1280, height: 720 } })
  await postFixtures(context)
  const writes = []
  let failPost = false
  await context.route('**/api/posts', async (route) => {
    if (route.request().method() !== 'POST') return route.fallback()
    writes.push(route.request().postDataJSON())
    await new Promise((resolve) => setTimeout(resolve, 550))
    return route.fulfill({ status: failPost ? 400 : 200, json: failPost ? { detail: 'Không thể đăng bản nháp thử nghiệm.' } : { id: 'created-post' } })
  })
  const page = await context.newPage()
  trackErrors(page, errors)
  page.on('console', message => { if (message.type() === 'error' && /400/.test(message.text())) { const index = errors.indexOf(message.text()); if (index >= 0) errors.splice(index, 1) } })
  await page.goto(`${baseUrl}/feed`)
  await page.getByRole('button', { name: /Bạn đang nghĩ gì/ }).click()
  const content = page.getByRole('textbox', { name: 'Nội dung bài viết', exact: true })
  await content.fill('Bản nháp cần giữ')
  await page.getByRole('button', { name: 'Công khai', exact: false }).click()
  await page.getByRole('menuitemradio', { name: /Chỉ mình tôi/ }).click()
  const background = page.getByRole('button', { name: /^Nền / }).first()
  const backgroundLabel = await background.getAttribute('aria-label')
  await background.click()
  await page.reload()
  await content.waitFor()
  assert.equal(await content.inputValue(), 'Bản nháp cần giữ')
  assert.equal(await page.getByRole('button', { name: /Chỉ mình tôi/ }).getAttribute('aria-expanded'), 'false')
  assert.equal(await page.getByRole('button', { name: backgroundLabel, exact: true }).getAttribute('aria-pressed'), 'true')
  console.log('PASS reload restores text, privacy and background')

  failPost = true
  await content.evaluate((element) => {
    element.dispatchEvent(new KeyboardEvent('keydown', { key: 'Enter', ctrlKey: true, bubbles: true }))
    element.dispatchEvent(new KeyboardEvent('keydown', { key: 'Enter', metaKey: true, bubbles: true }))
  })
  await until(() => writes.length === 1)
  assert.equal(await content.isDisabled(), true)
  assert.equal(await page.getByRole('button', { name: 'Đóng', exact: true }).isDisabled(), true)
  await page.getByRole('alert').filter({ hasText: 'Không thể đăng bản nháp thử nghiệm.' }).waitFor()
  assert.equal(writes.length, 1)
  assert.equal(await content.inputValue(), 'Bản nháp cần giữ')
  assert.equal(await content.isDisabled(), false)
  console.log('PASS repeated submit shortcuts issue one write and failures preserve editable draft')

  await page.getByRole('button', { name: /Chỉ mình tôi/ }).click()
  await page.keyboard.press('Home')
  assert.equal(await page.locator(':focus').getAttribute('aria-checked'), 'false')
  await page.keyboard.press('End')
  assert.equal(await page.locator(':focus').getAttribute('aria-checked'), 'true')
  await page.keyboard.press('Escape')
  assert.equal(await page.locator(':focus').getAttribute('aria-haspopup'), 'menu')
  console.log('PASS privacy menu supports keyboard navigation and focus return')

  await page.locator('input[type=file]').setInputFiles({ name: 'draft.png', mimeType: 'image/png', buffer: Buffer.from('iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO/aR1EAAAAASUVORK5CYII=', 'base64') })
  await page.getByRole('link', { name: 'Chơi game', exact: true }).first().click()
  await page.getByRole('dialog', { name: 'Rời trang soạn bài?' }).waitFor()
  await page.getByRole('button', { name: 'Rời trang', exact: true }).click()
  await page.waitForURL('**/games')
  await page.locator('#games-page-title').waitFor()
  await page.goBack()
  await content.waitFor()
  await page.getByRole('status').filter({ hasText: 'Tệp đính kèm của bản nháp cần được chọn lại' }).waitFor()
  assert.equal(await page.getByRole('button', { name: 'Đăng', exact: true }).isDisabled(), true)
  await page.getByRole('button', { name: 'Bỏ các tệp đính kèm cũ', exact: true }).click()
  assert.equal(await page.getByRole('button', { name: 'Đăng', exact: true }).isDisabled(), false)
  console.log('PASS leaving with attachments warns and returning requires explicit reselection or discard')

  failPost = false
  await page.getByRole('button', { name: 'Đăng', exact: true }).click()
  await until(async () => await content.count() === 0)
  const key = `fookbase.post-draft.${encodeURIComponent(viewerId)}.${encodeURIComponent('/feed')}`
  assert.equal(await page.evaluate((key) => sessionStorage.getItem(key), key), null)
  await page.reload()
  assert.equal(await content.count(), 0)
  assert.equal(writes.length, 2)
  console.log('PASS successful posting clears its draft')

  await context.close()
  assert.deepEqual(errors, [])
} finally {
  await browser.close()
}
