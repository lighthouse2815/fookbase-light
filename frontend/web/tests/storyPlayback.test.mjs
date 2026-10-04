import assert from 'node:assert/strict'
import test from 'node:test'
import { imageElapsedMs, pauseImageClock } from '../src/pages/feed/components/storyPlayback.ts'

test('image clock preserves 40 percent through a ten-second pause', () => {
  const clock = { elapsedMs: 0, startedAt: 100 }
  pauseImageClock(clock, 2100)
  assert.equal(imageElapsedMs(clock, 12100), 2000)
  clock.startedAt = 12100
  assert.equal(imageElapsedMs(clock, 12600), 2500)
})
test('repeated pauses preserve accumulated elapsed time', () => {
  const clock = { elapsedMs: 0, startedAt: 0 }
  pauseImageClock(clock, 1000)
  pauseImageClock(clock, 6000)
  clock.startedAt = 7000
  pauseImageClock(clock, 8500)
  assert.equal(imageElapsedMs(clock, 20000), 2500)
})
test('image clock caps completion and is independent of UI ticks', () => {
  assert.equal(imageElapsedMs({ elapsedMs: 2000, startedAt: 1000 }, 10000), 5000)
  assert.equal(imageElapsedMs({ elapsedMs: 2000, startedAt: 1000 }, 1800), 2800)
})
