import assert from 'node:assert/strict'
import { mkdir } from 'node:fs/promises'
import { notificationFixtures, viewerId } from './notificationsFixture.mjs'

const { chromium } = await import(process.env.PLAYWRIGHT_MODULE ?? 'playwright')
const browser = await chromium.launch({ headless: true, executablePath: process.env.CHROMIUM_EXECUTABLE, args: ['--no-sandbox'] })
const baseUrl = process.env.FLOATING_CHAT_BASE_URL ?? 'http://127.0.0.1:5194'
const artifacts = process.env.FLOATING_CHAT_ARTIFACTS ?? '/tmp/floating-chat-browser-artifacts'
await mkdir(artifacts, { recursive: true })
const errors = []
const passed = []
const pause = (milliseconds) => new Promise((resolve) => setTimeout(resolve, milliseconds))
const until = async (condition, message = 'Condition did not settle') => {
  for (let attempt = 0; attempt < 250; attempt++) {
    if (await condition()) return
    await pause(30)
  }
  assert.fail(message)
}
const aId = '00000000-0000-0000-0000-000000000801'
const bId = '00000000-0000-0000-0000-000000000802'
const peerId = '00000000-0000-0000-0000-000000000008'
const message = (conversationId, index, senderUserId = viewerId) => ({
  id: `00000000-0000-0000-0000-${String((conversationId === aId ? 810000 : 820000) + index).padStart(12, '0')}`,
  conversationId, senderUserId, content: `Tin ${conversationId === aId ? 'A' : 'B'} ${index}: Nội dung để kiểm tra vị trí đọc tin nhắn.`,
  createdAtUtc: new Date(Date.UTC(2026, 9, 5, 8, index)).toISOString(), readAtUtc: null, deletedAtUtc: null, attachments: [],
})
const conversation = (id, messages) => ({
  id, participantUserId: null, createdAtUtc: '2026-10-05T08:00:00Z', lastMessageAtUtc: messages.at(-1).createdAtUtc,
  lastMessage: messages.at(-1), unreadCount: 0, type: 'group', title: id === aId ? 'Nhóm A' : 'Nhóm B',
})
let latestPage

