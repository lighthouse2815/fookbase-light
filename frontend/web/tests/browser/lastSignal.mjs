import assert from 'node:assert/strict'
import { mkdir, writeFile } from 'node:fs/promises'
import { fixtures, trackErrors } from './lastSignalFixture.mjs'

// Install Playwright outside the project, or set PLAYWRIGHT_MODULE to your installation.
const { chromium } = await import(process.env.PLAYWRIGHT_MODULE || 'playwright')
const baseUrl = process.env.GAME_BASE_URL || 'http://127.0.0.1:5183'
const artifacts = process.env.GAME_ARTIFACTS || '/tmp/last-signal-artifacts'
await mkdir(artifacts, { recursive: true })
const browser = await chromium.launch({
  executablePath: process.env.BROWSER_EXECUTABLE || undefined,
  headless: true,
  args: ['--use-angle=swiftshader', '--enable-unsafe-swiftshader', '--no-sandbox'],
})
const errors = []
const passed = []
const mark = (message) => { passed.push(message); console.log(`PASS ${message}`) }

async function inspect(page) {
  return page.evaluate(async () => {
    // Observe Fiber internals only in this test; production code exposes no test hooks.
    const { _roots } = await import('/node_modules/.vite/deps/@react-three_fiber.js')
    const state = _roots.get(document.querySelector('.signal-viewport canvas')).store.getState()
    let meshes = 0
    state.scene.traverse((object) => { if (object.isMesh) meshes++ })
    return {
      position: state.camera.position.toArray(), yaw: state.camera.rotation.y, pitch: state.camera.rotation.x,
      camera: state.camera.type, fov: state.camera.fov, context: state.gl.getContext().constructor.name,
      meshes, calls: state.gl.info.render.calls, shadows: state.gl.shadowMap.enabled,
      doorRotation: state.scene.getObjectByName('StationDoor')?.rotation.y,
      keyExists: Boolean(state.scene.getObjectByName('StationKey')),
      apparition: state.scene.getObjectByName('Apparition')?.visible,
      memory: { ...state.gl.info.memory },
    }
  })
}

async function mouseLook(page, dx, dy = 0) {
  // Pointer lock must be real; exercise Drei's mouse handler without changing camera state.
  assert.equal(await page.evaluate(() => Boolean(document.pointerLockElement)), true)
  await page.evaluate(({ x, y }) => document.dispatchEvent(new MouseEvent('mousemove', { movementX: x, movementY: y, bubbles: true })), { x: dx, y: dy })
}

async function waitForScene(page, predicate) {
  const deadline = Date.now() + 8000
  while (Date.now() < deadline) {
    const scene = await inspect(page)
    if (predicate(scene)) return scene
    await page.waitForTimeout(100)
  }
  assert.fail('The 3D scene did not reach the expected state')
}

async function aimAt(page, x, y, z) {
  const view = await inspect(page)
  const dx = x - view.position[0]
  const dz = z - view.position[2]
  const yaw = Math.atan2(-dx, -dz)
  const pitch = Math.atan2(y - view.position[1], Math.hypot(dx, dz))
  const yawChange = Math.atan2(Math.sin(yaw - view.yaw), Math.cos(yaw - view.yaw))
  await mouseLook(page, -yawChange / 0.002, -(pitch - view.pitch) / 0.002)
}

async function moveTo(page, x, z, tolerance = 0.45) {
  const deadline = Date.now() + 30000
  await page.keyboard.down('w')
  try {
    while (Date.now() < deadline) {
      const view = await inspect(page)
      if (Math.hypot(x - view.position[0], z - view.position[2]) < tolerance) return
      await aimAt(page, x, view.position[1], z)
      await page.waitForTimeout(110)
    }
    assert.fail(`Could not walk to (${x}, ${z}): ${JSON.stringify((await inspect(page)).position)}`)
  } finally {
    await page.keyboard.up('w')
    await page.waitForTimeout(180)
  }
}

async function interaction(page, label) {
  await page.waitForFunction((text) => document.querySelector('.signal-interaction')?.textContent.includes(text), label, { timeout: 8000 })
  await page.keyboard.press('e')
  await page.waitForTimeout(500)
}

async function objective(page, title) {
  await page.getByRole('region', { name: 'Mục tiêu' }).getByRole('heading', { name: title, exact: true }).waitFor({ state: 'visible', timeout: 8000 })
}

