import assert from 'node:assert/strict'
import { mkdir } from 'node:fs/promises'
import { invitationNotificationFixture, pageUsername } from './invitationNotificationFixture.mjs'

const { chromium } = await import(process.env.PLAYWRIGHT_MODULE ?? 'playwright')
const browser = await chromium.launch({ headless: true, executablePath: process.env.CHROMIUM_EXECUTABLE, args: ['--no-sandbox'] })
const baseUrl = process.env.NOTIFICATIONS_BASE_URL ?? 'http://127.0.0.1:5184'
const artifacts = process.env.NOTIFICATIONS_ARTIFACTS ?? '/tmp/notification-browser-artifacts'
await mkdir(artifacts, { recursive: true })
const errors = []
const passed = []
const check = async (name, run) => { await run(); passed.push(name); console.log(`PASS ${name}`) }
const until = async (condition) => {
  for (let attempt = 0; attempt < 250; attempt++) {
    if (await condition()) return
    await new Promise((resolve) => setTimeout(resolve, 30))
  }
  assert.fail('Invitation state did not settle')
}
const selected = (page) => page.locator('[data-selected-invitation="true"]')
const unavailable = (page) => page.getByRole('status').filter({ hasText: 'Lời mời này đã được xử lý hoặc không còn khả dụng.' })