const create = async ({ hidden = false, peers = false } = {}) => {
  const context = await browser.newContext({ viewport: { width: 1366, height: 900 } })
  await notificationFixtures(context, { items: [] })
  if (hidden) await context.addInitScript(() => {
    window.__chatTestVisibility = 'hidden'
    Object.defineProperty(document, 'visibilityState', { configurable: true, get: () => window.__chatTestVisibility })
    Object.defineProperty(document, 'hidden', { configurable: true, get: () => window.__chatTestVisibility === 'hidden' })
  })
  const allMessages = new Map([
    [aId, Array.from({ length: 40 }, (_, index) => message(aId, index + 1, peers ? peerId : viewerId))],
    [bId, [message(bId, 1)]],
  ])
  const state = {
    allMessages, historyDelay: new Map(), historyReads: [], historyResponses: [], sends: [], sendResponses: 0,
    sendDelay: 0, failSend: false, failRead: false, reads: [], readResponses: 0, unread: [], notificationDelay: 0, notificationReads: 0, notificationResponses: 0, connections: new Map(), negotiations: 0,
  }
  // Exercise SignalR's real negotiation, handshake and polling without application hooks.
  await context.route('**/hubs/messages**', async (route) => {
    const request = route.request()
    const url = new URL(request.url())
    if (url.pathname.endsWith('/negotiate')) {
      const id = `message-${++state.negotiations}`
      state.connections.set(id, { started: false, connected: false, queue: [] })
      return route.fulfill({ json: { negotiateVersion: 1, connectionId: id, connectionToken: id, availableTransports: [{ transport: 'LongPolling', transferFormats: ['Text'] }] } })
    }
    const id = url.searchParams.get('id')
    const connection = state.connections.get(id)
    if (request.method() === 'DELETE') {
      state.connections.delete(id)
      return route.fulfill({ status: 202, body: '' })
    }
    if (!connection) return route.fulfill({ status: 404, body: '' }).catch(() => undefined)
    if (request.method() === 'POST') {
      if (request.postData()?.includes('protocol')) {
        connection.queue.unshift('{}\x1e')
        connection.connected = true
      }
      return route.fulfill({ status: 200, body: '' }).catch(() => undefined)
    }
    if (!connection.started) {
      connection.started = true
      return route.fulfill({ status: 200, body: '' }).catch(() => undefined)
    }
    await pause(70)
    const body = connection.queue.splice(0).join('') || '{"type":6}\x1e'
    return route.fulfill({ status: 200, contentType: 'text/plain', body }).catch(() => undefined)
  })
  await context.route('**/api/messages/**', async (route) => {
    const request = route.request()
    const url = new URL(request.url())
    if (url.pathname === '/api/messages/conversations') return route.fulfill({ json: { items: [...allMessages].map(([id, items]) => conversation(id, items)), offset: 0, limit: 30, total: 2 } })
    if (url.pathname === '/api/messages/notifications') {
      state.notificationReads++
      const items = structuredClone(state.unread)
      await pause(state.notificationDelay)
      state.notificationResponses++
      return route.fulfill({ json: { items, offset: 0, limit: 100, total: items.length } })
    }
    const match = url.pathname.match(/^\/api\/messages\/conversations\/([^/]+)\/(messages|read)$/)
    if (!match) return route.fallback()
    const [, id, action] = match
    if (action === 'read') {
      const body = request.postDataJSON()
      state.reads.push({ conversationId: id, ...body })
      if (state.failRead) return route.fulfill({ status: 503, json: { detail: 'Chưa cập nhật được trạng thái đã đọc.' } })
      const items = allMessages.get(id)
      const index = items.findIndex((item) => item.id === body.lastReadMessageId)
      const included = new Set(items.slice(0, index + 1).map((item) => item.id))
      state.unread = state.unread.filter((item) => item.conversation.id !== id || !included.has(item.message.id))
      state.readResponses++
      return route.fulfill({ status: 204 })
    }
    if (request.method() === 'POST') {
      const body = request.postDataJSON()
      state.sends.push({ conversationId: id, ...body })
      const fail = state.failSend
      await pause(state.sendDelay)
      state.sendResponses++
      if (fail) return route.fulfill({ status: 503, json: { detail: 'Không thể gửi tin nhắn thử nghiệm.' } })
      const sent = { ...message(id, 100 + state.sends.length), content: body.content }
      allMessages.get(id).push(sent)
      return route.fulfill({ json: sent })
    }
    const before = url.searchParams.get('before')
    state.historyReads.push({ id, before, limit: url.searchParams.get('limit') })
    const items = structuredClone(before ? allMessages.get(id).slice(0, 20) : allMessages.get(id).slice(-20))
    const hasMore = !before && allMessages.get(id).length > 20
    await pause(state.historyDelay.get(id) ?? 0)
    state.historyResponses.push({ id, before })
    return route.fulfill({ json: { items: items.reverse(), nextCursor: hasMore ? 'older-page' : null, hasMore } }).catch(() => undefined)
  })
  state.push = async (incoming) => {
    await until(() => [...state.connections.values()].some((connection) => connection.connected), 'Message SignalR did not connect')
    allMessages.get(incoming.conversationId).push(incoming)
    const item = { conversation: conversation(incoming.conversationId, allMessages.get(incoming.conversationId)), message: incoming }
    state.unread.push(item)
    for (const connection of state.connections.values()) if (connection.connected) connection.queue.push(JSON.stringify({ type: 1, target: 'MessageReceived', arguments: [item] }) + '\x1e')
  }
  const page = await context.newPage()
  latestPage = page
  page.on('pageerror', (error) => errors.push(error.stack))
  page.on('console', (entry) => {
    if (!['error', 'warning'].includes(entry.type())) return
    if (/connection was stopped during negotiation|HttpConnection before stop\(\)/.test(entry.text())) return
    if (/503/.test(entry.text()) && entry.location().url.includes('/api/messages/')) return
    errors.push(`${entry.type()}: ${entry.text()}`)
  })
  await page.goto(`${baseUrl}/feed`)
  await page.getByRole('button', { name: 'Tin nhắn', exact: true }).waitFor()
  return { context, page, state }
}
const chat = (page, name = 'Nhóm A') => page.getByRole('region', { name: `Đoạn chat với ${name}` })
const history = (page, name = 'Nhóm A') => chat(page, name).locator('[data-chat-history]')
const openChat = async (page, name = 'Nhóm A') => {
  await page.getByRole('button', { name: 'Tin nhắn', exact: true }).click()
  await page.getByRole('button').filter({ has: page.getByText(name, { exact: true }) }).click()
  await chat(page, name).waitFor()
}
const check = async (name, run) => { await run(); passed.push(name); console.log(`PASS ${name}`) }
const visibility = async (page, value) => page.evaluate((next) => { window.__chatTestVisibility = next; document.dispatchEvent(new Event('visibilitychange')) }, value)
const anchor = async (viewport) => viewport.evaluate((element) => {
  const top = element.getBoundingClientRect().top
  const row = [...element.querySelectorAll('[data-message-id]')].find((item) => item.getBoundingClientRect().bottom > top + 2)
  return { id: row?.dataset.messageId, offset: row?.getBoundingClientRect().top - top, scrollTop: element.scrollTop, height: element.scrollHeight }
})

