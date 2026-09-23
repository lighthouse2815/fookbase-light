import assert from 'node:assert/strict'
import test from 'node:test'
import { advanceMole, createMoleGame, hitMole } from '../src/pages/games/whackMoleEngine.ts'
const playing = () => advanceMole({ ...createMoleGame(), phase: 'playing' }, 0, () => 0)

test('a hit scores once, hides the mole briefly and counts empty taps as misses', () => {
  const original = playing()
  const hit = hitMole(original, 0)
  assert.equal(hit.score, 10)
  assert.equal(hit.target, -1)
  assert.equal(hitMole(hit, 0).score, 10)
  assert.equal(hitMole(hit, 0).misses, 1)
  assert.equal(advanceMole(hit, 179).target, -1)
  assert.ok(advanceMole(hit, 180).target >= 0)
  assert.equal(original.score, 0)
})

test('expired targets move to a different hole and speed has a playable lower bound', () => {
  const state = playing()
  assert.notEqual(advanceMole(state, 1000, () => 0).target, state.target)
  assert.equal(advanceMole({ ...state, score: 1000 }, 1000).targetRemaining, 450)
})

test('elapsed time ends the round exactly and prevents late hits even after a delayed timer', () => {
  const state = playing()
  const almostOver = advanceMole(state, 29_999)
  assert.equal(almostOver.phase, 'playing')
  const over = advanceMole(almostOver, 1)
  assert.equal(over.phase, 'over')
  assert.equal(over.remaining, 0)
  assert.equal(hitMole(over, 0), over)
  assert.equal(advanceMole(state, 60_000).remaining, 0)
})

test('inactive rounds preserve time and score; a new round clears everything', () => {
  for (const phase of ['ready', 'paused', 'over']) {
    const state = { ...playing(), phase }
    assert.equal(advanceMole(state, 5000), state)
    assert.equal(hitMole(state, 0), state)
  }
  assert.equal(createMoleGame().remaining, 30_000)
  assert.equal(createMoleGame().score, 0)
  assert.equal(createMoleGame().misses, 0)
})
