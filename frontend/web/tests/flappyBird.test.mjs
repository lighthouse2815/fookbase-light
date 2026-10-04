import assert from 'node:assert/strict'
import test from 'node:test'
import { advanceGame, createGame, flap, WORLD } from '../src/pages/games/flappyBirdEngine.ts'

const random = () => 0.5
const flyFor = (state, seconds, fps = 60) => {
  for (let frame = 0; frame < seconds * fps; frame++) state = advanceGame(state, 1 / fps, random)
  return state
}

test('flapping applies lift without teleporting the bird, then gravity brings it down', () => {
  const ready = createGame()
  const start = flap(ready)
  assert.equal(start.birdY, ready.birdY)
  assert.ok(start.velocity < 0)
  const rising = flyFor(start, 0.1)
  assert.ok(rising.birdY < start.birdY)
  const falling = flyFor(rising, 0.5)
  assert.ok(falling.velocity > 0)
  assert.ok(falling.birdY > rising.birdY)
  assert.equal(flap(falling).velocity, start.velocity)
})

test('30, 60 and 144 Hz screens simulate the same elapsed time', () => {
  const initial = flap(createGame())
  const results = [30, 60, 144].map(fps => flyFor(initial, 0.5, fps))
  for (const result of results.slice(1)) {
    assert.ok(Math.abs(result.birdY - results[0].birdY) < 0.001)
    assert.ok(Math.abs(result.pipes[0].x - results[0].pipes[0].x) < 0.001)
  }
})

test('a pipe awards one point only after the entire bird has passed', () => {
  let state = { ...flap(createGame()), birdY: 230, velocity: 0,
    pipes: [{ id: 1, x: WORLD.birdX - WORLD.pipeWidth - WORLD.radius - 5, gapCenter: 230, scored: false }] }
  state = advanceGame(state, 1 / 60, random)
  assert.equal(state.score, 1)
  state = advanceGame(state, 1 / 60, random)
  assert.equal(state.score, 1)
})

test('collision matches the visible pipe lip and keeps the gap passable', () => {
  const base = { ...flap(createGame()), velocity: 0,
    pipes: [{ id: 1, x: WORLD.birdX + WORLD.radius + 2, gapCenter: 230, scored: false }] }
  assert.equal(advanceGame({ ...base, birdY: 230 }, 1 / 120, random).phase, 'playing')
  assert.equal(advanceGame({ ...base, birdY: 140 }, 1 / 120, random).phase, 'over')
  assert.equal(advanceGame({ ...base, birdY: 315 }, 1 / 120, random).phase, 'over')
})

test('the wider pipe lip does not extend into empty space beside the pipe body', () => {
  const base = { ...flap(createGame()), velocity: 0,
    pipes: [{ id: 1, x: WORLD.birdX + WORLD.radius + 2, gapCenter: 230, scored: false }] }
  for (const birdY of [100, 350]) {
    assert.equal(advanceGame({ ...base, birdY }, 1 / 120, random).phase, 'playing')
    assert.equal(advanceGame({ ...base, birdY,
      pipes: [{ ...base.pipes[0], x: WORLD.birdX + WORLD.radius - 1 }] }, 1 / 120, random).phase, 'over')
  }
})

test('touching the visible ground or ceiling ends the round', () => {
  const base = flap(createGame())
  const ground = advanceGame({ ...base, birdY: WORLD.groundY - WORLD.radius, velocity: 100 }, 1 / 120, random)
  assert.equal(ground.phase, 'over')
  assert.equal(ground.birdY, WORLD.groundY - WORLD.radius)
  assert.equal(advanceGame({ ...base, birdY: WORLD.radius, velocity: -100 }, 1 / 120, random).phase, 'over')
})

test('each new pipe has consistent spacing and a reachable opening', () => {
  const initial = { ...flap(createGame()), velocity: 0,
    pipes: [{ id: 1, x: WORLD.width - WORLD.pipeSpacing, gapCenter: 230, scored: false }] }
  for (const randomValue of [0, 1]) {
    const next = advanceGame(initial, 1 / 120, () => randomValue)
    assert.equal(next.pipes.length, 2)
    assert.ok(Math.abs(next.pipes[1].x - next.pipes[0].x - WORLD.pipeSpacing) < 0.001)
    assert.ok(Math.abs(next.pipes[1].gapCenter - 230) <= 70)
    assert.ok(next.pipes[1].gapCenter - WORLD.gap / 2 > 0)
    assert.ok(next.pipes[1].gapCenter + WORLD.gap / 2 < WORLD.groundY)
  }
})

test('a shared seed produces the same pipes for every online player', () => {
  const initial = { ...flap(createGame(8675309)), velocity: 0,
    pipes: [{ id: 1, x: WORLD.width - WORLD.pipeSpacing, gapCenter: 230, scored: false }] }
  const firstPlayer = advanceGame(initial, 1 / 120, () => 0)
  const secondPlayer = advanceGame(initial, 1 / 120, () => 1)
  assert.deepEqual(firstPlayer.pipes, secondPlayer.pipes)
})

test('updates are pure so repeated calculations cannot duplicate points or pipes', () => {
  const state = flap(createGame())
  const copy = structuredClone(state)
  assert.deepEqual(advanceGame(state, 1 / 60, random), advanceGame(state, 1 / 60, random))
  assert.deepEqual(state, copy)
})

test('ready, paused and finished games stay still; restart clears the previous round', () => {
  for (const phase of ['ready', 'paused', 'over']) {
    const state = { ...createGame(), phase }
    assert.deepEqual(advanceGame(state, 1, random), state)
  }
  const ended = flyFor(flap(createGame()), 3)
  assert.equal(ended.phase, 'over')
  assert.deepEqual(flap(ended), ended)
  const restarted = flap(createGame())
  assert.equal(restarted.score, 0)
  assert.equal(restarted.pipes.length, 1)
  assert.equal(restarted.phase, 'playing')
})

test('a stalled animation frame does not cause a multi-second physics jump', () => {
  const state = flap(createGame())
  assert.deepEqual(advanceGame(state, 5, random), advanceGame(state, 0.1, random))
})
