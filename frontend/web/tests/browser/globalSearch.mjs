import assert from 'node:assert/strict'
import { searchFixtures, searchPostIds, personId, groupId, pageId, person, group, searchPage, eventId, event, emptySearch } from './globalSearchFixture.mjs'

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
  if (['results', 'full'].includes(phase)) {
    await main.page.goto(`${baseUrl}/search?q=dang`)
    await check('Events tab uses the existing backend type and deep-links to the exact event without detail requests', async () => {
      await main.page.getByRole('heading', { name: 'Tìm kiếm', exact: true }).waitFor()
      assert.equal(await main.page.getByRole('button', { name: 'Sự kiện', exact: true }).count(), 1)
      await main.page.getByRole('button', { name: 'Sự kiện', exact: true }).click()
      await main.page.waitForURL(`${baseUrl}/search?q=dang&type=events`)
      await until(() => main.state.requests.some((request) => request.type === 'events' && request.limit === '20'))
      const card = main.page.locator('main a').filter({ hasText: event.name })
      await card.waitFor()
      assert.equal(await card.getAttribute('href'), `/events/${eventId}`)
      assert.equal(await card.locator('[data-search-highlight]').filter({ hasText: 'Đăng' }).count(), 1)
      assert.equal(await card.getByText('Hà Nội', { exact: false }).count() > 0, true)
      assert.ok(main.state.requests.some((request) => request.type === 'events' && request.limit === '20'))
    })
    await check('event cursor pagination keeps existing rows, deduplicates IDs and retries only the failed page', async () => {
      const second = { ...event, eventId: '00000000-0000-0000-0000-000000000551', name: 'Sự kiện tiếp theo' }
      main.state.results.set('paged:events:', { ...emptySearch(), events: [event], nextCursor: 'event-page-2' })
      main.state.results.set('paged:events:event-page-2', { ...emptySearch(), events: [event, second, second] })
      main.state.failures.add('results:paged:events:event-page-2')
      await main.page.goto(`${baseUrl}/search?q=paged&type=events`)
      await main.page.getByRole('button', { name: 'Xem thêm', exact: true }).click()
      await main.page.getByRole('button', { name: 'Thử lại tải thêm', exact: true }).waitFor()
      assert.equal(await main.page.locator('main a').filter({ hasText: event.name }).count(), 1)
      main.state.failures.delete('results:paged:events:event-page-2')
      await main.page.getByRole('button', { name: 'Thử lại tải thêm', exact: true }).click()
      await main.page.locator('main a').filter({ hasText: second.name }).waitFor()
      assert.equal(await main.page.locator('main a').filter({ hasText: event.name }).count(), 1)
      assert.equal(await main.page.locator('main a').filter({ hasText: second.name }).count(), 1)
      assert.equal(main.state.requests.filter((request) => request.query === 'paged' && request.cursor === '').length, 1)
    })
    await check('result skeleton, query-specific empty/error and retry preserve URL and do not flash old results', async () => {
      main.state.results.set('none:posts:', emptySearch())
      main.state.delays.set('results:none:posts:', 700)
      await main.page.goto(`${baseUrl}/search?q=none&type=posts`)
      await main.page.getByRole('status', { name: 'Đang tải kết quả tìm kiếm' }).waitFor()
      assert.equal(await main.page.getByText('Không tìm thấy bài viết cho “none”', { exact: true }).count(), 0)
      await main.page.getByText('Không tìm thấy bài viết cho “none”', { exact: true }).waitFor()
      main.state.failures.add('results:error:posts:')
      await main.page.goto(`${baseUrl}/search?q=error&type=posts`)
      await main.page.getByText('Không thể tải kết quả tìm kiếm', { exact: true }).waitFor()
      main.state.failures.delete('results:error:posts:')
      await main.page.locator('main').getByRole('button', { name: 'Thử lại', exact: true }).click()
      await main.page.locator('main a').filter({ hasText: 'Post từ profile:' }).waitFor()
      assert.equal(new URL(main.page.url()).searchParams.get('type'), 'posts')
    })
    await check('tab changes reset cursor, preserve query and browser Back/Forward restore the query/category', async () => {
      await main.page.getByRole('button', { name: 'Mọi người', exact: true }).click()
      await main.page.locator('main a').filter({ hasText: person.displayName }).waitFor()
      assert.equal(new URL(main.page.url()).searchParams.get('q'), 'error')
      await main.page.getByRole('button', { name: 'Nhóm', exact: true }).click()
      await main.page.locator('main a').filter({ hasText: group.name }).waitFor()
      await main.page.goBack()
      await main.page.locator('main a').filter({ hasText: person.displayName }).waitFor()
      assert.equal(new URL(main.page.url()).searchParams.get('type'), 'people')
      await main.page.goForward()
      await main.page.locator('main a').filter({ hasText: group.name }).waitFor()
      assert.equal(new URL(main.page.url()).searchParams.get('type'), 'groups')
      assert.ok(main.state.requests.filter((request) => request.query === 'error' && ['people', 'groups'].includes(request.type)).every((request) => request.cursor === ''))
    })
  }
  if (['history', 'full'].includes(phase)) {
    await check('recent searches are recorded only after an explicit search and can be searched again', async () => {
      await input(main.page).fill('typed-only')
      assert.equal(await main.page.evaluate(() => (localStorage.getItem('fookbase.search.recent.00000000-0000-0000-0000-000000000007') ?? '').includes('typed-only')), false)
      await input(main.page).fill('fookbase')
      await input(main.page).press('Enter')
      await main.page.waitForURL(`${baseUrl}/search?q=fookbase`)
      await input(main.page).focus()
      await main.page.getByRole('button', { name: 'Xóa nội dung tìm kiếm', exact: true }).click()
      const recent = main.page.getByRole('button', { name: 'Tìm lại “fookbase”', exact: true })
      assert.equal(await recent.count(), 1)
      await recent.click()
      await main.page.waitForURL(`${baseUrl}/search?q=fookbase`)
    })
    await check('recent history deduplicates casing and deletion/clear keep focus without navigating', async () => {
      await input(main.page).fill(' FookBASE ')
      await input(main.page).press('Enter')
      await main.page.waitForURL(`${baseUrl}/search?q=FookBASE`)
      await main.page.getByRole('button', { name: 'Xóa nội dung tìm kiếm', exact: true }).click()
      const saved = await main.page.evaluate(() => JSON.parse(localStorage.getItem('fookbase.search.recent.00000000-0000-0000-0000-000000000007')))
      assert.equal(saved.filter((query) => query.toLowerCase() === 'fookbase').length, 1)
      assert.equal(saved[0], 'FookBASE')
      const before = main.page.url()
      await main.page.getByRole('button', { name: 'Xóa tìm kiếm “FookBASE”', exact: true }).click()
      assert.equal(main.page.url(), before)
      assert.equal(await input(main.page).evaluate((element) => document.activeElement === element), true)
      assert.equal(await main.page.getByRole('button', { name: 'Tìm lại “FookBASE”', exact: true }).count(), 0)
      if (await main.page.getByRole('button', { name: 'Xóa tất cả', exact: true }).count()) await main.page.getByRole('button', { name: 'Xóa tất cả', exact: true }).click()
      await main.page.getByText('Chưa có tìm kiếm gần đây.', { exact: true }).waitFor()
    })
    await check('broken or unavailable history storage cannot block the search flow', async () => {
      await main.page.evaluate(() => localStorage.setItem('fookbase.search.recent.00000000-0000-0000-0000-000000000007', '{broken'))
      await input(main.page).blur()
      await input(main.page).focus()
      await main.page.getByText('Chưa có tìm kiếm gần đây.', { exact: true }).waitFor()
      await main.page.evaluate(() => {
        const native = Storage.prototype.setItem
        Storage.prototype.setItem = function (key, value) { if (key.startsWith('fookbase.search.recent.')) throw new Error('Quota exceeded'); return native.call(this, key, value) }
      })
      await input(main.page).fill('quota')
      await input(main.page).press('Enter')
      await main.page.waitForURL(`${baseUrl}/search?q=quota`)
      await main.page.locator('main').getByRole('heading', { name: 'Tìm kiếm', exact: true }).waitFor()
    })
  }
  await main.context.close()
  assert.deepEqual(errors, [])
  console.log(`${passed.length} global search ${phase} checks passed; no unexpected console/page errors.`)
} finally {
  await browser.close()
}
