import assert from 'node:assert/strict'
import { searchFixtures, searchPostIds, personId, groupId, pageId, person, group, searchPage } from './globalSearchFixture.mjs'

const { chromium } = await import(process.env.PLAYWRIGHT_MODULE ?? 'playwright')
const browser = await chromium.launch({ headless: true, executablePath: process.env.CHROMIUM_EXECUTABLE, args: ['--no-sandbox'] })
const baseUrl = process.env.SEARCH_BASE_URL ?? 'http://127.0.0.1:5186'
const phase = process.env.SEARCH_CHECK_PHASE ?? 'links'
const errors = []
const passed = []
const check = async (name, run) => { await run(); passed.push(name); console.log(`PASS ${name}`) }
const until = async (condition) => {
  for (let attempt = 0; attempt < 250; attempt++) {
    if (await condition()) return
    await new Promise((resolve) => setTimeout(resolve, 30))
  }
  assert.fail('Search state did not settle')
}
const input = (page) => page.getByRole('combobox', { name: 'Tìm kiếm Fookbase', exact: true })
const options = (page) => page.getByRole('listbox').getByRole('option')
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
  if (['autocomplete', 'full'].includes(phase)) {
    await check('autocomplete is an accessible combobox and shows a row skeleton while suggestions load', async () => {
      assert.equal(await input(main.page).count(), 1)
      main.state.delays.set('suggestions', 500)
      await input(main.page).fill('dang')
      await input(main.page).focus()
      await main.page.getByRole('status', { name: 'Đang tải gợi ý' }).waitFor()
      await main.page.getByRole('listbox').getByText('Trần Đăng Khoa', { exact: true }).waitFor()
      assert.equal(await input(main.page).getAttribute('aria-expanded'), 'true')
      assert.equal(await main.page.getByRole('listbox').getByText('Trần Đăng Khoa', { exact: true }).count(), 1)
      assert.equal(await main.page.getByRole('listbox').locator('[data-search-highlight]').filter({ hasText: 'Đăng' }).count() > 0, true)
    })
    await check('ArrowDown/Up select suggestions without moving focus or scrolling the page; Escape preserves text and resets selection', async () => {
      await input(main.page).press('ArrowDown')
      assert.equal(await options(main.page).first().getAttribute('aria-selected'), 'true')
      await input(main.page).press('ArrowDown')
      assert.equal(await options(main.page).nth(1).getAttribute('aria-selected'), 'true')
      await input(main.page).press('ArrowUp')
      assert.equal(await options(main.page).first().getAttribute('aria-selected'), 'true')
      assert.equal(await input(main.page).evaluate((element) => document.activeElement === element), true)
      await input(main.page).press('Escape')
      assert.equal(await input(main.page).inputValue(), 'dang')
      assert.equal(await input(main.page).getAttribute('aria-expanded'), 'false')
      await input(main.page).press('ArrowDown')
      assert.equal(await options(main.page).first().getAttribute('aria-selected'), 'true')
    })
    await check('Enter with an active suggestion opens that entity rather than searching the raw query', async () => {
      await input(main.page).press('Enter')
      await main.page.waitForURL(`${baseUrl}/profile/${personId}`)
      await main.page.goBack()
    })
    await check('changing query resets the active option and a late old response cannot replace new suggestions', async () => {
      main.state.suggestions.set('da', { people: [{ ...person, displayName: 'Old result' }], groups: [], pages: [] })
      main.state.suggestions.set('dang', { people: [person], groups: [group], pages: [searchPage] })
      main.state.delays.set('suggestions:da:all:', 900)
      main.state.delays.set('suggestions:dang:all:', 50)
      await input(main.page).fill('da')
      await until(() => main.state.requests.some((request) => request.kind === 'suggestions' && request.query === 'da'))
      await input(main.page).fill('dang')
      await main.page.getByRole('listbox').getByText('Trần Đăng Khoa', { exact: true }).waitFor()
      assert.equal(await input(main.page).getAttribute('aria-activedescendant'), null)
      await main.page.waitForTimeout(1000)
      assert.equal(await main.page.getByRole('listbox').getByText('Old result').count(), 0)
      assert.equal(await options(main.page).first().getAttribute('href'), `/profile/${personId}`)
      assert.equal(await options(main.page).nth(1).getAttribute('href'), `/groups/${groupId}`)
      assert.equal(await options(main.page).nth(2).getAttribute('href'), `/pages/${searchPage.username}`)
      assert.notEqual(personId, pageId)
    })
    await check('Enter without a selected suggestion searches the query, and clearing keeps input focus', async () => {
      await input(main.page).press('Enter')
      await main.page.waitForURL(`${baseUrl}/search?q=dang`)
      await input(main.page).focus()
      await main.page.getByRole('button', { name: 'Xóa nội dung tìm kiếm', exact: true }).click()
      assert.equal(await input(main.page).inputValue(), '')
      assert.equal(await input(main.page).evaluate((element) => document.activeElement === element), true)
    })
    await check('autocomplete displays a compact empty/error state with request-specific retry', async () => {
      main.state.suggestions.set('none', { people: [], groups: [], pages: [] })
      await input(main.page).fill('none')
      await main.page.getByText('Không tìm thấy gợi ý cho “none”', { exact: true }).waitFor()
      main.state.failures.add('suggestions')
      await input(main.page).fill('error')
      await main.page.getByText('Không thể tải gợi ý', { exact: true }).waitFor()
      main.state.failures.delete('suggestions')
      await main.page.getByRole('button', { name: 'Thử lại gợi ý', exact: true }).click()
      await main.page.getByRole('listbox').getByText('Trần Đăng Khoa', { exact: true }).waitFor()
    })
  }
  await main.context.close()
  assert.deepEqual(errors, [])
  console.log(`${passed.length} global search ${phase} checks passed; no unexpected console/page errors.`)
} finally {
  await browser.close()
}
