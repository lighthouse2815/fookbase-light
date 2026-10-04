import assert from 'node:assert/strict'
import test from 'node:test'
import {
  clampImagePan,
  clampMediaIndex,
  getNeighborImageUrls,
  getPhotoSwipeDirection,
  getSelectedMediaIndex,
  isEditableTarget,
} from '../src/pages/feed/components/postPhotoLightbox.ts'

test('media navigation clamps at both ends without wrapping and handles missing media', () => {
  assert.equal(clampMediaIndex(0, 5), 0)
  assert.equal(clampMediaIndex(3, 5), 3)
  assert.equal(clampMediaIndex(-1, 5), 0)
  assert.equal(clampMediaIndex(5, 5), 4)
  assert.equal(clampMediaIndex(20, 5), 4)
  assert.equal(clampMediaIndex(1, 1), 0)
  assert.equal(clampMediaIndex(-1, 0), -1)
  assert.equal(clampMediaIndex(0, 0), -1)
  assert.equal(clampMediaIndex(Number.NaN, 5), 0)
  assert.equal(clampMediaIndex(2.8, 5), 2)
})

test('rapid navigation uses the latest index and clamps after media removal', () => {
  let index = 0
  for (let i = 0; i < 10; i++) index = clampMediaIndex(index + 1, 5)
  assert.equal(index, 4)
  index = clampMediaIndex(index, 2)
  assert.equal(index, 1)
  for (let i = 0; i < 10; i++) index = clampMediaIndex(index - 1, 2)
  assert.equal(index, 0)
})

test('opening a later attachment retains that attachment index in a mixed media list', () => {
  const media = [
    { mediaId: 'first-photo', mediaType: 'image' },
    { mediaId: 'video', mediaType: 'video' },
    { mediaId: 'clicked-photo', mediaType: 'image' },
  ]
  const clickedIndex = media.findIndex((item) => item.mediaId === 'clicked-photo')
  const index = clampMediaIndex(clickedIndex, media.length)
  assert.equal(index, 2)
  assert.equal(media[index].mediaId, 'clicked-photo')
})

test('selection follows the media ID across reorder, earlier removal and signed URL refresh', () => {
  const selection = { mediaId: 'selected-photo', index: 2 }
  assert.equal(getSelectedMediaIndex([
    { mediaId: 'selected-photo' }, { mediaId: 'first-photo' }, { mediaId: 'video' },
  ], selection), 0)
  assert.equal(getSelectedMediaIndex([
    { mediaId: 'video' }, { mediaId: 'selected-photo', url: 'https://authorized.test/refreshed' },
  ], selection), 1)
  assert.equal(getSelectedMediaIndex([], null), -1)
})

test('removed selection falls back to a valid nearby index and navigates from the displayed image', () => {
  const media = [{ mediaId: 'first-photo' }, { mediaId: 'second-photo' }]
  const selection = { mediaId: 'removed-photo', index: 4 }
  const displayedIndex = getSelectedMediaIndex(media, selection)
  assert.equal(displayedIndex, 1)
  const previousIndex = clampMediaIndex(displayedIndex - 1, media.length)
  assert.equal(previousIndex, 0)
  assert.equal(media[previousIndex].mediaId, 'first-photo')
  assert.equal(getSelectedMediaIndex([], selection), -1)
})

test('preloading includes only the immediate neighboring image URLs', () => {
  const media = Array.from({ length: 5 }, (_, index) => ({ url: `https://authorized.test/${index}`, mediaType: 'image' }))
  assert.deepEqual(getNeighborImageUrls(media, 2), ['https://authorized.test/1', 'https://authorized.test/3'])
  assert.deepEqual(getNeighborImageUrls(media, 0), ['https://authorized.test/1'])
  assert.deepEqual(getNeighborImageUrls(media, 4), ['https://authorized.test/3'])
  assert.deepEqual(getNeighborImageUrls([], 0), [])
  assert.deepEqual(getNeighborImageUrls(media, -1), [])
  assert.deepEqual(getNeighborImageUrls(media, 5), [])
})