try {
  await check('switching conversations ignores a delayed response from the previous conversation', async () => {
    const { context, page, state } = await create()
    state.historyDelay.set(aId, 700)
    await openChat(page)
    await until(() => state.historyReads.some((read) => read.id === aId))
    await openChat(page, 'Nhóm B')
    await chat(page, 'Nhóm B').getByText(message(bId, 1).content, { exact: true }).waitFor()
    await until(() => state.historyResponses.some((read) => read.id === aId))
    await pause(100)
    assert.equal(await chat(page, 'Nhóm A').count(), 0)
    assert.equal(await chat(page, 'Nhóm B').locator('[data-message-id]').count(), 1)
    assert.doesNotMatch(await chat(page, 'Nhóm B').innerText(), /Tin A /)
    await context.close()
  })
  await check('each conversation retains its draft when switching, closing and reopening', async () => {
    const { context, page } = await create()
    await openChat(page)
    await chat(page).getByRole('textbox', { name: 'Nhập tin nhắn' }).fill('Bản nháp riêng của A')
    await openChat(page, 'Nhóm B')
    await chat(page, 'Nhóm B').getByRole('textbox', { name: 'Nhập tin nhắn' }).fill('Bản nháp riêng của B')
    await openChat(page)
    assert.equal(await chat(page).getByRole('textbox', { name: 'Nhập tin nhắn' }).inputValue(), 'Bản nháp riêng của A')
    await chat(page).getByRole('button', { name: 'Đóng đoạn chat' }).click()
    await openChat(page)
    assert.equal(await chat(page).getByRole('textbox', { name: 'Nhập tin nhắn' }).inputValue(), 'Bản nháp riêng của A')
    await openChat(page, 'Nhóm B')
    assert.equal(await chat(page, 'Nhóm B').getByRole('textbox', { name: 'Nhập tin nhắn' }).inputValue(), 'Bản nháp riêng của B')
    await context.close()
  })
  await check('repeated submission sends once and a failed send keeps the draft editable', async () => {
    const { context, page, state } = await create()
    state.sendDelay = 400
    state.failSend = true
    await openChat(page)
    await chat(page).getByRole('textbox', { name: 'Nhập tin nhắn' }).fill('Giữ tin nhắn khi gửi thất bại')
    await chat(page).locator('form').evaluate((form) => {
      form.dispatchEvent(new Event('submit', { bubbles: true, cancelable: true }))
      form.dispatchEvent(new Event('submit', { bubbles: true, cancelable: true }))
    })
    await until(() => state.sends.length > 0)
    assert.equal(state.sends.length, 1)
    assert.equal(await chat(page).getByRole('textbox', { name: 'Nhập tin nhắn' }).isDisabled(), true)
    await chat(page).getByRole('alert').filter({ hasText: 'Không thể gửi tin nhắn.' }).waitFor()
    assert.equal(state.sendResponses, 1)
    assert.equal(state.sends.length, 1)
    assert.equal(await chat(page).getByRole('textbox', { name: 'Nhập tin nhắn' }).inputValue(), 'Giữ tin nhắn khi gửi thất bại')
    assert.equal(await chat(page).getByRole('textbox', { name: 'Nhập tin nhắn' }).isEnabled(), true)
    state.failSend = false
    await chat(page).getByRole('button', { name: 'Gửi', exact: true }).click()
    await until(() => state.sendResponses === 2)
    await until(async () => await chat(page).getByRole('textbox', { name: 'Nhập tin nhắn' }).inputValue() === '')
    assert.equal(await chat(page).getByRole('alert').count(), 0)
    await context.close()
  })
  await check('loading older history preserves the visible message and scroll position', async () => {
    const { context, page, state } = await create()
    await openChat(page)
    await until(async () => await history(page).locator('[data-message-id]').count() === 20)
    await history(page).evaluate((element) => { element.scrollTop = 0; element.dispatchEvent(new Event('scroll')) })
    const before = await anchor(history(page))
    await chat(page).getByRole('button', { name: 'Tải tin cũ hơn', exact: true }).click()
    await until(async () => await history(page).locator('[data-message-id]').count() === 40)
    // The load-more button disappears; older rows may fill its space while the
    // message the reader was viewing must stay at the same viewport offset.
    const offset = await history(page).locator(`[data-message-id="${before.id}"]`).evaluate(row => row.getBoundingClientRect().top - row.closest('[data-chat-history]').getBoundingClientRect().top)
    assert.ok(Math.abs(offset - before.offset) < 3, `Visible row moved ${offset - before.offset}px`)
    assert.equal(state.historyReads.filter((read) => read.before === 'older-page').length, 1)
    assert.equal(state.historyReads.find((read) => read.before === 'older-page').limit, '50')
    await context.close()
  })
  await check('switching back to a sending conversation keeps its composer locked and shows the completed send', async () => {
    const { context, page, state } = await create()
    state.sendDelay = 900
    await openChat(page)
    await chat(page).getByRole('textbox', { name: 'Nhập tin nhắn' }).fill('Tin đang gửi khi đổi hội thoại')
    await chat(page).getByRole('button', { name: 'Gửi', exact: true }).click()
    await until(() => state.sends.length === 1)
    await openChat(page, 'Nhóm B')
    await openChat(page)
    assert.equal(await chat(page).getByRole('textbox', { name: 'Nhập tin nhắn' }).isDisabled(), true)
    assert.equal(await chat(page).getByRole('button', { name: 'Gửi', exact: true }).isDisabled(), true)
    await until(() => state.sendResponses === 1)
    await until(async () => await chat(page).getByRole('textbox', { name: 'Nhập tin nhắn' }).inputValue() === '')
    await chat(page).getByText('Tin đang gửi khi đổi hội thoại', { exact: true }).waitFor()
    assert.equal(state.sends.length, 1)
    await context.close()
  })
  await check('a hidden document does not read messages and becoming visible reads the latest visible message', async () => {
    const { context, page, state } = await create({ hidden: true, peers: true })
    await openChat(page)
    await until(async () => await history(page).locator('[data-message-id]').count() === 20)
    await pause(200)
    assert.equal(state.reads.length, 0)
    await visibility(page, 'visible')
    await until(() => state.reads.length === 1)
    assert.deepEqual(state.reads[0], { conversationId: aId, lastReadMessageId: message(aId, 40, peerId).id })
    await until(() => state.readResponses === 1)
    await pause(100)
    await visibility(page, 'hidden')
    const incoming = message(aId, 41, peerId)
    await state.push(incoming)
    await history(page).locator(`[data-message-id="${incoming.id}"]`).waitFor()
    await pause(100)
    assert.equal(state.reads.length, 1)
    await visibility(page, 'visible')
    await until(() => state.reads.length === 2)
    assert.deepEqual(state.reads[1], { conversationId: aId, lastReadMessageId: incoming.id })
    await context.close()
  })
  await check('new messages preserve scroll while reading older content and offer a jump to the latest message', async () => {
    const { context, page, state } = await create()
    await openChat(page)
    await until(async () => await history(page).locator('[data-message-id]').count() === 20)
    await history(page).evaluate((element) => { element.scrollTop = 150; element.dispatchEvent(new Event('scroll')) })
    const before = await anchor(history(page))
    const incoming = message(aId, 41, peerId)
    await state.push(incoming)
    await history(page).locator(`[data-message-id="${incoming.id}"]`).waitFor()
    await chat(page).getByRole('button', { name: 'Có tin nhắn mới ↓', exact: true }).waitFor()
    const after = await anchor(history(page))
    assert.equal(after.id, before.id)
    assert.ok(Math.abs(after.scrollTop - before.scrollTop) < 3, `New message moved scroll by ${after.scrollTop - before.scrollTop}px`)
    assert.equal(state.reads.length, 0)
    await chat(page).getByRole('button', { name: 'Có tin nhắn mới ↓', exact: true }).click()
    await until(() => state.reads.some((read) => read.lastReadMessageId === incoming.id))
    assert.ok(await history(page).evaluate((element) => element.scrollHeight - element.scrollTop - element.clientHeight < 3))
    assert.equal(await chat(page).getByRole('button', { name: 'Có tin nhắn mới ↓', exact: true }).count(), 0)
    await context.close()
  })
  await check('a failed read retains unread state and retries when the message is visible', async () => {
    const { context, page, state } = await create({ hidden: true })
    await openChat(page)
    await until(async () => await history(page).locator('[data-message-id]').count() === 20)
    const incoming = message(aId, 41, peerId)
    await state.push(incoming)
    await history(page).locator(`[data-message-id="${incoming.id}"]`).waitFor()
    state.failRead = true
    await visibility(page, 'visible')
    await chat(page).getByRole('alert').filter({ hasText: 'Chưa thể cập nhật trạng thái đã đọc.' }).waitFor()
    assert.equal(state.unread.length, 1)
    state.failRead = false
    await chat(page).getByRole('alert').getByRole('button', { name: 'Thử lại' }).click()
    await until(() => state.unread.length === 0)
    assert.equal(state.reads.at(-1).lastReadMessageId, incoming.id)
    await context.close()
  })
  await check('a realtime arrival during unread reconciliation keeps its badge', async () => {
    const { context, page, state } = await create({ hidden: true })
    await openChat(page)
    await until(async () => await history(page).locator('[data-message-id]').count() === 20)
    const incomingA = message(aId, 41, peerId)
    await state.push(incomingA)
    await history(page).locator(`[data-message-id="${incomingA.id}"]`).waitFor()
    state.notificationDelay = 350
    const notificationReads = state.notificationReads
    const notificationResponses = state.notificationResponses
    await visibility(page, 'visible')
    await until(() => state.notificationReads > notificationReads)
    await visibility(page, 'hidden')
    await state.push(message(bId, 2, peerId))
    await until(async () => await page.getByRole('button', { name: 'Tin nhắn', exact: true }).locator('span').textContent() === '2')
    await until(() => state.notificationResponses > notificationResponses)
    await until(async () => await page.getByRole('button', { name: 'Tin nhắn', exact: true }).locator('span').textContent() === '1')
    assert.equal(state.unread[0].conversation.id, bId)
    await context.close()
  })
  assert.deepEqual(errors, [])
  console.log(`PASS ${passed.length} floating chat browser checks; no runtime errors`)
} catch (error) {
  if (latestPage && !latestPage.isClosed()) await latestPage.screenshot({ path: `${artifacts}/failure.png`, fullPage: true }).catch(() => undefined)
  console.error(JSON.stringify({ passed, errors }, null, 2))
  throw error
} finally {
  await browser.close()
}
