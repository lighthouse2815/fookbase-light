import assert from 'node:assert/strict'
import { storyIds, storyViewerFixture } from './storyViewerFixture.mjs'

const { chromium } = await import(process.env.PLAYWRIGHT_MODULE ?? 'playwright')
const browser = await chromium.launch({ headless: true, executablePath: process.env.CHROMIUM_EXECUTABLE, args: ['--no-sandbox'] })
const baseUrl = process.env.STORY_BASE_URL ?? 'http://127.0.0.1:5184'
const scope = process.env.STORY_CHECK_SCOPE ?? 'reply'
const pause = ms => new Promise(resolve => setTimeout(resolve, ms))
let passed = 0
const errors = []
async function check(name, run, options = {}) {
  const context = await browser.newContext({ viewport: options.viewport ?? { width: 1366, height: 900 }, hasTouch: options.touch ?? false, isMobile: options.touch ?? false })
  const state = await storyViewerFixture(context, options)
  const page = await context.newPage(); page.setDefaultTimeout(10000)
  page.on('pageerror', error => errors.push(error.stack))
  try {
    await page.goto(`${baseUrl}/feed`, { waitUntil: 'domcontentloaded' })
    await page.getByRole('button', { name: options.owner ? /Story của bạn/ : /Minh$/ }).click()
    const viewer = page.getByRole('dialog', { name: 'Trình xem Story', exact: true })
    await viewer.locator(options.video ? 'video' : 'img[alt="Story 1"]').waitFor()
    const reply = viewer.getByPlaceholder('Trả lời qua Zola Light…')
    const next = () => viewer.getByRole('button', { name: 'Story tiếp theo', exact: true }).click()
    const previous = () => viewer.getByRole('button', { name: 'Story trước', exact: true }).click()
    await run({ page, context, state, viewer, reply, next, previous })
    passed++; console.log(`PASS ${name}`)
  } finally { await context.close() }
}

try {
  if (['reply', 'full'].includes(scope)) {
    await check('reply accepts Space and Arrow editing without navigating', async ({ viewer, reply }) => {
      await reply.focus(); await reply.pressSequentially('Xin chào Minh'); await reply.press('ArrowLeft'); await reply.press('ArrowRight')
      assert.equal(await reply.inputValue(), 'Xin chào Minh'); assert.equal(await viewer.locator('img[alt="Story 1"]').count(), 1)
    })
    await check('reply focus prevents auto-next beyond image duration', async ({ viewer, reply }) => {
      await reply.fill('Đang nhập trả lời'); await pause(5400)
      assert.equal(await viewer.locator('img[alt="Story 1"]').count(), 1)
    })
    await check('draft clears on navigation; later response cannot clear the new draft', async ({ viewer, reply, next, state }) => {
      state.replyDelay = 700; await reply.fill('Cho Story A'); await reply.press('Enter'); await next()
      await viewer.locator('img[alt="Story 2"]').waitFor(); assert.equal(await reply.inputValue(), '')
      await reply.fill('Cho Story B'); await pause(850)
      assert.equal(await reply.inputValue(), 'Cho Story B'); assert.equal(await viewer.getByText('Đã gửi vào Zola Light.', { exact: true }).count(), 0)
      assert.deepEqual(state.replies, [{ storyId: storyIds[0], content: 'Cho Story A' }])
    })
    await check('reply captures StoryId, prevents double-submit and displays success', async ({ viewer, reply, state }) => {
      state.replyDelay = 400; await reply.fill('Gửi một lần'); await reply.press('Enter'); await reply.press('Enter')
      await viewer.getByText('Đã gửi vào Zola Light.', { exact: true }).waitFor()
      assert.deepEqual(state.replies, [{ storyId: storyIds[0], content: 'Gửi một lần' }]); assert.equal(await reply.inputValue(), '')
    })
    await check('reply failure retains the draft and permits retry', async ({ viewer, reply, state }) => {
      state.failReply = true; await reply.fill('Giữ nội dung'); await reply.press('Enter')
      await viewer.getByText('Gửi thất bại thử nghiệm', { exact: true }).waitFor(); assert.equal(await reply.inputValue(), 'Giữ nội dung')
      state.failReply = false; await reply.press('Enter'); await viewer.getByText('Đã gửi vào Zola Light.', { exact: true }).waitFor()
      assert.equal(state.replies.length, 2)
    })
  }
  assert.deepEqual(errors, [])
  console.log(`${passed} Story viewer browser checks passed (${scope}).`)
} finally { await browser.close() }
