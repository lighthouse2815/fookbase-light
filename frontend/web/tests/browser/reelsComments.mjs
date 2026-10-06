import assert from 'node:assert/strict'
import { execFileSync } from 'node:child_process'
import { mkdtemp, readFile, rm } from 'node:fs/promises'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import { postFixtures, postId, viewerId } from './postInteractionsFixture.mjs'

// Uses the existing external Playwright/Chromium and FFmpeg setup.
// PLAYWRIGHT_MODULE=/path/to/playwright/index.mjs REELS_BASE_URL=http://127.0.0.1:5183 node tests/browser/reelsComments.mjs
const { chromium } = await import(process.env.PLAYWRIGHT_MODULE ?? 'playwright')
const baseUrl = process.env.REELS_BASE_URL ?? 'http://127.0.0.1:5183'
const directory = await mkdtemp(join(tmpdir(), 'reels-comments-'))
const videoPath = join(directory, 'reel.webm')
execFileSync('ffmpeg', ['-hide_banner', '-loglevel', 'error', '-f', 'lavfi', '-i', 'color=c=black:s=90x160:r=1', '-t', '60', '-c:v', 'libvpx', videoPath])
const videoBody = await readFile(videoPath)
const browser = await chromium.launch({ headless: true, executablePath: process.env.CHROMIUM_EXECUTABLE, args: ['--no-sandbox'] })
const errors = []
const until = async (condition, message) => {
  for (let attempt = 0; attempt < 200; attempt++) {
    if (await condition()) return
    await new Promise((resolve) => setTimeout(resolve, 25))
  }
  assert.fail(message)
}

