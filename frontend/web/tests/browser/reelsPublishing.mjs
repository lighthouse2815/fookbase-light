import assert from 'node:assert/strict'
import { fixtures } from './lastSignalFixture.mjs'

// PLAYWRIGHT_MODULE=/path/to/playwright/index.mjs REELS_BASE_URL=http://127.0.0.1:5184 node tests/browser/reelsPublishing.mjs
const { chromium } = await import(process.env.PLAYWRIGHT_MODULE ?? 'playwright')
const baseUrl = process.env.REELS_BASE_URL ?? 'http://127.0.0.1:5184'
const browser = await chromium.launch({ headless: true, executablePath: process.env.CHROMIUM_EXECUTABLE, args: ['--no-sandbox'] })
const until = async (condition, message) => {
  for (let attempt = 0; attempt < 150; attempt++) {
    if (await condition()) return
    await new Promise((resolve) => setTimeout(resolve, 30))
  }
  assert.fail(message)
}

async function scenario(run) {
  const context = await browser.newContext()
  await fixtures(context)
  const state = { status: 'Processing', polls: 0, publications: [], failPublication: false, holdMetadata: false, releaseMetadata: null }
  const metadata = () => ({
    id: 'video-1', ownerUserId: '00000000-0000-0000-0000-000000000007', mediaType: 'video',
    status: state.status, fileName: 'reel.mp4', contentType: 'video/mp4', declaredSizeBytes: 12,
    actualSizeBytes: 12, durationMs: state.status === 'Ready' ? 58_027 : null,
    width: 720, height: 1280, hasProcessedVideo: state.status === 'Ready',
  })
  await context.route('**/api/media/**', async (route) => {
    const path = new URL(route.request().url()).pathname
    if (path.endsWith('/uploads')) return route.fulfill({ json: {
      mediaId: 'video-1', uploadUrl: `${baseUrl}/reel-upload-fixture`, uploadMethod: 'POST', uploadParameters: {},
    } })
    if (path.endsWith('/complete')) return route.fulfill({ json: metadata() })
    if (path.endsWith('/access')) return route.fulfill({ json: {
      url: 'data:image/gif;base64,R0lGODlhAQABAIAAAAAAAP///yH5BAEAAAAALAAAAAABAAEAAAIBRAA7', expiresAtUtc: '2099-01-01T00:00:00Z',
    } })
    state.polls++
    if (state.holdMetadata) await new Promise((resolve) => { state.releaseMetadata = resolve })
    return route.fulfill({ json: metadata() })
  })
  await context.route('**/reel-upload-fixture', (route) => route.fulfill({ status: 200, body: '{}' }))
  await context.route('**/api/reels', async (route) => {
    if (route.request().method() !== 'POST') return route.fulfill({ json: { items: [], nextCursor: null } })
    state.publications.push(route.request().postDataJSON())
    if (state.failPublication) return route.fulfill({ status: 503, json: { detail: 'Hãy thử đăng lại.' } })
    return route.fulfill({ json: {
      id: 'new-reel', caption: 'Reel mới', privacy: 'friends', createdAtUtc: new Date().toISOString(),
      author: { userId: 'user-1', username: 'creator', displayName: 'Creator', avatarUrl: null },
      video: { mediaId: 'video-1', durationMs: 58_027, width: 720, height: 1280 },
      reactionCounts: {}, reactionCount: 0, commentCount: 0, viewCount: 0, completionCount: 0,
    } })
  })
  const page = await context.newPage()
  const errors = []
  page.on('pageerror', (error) => errors.push(error.message))
  try {
    await page.goto(`${baseUrl}/reels`)
    await page.getByRole('button', { name: '＋ Tạo Reel', exact: true }).click()
    const dialog = page.getByRole('dialog', { name: 'Tạo Reel' })
    const chooseVideo = () => dialog.locator('input[type=file]').setInputFiles({
      name: 'reel.mp4', mimeType: 'video/mp4', buffer: Buffer.from('video-fixture'),
    })
    await run({ page, dialog, state, chooseVideo })
    assert.deepEqual(errors, [])
  } finally {
    await context.close()
  }
}

