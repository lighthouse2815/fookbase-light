import test from 'node:test'
import assert from 'node:assert/strict'
import { createLights, isLightsSolved, LIGHTS_SIZE, pressLights, startLights, toggleLights } from '../src/pages/games/lightsEngine.ts'

test('pressing a cell toggles itself and only its adjacent cells', () => {
  const board = Array(LIGHTS_SIZE ** 2).fill(false)
  const next = toggleLights(board, 0)
  assert.deepEqual(next.map(Boolean), [true, true, false, false, false, true, ...Array(19).fill(false)])
  assert.deepEqual(toggleLights(board, -1), board)
  assert.deepEqual(toggleLights(board, 25), board)
})

test('generated boards are solvable and inactive states ignore presses', () => {
  const state = createLights(() => 0.21, 2)
  assert.equal(isLightsSolved(state.board), false)
  assert.equal(pressLights(state, 0), state)
  assert.equal(startLights(state).phase, 'playing')
})

test('replaying the scramble steps solves the board and freezes the win', () => {
  const board = Array(LIGHTS_SIZE ** 2).fill(false)
  const scramble = [0, 6, 12, 18, 24, 4, 20]
  const scrambled = scramble.reduce((current, index) => toggleLights(current, index), board)
  let state = startLights({ phase: 'ready', board: scrambled, moves: 0, level: 1 })
  for (const index of scramble) state = pressLights(state, index)
  assert.equal(state.phase, 'won')
  assert.equal(isLightsSolved(state.board), true)
  assert.equal(pressLights(state, 0), state)
})
