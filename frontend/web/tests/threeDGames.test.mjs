import assert from 'node:assert/strict'
import test from 'node:test'
import { advanceGravityFlip, createGravityFlip, flipGravity, startGravityFlip, GRAVITY_FLIP_DURATION } from '../src/pages/games/gravityFlipEngine.ts'
import { advanceNeonDrift, createNeonDrift, moveNeonDrift, phaseShift, startNeonDrift, NEON_DRIFT_DURATION } from '../src/pages/games/neonDriftEngine.ts'

test('neon drift keeps the ship in three lanes and phase shift absorbs a hit', () => {
  const ready = createNeonDrift(() => 0.2)
  assert.equal(moveNeonDrift(ready, -1), ready)
  const active = { ...startNeonDrift(ready), lane: 0, obstacles: [{ id: 1, lane: 0, depth: 0.84, speed: 0.1, kind: 'gate' }] }
  const hit = advanceNeonDrift(active, 0.1, () => 0.2)
  assert.equal(hit.lives, 2)
  const shielded = advanceNeonDrift(phaseShift(active), 0.1, () => 0.2)
  assert.equal(shielded.lives, 3)
  assert.equal(shielded.score, 20)
})

test('neon drift ends at the time limit and does not move while inactive', () => {
  const ready = createNeonDrift(() => 0.5)
  assert.deepEqual(advanceNeonDrift(ready, 1), ready)
  const active = startNeonDrift(ready)
  const finished = advanceNeonDrift({ ...active, elapsed: NEON_DRIFT_DURATION - 0.02 }, 0.1, () => 0.5)
  assert.equal(finished.phase, 'over')
  assert.equal(finished.elapsed, NEON_DRIFT_DURATION)
})

test('gravity flip reverses direction and catches a missed gate', () => {
  const active = startGravityFlip(createGravityFlip(() => 0.5))
  const flipped = flipGravity(active)
  assert.equal(flipped.gravity, -1)
  assert.equal(flipped.flips, 1)
  const crashed = advanceGravityFlip({ ...active, playerY: 10, velocity: 0, gates: [{ id: 1, x: 0.23, gap: 50, size: 16 }] }, 0.1, () => 0.5)
  assert.equal(crashed.phase, 'over')
  assert.equal(crashed.event, 'hit')
})

test('gravity flip can finish by time when the pilot stays inside every gap', () => {
  const active = startGravityFlip(createGravityFlip(() => 0.5))
  const safe = { ...active, playerY: 50, velocity: 0, gates: [{ id: 1, x: 0.23, gap: 50, size: 100 }] }
  const finished = advanceGravityFlip({ ...safe, elapsed: GRAVITY_FLIP_DURATION - 0.02 }, 0.1, () => 0.5)
  assert.equal(finished.phase, 'over')
  assert.equal(finished.elapsed, GRAVITY_FLIP_DURATION)
})