try {
  const context = await browser.newContext({ viewport: { width: 1100, height: 800 }, deviceScaleFactor: 1 })
  await fixtures(context)
  const page = await context.newPage()
  trackErrors(page, errors)
  await page.goto(`${baseUrl}/game`)
  await page.getByRole('button', { name: 'Start Game' }).waitFor({ state: 'visible', timeout: 30000 })
  assert.equal(await page.evaluate(() => document.pointerLockElement), null)
  assert.equal(await page.locator('.signal-viewport canvas').count(), 1)
  await page.screenshot({ path: `${artifacts}/start.png` })
  await page.getByRole('button', { name: 'Start Game' }).click()
  await page.waitForFunction(() => document.querySelector('.signal-hud')?.dataset.phase === 'playing')
  await page.waitForFunction(() => window.__lastSignalAudioContexts.length === 1 && window.__lastSignalAudioContexts[0].state === 'running')
  const scene = await inspect(page)
  assert.equal(scene.camera, 'PerspectiveCamera')
  assert.match(scene.context, /WebGL/)
  assert.ok(scene.fov > 40 && scene.fov < 100)
  assert.ok(scene.meshes > 45 && scene.calls > 0 && scene.shadows)
  mark(`real perspective WebGL scene (${scene.meshes} meshes), user-gesture pointer lock`)

  const initialYaw = scene.yaw
  await mouseLook(page, 120, -60)
  const looked = await inspect(page)
  assert.ok(Math.abs(looked.yaw - initialYaw) > 0.1)
  assert.ok(looked.pitch > 0.05)
  await mouseLook(page, -120, 60)
  mark('mouse controls yaw and bounded pitch')

  await page.keyboard.press('Escape')
  await page.getByRole('heading', { name: 'Tạm dừng.' }).waitFor()
  await page.waitForFunction(() => window.__lastSignalAudioContexts.every((audio) => audio.state === 'suspended'))
  const stopped = await inspect(page)
  await page.keyboard.down('w')
  await page.waitForTimeout(700)
  await page.keyboard.up('w')
  assert.deepEqual((await inspect(page)).position, stopped.position)
  assert.equal(await page.evaluate(() => document.pointerLockElement), null)
  await page.waitForTimeout(1000)
  await page.getByRole('button', { name: 'Tiếp tục', exact: true }).click()
  await page.waitForFunction(() => document.querySelector('.signal-hud')?.dataset.phase === 'playing')
  await page.keyboard.down('w')
  await waitForScene(page, (view) => view.position[2] < stopped.position[2] - 0.4)
  await page.keyboard.up('w')
  mark('ESC pauses, paused input is ignored, explicit resume reacquires pointer')

  await moveTo(page, 0, 1.5)
  await aimAt(page, 0, 1.35, 0.3)
  await interaction(page, 'Mở cửa')
  await objective(page, 'Tìm chìa khóa nhà trạm')
  await page.keyboard.down('w')
  await page.waitForTimeout(1700)
  await page.keyboard.up('w')
  assert.ok((await inspect(page)).position[2] >= 0.42)
  mark('locked door blocks movement and cannot skip the key objective')

  await moveTo(page, 0, 10.3)
  await moveTo(page, -9.4, 9.9)
  await aimAt(page, -9.4, 1.14, 8)
  await interaction(page, 'Nhặt chìa khóa')
  await objective(page, 'Mở cửa trạm phát sóng')
  assert.equal((await inspect(page)).keyExists, false)
  await aimAt(page, -10.7, 1.09, 8)
  await interaction(page, 'Đọc ghi chú')
  assert.match(await page.locator('.signal-message').textContent(), /GHI CHÚ/)
  await moveTo(page, -11.4, 10.3)
  await aimAt(page, -11.8, 1.6, 9.5)
  await interaction(page, 'Bật / tắt')
  assert.match(await page.locator('.signal-message').textContent(), /đã tắt/)
  await interaction(page, 'Bật / tắt')
  mark('E collects the 3D key, updates objective, reads note and toggles lamp')

  await moveTo(page, 0, 10.3)
  await moveTo(page, 0, 2)
  await aimAt(page, 0, 1.35, 0.3)
  await interaction(page, 'Mở cửa')
  await objective(page, 'Khôi phục nguồn điện')
  await waitForScene(page, (view) => view.doorRotation < -0.7)
  await moveTo(page, 0, -3)
  await moveTo(page, -3.7, -3)
  await aimAt(page, -4, 1.1, -4.3)
  await interaction(page, 'Khởi động')
  await objective(page, 'Khám phá căn phòng phía sau')
  mark('door opens, player enters the actual 3D station and restores power')

  await moveTo(page, -1, -3)
  await moveTo(page, -1, -7.4)
  await moveTo(page, -1, -10.9, 0.3)
  await objective(page, 'Điều tra tín hiệu lạ')
  const eventScene = await waitForScene(page, (view) => view.doorRotation > -1.3 && view.apparition)
  assert.equal(eventScene.apparition, true)
  await page.screenshot({ path: `${artifacts}/room-event.png` })
  await moveTo(page, 1.8, -10.7)
  await aimAt(page, 1.8, 1.6, -11.78)
  await interaction(page, 'Điều tra máy thu')
  await page.getByRole('heading', { name: 'Có ai ở đó?' }).waitFor()
  assert.equal(await page.evaluate(() => document.pointerLockElement), null)
  assert.equal(await page.locator('.signal-hud').getAttribute('data-phase'), 'complete')
  await page.screenshot({ path: `${artifacts}/complete.png` })
  mark('room trigger flickers lights, closes door, reveals a 3D apparition; receiver completes the loop')

  await page.getByRole('button', { name: 'Chơi lại', exact: true }).click()
  await page.getByRole('button', { name: 'Start Game' }).waitFor()
  assert.equal((await inspect(page)).keyExists, true)
  assert.equal((await inspect(page)).apparition, false)
  await page.setViewportSize({ width: 800, height: 680 })
  await page.waitForTimeout(300)
  assert.equal(await page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth), true)
  await page.screenshot({ path: `${artifacts}/resize.png` })
  mark('replay resets the world and container resize does not overflow')

  await page.evaluate(async () => {
    const { _roots } = await import('/node_modules/.vite/deps/@react-three_fiber.js')
    window.__lastSignalRenderer = _roots.get(document.querySelector('.signal-viewport canvas')).store.getState().gl
  })
  await page.getByRole('link', { name: '← Thư viện game' }).click()
  await page.getByRole('heading', { name: /Tần số 0/ }).waitFor()
  assert.equal(await page.locator('.signal-viewport canvas').count(), 0)
  // Texture bookkeeping can remain after renderer disposal; native context loss releases GPU allocations.
  await page.waitForFunction(() => window.__lastSignalRenderer.info.memory.geometries === 0 && window.__lastSignalRenderer.getContext().isContextLost())
  await page.waitForFunction(() => window.__lastSignalAudioContexts.every((audio) => audio.state === 'closed'))
  await page.getByRole('link', { name: /Tần số 0.*THE LAST SIGNAL/ }).click()
  await page.getByRole('button', { name: 'Start Game' }).waitFor()
  assert.equal((await inspect(page)).keyExists, true)
  mark('navigation releases geometry, textures and audio; reentry creates a fresh game')

  const mobile = await browser.newContext({ viewport: { width: 390, height: 844 }, isMobile: true, hasTouch: true, deviceScaleFactor: 1 })
  await fixtures(mobile)
  const mobilePage = await mobile.newPage()
  trackErrors(mobilePage, errors)
  await mobilePage.goto(`${baseUrl}/game`)
  await mobilePage.getByRole('button', { name: 'Start Game' }).waitFor({ timeout: 30000 })
  await mobilePage.getByText('Desktop controls recommended').waitFor()
  assert.equal(await mobilePage.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth), true)
  await mobilePage.screenshot({ path: `${artifacts}/mobile.png` })
  await mobile.close()
  mark('touch viewport renders safely and displays Desktop controls recommended')

  // Drei reports this deliberately injected failure; keep all other console errors fatal.
  const errorsBeforePointerFailure = errors.length
  const expectedPointerFailure = 'THREE.PointerLockControls: Unable to use Pointer Lock API'
  const pointerFailureLog = page.waitForEvent('console', {
    predicate: (message) => message.type() === 'error' && message.text() === expectedPointerFailure,
  })
  await page.evaluate(() => document.dispatchEvent(new Event('pointerlockerror')))
  await pointerFailureLog
  await page.getByRole('alert').filter({ hasText: 'chưa cho phép giữ chuột' }).waitFor()
  assert.deepEqual(errors.slice(errorsBeforePointerFailure), [expectedPointerFailure])
  errors.splice(errorsBeforePointerFailure, 1)
  await page.getByRole('button', { name: 'Start Game' }).click()
  await page.waitForFunction(() => document.querySelector('.signal-hud')?.dataset.phase === 'playing')
  await page.keyboard.press('Space')
  await waitForScene(page, (view) => view.position[1] > 1.8)
  await page.evaluate(() => window.dispatchEvent(new Event('blur')))
  await page.getByRole('heading', { name: 'Tạm dừng.' }).waitFor()
  assert.equal(await page.evaluate(() => document.pointerLockElement), null)
  await page.locator('.signal-menu-audio').click()
  assert.equal(await page.locator('.signal-menu-audio').getAttribute('aria-pressed'), 'true')
  mark('pointer-lock failure has a working retry, Space jumps, blur pauses, mute works in the menu')

  const disabled = await chromium.launch({ executablePath: process.env.BROWSER_EXECUTABLE || undefined, headless: true, args: ['--disable-webgl', '--no-sandbox'] })
  try {
    const unavailable = await disabled.newContext()
    await fixtures(unavailable)
    const noGraphics = await unavailable.newPage()
    const unhandled = []
    noGraphics.on('pageerror', (error) => unhandled.push(error.message))
    await noGraphics.goto(`${baseUrl}/game`)
    await noGraphics.getByRole('alert').filter({ hasText: 'WebGL' }).waitFor()
    await noGraphics.getByRole('button', { name: 'Tải lại trang' }).waitFor()
    assert.deepEqual(unhandled, [])
  } finally { await disabled.close() }
  mark('disabled WebGL produces an actionable error overlay without an unhandled page error')

  const anonymous = await browser.newContext()
  await fixtures(anonymous, false)
  const login = await anonymous.newPage()
  trackErrors(login, errors)
  await login.goto(`${baseUrl}/game`)
  await login.waitForURL('**/login')
  await login.locator('input[type="password"]').first().waitFor()
  await anonymous.close()
  mark('existing authentication guard and login route remain intact')

  assert.deepEqual(errors, [], `Browser errors: ${errors.join('\n')}`)
  mark('no serious console or page errors')
} finally {
  await writeFile(`${artifacts}/report.json`, JSON.stringify({ passed, errors }, null, 2))
  await browser.close()
}
