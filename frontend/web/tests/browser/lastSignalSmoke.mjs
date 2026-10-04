import assert from 'node:assert/strict'
import { mkdir } from 'node:fs/promises'
import { fixtures, trackErrors } from './lastSignalFixture.mjs'

const { chromium } = await import(process.env.PLAYWRIGHT_MODULE || 'playwright')
const baseUrl = process.env.GAME_BASE_URL || 'http://127.0.0.1:5184'
const artifacts = process.env.GAME_ARTIFACTS || '/tmp/last-signal-artifacts'
await mkdir(artifacts, { recursive: true })
const browser = await chromium.launch({ executablePath: process.env.BROWSER_EXECUTABLE || undefined, headless: true, args: ['--no-sandbox', '--use-angle=swiftshader', '--enable-unsafe-swiftshader'] })
const errors = []
try {
  const context = await browser.newContext({ viewport: { width: 1100, height: 800 } })
  await fixtures(context)
  const page = await context.newPage()
  trackErrors(page, errors)
  await page.goto(`${baseUrl}/game`)
  assert.equal(await page.locator('script[src="/@vite/client"]').count(), 0, 'Production smoke requires vite preview; GAME_BASE_URL currently points to a dev server')
  await page.getByRole('button', { name: 'Start Game' }).waitFor({ timeout: 30000 })
  await page.getByRole('button', { name: 'Start Game' }).click()
  await page.waitForFunction(() => document.querySelector('.signal-hud')?.dataset.phase === 'playing')
  await page.waitForFunction(() => window.__lastSignalAudioContexts.length === 1 && window.__lastSignalAudioContexts[0].state === 'running')
  const graphics = await page.evaluate(() => {
    const canvas = document.querySelector('.signal-viewport canvas')
    const gl = canvas.getContext('webgl2')
    const program = gl.getParameter(gl.CURRENT_PROGRAM)
    const projection = gl.getUniform(program, gl.getUniformLocation(program, 'projectionMatrix'))
    return { context: gl.constructor.name, width: gl.drawingBufferWidth, projection: Array.from(projection) }
  })
  assert.match(graphics.context, /WebGL/)
  assert.ok(graphics.width > 0)
  assert.equal(graphics.projection[11], -1)
  assert.equal(graphics.projection[15], 0)
  assert.equal(await page.locator('.signal-debug').count(), 0)
  await page.keyboard.down('w')
  await page.waitForTimeout(1200)
  await page.keyboard.up('w')
  await page.screenshot({ path: `${artifacts}/production.png` })
  await page.keyboard.press('Escape')
  await page.getByRole('heading', { name: 'Tạm dừng.' }).waitFor()
  assert.equal(await page.evaluate(() => document.pointerLockElement), null)
  await page.getByRole('link', { name: 'Về thư viện game' }).click()
  await page.getByRole('heading', { name: /Tần số 0/ }).waitFor()
  assert.deepEqual(errors, [])
  console.log('PASS production bundle: native WebGL perspective projection, Start, input, pause, library and no development HUD/errors')
} finally { await browser.close() }
