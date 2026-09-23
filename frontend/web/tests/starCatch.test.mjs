import test from 'node:test'
import assert from 'node:assert/strict'
import { advanceStarCatch, createStarCatch, moveShip, startStarCatch, STAR_CATCH_DURATION } from '../src/pages/games/starCatchEngine.ts'

test('ship movement is bounded and inactive games stay immutable', () => {
  const ready = createStarCatch(() => 0.2)
  assert.equal(moveShip(ready, -1), ready)
  const active = startStarCatch(ready)
  assert.equal(moveShip(active, -1, 100).shipX, 10)
  assert.equal(moveShip(active, 1, 100).shipX, 90)
  assert.equal(advanceStarCatch(active, 0), active)
})

test('catching a star scores points and hitting a meteor costs one life', () => {
  const active = { ...startStarCatch(createStarCatch()), items: [
    { id: 1, x: 50, y: 77, kind: 'star', speed: 10 },
    { id: 2, x: 50, y: 77, kind: 'meteor', speed: 10 },
  ] }
  const next = advanceStarCatch(active, 0.1, () => 0.4)
  assert.equal(next.score, 10)
  assert.equal(next.lives, 2)
  assert.equal(next.event, 'meteor')
  assert.equal(next.items.length, 6)
})

test('a round ends at zero lives or at the thirty second limit', () => {
  const active = startStarCatch(createStarCatch())
  const noLives = advanceStarCatch({ ...active, lives: 1, items: [{ id: 1, x: 50, y: 77, kind: 'meteor', speed: 10 }] }, 0.1)
  assert.equal(noLives.phase, 'over')
  const timeout = advanceStarCatch({ ...active, elapsed: STAR_CATCH_DURATION - 0.05 }, 0.1)
  assert.equal(timeout.phase, 'over')
  assert.equal(timeout.elapsed, STAR_CATCH_DURATION)
})
