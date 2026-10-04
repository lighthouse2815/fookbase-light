import assert from 'node:assert/strict'
import { mkdir } from 'node:fs/promises'
import { fixtures, trackErrors } from './lastSignalFixture.mjs'

const { chromium } = await import(process.env.PLAYWRIGHT_MODULE || 'playwright')
const baseUrl = process.env.GAME_BASE_URL || 'http://127.0.0.1:5186'
const artifacts = process.env.GAME_ARTIFACTS || '/tmp/jumping-artifacts'
await mkdir(artifacts, { recursive: true })
const browser = await chromium.launch({ headless: true, executablePath: process.env.BROWSER_EXECUTABLE || undefined })
const errors = []

async function openGame(options = {}, identity = null) {
  const context = await browser.newContext(options)
  await fixtures(context)
  if (identity) {
    // Exercise the real Jumping hub; unrelated API/presence requests keep the existing fixtures.
    await context.route(/\/hubs\/jumping(?:[/?]|$)/, route => route.continue())
    await context.addInitScript(({ userId, token }) => {
      const session = JSON.parse(localStorage.getItem('fookbase.session'))
      session.user.id = userId
      session.accessToken = token
      localStorage.setItem('fookbase.session', JSON.stringify(session))
      localStorage.setItem('fookbase.accessToken', token)
    }, identity)
  }
  const page = await context.newPage()
  let cutConnection = null
  if (identity) {
    await page.routeWebSocket('**/hubs/jumping**', socket => {
      socket.connectToServer()
      cutConnection = () => socket.close({ code: 1012, reason: 'Transport reconnect regression' })
    })
  }
  trackErrors(page, errors)
  await page.goto(`${baseUrl}/games`)
  await page.getByRole('heading', { name: 'Chọn mood của bạn' }).waitFor()
  await page.getByRole('button', { name: /Jumping/ }).click({ timeout: 3000 })
  await page.locator('.jumping-stage').waitFor()
  return { page, context, disconnect: () => cutConnection?.() }
}

const phase = (page, value) => page.waitForFunction(expected => document.querySelector('.jumping-stage')?.dataset.phase === expected, value)

