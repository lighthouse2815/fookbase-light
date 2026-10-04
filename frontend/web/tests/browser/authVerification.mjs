import assert from 'node:assert/strict'
import { fixtures } from './lastSignalFixture.mjs'

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
const unauthenticatedContext = async () => {
  const context = await browser.newContext({ viewport: { width: 1280, height: 900 } })
  await fixtures(context, false)
  await context.addInitScript(() => localStorage.setItem('fookbase.preferences', JSON.stringify({ language: 'vi', theme: 'light' })))
  return context
}
const session = {
  user: { id: '00000000-0000-0000-0000-000000000007', username: 'tester', email: 'tester@example.com', roles: [] },
  accessToken: 'verification-fixture', accessTokenExpiresAt: new Date(Date.now() + 7200000).toISOString(), refreshTokenExpiresAt: new Date(Date.now() + 86400000).toISOString(),
}

try {
  const context = await unauthenticatedContext()
  const page = await context.newPage()
  page.on('pageerror', (error) => errors.push(error.stack))
  let clockTime = Date.now()
  const advance = async (milliseconds) => { clockTime += milliseconds; await page.clock.runFor(milliseconds) }
  const challenge = (cooldown, expiry) => ({ challengeId: 'registration-challenge', resendAvailableAtUtc: new Date(clockTime + cooldown).toISOString(), expiresAtUtc: new Date(clockTime + expiry).toISOString() })
  const registrationWrites = []
  const resendWrites = []
  const verificationWrites = []
  let failResend = false
  await context.route('**/api/auth/registration/**', async (route) => {
    const path = new URL(route.request().url()).pathname
    if (path.endsWith('/start')) {
      registrationWrites.push(route.request().postDataJSON())
      return route.fulfill({ json: { data: challenge(2000, 5000) } })
    }
    if (path.endsWith('/resend')) {
      resendWrites.push(route.request().postDataJSON())
      await new Promise((resolve) => setTimeout(resolve, 150))
      return route.fulfill({ status: failResend ? 429 : 200, json: failResend ? { detail: 'Chưa gửi lại được mã. Vui lòng thử lại.' } : { data: challenge(3000, 10000) } })
    }
    verificationWrites.push(route.request().postDataJSON())
    return route.fulfill({ json: { data: session } })
  })
  await page.goto(`${baseUrl}/login`)
  await page.getByRole('button', { name: 'Tạo tài khoản mới', exact: true }).waitFor()
  await page.clock.install({ time: clockTime })
  clockTime += 1000
  await page.clock.pauseAt(clockTime)
  await page.getByRole('button', { name: 'Tạo tài khoản mới', exact: true }).click()
  await page.getByLabel('Họ', { exact: true }).fill('Nguyễn')
  await page.getByLabel('Tên', { exact: true }).fill('Lan')
  const birthDate = page.getByRole('group', { name: 'Ngày sinh' }).getByRole('combobox')
  await birthDate.nth(0).selectOption('15')
  await birthDate.nth(1).selectOption('6')
  await birthDate.nth(2).selectOption('2000')
  await page.getByLabel(/^Số di động hoặc email/).fill('lan@example.com')
  await page.getByLabel(/^Mật khẩu/).fill('StrongPassword123!')
  await page.getByRole('button', { name: 'Tạo tài khoản', exact: true }).click()
  const code = page.getByLabel('Mã xác nhận', { exact: true })
  await code.waitFor()
  assert.equal(registrationWrites.length, 1)
  const resend = page.getByRole('button', { name: /^Gửi lại mã/ })
  assert.equal(await resend.isDisabled(), true)
  assert.match(await resend.textContent(), /2s/)
  await resend.evaluate((button) => button.click())
  assert.equal(resendWrites.length, 0)
  await advance(2100)
  assert.equal(await resend.isDisabled(), false)
  console.log('PASS registration resend respects server cooldown and becomes available after its deadline')

  await code.fill('123456')
  const verify = page.getByRole('button', { name: 'Xác nhận và tạo tài khoản', exact: true })
  assert.equal(await verify.isDisabled(), false)
  await advance(3100)
  await page.getByRole('status').filter({ hasText: 'Mã đã hết hạn' }).waitFor()
  assert.equal(await verify.isDisabled(), true)
  await page.locator('form').evaluate((form) => form.requestSubmit())
  await page.getByRole('alert').filter({ hasText: 'Mã đã hết hạn' }).waitFor()
  assert.equal(verificationWrites.length, 0)
  console.log('PASS expired registration code is explained and cannot trigger verification')

  await resend.evaluate((button) => { button.click(); button.click() })
  await until(() => resendWrites.length === 1)
  assert.equal(await code.isDisabled(), true)
  await page.getByRole('status').filter({ hasText: 'Đã gửi mã xác minh mới.' }).waitFor()
  assert.equal(resendWrites.length, 1)
  assert.deepEqual(resendWrites[0], { challengeId: 'registration-challenge' })
  assert.equal(await code.inputValue(), '')
  assert.equal(await page.getByRole('status').filter({ hasText: 'Mã đã hết hạn' }).count(), 0)
  assert.equal(await page.getByRole('alert').count(), 0)
  assert.equal(await resend.isDisabled(), true)
  console.log('PASS resend is submitted once, clears the old code and error, and confirms success')

  await code.fill('654321')
  await advance(3100)
  failResend = true
  await resend.click()
  await page.getByRole('alert').filter({ hasText: 'Chưa gửi lại được mã' }).waitFor()
  assert.equal(await code.inputValue(), '654321')
  assert.equal(await code.isDisabled(), false)
  assert.equal(await resend.isDisabled(), false)
  assert.equal(await verify.isDisabled(), false)
  console.log('PASS a failed resend preserves the current code and allows retry')
  await context.close()

  const twoFactorContext = await unauthenticatedContext()
  const loginWrites = []
  const twoFactorWrites = []
  let failTwoFactor = true
  await twoFactorContext.route('**/api/auth/login', (route) => {
    loginWrites.push(route.request().postDataJSON())
    return route.fulfill({ json: { data: { twoFactorRequired: true, challenge: 'two-factor-challenge', expiresAtUtc: new Date(Date.now() + 600000).toISOString() } } })
  })
  await twoFactorContext.route('**/api/auth/2fa/verify', async (route) => {
    twoFactorWrites.push(route.request().postDataJSON())
    await new Promise((resolve) => setTimeout(resolve, 250))
    return route.fulfill({ status: failTwoFactor ? 400 : 200, json: failTwoFactor ? { detail: 'Mã xác thực chưa đúng.' } : { data: session } })
  })
  const twoFactorPage = await twoFactorContext.newPage()
  twoFactorPage.on('pageerror', (error) => errors.push(error.stack))
  await twoFactorPage.goto(`${baseUrl}/login`)
  const signIn = async () => {
    await twoFactorPage.getByLabel(/^Số di động hoặc email/).fill('tester@example.com')
    await twoFactorPage.getByLabel(/^Mật khẩu/).fill('StrongPassword123!')
    await twoFactorPage.getByRole('button', { name: 'Đăng nhập', exact: true }).click()
  }
  await signIn()
  const twoFactorCode = twoFactorPage.getByLabel('Mã xác nhận', { exact: true })
  await twoFactorCode.fill('ABC123XYZ')
  await twoFactorCode.press('Enter')
  await twoFactorPage.locator('form').evaluate((form) => { form.requestSubmit(); form.requestSubmit() })
  await twoFactorPage.getByRole('alert').filter({ hasText: 'Mã xác thực chưa đúng.' }).waitFor()
  assert.equal(loginWrites.length, 1)
  assert.equal(twoFactorWrites.length, 1)
  assert.deepEqual(twoFactorWrites[0], { challenge: 'two-factor-challenge', code: 'ABC123XYZ' })
  assert.equal(await twoFactorCode.inputValue(), 'ABC123XYZ')
  assert.equal(await twoFactorCode.isDisabled(), false)
  console.log('PASS Enter submits the two-factor endpoint once and preserves recovery codes on failure')

  await twoFactorPage.getByRole('button', { name: 'Quay lại đăng nhập', exact: true }).click()
  assert.equal(await twoFactorCode.count(), 0)
  await signIn()
  await twoFactorCode.waitFor()
  assert.equal(await twoFactorCode.inputValue(), '')
  failTwoFactor = false
  await twoFactorCode.fill('123456')
  await twoFactorCode.press('Enter')
  await twoFactorPage.waitForURL('**/feed')
  assert.equal(loginWrites.length, 2)
  assert.equal(twoFactorWrites.length, 2)
  console.log('PASS returning to sign-in clears the challenge and successful two-factor verification opens the feed')
  await twoFactorContext.close()
  assert.deepEqual(errors, [])
} finally {
  await browser.close()
}