test('preloading deduplicates neighboring URLs and excludes videos, current URL and missing URLs', () => {
  const image = { url: 'https://authorized.test/image', mediaType: 'image' }
  const current = { url: 'https://authorized.test/current', mediaType: 'image' }
  assert.deepEqual(getNeighborImageUrls([image, current, image], 1), [image.url])
  assert.deepEqual(getNeighborImageUrls([current, current, current], 1), [])
  assert.deepEqual(getNeighborImageUrls([{ ...image, mediaType: 'video' }, current, undefined], 1), [])
  assert.deepEqual(getNeighborImageUrls([{ ...image, url: '  ' }, current, { ...image, url: '' }], 1), [])
  assert.deepEqual(getNeighborImageUrls([image, undefined, image], 1), [])
})

test('zoomed image pan clamps against both edges of the actual rendered image', () => {
  const image = { width: 600, height: 400 }
  const viewport = { width: 800, height: 500 }
  assert.deepEqual(clampImagePan({ x: 999, y: -999 }, 2, image, viewport), { x: 200, y: -150 })
  assert.deepEqual(clampImagePan({ x: -999, y: 999 }, 2, image, viewport), { x: -200, y: 150 })
  assert.deepEqual(clampImagePan({ x: 75, y: -50 }, 2, image, viewport), { x: 75, y: -50 })
  assert.deepEqual(clampImagePan({ x: 999, y: -999 }, 1, image, viewport), { x: 0, y: 0 })
})

test('portrait and small images stay centered along axes that do not exceed the viewport', () => {
  assert.deepEqual(clampImagePan({ x: 100, y: 1000 }, 2,
    { width: 250, height: 500 }, { width: 800, height: 600 }), { x: 0, y: 200 })
  assert.deepEqual(clampImagePan({ x: 100, y: -100 }, 3,
    { width: 100, height: 100 }, { width: 800, height: 600 }), { x: 0, y: 0 })
})

test('viewport resize reclamps existing pan and unavailable image dimensions reset pan', () => {
  const pan = { x: 200, y: -150 }
  const image = { width: 600, height: 400 }
  assert.deepEqual(clampImagePan(pan, 2, image, { width: 1100, height: 760 }), { x: 50, y: -20 })
  assert.deepEqual(clampImagePan(pan, 2, { width: 0, height: 0 }, { width: 800, height: 500 }), { x: 0, y: 0 })
  assert.deepEqual(clampImagePan(pan, 2, image, { width: 0, height: 0 }), { x: 0, y: 0 })
})

test('touch swipes navigate only after 60 pixels and horizontal movement dominates', () => {
  assert.equal(getPhotoSwipeDirection(-60, 0, 1, 'touch'), 1)
  assert.equal(getPhotoSwipeDirection(60, 0, 1, 'touch'), -1)
  assert.equal(getPhotoSwipeDirection(-59, 0, 1, 'touch'), 0)
  assert.equal(getPhotoSwipeDirection(59, 0, 1, 'touch'), 0)
  assert.equal(getPhotoSwipeDirection(-80, 79, 1, 'touch'), 1)
  assert.equal(getPhotoSwipeDirection(-80, 80, 1, 'touch'), 0)
  assert.equal(getPhotoSwipeDirection(100, 101, 1, 'touch'), 0)
  assert.equal(getPhotoSwipeDirection(0, -200, 1, 'touch'), 0)
})

test('zoomed drag and mouse or pen movement never trigger gallery navigation', () => {
  for (const scale of [1.5, 2, 3]) {
    assert.equal(getPhotoSwipeDirection(-300, 0, scale, 'touch'), 0)
    assert.equal(getPhotoSwipeDirection(300, 0, scale, 'touch'), 0)
  }
  assert.equal(getPhotoSwipeDirection(-300, 0, 1, 'mouse'), 0)
  assert.equal(getPhotoSwipeDirection(-300, 0, 1, 'pen'), 0)
})

test('keyboard guard recognizes text fields and contenteditable descendants without blocking controls', () => {
  for (const tagName of ['INPUT', 'TEXTAREA', 'SELECT', 'textarea']) {
    assert.equal(isEditableTarget({ tagName }), true)
  }
  assert.equal(isEditableTarget({ tagName: 'DIV', isContentEditable: true }), true)
  assert.equal(isEditableTarget({ tagName: 'SPAN', parentElement: { tagName: 'DIV', isContentEditable: true } }), true)
  assert.equal(isEditableTarget({ parentElement: { tagName: 'TEXTAREA' } }), true)
  assert.equal(isEditableTarget({ tagName: 'BUTTON', parentElement: { tagName: 'DIV' } }), false)
  assert.equal(isEditableTarget({ tagName: 'IMG' }), false)
  assert.equal(isEditableTarget(null), false)
})
