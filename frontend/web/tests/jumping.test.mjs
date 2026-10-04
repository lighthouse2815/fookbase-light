import assert from 'node:assert/strict'
import test from 'node:test'
import { advanceJumping, createJumping, jump, startJumping, JUMPING_WORLD } from '../src/pages/games/jumpingEngine.ts'

const simulate = (state, seconds, fps = 60) => {
  for (let frame = 0; frame < Math.round(seconds * fps); frame++) state = advanceJumping(state, 1 / fps)
  return state
}

test('a jump lifts the runner then lands, without allowing another jump in midair', () => {
  const ready = createJumping(10)
  assert.equal(jump(ready), ready)
  const started = startJumping(ready)
  const lifted = jump(started)
  assert.equal(lifted.height, 0)
  assert.ok(lifted.velocity > 0)
  const rising = simulate(lifted, 0.2)
  assert.ok(rising.height > 60)
  assert.equal(jump(rising), rising)
  const landed = simulate(rising, 0.6)
  assert.equal(landed.height, 0)
  assert.equal(landed.velocity, 0)
  assert.ok(jump(landed).velocity > 0)
})

test('30, 60 and 144 Hz screens move the runner and course equally', () => {
  const initial = jump(startJumping(createJumping(44)))
  const results = [30, 60, 144].map(fps => simulate(initial, 0.5, fps))
  for (const result of results.slice(1)) {
    assert.ok(Math.abs(result.height - results[0].height) < 0.001)
    assert.ok(Math.abs(result.distance - results[0].distance) < 0.001)
    assert.ok(Math.abs(result.obstacles[0].x - results[0].obstacles[0].x) < 0.001)
  }
})

test('running into a block ends the round but jumping clears it', () => {
  const active = startJumping(createJumping(12))
  const obstacle = { ...active.obstacles[0], x: JUMPING_WORLD.playerX, width: 35, height: 48 }
  assert.equal(advanceJumping({ ...active, obstacles: [obstacle] }, 1 / 120).phase, 'over')
  const cleared = advanceJumping({ ...active, height: 90, obstacles: [obstacle] }, 1 / 120)
  assert.equal(cleared.phase, 'playing')
})

test('a block awards exactly one point after the runner fully passes it', () => {
  const active = startJumping(createJumping(12))
  const obstacle = { ...active.obstacles[0], x: JUMPING_WORLD.playerX - JUMPING_WORLD.radius - 60, width: 35 }
  const passed = advanceJumping({ ...active, obstacles: [obstacle] }, 1 / 120)
  assert.equal(passed.score, 1)
  assert.equal(advanceJumping(passed, 1 / 120).score, 1)
})

test('the visible circular runner can pass a block corner without a false collision', () => {
  const active = startJumping(createJumping(12))
  const obstacle = { ...active.obstacles[0], x: JUMPING_WORLD.playerX + 16, width: 30, height: 40 }
  assert.equal(advanceJumping({ ...active, height: 36, velocity: 0, obstacles: [obstacle] }, 1 / 120).phase, 'playing')
  assert.equal(advanceJumping({ ...active, height: 16, velocity: 0, obstacles: [obstacle] }, 1 / 120).phase, 'over')
})

test('players with a shared seed see identical blocks and spacing', () => {
  const first = startJumping(createJumping(8675309))
  const second = startJumping(createJumping(8675309))
  assert.deepEqual(first.obstacles, second.obstacles)
  const move = state => advanceJumping({ ...state, obstacles: state.obstacles.map(block => ({ ...block, x: block.x - 300 })) }, 0.1)
  assert.deepEqual(move(first).obstacles, move(second).obstacles)
  assert.notDeepEqual(first.obstacles, startJumping(createJumping(1234)).obstacles)
})

test('blocks are reachable and leave time to land before jumping again', () => {
  for (let seed = 0; seed < 100; seed++) {
    const state = startJumping(createJumping(seed))
    for (const block of state.obstacles) {
      assert.ok(block.height >= 30 && block.height <= 60)
      assert.ok(block.width >= 26 && block.width <= 50)
    }
    for (let index = 1; index < state.obstacles.length; index++) {
      assert.ok(state.obstacles[index].x - state.obstacles[index - 1].x >= 360)
    }
  }
})

test('an ordinary player timing jumps can survive an increasingly fast course', () => {
  for (const seed of [0, 123, 8675309]) {
    let state = startJumping(createJumping(seed))
    for (let frame = 0; frame < 60 * 60; frame++) {
      const next = state.obstacles.find(block => block.x + block.width >= JUMPING_WORLD.playerX - JUMPING_WORLD.radius)
      if (state.height === 0 && next && next.x - JUMPING_WORLD.playerX < 100) state = jump(state)
      state = advanceJumping(state, 1 / 60)
      assert.equal(state.phase, 'playing', `seed ${seed}, frame ${frame}`)
    }
    assert.ok(state.score >= 30)
  }
})

test('pausing freezes the game and resuming preserves the course and score', () => {
  const active = simulate(startJumping(createJumping(12)), 0.2)
  for (const phase of ['ready', 'paused', 'over']) {
    const stopped = { ...active, phase }
    assert.equal(advanceJumping(stopped, 1), stopped)
    assert.equal(jump(stopped), stopped)
  }
  const resumed = startJumping({ ...active, phase: 'paused' })
  assert.equal(resumed.phase, 'playing')
  assert.equal(resumed.score, active.score)
  assert.deepEqual(resumed.obstacles, active.obstacles)
  assert.equal(startJumping({ ...active, phase: 'over' }).phase, 'over')
  assert.equal(createJumping(12).score, 0)
})

test('invalid elapsed values do not corrupt physics and long frames are bounded', () => {
  const active = startJumping(createJumping(12))
  for (const seconds of [0, -1, NaN, Infinity]) assert.equal(advanceJumping(active, seconds), active)
  assert.deepEqual(advanceJumping(active, 5), advanceJumping(active, 0.1))
})

test('advancing physics never mutates the previous state', () => {
  const active = jump(startJumping(createJumping(12)))
  const copy = structuredClone(active)
  assert.deepEqual(advanceJumping(active, 0.1), advanceJumping(active, 0.1))
  assert.deepEqual(active, copy)
})