try {
  await scenario(async ({ dialog, state, chooseVideo }) => {
    await chooseVideo()
    await dialog.getByText(/Đang xử lý video/).waitFor()
    const publish = dialog.getByRole('button', { name: 'Xuất bản Reel', exact: true })
    assert.equal(await publish.isEnabled(), true, 'An uploaded video should allow requesting publication while it is processing')
    await dialog.locator('textarea').fill('  Reel mới  ')
    await dialog.locator('select').selectOption('friends')
    await publish.click()
    await dialog.getByRole('button', { name: 'Đang chờ video…', exact: true }).waitFor()
    const initialPolls = state.polls
    await until(() => state.polls > initialPolls, 'Publication should wait for processing metadata')
    assert.equal(state.publications.length, 0, 'A processing video must never be sent to the Reel API')
    state.status = 'Ready'
    await dialog.waitFor({ state: 'hidden' })
    assert.deepEqual(state.publications, [{ caption: 'Reel mới', privacy: 'friends', videoMediaId: 'video-1' }])
    console.log('PASS processing video is published once after it becomes ready')
  })

  await scenario(async ({ dialog, state, chooseVideo }) => {
    state.status = 'Ready'
    await chooseVideo()
    await dialog.getByText('Video đã sẵn sàng để xuất bản.', { exact: true }).waitFor()
    await dialog.getByRole('button', { name: 'Xuất bản Reel', exact: true }).click()
    await dialog.waitFor({ state: 'hidden' })
    assert.equal(state.publications.length, 1)
    assert.equal(state.polls, 0, 'A ready video should not need another processing poll')
    console.log('PASS ready video is published immediately')
  })

  await scenario(async ({ dialog, state, chooseVideo }) => {
    await chooseVideo()
    await dialog.getByText(/Đang xử lý video/).waitFor()
    await dialog.getByRole('button', { name: 'Xuất bản Reel', exact: true }).click()
    state.status = 'Failed'
    await dialog.getByText('Xử lý video thất bại. Hãy chọn video khác.', { exact: true }).first().waitFor()
    assert.equal(state.publications.length, 0)
    assert.equal(await dialog.getByRole('button', { name: 'Xuất bản Reel', exact: true }).isDisabled(), true)
    assert.equal(await dialog.getByRole('button', { name: 'Chọn video', exact: true }).isEnabled(), true)
    console.log('PASS failed processing never publishes and permits another video')
  })

  await scenario(async ({ dialog, state, chooseVideo }) => {
    state.status = 'Ready'
    state.failPublication = true
    await chooseVideo()
    await dialog.getByText('Video đã sẵn sàng để xuất bản.', { exact: true }).waitFor()
    await dialog.getByRole('button', { name: 'Xuất bản Reel', exact: true }).click()
    await dialog.getByText('Hãy thử đăng lại.', { exact: true }).waitFor()
    state.failPublication = false
    await dialog.getByRole('button', { name: 'Xuất bản Reel', exact: true }).click()
    await dialog.waitFor({ state: 'hidden' })
    assert.equal(state.publications.length, 2)
    assert.deepEqual(state.publications[0], state.publications[1])
    console.log('PASS publication can be retried without uploading again')
  })

  await scenario(async ({ page, dialog, state, chooseVideo }) => {
    await chooseVideo()
    await dialog.getByText(/Đang xử lý video/).waitFor()
    await dialog.getByRole('button', { name: 'Xuất bản Reel', exact: true }).click()
    await page.goto(`${baseUrl}/games`)
    state.status = 'Ready'
    await new Promise((resolve) => setTimeout(resolve, 2_500))
    assert.equal(state.publications.length, 0, 'Leaving the page must cancel a waiting publication')
    console.log('PASS navigating away cancels waiting publication')
  })

  await scenario(async ({ dialog, state, chooseVideo }) => {
    await chooseVideo()
    await dialog.getByText(/Đang xử lý video/).waitFor()
    state.holdMetadata = true
    await dialog.getByRole('button', { name: 'Xuất bản Reel', exact: true }).click()
    await until(() => state.releaseMetadata !== null, 'Publication should be awaiting the metadata response')
    await dialog.getByRole('button', { name: 'Đóng', exact: true }).click()
    await dialog.waitFor({ state: 'hidden' })
    state.status = 'Ready'
    state.releaseMetadata()
    await new Promise((resolve) => setTimeout(resolve, 500))
    assert.equal(state.publications.length, 0, 'A metadata response arriving after closing must never publish')
    console.log('PASS closing the dialog cancels an in-flight wait for processing')
  })
} finally {
  await browser.close()
}