try {
  const { page, context } = await openGame({ viewport: { width: 1100, height: 860 } })
  const stage = page.locator('.jumping-stage')
  await stage.focus()
  await page.keyboard.press('Space')
  await phase(page, 'playing')
  await page.keyboard.press('Escape')
  await phase(page, 'paused')
  const before = await page.locator('.jumping-block').first().getAttribute('x')
  await page.waitForTimeout(250)
  assert.equal(await page.locator('.jumping-block').first().getAttribute('x'), before)
  await page.getByRole('button', { name: 'Tiếp tục chạy', exact: true }).click()
  await phase(page, 'playing')
  await page.waitForFunction(() => Number(document.querySelector('.jumping-block')?.getAttribute('x')) < 224)
  await page.keyboard.press('Space')
  await page.waitForFunction(() => Number(document.querySelector('.jumping-runner')?.getAttribute('cy')) < 250)
  await page.waitForFunction(() => document.querySelector('.jumping-stage')?.dataset.score === '1')
  assert.equal(await page.evaluate(() => localStorage.getItem('fookbase.jumping.best')), '1')
  await page.keyboard.press('p')
  await phase(page, 'paused')
  await page.screenshot({ path: `${artifacts}/desktop.png`, fullPage: true })
  await page.getByRole('button', { name: 'Ván mới', exact: true }).click()
  await phase(page, 'ready')
  await stage.focus()
  await page.keyboard.press('Space')
  await phase(page, 'over')
  await page.getByRole('button', { name: 'Chạy lại', exact: true }).click()
  await phase(page, 'playing')
  assert.equal(await stage.getAttribute('data-score'), '0')
  await context.close()

  const mobile = await openGame({ viewport: { width: 390, height: 844 }, isMobile: true, hasTouch: true })
  assert.ok(await mobile.page.evaluate(() => document.documentElement.scrollWidth <= innerWidth + 1))
  await mobile.page.locator('.jumping-stage').tap()
  await phase(mobile.page, 'playing')
  await mobile.page.getByRole('button', { name: 'Nhảy', exact: true }).tap()
  await mobile.page.waitForFunction(() => Number(document.querySelector('.jumping-runner')?.getAttribute('cy')) < 250)
  await mobile.page.screenshot({ path: `${artifacts}/mobile.png`, fullPage: true })
  await mobile.page.getByRole('button', { name: 'Tạm dừng', exact: true }).tap()
  await phase(mobile.page, 'paused')
  await mobile.context.close()
  console.log('PASS desktop/mobile: keyboard and touch, jump, collision, pause/resume, score, record and restart')

  if (process.env.JUMPING_HOST_TOKEN && process.env.JUMPING_GUEST_TOKEN) {
    const host = await openGame({}, { userId: process.env.JUMPING_HOST_ID, token: process.env.JUMPING_HOST_TOKEN })
    const guest = await openGame({}, { userId: process.env.JUMPING_GUEST_ID, token: process.env.JUMPING_GUEST_TOKEN })
    await host.page.getByText('Đã kết nối', { exact: true }).waitFor()
    await guest.page.getByText('Đã kết nối', { exact: true }).waitFor()
    await host.page.getByRole('button', { name: 'Tạo phòng', exact: true }).click()
    await host.page.locator('[data-room-code]').waitFor()
    const code = await host.page.locator('[data-room-code]').innerText()
    await guest.page.getByRole('textbox', { name: 'Mã phòng', exact: true }).fill(code)
    await guest.page.getByRole('button', { name: 'Vào phòng', exact: true }).click()
    await host.page.getByText('2 người trong phòng', { exact: true }).waitFor()
    await host.page.getByRole('button', { name: 'Bắt đầu vòng', exact: true }).click()
    await Promise.all([phase(host.page, 'playing'), phase(guest.page, 'playing')])
    const course = page => page.locator('.jumping-block').evaluateAll(blocks => blocks.map(block => ({ width: block.getAttribute('width'), height: block.getAttribute('height') })))
    assert.deepEqual(await course(host.page), await course(guest.page))
    assert.equal(await host.page.evaluate(() => document.activeElement?.classList.contains('jumping-stage')), true, 'Online starts must focus the keyboard controls')
    await host.page.waitForFunction(() => Number(document.querySelector('.jumping-block')?.getAttribute('x')) < 224)
    await host.page.keyboard.press('Space')
    await phase(guest.page, 'over')
    await guest.page.waitForFunction(() => [...document.querySelectorAll('.jumping-leaderboard li')].some(row => row.dataset.local !== 'true' && Number(row.dataset.score) >= 1))
    await guest.page.getByRole('button', { name: 'Rời phòng', exact: true }).click()
    await guest.page.getByRole('textbox', { name: 'Mã phòng', exact: true }).fill(code)
    await guest.page.getByRole('button', { name: 'Vào phòng', exact: true }).click()
    await host.page.getByText('2 người trong phòng', { exact: true }).waitFor()
    await phase(guest.page, 'ready')
    await guest.page.waitForFunction(() => [...document.querySelectorAll('.jumping-leaderboard li')].some(row => row.dataset.local !== 'true' && Number(row.dataset.score) >= 1), undefined, { timeout: 3000 })
    await phase(host.page, 'over')
    await guest.disconnect()
    await guest.page.waitForTimeout(150)
    await guest.page.getByText('Đã kết nối', { exact: true }).waitFor()
    await guest.page.waitForFunction(() => [...document.querySelectorAll('.jumping-leaderboard li')].some(row => row.dataset.local !== 'true' && Number(row.dataset.score) >= 1))
    assert.equal(await guest.page.locator('[data-room-code]').innerText(), code)
    await host.page.getByText('2 người trong phòng', { exact: true }).waitFor()
    await host.page.screenshot({ path: `${artifacts}/online.png`, fullPage: true })
    await guest.page.getByRole('button', { name: 'Rời phòng', exact: true }).click()
    await host.page.getByText('1 người trong phòng', { exact: true }).waitFor()
    await guest.page.getByRole('textbox', { name: 'Mã phòng', exact: true }).fill(code)
    await guest.page.getByRole('button', { name: 'Vào phòng', exact: true }).click()
    await host.page.getByText('2 người trong phòng', { exact: true }).waitFor()
    await host.page.getByRole('button', { name: 'Rời phòng', exact: true }).click()
    await guest.page.getByRole('button', { name: 'Bắt đầu vòng', exact: true }).click()
    await guest.page.getByText('Bắt đầu sau 3', { exact: true }).waitFor()
    await guest.page.evaluate(() => {
      Object.defineProperty(document, 'hidden', { configurable: true, value: true })
      document.dispatchEvent(new Event('visibilitychange'))
    })
    await phase(guest.page, 'over')
    await guest.page.waitForTimeout(3300)
    assert.equal(await guest.page.locator('.jumping-stage').getAttribute('data-phase'), 'over', 'A hidden countdown must not start playing later')
    await guest.page.evaluate(() => { delete document.hidden; document.dispatchEvent(new Event('visibilitychange')) })
    await guest.page.getByRole('button', { name: 'Chơi vòng mới', exact: true }).click()
    await phase(guest.page, 'playing')
    await Promise.all([host.context.close(), guest.context.close()])
    console.log('PASS real SignalR: create/join, shared course, live scores, late join, transport reconnect, host transfer and hidden countdown recovery')
  }
  assert.deepEqual(errors, [])
} finally { await browser.close() }
