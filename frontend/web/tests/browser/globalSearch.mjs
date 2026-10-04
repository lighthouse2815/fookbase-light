import assert from 'node:assert/strict'
import { searchFixtures, searchPostIds } from './globalSearchFixture.mjs'

const { chromium } = await import(process.env.PLAYWRIGHT_MODULE ?? 'playwright')
const browser = await chromium.launch({ headless: true, executablePath: process.env.CHROMIUM_EXECUTABLE, args: ['--no-sandbox'] })
const baseUrl = process.env.SEARCH_BASE_URL ?? 'http://127.0.0.1:5186'
const phase = process.env.SEARCH_CHECK_PHASE ?? 'links'
const errors = []
const passed = []
const check = async (name, run) => { await run(); passed.push(name); console.log(`PASS ${name}`) }
async function create(contextOptions = {}) {
  const context = await browser.newContext({ viewport: { width: 1366, height: 900 }, ...contextOptions })
  const state = await searchFixtures(context)
  const page = await context.newPage()
  page.on('pageerror', (error) => errors.push(error.stack))
  page.on('console', (message) => {
    if (!['error', 'warning'].includes(message.type())) return
    if (/connection was stopped during negotiation|HttpConnection before stop\(\)/.test(message.text())) return
    if (/503/.test(message.text()) && message.location().url.includes('/api/search')) return
    errors.push(message.text())
  })
  return { context, page, state }
}

try {
  const main = await create()
  await main.page.goto(`${baseUrl}/search?q=dang&type=posts`)
  for (const [index, container] of ['profile', 'group', 'page', 'event'].entries()) {
    await check(`search ${container} Post opens its exact Post detail instead of a container`, async () => {
      const card = main.page.getByRole('link').filter({ hasText: `Post từ ${container}:` })
      await card.waitFor()
      assert.equal(await card.getAttribute('href'), `/posts/${searchPostIds[index]}`)
      await card.click()
      await main.page.waitForURL(`${baseUrl}/posts/${searchPostIds[index]}`)
      await main.page.locator('article').filter({ hasText: `Post từ ${container}:` }).waitFor()
      await main.page.goBack()
    })
  }
  await main.context.close()
  assert.deepEqual(errors, [])
  console.log(`${passed.length} global search ${phase} checks passed; no unexpected console/page errors.`)
} finally {
  await browser.close()
}