async function create(kind, fixtureOptions = {}, viewport = { width: 1366, height: 900 }) {
  const context = await browser.newContext({ viewport })
  const state = await invitationNotificationFixture(context, kind, fixtureOptions)
  const page = await context.newPage()
  page.setDefaultTimeout(10000)
  page.on('pageerror', (error) => errors.push(error.stack))
  page.on('console', (message) => {
    if (!['error', 'warning'].includes(message.type())) return
    if (/connection was stopped during negotiation|HttpConnection before stop\(\)/.test(message.text())) return
    if (/403|404|503/.test(message.text()) && /\/api\/(groups|pages)\//.test(message.location().url)) return
    errors.push(`${message.type()}: ${message.text()}`)
  })
  return { context, page, state }
}

async function openNotification(page, state) {
  await page.goto(`${baseUrl}/feed`, { waitUntil: 'domcontentloaded' })
  await page.getByRole('button', { name: /^Thông báo, \d+ chưa đọc$/ }).click()
  const panel = page.getByRole('dialog', { name: 'Thông báo', exact: true })
  const row = panel.locator('[data-notification-id]').first()
  await row.waitFor()
  const destination = new URL(await row.getAttribute('href'), baseUrl)
  assert.equal(destination.pathname, state.kind === 'group' ? '/groups' : '/pages')
  assert.equal(destination.searchParams.get('invite'), state.inviteId)
  assert.equal(destination.searchParams.get(state.kind === 'group' ? 'group' : 'pageId'), state.resourceId)
  if (state.kind === 'page') assert.equal(destination.searchParams.get('page'), 'ten-trang-cu')
  await row.click()
}

try {
  for (const kind of ['group', 'page']) {
    const pending = await create(kind)
    await check(`${kind} pending invitation is located after a cursor page, focused and highlighted without fetching private detail`, async () => {
      await openNotification(pending.page, pending.state)
      await selected(pending.page).waitFor()
      await until(async () => await selected(pending.page).evaluate((element) => element === document.activeElement))
      assert.equal(await selected(pending.page).getAttribute('data-invitation-id'), pending.state.inviteId)
      assert.match(await selected(pending.page).getAttribute('class'), /ring-2/)
      if (kind === 'page') assert.match(await selected(pending.page).innerText(), /@ten-trang-cu/)
      assert.deepEqual(pending.state.invitationReads, [null, 'invitation-second-page'])
      assert.deepEqual(pending.state.detailReads, [])
      assert.equal(await pending.page.getByRole('dialog', { name: 'Thông báo', exact: true }).count(), 0)
      await pending.page.screenshot({ path: `${artifacts}/${kind}-invitation-desktop.png` })
    })
    await check(`${kind} accepting the selected invitation uses its authoritative resource ID and prevents double-submit`, async () => {
      await selected(pending.page).getByRole('button', { name: 'Chấp nhận', exact: true }).click()
      assert.equal(await selected(pending.page).getByRole('button', { name: /Đang xử lý/ }).isDisabled(), true)
      assert.deepEqual(pending.state.detailReads, [])
      await pending.page.waitForURL(`${baseUrl}/${kind === 'group' ? 'groups' : 'pages'}/${pending.state.resourceId}`)
      await pending.page.getByRole('heading', { name: kind === 'group' ? 'Nhóm riêng tư được mời' : 'Trang chưa xuất bản được mời', exact: true }).first().waitFor()
      assert.equal(pending.state.writes.length, 1)
      assert.equal(pending.state.writes[0].action, 'accept')
      assert.ok(pending.state.detailReads.every((read) => read.accepted))
      assert.equal(pending.state.detailReads[0].path, `/api/${kind === 'group' ? 'groups' : 'pages'}/${pending.state.resourceId}`)
    })
    await pending.context.close()

    const declined = await create(kind, {}, { width: 375, height: 812 })
    await check(`${kind} mobile decline clears the targeted query and removes the invitation without opening private detail`, async () => {
      await openNotification(declined.page, declined.state)
      await selected(declined.page).waitFor()
      assert.equal(await declined.page.evaluate(() => document.documentElement.scrollWidth > innerWidth), false)
      await declined.page.screenshot({ path: `${artifacts}/${kind}-invitation-mobile.png` })
      await selected(declined.page).getByRole('button', { name: kind === 'group' ? 'Từ chối lời mời' : 'Từ chối', exact: true }).click()
      await until(() => !new URL(declined.page.url()).searchParams.has('invite'))
      await selected(declined.page).waitFor({ state: 'hidden' })
      assert.equal(new URL(declined.page.url()).pathname, kind === 'group' ? '/groups' : '/pages')
      assert.equal(declined.state.writes[0].action, 'decline')
      assert.deepEqual(declined.state.detailReads, [])
      assert.equal(await declined.page.locator(`[data-invitation-id="${declined.state.inviteId}"]`).count(), 0)
    })
    await declined.context.close()

    const failed = await create(kind)
    await check(`${kind} invitation API failure keeps its actions available for retry`, async () => {
      await openNotification(failed.page, failed.state)
      await selected(failed.page).waitFor()
      failed.state.failAction = true
      await selected(failed.page).getByRole('button', { name: 'Chấp nhận', exact: true }).click()
      await until(() => failed.state.responses === 1)
      await until(async () => await selected(failed.page).getByRole('button', { name: 'Chấp nhận', exact: true }).isEnabled())
      await failed.page.getByText('Không thể xử lý lời mời thử nghiệm.', { exact: true }).waitFor()
      assert.deepEqual(failed.state.detailReads, [])
      assert.equal(new URL(failed.page.url()).searchParams.get('invite'), failed.state.inviteId)
    })
    await failed.context.close()

    const accepted = await create(kind, { accepted: true })
    await check(`${kind} already accepted invitation resolves its exact resource after exhausting pending invitation pages`, async () => {
      await openNotification(accepted.page, accepted.state)
      const target = kind === 'group' ? accepted.state.resourceId : pageUsername
      await accepted.page.waitForURL(`${baseUrl}/${kind === 'group' ? 'groups' : 'pages'}/${target}`)
      await accepted.page.getByRole('heading', { name: kind === 'group' ? 'Nhóm riêng tư được mời' : 'Trang chưa xuất bản được mời', exact: true }).first().waitFor()
      assert.deepEqual(accepted.state.invitationReads, [null, 'invitation-second-page'])
      assert.equal(accepted.state.detailReads[0].path, `/api/${kind === 'group' ? 'groups' : 'pages'}/${accepted.state.resourceId}`)
      assert.deepEqual(accepted.state.detailReads[0].invitationReads, [null, 'invitation-second-page'])
      assert.equal(accepted.state.writes.length, 0)
      assert.equal(new URL(accepted.page.url()).searchParams.has('invite'), false)
    })
    await accepted.context.close()

    for (const status of [403, 404]) {
      const missing = await create(kind, { missing: true, detailStatus: status })
      await check(`${kind} unavailable invitation (${status}) remains compact and does not navigate to inaccessible content`, async () => {
        await openNotification(missing.page, missing.state)
        await unavailable(missing.page).waitFor()
        assert.deepEqual(missing.state.invitationReads, [null, 'invitation-second-page'])
        assert.equal(missing.state.detailReads.length, 1)
        assert.equal(await selected(missing.page).count(), 0)
        assert.equal(new URL(missing.page.url()).pathname, kind === 'group' ? '/groups' : '/pages')
        assert.equal(missing.state.writes.length, 0)
      })
      await missing.context.close()
    }

    const ordinary = await create(kind)
    await check(`${kind} collection without an invitation query preserves its normal first-page behavior`, async () => {
      await ordinary.page.goto(`${baseUrl}/${kind === 'group' ? 'groups' : 'pages'}`, { waitUntil: 'domcontentloaded' })
      if (kind === 'group') await ordinary.page.getByRole('button', { name: 'Nhóm của bạn', exact: true }).click()
      await ordinary.page.locator('[data-invitation-id]').first().waitFor()
      assert.equal(await selected(ordinary.page).count(), 0)
      assert.deepEqual(ordinary.state.invitationReads, [null])
      assert.deepEqual(ordinary.state.detailReads, [])
      assert.equal(await ordinary.page.locator(`[data-invitation-id="${ordinary.state.inviteId}"]`).count(), 0)
    })
    await ordinary.context.close()

    const unknown = await create(kind)
    await check(`${kind} unknown invitation without a resource ID never guesses a detail destination`, async () => {
      await unknown.page.goto(`${baseUrl}/${kind === 'group' ? 'groups' : 'pages'}?invite=unknown-invitation`, { waitUntil: 'domcontentloaded' })
      await unavailable(unknown.page).waitFor()
      assert.equal(await selected(unknown.page).count(), 0)
      assert.deepEqual(unknown.state.invitationReads, [null, 'invitation-second-page'])
      assert.deepEqual(unknown.state.detailReads, [])
      assert.equal(unknown.state.writes.length, 0)
    })
    await unknown.context.close()
  }
  assert.deepEqual(errors, [])
  console.log(`${passed.length} invitation notification browser checks passed; no unexpected console/page errors.`)
} finally {
  await browser.close()
}