try {
  for (const viewport of [{ width: 1366, height: 900 }, { width: 820, height: 900 }, { width: 390, height: 844 }]) {
    const context = await browser.newContext({ viewport, reducedMotion: 'reduce' })
    const state = await postFixtures(context, { commentCount: 25, contentType: 'reel' })
    state.comments = Array.from({ length: 25 }, (_, index) => ({
      id: `comment-${index + 1}`, postId, authorUserId: index === 2 ? 'another-user' : viewerId,
      parentCommentId: index === 1 || index === 24 ? 'comment-1' : null,
      content: index === 1 ? 'Trả lời dài ' + 'abcdefghij'.repeat(40) : `Bình luận ${index + 1}`,
      createdAtUtc: `2026-10-01T08:00:${String(index).padStart(2, '0')}Z`, updatedAtUtc: null,
      reactionCounts: index === 0 ? { like: 2 } : {}, viewerReaction: null,
      author: { userId: index === 2 ? 'another-user' : viewerId, username: 'explorer', displayName: index === 2 ? 'Người khác' : 'Người kiểm tra', avatarUrl: null },
    }))
    const reel = {
      ...state.post, author: { userId: viewerId, username: 'explorer', displayName: 'Người kiểm tra', avatarUrl: null },
      caption: 'Reel kiểm tra bình luận', reactionCount: 3, viewCount: 0, completionCount: 0, viewerFollowsAuthor: false,
      video: { mediaId: 'video-1', durationMs: 60_000, width: 90, height: 160, contentType: 'video/webm' },
    }
    await context.route('**/api/reels**', (route) => {
      const path = new URL(route.request().url()).pathname
      if (path === '/api/reels') return route.fulfill({ json: { items: [reel, { ...reel, id: 'another-reel', commentCount: 0 }], nextCursor: null } })
      if (path.endsWith('/video/access')) return route.fulfill({ json: { url: `${baseUrl}/reel-comments-fixture.webm`, expiresAtUtc: '2099-01-01T00:00:00Z' } })
      if (path.endsWith('/poster/access')) return route.fulfill({ json: { url: 'data:image/gif;base64,R0lGODlhAQABAIAAAAAAAP///yH5BAEAAAAALAAAAAABAAEAAAIBRAA7', expiresAtUtc: '2099-01-01T00:00:00Z' } })
      if (path.endsWith('/views')) return route.fulfill({ status: 204 })
      return route.fallback()
    })
    await context.route('**/reel-comments-fixture.webm', (route) => route.fulfill({ contentType: 'video/webm', body: videoBody }))
    let failRead = false
    await context.route('**/api/posts/**', async (route) => {
      const request = route.request()
      const url = new URL(request.url())
      if (request.method() === 'GET' && url.pathname === `/api/posts/${postId}/comments` && failRead) {
        return route.fulfill({ status: 503, json: { detail: 'Không thể tải bình luận thử nghiệm.' } })
      }
      if (request.method() === 'DELETE' && url.pathname.startsWith('/api/posts/comments/') && !url.pathname.endsWith('/reaction')) {
        state.writes.push({ action: 'comment-delete', id: url.pathname.split('/').at(-1) })
        await new Promise((resolve) => setTimeout(resolve, state.actionDelay))
        state.comments = state.comments.filter((comment) => comment.id !== url.pathname.split('/').at(-1))
        state.post.commentCount = state.comments.length
        return route.fulfill({ status: 204 })
      }
      return route.fallback()
    })
    const page = await context.newPage()
    page.setDefaultTimeout(6000)
    page.on('pageerror', (error) => errors.push(error.message))
    await page.goto(`${baseUrl}/reels`)
    const card = page.locator('[data-reel-index="0"]')
    const open = card.getByRole('button', { name: 'Xem bình luận', exact: true })
    const panel = card.locator('.reel-comments')
    const root = panel.locator('[data-comment-id="comment-1"]')
    const reply = panel.locator('[data-comment-id="comment-2"]')
    const composer = panel.getByRole('textbox')
    const send = panel.getByRole('button', { name: 'Gửi', exact: true })
    const check = async (name, run) => { await run(); console.log(`PASS ${viewport.width}px: ${name}`) }

    await check('read failures can be retried; replies and author actions match Posts', async () => {
      failRead = true
      await open.click()
      await panel.getByText('Không thể tải bình luận thử nghiệm.').waitFor()
      failRead = false
      await panel.getByRole('button', { name: 'Thử lại', exact: true }).click()
      await root.waitFor()
      assert.equal(await panel.locator('[data-comment-id]').count(), 20)
      assert.equal(await root.locator('..').locator('[data-comment-id]').count(), 2, 'Replies must be grouped with their parent')
      const rootBounds = await root.boundingBox()
      const replyBounds = await reply.boundingBox()
      assert.ok(replyBounds.x > rootBounds.x, 'Replies must be indented')
      assert.equal(await root.getByRole('button', { name: 'Sửa', exact: true }).count(), 1)
      assert.equal(await root.getByRole('button', { name: 'Xóa', exact: true }).count(), 1)
      assert.equal(await panel.locator('[data-comment-id="comment-3"]').getByRole('button', { name: 'Xóa', exact: true }).count(), 0)
      assert.equal(await panel.evaluate((element) => element.scrollWidth <= element.clientWidth), true, 'Long comments and actions must fit the panel')
      assert.equal(await page.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth), true)
      await page.screenshot({ path: join(directory, `comments-${viewport.width}.png`) })
    })

    await check('pagination errors preserve loaded rows and retry without losing replies', async () => {
      failRead = true
      await panel.getByRole('button', { name: 'Tải thêm bình luận', exact: true }).click()
      await panel.getByRole('alert').waitFor()
      assert.equal(await panel.locator('[data-comment-id]').count(), 20)
      failRead = false
      await panel.getByRole('button', { name: 'Thử lại', exact: true }).click()
      await until(async () => await panel.locator('[data-comment-id]').count() === 25, 'All comments should load')
      assert.equal(await root.locator('..').locator('[data-comment-id]').count(), 3)
      assert.equal(await panel.getByRole('button', { name: 'Tải thêm bình luận', exact: true }).count(), 0)
    })

    await check('reply submission is immediate, counted once and targets the root comment', async () => {
      await reply.getByRole('button', { name: 'Trả lời', exact: true }).click()
      await composer.fill('Trả lời mới')
      await send.click()
      await until(async () => await panel.locator('[aria-busy="true"][data-comment-id]').count() === 1, 'The reply should appear before its response')
      assert.equal(await open.innerText(), '26')
      assert.equal(await send.isDisabled(), true)
      await panel.locator('[data-comment-id="comment-26"]').waitFor()
      const write = state.writes.find((item) => item.action === 'comment')
      assert.deepEqual(write, { action: 'comment', content: 'Trả lời mới', parentCommentId: 'comment-1' })
      assert.equal(await root.locator('..').locator('[data-comment-id]').count(), 4)
      assert.equal(await open.innerText(), '26')
      assert.equal(await composer.inputValue(), '')
      assert.equal(await panel.getByText('Đang trả lời', { exact: false }).count(), 0)
    })

    await check('failed submissions restore the count and preserve the draft', async () => {
      state.failComment = true
      await composer.fill('Bình luận chưa gửi được')
      await send.click()
      await until(async () => await send.isEnabled(), 'The composer should recover after failure')
      assert.equal(await open.innerText(), '26')
      assert.equal(await composer.inputValue(), 'Bình luận chưa gửi được')
      assert.equal(await panel.locator('[data-comment-id]').count(), 26)
      await page.getByRole('button', { name: 'Đóng thông báo', exact: true }).click()
      state.failComment = false
      await send.click()
      await composer.fill('Bản nháp tiếp theo')
      await panel.locator('[data-comment-id="comment-27"]').waitFor()
      assert.equal(await composer.inputValue(), 'Bản nháp tiếp theo', 'A successful response must not erase a newer draft')
      assert.equal(await open.innerText(), '27')
    })

    await check('comment reactions, edits and deletion use the shared Post behavior', async () => {
      await root.getByRole('button', { name: 'Thích', exact: true }).click()
      await page.getByRole('menuitemradio', { name: 'Thích', exact: true }).click()
      await root.getByRole('button', { name: 'Bỏ cảm xúc Thích', exact: true }).waitFor()
      await root.getByRole('button', { name: 'Bỏ cảm xúc Thích', exact: true }).click()
      await root.getByRole('button', { name: 'Thích', exact: true }).waitFor()
      await root.getByRole('button', { name: 'Sửa', exact: true }).click()
      let dialog = page.getByRole('dialog', { name: 'Chỉnh sửa bình luận', exact: true })
      await dialog.waitFor()
      await page.keyboard.press('Escape')
      assert.equal(await dialog.count(), 0)
      assert.equal(await open.getAttribute('aria-expanded'), 'true', 'Escape should close only the edit dialog')
      await root.getByRole('button', { name: 'Sửa', exact: true }).click()
      dialog = page.getByRole('dialog', { name: 'Chỉnh sửa bình luận', exact: true })
      await dialog.getByRole('textbox').fill('Bình luận đã sửa')
      await dialog.getByRole('button', { name: 'Lưu', exact: true }).click()
      await dialog.waitFor({ state: 'hidden' })
      assert.match(await root.innerText(), /Bình luận đã sửa/)
      await root.getByRole('button', { name: 'Xóa', exact: true }).click()
      dialog = page.getByRole('dialog', { name: 'Xóa bình luận?', exact: true })
      await dialog.getByRole('button', { name: 'Hủy', exact: true }).click()
      assert.equal(await root.count(), 1, 'Canceling deletion must preserve the comment')
      await root.getByRole('button', { name: 'Xóa', exact: true }).click()
      await dialog.getByRole('button', { name: 'Xóa', exact: true }).click()
      await dialog.waitFor({ state: 'hidden' })
      assert.equal(await root.count(), 0)
      assert.equal(await reply.count(), 1, 'Deleting a root must preserve its replies, matching the backend')
      assert.equal(await open.innerText(), '26')
    })

    await check('closing and switching Reels retain comments without leaking them to another video', async () => {
      await page.keyboard.press('Escape')
      assert.equal(await open.getAttribute('aria-expanded'), 'false')
      assert.equal(await open.evaluate((element) => element === document.activeElement), true)
      await page.getByRole('button', { name: 'Reel tiếp theo', exact: true }).click()
      const second = page.locator('[data-reel-index="1"]')
      await second.getByRole('button', { name: 'Xem bình luận', exact: true }).click()
      await second.getByText('Chưa có bình luận nào.', { exact: true }).waitFor()
      assert.equal(await second.locator('[data-comment-id]').count(), 0)
      await page.keyboard.press('Escape')
      await page.getByRole('button', { name: 'Reel trước', exact: true }).click()
      await open.click()
      assert.equal(await panel.locator('[data-comment-id]').count(), 26)
      assert.equal(await composer.inputValue(), 'Bản nháp tiếp theo')
    })
    await context.close()
  }
  assert.deepEqual(errors, [])
} finally {
  await browser.close()
  await rm(directory, { recursive: true, force: true })
}
