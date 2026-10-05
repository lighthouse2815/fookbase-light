import assert from 'node:assert/strict'
import { fixtures } from './lastSignalFixture.mjs'
import { viewerId } from './postInteractionsFixture.mjs'
const { chromium } = await import(process.env.PLAYWRIGHT_MODULE ?? 'playwright')
const baseUrl = process.env.ALBUM_BASE_URL ?? process.env.FEED_BASE_URL ?? 'http://127.0.0.1:5196'
const pixel = 'data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+jWZkAAAAASUVORK5CYII='
const album = { id: 'album-one', ownerUserId: viewerId, name: 'Album kiểm tra', description: null, albumType: 'custom', privacy: 'public', photoCount: 2, previewUrl: null, createdAtUtc: '2026-10-01T08:00:00Z', updatedAtUtc: '2026-10-01T08:00:00Z', canManage: true }
const photo = (id, caption = '') => ({ mediaId: id, caption, sortOrder: 0, addedAtUtc: album.createdAtUtc, accessUrl: pixel })
const browser = await chromium.launch({ headless: true, args: ['--no-sandbox'] })
async function setup() {
  const context = await browser.newContext()
  await fixtures(context)
  const state = { albums: [], photos: [photo('photo-one', 'Ảnh một'), photo('photo-two', 'Ảnh hai')], creates: 0, uploads: [], adds: [], writes: [], failUpload: '', failAdd: false, commitAddFailure: false, commitDeleteFailure: false, failDetail: false, failCreate: false, failList: false, failInitial: false, failPage: false, failWrite: false, detailDelay: {}, detailResponses: [], readDelay: 0, albumDelay: 0, albumResponses: 0 }
  await context.route('**/album-upload/**', route => route.fulfill({ status: 200, body: '' }))
  await context.route('**/api/**', async route => {
    const req = route.request(); const url = new URL(req.url()); const path = url.pathname
    const fail = () => route.fulfill({ status: 503, json: { detail: 'Lỗi máy chủ thử nghiệm.' } })
    const delay = ms => new Promise(resolve => setTimeout(resolve, ms))
    if (path === '/api/albums' && req.method() === 'POST') {
      state.creates++; await delay(200); if (state.failCreate) return fail()
      const created = { ...album, ...req.postDataJSON(), photoCount: 0 }; state.albums.push(created)
      return route.fulfill({ json: created })
    }
    if (/\/api\/users\/[^/]+\/albums$/.test(path)) {
      if (state.failList) return fail()
      const snapshot = structuredClone(state.albums); await delay(state.readDelay)
      return route.fulfill({ json: { items: snapshot, nextCursor: null } })
    }
    if (path === '/api/media/uploads') {
      const body = req.postDataJSON(); state.uploads.push(body.fileName)
      if (body.fileName === state.failUpload) { state.failUpload = ''; return fail() }
      const mediaId = `upload-${body.fileName}`
      return route.fulfill({ json: { mediaId, uploadUrl: `${baseUrl}/album-upload/${mediaId}`, uploadMethod: 'POST', uploadParameters: {}, expiresAtUtc: '2099-01-01T00:00:00Z' } })
    }
    if (/\/api\/media\/.*\/complete$/.test(path)) return route.fulfill({ json: { id: path.split('/')[3], ownerUserId: viewerId, mediaType: 'image', status: 'ready', fileName: 'image.png', contentType: 'image/png', declaredSizeBytes: 1, actualSizeBytes: 1, createdAtUtc: album.createdAtUtc, uploadExpiresAtUtc: null, uploadedAtUtc: album.createdAtUtc, deletedAtUtc: null, durationMs: null, width: 1, height: 1, hasProcessedVideo: false, processedAtUtc: null } })
    if (path === '/api/albums/album-one') { await delay(state.albumDelay); state.albumResponses++; return state.failInitial ? fail() : route.fulfill({ json: album }) }
    if (path === '/api/albums/album-one/media') {
      if (req.method() === 'POST') {
        const { mediaId } = req.postDataJSON(); state.adds.push(mediaId)
        if (state.failAdd) { state.failAdd = false; return fail() }
        const added = photo(mediaId); if (!state.photos.some(p => p.mediaId === mediaId)) state.photos.push(added)
        state.albums.forEach(a => { a.photoCount++ }); if (state.commitAddFailure) { state.commitAddFailure = false; return fail() }; return route.fulfill({ json: added })
      }
      const next = url.searchParams.get('cursor')
      if (state.failInitial || (next && state.failPage)) return fail()
      return route.fulfill({ json: { items: next ? [photo('photo-three', 'Ảnh ba')] : state.photos, nextCursor: next ? null : 'page-two' } })
    }
    if (path.startsWith('/api/albums/album-one/media/')) {
      const id = path.split('/').at(-1); const current = state.photos.find(p => p.mediaId === id)
      if (req.method() !== 'GET') {
        state.writes.push({ id, method: req.method(), body: req.postDataJSON() }); await delay(200)
        if (state.failWrite) return fail()
        if (req.method() === 'DELETE') { if (!current) return route.fulfill({ status: 404, json: { detail: 'Ảnh không tồn tại.' } }); state.photos = state.photos.filter(p => p.mediaId !== id); if (state.commitDeleteFailure) { state.commitDeleteFailure = false; return fail() }; return route.fulfill({ status: 204 }) }
        current.caption = req.postDataJSON().caption; return route.fulfill({ json: current })
      }
      if (state.failDetail) return fail()
      if (!current) return route.fulfill({ status: 404, json: { detail: 'Ảnh không tồn tại.' } })
      await delay(state.detailDelay[id] ?? 0)
      await route.fulfill({ json: { ...current, albumId: album.id, ownerUserId: viewerId, url: `${pixel}#${id}` } }); state.detailResponses.push(id); return
    }
    return route.fallback()
  })
  const page = await context.newPage(); page.setDefaultTimeout(3000)
  const errors = []; page.on('pageerror', e => errors.push(e.message))
  return { context, page, state, errors }
}
const tests = [
  ['uncertain create failure does not automatically create again', async ({ page, state }) => {
    state.failCreate = true; await page.goto(`${baseUrl}/photos`); await page.getByPlaceholder('Tên album').fill('Album mới')
    await page.getByRole('button', { name: 'Tạo album', exact: true }).click(); await page.getByRole('alert').waitFor()
    assert.equal(state.creates, 1); assert.equal(await page.getByRole('button', { name: 'Tạo album', exact: true }).isDisabled(), true)
    await page.getByRole('button', { name: 'Tải lại danh sách', exact: true }).click(); assert.equal(state.creates, 1)
  }],
  ['uncertain create can recover only after list verification and explicit reset', async ({ page, state }) => {
    state.failCreate = true; await page.goto(`${baseUrl}/photos`); await page.getByPlaceholder('Tên album').fill('Album chưa xác nhận')
    await page.getByRole('button', { name: 'Tạo album', exact: true }).click(); await page.getByRole('alert').waitFor()
    assert.equal(await page.getByRole('button', { name: 'Bắt đầu album mới', exact: true }).count(), 0)
    state.failList = true; await page.getByRole('button', { name: 'Tải lại danh sách', exact: true }).click()
    await page.getByRole('alert').filter({ hasText: 'danh sách album' }).waitFor()
    assert.equal(await page.getByRole('button', { name: 'Bắt đầu album mới', exact: true }).count(), 0)
    state.failList = false; await page.getByRole('button', { name: 'Tải lại danh sách', exact: true }).click()
    await page.getByRole('button', { name: 'Bắt đầu album mới', exact: true }).click()
    assert.equal(state.creates, 1); assert.equal(await page.getByPlaceholder('Tên album').inputValue(), '')
    assert.equal(await page.getByRole('button', { name: 'Tạo album', exact: true }).isDisabled(), false)
    state.failCreate = false; await page.getByPlaceholder('Tên album').fill('Album tiếp theo')
    await page.getByRole('button', { name: 'Tạo album', exact: true }).click()
    await page.getByRole('status').filter({ hasText: 'Đã tạo album' }).waitFor(); assert.equal(state.creates, 2)
  }],
  ['oversized image is rejected before album creation and remains replaceable', async ({ page, state }) => {
    await page.goto(`${baseUrl}/photos`); await page.getByPlaceholder('Tên album').fill('Album ảnh lớn')
    await page.locator('input[type=file]').setInputFiles({ name: 'large.png', mimeType: 'image/png', buffer: Buffer.alloc(20 * 1024 * 1024 + 1) }, { timeout: 15_000 })
    await page.getByRole('button', { name: 'Tạo album', exact: true }).click(); await page.getByRole('alert').waitFor()
    assert.equal(state.creates, 0); assert.equal(await page.locator('input[type=file]').isDisabled(), false)
    await page.locator('input[type=file]').setInputFiles({ name: 'valid.png', mimeType: 'image/png', buffer: Buffer.from('image') })
    await page.getByRole('button', { name: 'Tạo album', exact: true }).click()
    await page.getByRole('status').filter({ hasText: 'Đã tạo album' }).waitFor(); assert.equal(state.creates, 1)
  }],
  ['retry uploads only unfinished files in the same album and blocks repeated submit', async ({ page, state }) => {
    state.failUpload = 'two.png'
    await page.goto(`${baseUrl}/photos`)
    await page.getByPlaceholder('Tên album').fill('Album mới')
    await page.locator('input[type=file]').setInputFiles(['one.png', 'two.png'].map(name => ({ name, mimeType: 'image/png', buffer: Buffer.from('image') })))
    await page.locator('main form').evaluate(form => { form.requestSubmit(); form.requestSubmit() })
    await page.getByRole('alert').waitFor()
    assert.equal(state.creates, 1, 'duplicate submit must not create a second album')
    assert.equal(await page.getByPlaceholder('Tên album').isDisabled(), true)
    await page.getByRole('button', { name: 'Thử lại ảnh còn lại', exact: true }).click()
    await page.getByRole('status').filter({ hasText: 'Đã tạo album' }).waitFor()
    assert.deepEqual(state.uploads, ['one.png', 'two.png', 'two.png'])
    assert.deepEqual(state.adds, ['upload-one.png', 'upload-two.png'])
    assert.equal(state.creates, 1)
  }],
  ['retry an add failure reuses the already uploaded media ID', async ({ page, state }) => {
    state.failAdd = true
    await page.goto(`${baseUrl}/photos`); await page.getByPlaceholder('Tên album').fill('Album mới')
    await page.locator('input[type=file]').setInputFiles({ name: 'one.png', mimeType: 'image/png', buffer: Buffer.from('image') })
    await page.getByRole('button', { name: 'Tạo album', exact: true }).click(); await page.getByRole('alert').waitFor()
    await page.getByRole('button', { name: 'Thử lại ảnh còn lại', exact: true }).click()
    await page.getByRole('status').filter({ hasText: 'Đã tạo album' }).waitFor()
    assert.deepEqual(state.uploads, ['one.png']); assert.deepEqual(state.adds, ['upload-one.png', 'upload-one.png']); assert.equal(state.creates, 1)
  }],
  ['create success survives album list refresh failure', async ({ page, state }) => {
    await page.goto(`${baseUrl}/photos`); await page.getByPlaceholder('Tên album').fill('Album đã lưu'); state.failList = true
    await page.getByRole('button', { name: 'Tạo album', exact: true }).click()
    await page.getByRole('status').filter({ hasText: 'Đã tạo album' }).waitFor()
    await page.getByRole('link', { name: /Album đã lưu/ }).waitFor(); assert.equal(await page.getByPlaceholder('Tên album').inputValue(), '')
  }],
  ['initial album error offers retry and pagination error preserves photos', async ({ page, state }) => {
    state.failInitial = true; await page.goto(`${baseUrl}/albums/album-one`); await page.getByRole('alert').waitFor()
    state.failInitial = false; await page.getByRole('button', { name: 'Thử lại', exact: true }).click(); await page.getByRole('button', { name: 'Xem ảnh', exact: true }).first().waitFor()
    state.failPage = true; await page.getByRole('button', { name: 'Xem thêm', exact: true }).click(); await page.getByRole('alert').waitFor()
    assert.equal(await page.getByRole('button', { name: 'Xem ảnh', exact: true }).count(), 2)
    state.failPage = false; await page.getByRole('button', { name: 'Thử lại', exact: true }).click()
    await page.getByRole('img', { name: 'Ảnh ba', exact: true }).waitFor()
  }],
  ['caption failure preserves draft and retries without duplicate writes', async ({ page, state }) => {
    state.failWrite = true; await page.goto(`${baseUrl}/albums/album-one`); await page.getByRole('button', { name: 'Chú thích', exact: true }).first().click()
    const dialog = page.getByRole('dialog'); await dialog.getByRole('textbox').fill('Chú thích mới')
    await dialog.locator('form').evaluate(form => { form.requestSubmit(); form.requestSubmit() }); await dialog.getByRole('alert').waitFor()
    assert.equal(state.writes.length, 1); assert.equal(await dialog.getByRole('textbox').inputValue(), 'Chú thích mới')
    state.failWrite = false; await dialog.getByRole('button', { name: 'Lưu', exact: true }).click(); await dialog.waitFor({ state: 'hidden' })
    await page.getByRole('img', { name: 'Chú thích mới', exact: true }).waitFor()
  }],
  ['delete requires confirmation and failure leaves photo present', async ({ page, state }) => {
    state.failWrite = true; await page.goto(`${baseUrl}/albums/album-one`); await page.getByRole('button', { name: 'Xóa', exact: true }).first().click()
    const dialog = page.getByRole('dialog'); await dialog.waitFor(); assert.equal(state.writes.length, 0)
    await dialog.getByRole('button', { name: 'Xóa ảnh', exact: true }).click(); await dialog.getByRole('alert').waitFor()
    assert.equal(await page.getByRole('button', { name: 'Xem ảnh', exact: true }).count(), 2)
    state.failWrite = false; await dialog.getByRole('button', { name: 'Xóa ảnh', exact: true }).click(); await dialog.waitFor({ state: 'hidden' })
    assert.equal(await page.getByRole('button', { name: 'Xem ảnh', exact: true }).count(), 1)
  }],
  ['an add committed before response failure is reconciled without adding twice', async ({ page, state }) => {
    state.commitAddFailure = true
    await page.goto(`${baseUrl}/photos`); await page.getByPlaceholder('Tên album').fill('Album đã thêm ảnh')
    await page.locator('input[type=file]').setInputFiles({ name: 'one.png', mimeType: 'image/png', buffer: Buffer.from('image') })
    await page.getByRole('button', { name: 'Tạo album', exact: true }).click(); await page.getByRole('alert').waitFor()
    await page.getByRole('button', { name: 'Thử lại ảnh còn lại', exact: true }).click()
    await page.getByRole('status').filter({ hasText: 'Đã tạo album' }).waitFor()
    assert.deepEqual(state.adds, ['upload-one.png']); assert.deepEqual(state.uploads, ['one.png'])
    await page.getByRole('link', { name: /Album đã thêm ảnh.*1 ảnh/ }).waitFor()
  }],
  ['a delete committed before response failure treats retry 404 as confirmed deletion', async ({ page, state }) => {
    state.commitDeleteFailure = true; await page.goto(`${baseUrl}/albums/album-one`)
    await page.getByRole('button', { name: 'Xóa', exact: true }).first().click()
    const dialog = page.getByRole('dialog'); await dialog.getByRole('button', { name: 'Xóa ảnh', exact: true }).click()
    await dialog.getByRole('alert').waitFor(); assert.equal(await page.getByRole('button', { name: 'Xem ảnh', exact: true }).count(), 2)
    await dialog.getByRole('button', { name: 'Xóa ảnh', exact: true }).click(); await dialog.waitFor({ state: 'hidden' })
    assert.equal(await page.getByRole('button', { name: 'Xem ảnh', exact: true }).count(), 1)
  }],
  ['photo detail retry keeps the thumbnail and dialog restores keyboard focus', async ({ page, state }) => {
    state.failDetail = true; await page.goto(`${baseUrl}/albums/album-one`)
    const opener = page.getByRole('button', { name: 'Xem ảnh', exact: true }).first(); await opener.click()
    const dialog = page.getByRole('dialog'); await dialog.getByRole('alert').waitFor()
    assert.equal(await dialog.locator('img').getAttribute('src'), pixel)
    state.failDetail = false; await dialog.getByRole('button', { name: 'Thử lại', exact: true }).click()
    await page.waitForFunction(() => document.querySelector('[role=dialog] img')?.src.endsWith('#photo-one'))
    await page.keyboard.press('Escape'); await dialog.waitFor({ state: 'hidden' })
    assert.equal(await opener.evaluate(button => button === document.activeElement), true)
    await page.getByRole('button', { name: 'Chú thích', exact: true }).first().click()
    const caption = page.getByRole('dialog'); assert.equal(await caption.getByRole('textbox').evaluate(input => input === document.activeElement), true)
    await page.keyboard.press('Escape'); await caption.waitFor({ state: 'hidden' })
  }],
  ['account switch discards an unfinished upload batch and old completion', async ({ page, state }) => {
    await page.goto(`${baseUrl}/photos`); await page.getByPlaceholder('Tên album').fill('Bản nháp tài khoản cũ')
    await page.getByRole('button', { name: 'Tạo album', exact: true }).click()
    await page.waitForFunction(() => document.querySelector('main form')?.getAttribute('aria-busy') === 'true')
    await page.evaluate(() => {
      const session = JSON.parse(localStorage.getItem('fookbase.session')); session.user.id = 'account-two'
      localStorage.setItem('fookbase.session', JSON.stringify(session)); window.dispatchEvent(new Event('fookbase.auth-session-changed'))
    })
    assert.equal(await page.getByPlaceholder('Tên album').inputValue(), '')
    await page.waitForTimeout(300)
    assert.equal(await page.getByPlaceholder('Tên album').inputValue(), '')
    assert.equal(await page.getByRole('status').filter({ hasText: 'Đã tạo album' }).count(), 0)
    assert.equal(state.uploads.length, 0)
  }],
  ['late album response cannot populate another owner list', async ({ page, state }) => {
    state.albumDelay = 600; await page.goto(`${baseUrl}/albums/album-one`)
    await page.getByRole('link', { name: '← Ảnh', exact: true }).click()
    await page.getByRole('heading', { name: 'Ảnh và album', exact: true }).waitFor()
    await page.waitForFunction(() => document.querySelector('main')?.textContent.includes('Chưa có album ảnh.'))
    await page.waitForTimeout(700); assert.ok(state.albumResponses > 0)
    assert.equal(await page.getByRole('img', { name: 'Ảnh một', exact: true }).count(), 0)
    assert.equal(await page.getByRole('heading', { name: 'Album kiểm tra', exact: true }).count(), 0)
  }],
  ['viewer restores original thumbnail focus after next photo', async ({ page }) => {
    await page.goto(`${baseUrl}/albums/album-one`)
    const opener = page.getByRole('button', { name: 'Xem ảnh', exact: true }).first(); await opener.click()
    await page.getByRole('dialog').getByRole('button', { name: 'Ảnh tiếp', exact: true }).click()
    await page.keyboard.press('Escape'); await page.getByRole('dialog').waitFor({ state: 'hidden' })
    assert.equal(await opener.evaluate(button => button === document.activeElement), true)
    assert.equal(await page.evaluate(() => document.body.style.overflow), '')
  }],
  ['late photo detail cannot overwrite newly selected photo', async ({ page, state }) => {
    state.detailDelay['photo-one'] = 600
    await page.goto(`${baseUrl}/albums/album-one`); await page.getByRole('button', { name: 'Xem ảnh', exact: true }).first().click()
    const dialog = page.getByRole('dialog'); await dialog.getByRole('button', { name: /→|Ảnh tiếp/ }).click()
    await page.waitForFunction(() => document.querySelector('[role=dialog] img')?.src.endsWith('#photo-two'))
    await page.waitForTimeout(700)
    assert.match(await dialog.locator('img').getAttribute('src'), /#photo-two$/)
  }],
]
let failures = 0
try {
  for (const [name, run] of tests) {
    if (process.env.ALBUM_TEST && !name.includes(process.env.ALBUM_TEST)) continue
    const fixture = await setup()
    try { await run(fixture); assert.deepEqual(fixture.errors, []); console.log(`PASS ${name}`) }
    catch (error) { failures++; console.error(`FAIL ${name}\n${error.stack}`) }
    finally { await fixture.context.close() }
  }
} finally { await browser.close() }
if (failures) process.exitCode = 1
