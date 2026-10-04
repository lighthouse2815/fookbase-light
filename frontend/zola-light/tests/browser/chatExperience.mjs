import assert from 'node:assert/strict'
import { fixtures } from '../../../web/tests/browser/lastSignalFixture.mjs'

const { chromium } = await import(process.env.PLAYWRIGHT_MODULE ?? 'playwright')
const browser = await chromium.launch({ headless: true, args: ['--no-sandbox'] })
const baseUrl = process.env.ZOLA_BASE_URL ?? 'http://127.0.0.1:5195'
const userId = '00000000-0000-0000-0000-000000000007'
const peerId = '00000000-0000-0000-0000-000000000008'
const timestamp = (index) => new Date(Date.UTC(2026, 9, 1, 8, 0, index)).toISOString()
function message(conversationId, index, overrides = {}) {
  return { id: `${conversationId}-${String(index).padStart(3, '0')}`, conversationId, senderUserId: peerId, content: `${conversationId} tin nhắn ${index}`, createdAtUtc: timestamp(index), readAtUtc: null, type: 'text', replyToMessageId: null, replyTo: null, editedAtUtc: null, deletedAtUtc: null, attachments: [], reactions: [], story: null, ...overrides }
}
async function setup({ delayA = false, hidden = false, failConnection = false } = {}) {
  const context = await browser.newContext({ viewport: { width: 1440, height: 820 } })
  await fixtures(context, false)
  await context.addInitScript(({ userId, hidden }) => {
    localStorage.setItem('fookbase.zola-light.session', JSON.stringify({ user: { id: userId, username: 'tester', email: '', roles: [], emailConfirmed: true }, accessToken: 'browser-fixture', accessTokenExpiresAt: new Date(Date.now() + 7200000).toISOString(), refreshTokenExpiresAt: new Date(Date.now() + 86400000).toISOString() }))
    window.__chatHidden = hidden
    Object.defineProperty(document, 'visibilityState', { configurable: true, get: () => window.__chatHidden ? 'hidden' : 'visible' })
  }, { userId, hidden })
  const state = { failGet: new Set(), failSend: false, failRead: false, failMedia: true, reads: [], sends: [], gets: [], releaseA: null, events: [], failConnection, negotiations: 0, sendDelay: 120 }
  // Reuse the project's long-polling handshake fixture and inject real hub frames.
  await context.route('**/hubs/messages**', (route) => {
    const request = route.request()
    const url = new URL(request.url())
    if (url.pathname.endsWith('/negotiate')) {
      state.negotiations += 1
      if (state.failConnection) return route.fulfill({ status: 503, body: 'Disconnected' })
    }
    if (request.method() === 'GET' && url.searchParams.has('id') && state.events.length) return route.fulfill({ status: 200, contentType: 'text/plain', body: `${JSON.stringify(state.events.shift())}\x1e` })
    return route.fallback()
  })
  const conversations = ['a', 'b'].map((id) => ({ id, participantUserId: null, createdAtUtc: timestamp(0), lastMessageAtUtc: timestamp(59), lastMessage: message(id, 59), unreadCount: 30, type: 'group', title: id === 'a' ? 'Alpha Chat' : 'Beta Chat', photoMediaId: null, participants: [{ userId, role: 'owner', joinedAtUtc: timestamp(0), leftAtUtc: null, lastReadMessageId: null, lastReadAtUtc: null, lastDeliveredMessageId: null, nickname: null }, { userId: peerId, role: 'member', joinedAtUtc: timestamp(0), leftAtUtc: null, lastReadMessageId: null, lastReadAtUtc: null, lastDeliveredMessageId: null, nickname: null }], isMuted: false, isArchived: false }))
  await context.route('**/api/**', async (route) => {
    const request = route.request(), url = new URL(request.url()), path = url.pathname
    if (path === '/api/messages/conversations') return route.fulfill({ json: { items: url.searchParams.has('includeArchived') ? [] : conversations, nextCursor: null, limit: 30 } })
    const history = path.match(/^\/api\/messages\/conversations\/(a|b)\/messages$/)
    if (history) {
      const id = history[1]
      if (request.method() === 'POST') {
        const body = request.postDataJSON(); state.sends.push({ id, body })
        await new Promise(resolve => setTimeout(resolve, state.sendDelay))
        return state.failSend ? route.fulfill({ status: 503, json: { error: { message: 'Máy chủ đang bận, hãy thử lại.' } } }) : route.fulfill({ json: message(id, 100 + state.sends.length, { senderUserId: userId, content: body.content }) })
      }
      const before = url.searchParams.get('before')
      state.gets.push({ id, before })
      if (delayA && id === 'a' && !state.releaseA) await new Promise(resolve => { state.releaseA = resolve })
      if (state.failGet.has(id)) return route.fulfill({ status: 503, json: { error: { message: 'Không thể tải lịch sử lúc này.' } } })
      return route.fulfill({ json: { items: Array.from({ length: 30 }, (_, index) => message(id, index + (before ? 0 : 30))), nextCursor: before ? null : 'older', hasMore: !before } })
    }
    const read = path.match(/^\/api\/messages\/conversations\/(a|b)\/read$/)
    if (read) {
      state.reads.push({ id: read[1], body: request.postDataJSON() })
      return state.failRead ? route.fulfill({ status: 503, json: { title: 'Đọc thất bại' } }) : route.fulfill({ status: 204 })
    }
    if (path.includes('/media/') && path.endsWith('/read-url')) return state.failMedia ? route.fulfill({ status: 503, json: { title: 'Media unavailable' } }) : route.fulfill({ json: { url: '/test-chat-image.svg' } })
    if (path.startsWith('/api/users/')) return route.fulfill({ json: { userId: path === '/api/users/me' ? userId : peerId, username: 'tester', displayName: path === '/api/users/me' ? 'Chat Tester' : 'Chat Peer', avatarUrl: null } })
    return route.fallback()
  })
  await context.route('**/test-chat-image.svg', route => route.fulfill({ contentType: 'image/svg+xml', body: '<svg xmlns="http://www.w3.org/2000/svg" width="100" height="100"><rect width="100" height="100" fill="blue"/></svg>' }))
  const page = await context.newPage(), errors = []
  page.on('pageerror', error => errors.push(error.message))
  const choose = (name) => page.locator('.conversation-pane').getByRole('button', { name: new RegExp(name) }).click()
  const emit = (target, value) => state.events.push({ type: 1, target, arguments: [value] })
  await page.goto(baseUrl)
  await page.locator('.chat-header').getByText('Alpha Chat', { exact: true }).waitFor()
  return { context, page, state, errors, choose, emit }
}
try {
  {
    const { context, page, state, errors, choose } = await setup({ delayA: true })
    await page.waitForFunction(() => document.querySelector('.message-list [role="status"]'))
    await choose('Beta Chat')
    await page.locator('.message-list').getByText('b tin nhắn 59', { exact: true }).waitFor()
    state.releaseA()
    await page.waitForTimeout(250)
    assert.equal(await page.locator('.message-list').getByText('a tin nhắn 59', { exact: true }).count(), 0)
    console.log('PASS late history for A cannot replace the active B conversation')
    await page.getByRole('textbox', { name: 'Nhập tin nhắn' }).fill('Bản nháp B')
    await choose('Alpha Chat')
    await page.getByRole('textbox', { name: 'Nhập tin nhắn' }).fill('Bản nháp A')
    await choose('Beta Chat')
    assert.equal(await page.getByRole('textbox', { name: 'Nhập tin nhắn' }).inputValue(), 'Bản nháp B')
    state.failSend = true
    await page.getByRole('textbox', { name: 'Nhập tin nhắn' }).evaluate(node => {
      node.dispatchEvent(new KeyboardEvent('keydown', { key: 'Enter', bubbles: true }))
      node.dispatchEvent(new KeyboardEvent('keydown', { key: 'Enter', bubbles: true }))
    })
    await page.getByRole('alert').getByText('Máy chủ đang bận, hãy thử lại.', { exact: true }).waitFor()
    assert.equal(await page.getByRole('textbox', { name: 'Nhập tin nhắn' }).inputValue(), 'Bản nháp B')
    assert.equal(state.sends.length, 1)
    await choose('Alpha Chat')
    assert.equal(await page.getByRole('textbox', { name: 'Nhập tin nhắn' }).inputValue(), 'Bản nháp A')
    console.log('PASS conversation drafts survive switching and a failed send')
    state.failSend = false
    state.sendDelay = 500
    await page.getByRole('button', { name: 'Gửi tin nhắn', exact: true }).click()
    await choose('Beta Chat')
    assert.equal(await page.getByRole('textbox', { name: 'Nhập tin nhắn' }).inputValue(), 'Bản nháp B')
    await choose('Alpha Chat')
    assert.equal(await page.getByRole('textbox', { name: 'Nhập tin nhắn' }).isDisabled(), true)
    await page.waitForFunction(() => !document.querySelector('.composer textarea').disabled)
    assert.equal(await page.getByRole('textbox', { name: 'Nhập tin nhắn' }).inputValue(), '')
    assert.equal(state.sends.length, 2)
    console.log('PASS switching back while sending keeps the composer locked until completion')
    assert.deepEqual(errors, [])
    await context.close()
  }
  {
    const { context, page, state, errors, choose, emit } = await setup({ hidden: true })
    await page.locator('.message-list').getByText('a tin nhắn 59', { exact: true }).waitFor()
    await page.waitForTimeout(150)
    assert.equal(state.reads.length, 0)
    state.failRead = true
    await page.evaluate(() => { window.__chatHidden = false; document.dispatchEvent(new Event('visibilitychange')) })
    await page.getByRole('alert').filter({ hasText: 'Chưa thể cập nhật trạng thái đã đọc.' }).waitFor()
    state.failRead = false
    await page.getByRole('alert').filter({ hasText: 'Chưa thể cập nhật trạng thái đã đọc.' }).getByRole('button', { name: 'Thử lại' }).click()
    await page.waitForFunction(() => !document.querySelector('.chat-pane > .alert'))
    assert.equal(state.reads.at(-1).body.lastReadMessageId, 'a-059')
    console.log('PASS hidden tabs keep unread messages and a failed visible read can retry')
    const viewport = page.locator('.message-list')
    await viewport.evaluate(node => { node.scrollTop = 0 })
    await page.waitForTimeout(100)
    const before = await viewport.evaluate(node => {
      const bounds = node.getBoundingClientRect()
      const row = [...node.querySelectorAll('[data-message-id]')].find(row => row.getBoundingClientRect().top >= bounds.top + 10)
      return { id: row.dataset.messageId, top: row.getBoundingClientRect().top }
    })
    await page.getByRole('button', { name: 'Tải tin cũ hơn', exact: true }).click()
    await page.getByText('a tin nhắn 0', { exact: true }).waitFor({ state: 'attached' })
    const after = await page.locator(`[data-message-id="${before.id}"]`).boundingBox()
    assert.ok(Math.abs(after.y - before.top) < 4, `history moved the reading position: ${before.top} -> ${after.y}`)
    assert.equal(await page.locator('[data-message-id]').count(), 60)
    console.log('PASS older history preserves the current reading position')
    const readingScroll = await viewport.evaluate(node => node.scrollTop)
    emit('MessageCreated', message('a', 70))
    await page.getByRole('button', { name: 'Có tin nhắn mới ↓', exact: true }).waitFor()
    assert.equal(await viewport.evaluate(node => node.scrollTop), readingScroll)
    assert.notEqual(state.reads.at(-1).body.lastReadMessageId, 'a-070')
    await page.getByRole('button', { name: 'Có tin nhắn mới ↓', exact: true }).click()
    await page.waitForTimeout(200)
    assert.equal(state.reads.at(-1).body.lastReadMessageId, 'a-070')
    console.log('PASS incoming messages preserve scroll and become read when opened')
    const toggle = page.locator('[data-message-id="a-070"]').getByRole('button', { name: 'Tùy chọn tin nhắn' })
    await toggle.focus(); await page.keyboard.press('Enter')
    assert.equal(await toggle.getAttribute('aria-expanded'), 'true')
    await page.keyboard.press('Tab')
    assert.equal(await page.evaluate(() => document.activeElement.getAttribute('aria-label')), 'Trả lời tin nhắn')
    await page.keyboard.press('Escape')
    assert.equal(await toggle.getAttribute('aria-expanded'), 'false')
    assert.equal(await toggle.evaluate(node => node === document.activeElement), true)
    console.log('PASS message actions open with keyboard and Escape restores focus')
    emit('MessageCreated', message('a', 71, { type: 'media', attachments: [{ mediaId: 'media-chat', sortOrder: 0 }] }))
    await page.getByRole('alert').filter({ hasText: 'Không thể tải tệp đính kèm.' }).waitFor()
    state.failMedia = false
    await page.getByRole('alert').filter({ hasText: 'Không thể tải tệp đính kèm.' }).getByRole('button', { name: 'Thử lại' }).click()
    await page.getByRole('img', { name: 'Tệp đính kèm' }).waitFor()
    console.log('PASS a failed attachment offers recovery instead of endless loading')
    state.failGet.add('b')
    await choose('Beta Chat')
    await page.getByRole('alert').filter({ hasText: 'Không thể tải lịch sử lúc này.' }).waitFor()
    state.failGet.delete('b')
    await page.getByRole('alert').filter({ hasText: 'Không thể tải lịch sử lúc này.' }).getByRole('button', { name: 'Thử lại' }).click()
    await page.locator('.message-list').getByText('b tin nhắn 59', { exact: true }).waitFor()
    console.log('PASS failed history requests show an actionable retry')
    assert.deepEqual(errors, [])
    await context.close()
  }
  {
    const { context, page, state, errors, choose } = await setup({ failConnection: true })
    await page.locator('.connection-notice').getByText('Chưa kết nối được tin nhắn trực tiếp.').waitFor()
    const before = state.gets.length
    state.failConnection = false
    await page.locator('.connection-notice').getByRole('button', { name: 'Thử lại' }).click()
    await page.locator('.connection-notice').waitFor({ state: 'hidden' })
    await page.waitForTimeout(200)
    assert.ok(state.gets.length > before, 'reconnecting should refresh the active history')
    const negotiations = state.negotiations
    await choose('Beta Chat')
    await page.locator('.message-list').getByText('b tin nhắn 59', { exact: true }).waitFor()
    await choose('Alpha Chat')
    await page.locator('.message-list').getByText('a tin nhắn 59', { exact: true }).waitFor()
    assert.equal(state.negotiations, negotiations)
    console.log('PASS reconnect retry refreshes history and conversation switching keeps the hub stable')
    assert.deepEqual(errors, [])
    await context.close()
  }
} finally { await browser.close() }
