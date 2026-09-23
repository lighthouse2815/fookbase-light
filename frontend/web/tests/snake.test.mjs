import assert from 'node:assert/strict'
import test from 'node:test'
import { createSnake, SNAKE_SIZE, stepSnake, turnSnake } from '../src/pages/games/snakeEngine.ts'

const playing = () => ({ ...createSnake(() => 0), phase: 'playing' })

test('movement preserves length until food is eaten, then grows and scores once', () => {
  const state = { ...playing(), food: { x: 8, y: 8 } }
  const copy = structuredClone(state)
  const next = stepSnake(state, () => 0)
  assert.equal(next.body.length, 4)
  assert.equal(next.score, 1)
  assert.equal(next.body.some(p => p.x === next.food.x && p.y === next.food.y), false)
  assert.equal(stepSnake(next).body.length, 4)
  assert.equal(stepSnake(next).score, 1)
  assert.deepEqual(state, copy)
})

test('opposite turns and multiple turns within one tick cannot reverse into the neck', () => {
  const state = playing()
  assert.equal(turnSnake(state, 'left'), state)
  const up = turnSnake(state, 'up')
  assert.equal(turnSnake(up, 'left'), up)
  const moved = stepSnake(up)
  assert.deepEqual(moved.body[0], { x: 7, y: 7 })
  assert.equal(turnSnake(moved, 'left').nextDirection, 'left')
})

test('hitting any wall or the body ends the round without moving outside the board', () => {
  for (const [direction, head] of [['up', { x: 3, y: 0 }], ['down', { x: 3, y: 15 }], ['left', { x: 0, y: 3 }], ['right', { x: 15, y: 3 }]]) {
    const state = { ...playing(), body: [head], direction, nextDirection: direction }
    const next = stepSnake(state)
    assert.equal(next.phase, 'over')
    assert.deepEqual(next.body, state.body)
  }
  const state = { ...playing(), body: [{ x: 2, y: 2 }, { x: 2, y: 3 }, { x: 3, y: 3 }, { x: 3, y: 2 }, { x: 4, y: 2 }] }
  assert.equal(stepSnake(state).phase, 'over')
})

test('moving into the departing tail cell is allowed', () => {
  const state = { ...playing(), body: [{ x: 2, y: 2 }, { x: 2, y: 3 }, { x: 3, y: 3 }, { x: 3, y: 2 }] }
  const next = stepSnake(state)
  assert.equal(next.phase, 'playing')
  assert.deepEqual(next.body[0], { x: 3, y: 2 })
})

test('filling the board wins without trying to spawn food on the snake', () => {
  const body = [{ x: 0, y: 0 }]
  for (let y = 0; y < SNAKE_SIZE; y++) {
    for (let x = 0; x < SNAKE_SIZE; x++) {
      if (y === 0 && x < 2) continue
      body.push({ x, y })
    }
  }
  const next = stepSnake({ ...playing(), body, food: { x: 1, y: 0 } }, () => { throw new Error('Board is full') })
  assert.equal(next.phase, 'won')
  assert.equal(next.body.length, SNAKE_SIZE ** 2)
  assert.equal(next.food, null)
})

test('ready, paused and finished rounds do not move or accept turns; restart clears score', () => {
  for (const phase of ['ready', 'paused', 'over', 'won']) {
    const state = { ...playing(), phase }
    assert.equal(stepSnake(state), state)
    assert.equal(turnSnake(state, 'up'), state)
  }
  assert.equal(createSnake().score, 0)
  assert.equal(createSnake().phase, 'ready')
})
